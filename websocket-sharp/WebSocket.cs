#region License
/*
 * WebSocket.cs
 *
 * A C# implementation of the WebSocket interface.
 *
 * This code is derived from WebSocket.java
 * (http://github.com/adamac/Java-WebSocket-client).
 *
 * The MIT License
 *
 * Copyright (c) 2009 Adam MacBeth
 * Copyright (c) 2010-2014 sta.blockhead
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using WebSocketSharp.Net;
using WebSocketSharp.Net.WebSockets;

namespace WebSocketSharp
{
    /// <summary>
    /// Implements the WebSocket interface.
    /// </summary>
    /// <remarks>
    /// The WebSocket class provides a set of methods and properties for two-way communication using
    /// the WebSocket protocol (<see href="http://tools.ietf.org/html/rfc6455">RFC 6455</see>).
    /// </remarks>
    public class WebSocket : IDisposable
    {
        #region Private Fields

        private string _base64Key;
        private RemoteCertificateValidationCallback
                                        _certValidationCallback;
        private Action _closeContext;
        private CompressionMethod _compression;
        private WebSocketContext _context;
        private CookieCollection _cookies;
        private NetworkCredential _credentials;
        private string _extensions;
        private AutoResetEvent _exitReceiving;
        private object _forConn;
        private object _forEvent;
        private object _forMessageEventQueue;
        private object _forSend;
        private const string _guid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        private Func<WebSocketContext, string>
                                        _handshakeRequestChecker;
        private Queue<MessageEventArgs> _messageEventQueue;
        private uint _nonceCount;
        private string _origin;
        private bool _preAuth;
        private string _protocol;
        private string[] _protocols;
        private NetworkCredential _proxyCredentials;
        private Uri _proxyUri;
        private volatile WebSocketState _readyState;
        private AutoResetEvent _receivePong;
        private bool _secure;
        private Stream _stream;
        private TcpClient _tcpClient;
        private Uri _uri;
        private const string _version = "13";

        #endregion

        #region Internal Fields

        internal const int FragmentLength = 1016; // Max value is int.MaxValue - 14.

        #endregion

        #region Internal Constructors

        // As server
        internal WebSocket(HttpListenerWebSocketContext context, string protocol)
        {
            _context = context;
            _protocol = protocol;

            _closeContext = context.Close;
            _secure = context.IsSecureConnection;
            _stream = context.Stream;

            init();
        }

        #endregion

        // As server
        internal Func<WebSocketContext, string> CustomHandshakeRequestChecker
        {
            get
            {
                return _handshakeRequestChecker ?? (context => null);
            }

            set
            {
                _handshakeRequestChecker = value;
            }
        }

        internal bool IsConnected
        {
            get
            {
                return _readyState == WebSocketState.Open || _readyState == WebSocketState.Closing;
            }
        }

        /// <summary>
        /// Gets the state of the WebSocket connection.
        /// </summary>
        /// <value>
        /// One of the <see cref="WebSocketState"/> enum values, indicates the state of the WebSocket
        /// connection. The default value is <see cref="WebSocketState.Connecting"/>.
        /// </value>
        public WebSocketState ReadyState
        {
            get
            {
                return _readyState;
            }
        }

        #region Public Events

        /// <summary>
        /// Occurs when the WebSocket connection has been closed.
        /// </summary>
        public event EventHandler<CloseEventArgs> OnClose;

        /// <summary>
        /// Occurs when the <see cref="WebSocket"/> gets an error.
        /// </summary>
        public event EventHandler<ErrorEventArgs> OnError;

        /// <summary>
        /// Occurs when the <see cref="WebSocket"/> receives a message.
        /// </summary>
        public event EventHandler<MessageEventArgs> OnMessage;

        /// <summary>
        /// Occurs when the WebSocket connection has been established.
        /// </summary>
        public event EventHandler OnOpen;

        #endregion

        #region Private Methods

        // As server
        private bool acceptHandshake()
        {
            var msg = checkIfValidHandshakeRequest(_context);
            if (msg != null)
            {
                error("An error has occurred while connecting.");
                Close(HttpStatusCode.BadRequest);

                return false;
            }

            if (_protocol != null &&
                !_context.SecWebSocketProtocols.Contains(protocol => protocol == _protocol))
                _protocol = null;

            var extensions = _context.Headers["Sec-WebSocket-Extensions"];
            if (extensions != null && extensions.Length > 0)
                processSecWebSocketExtensionsHeader(extensions);

            return sendHttpResponse(createHandshakeResponse());
        }

        // As server
        private string checkIfValidHandshakeRequest(WebSocketContext context)
        {
            var headers = context.Headers;
            return context.RequestUri == null
                   ? "Invalid request url."
                   : !context.IsWebSocketRequest
                     ? "Not WebSocket connection request."
                     : !validateSecWebSocketKeyHeader(headers["Sec-WebSocket-Key"])
                       ? "Invalid Sec-WebSocket-Key header."
                       : !validateSecWebSocketVersionClientHeader(headers["Sec-WebSocket-Version"])
                         ? "Invalid Sec-WebSocket-Version header."
                         : CustomHandshakeRequestChecker(context);
        }

        private void close(CloseStatusCode code, string reason, bool wait)
        {
            close(new PayloadData(((ushort)code).Append(reason)), !code.IsReserved(), wait);
        }

        private void close(PayloadData payload, bool send, bool wait)
        {
            lock (_forConn)
            {
                if (_readyState == WebSocketState.Closing || _readyState == WebSocketState.Closed)
                {
                    return;
                }

                _readyState = WebSocketState.Closing;
            }

            var e = new CloseEventArgs(payload);
            e.WasClean =
              closeHandshake(
                  send ? WebSocketFrame.CreateCloseFrame(Mask.Unmask, payload).ToByteArray() : null,
                  wait ? 1000 : 0,
                  closeServerResources);

            _readyState = WebSocketState.Closed;
            try
            {
                OnClose.Emit(this, e);
            }
            catch (Exception ex)
            {
                error("An exception has occurred while OnClose.");
            }
        }

        private bool closeHandshake(byte[] frameAsBytes, int millisecondsTimeout, Action release)
        {
            var sent = frameAsBytes != null && writeBytes(frameAsBytes);
            var received =
              millisecondsTimeout == 0 ||
              (sent && _exitReceiving != null && _exitReceiving.WaitOne(millisecondsTimeout));

            release();
            if (_receivePong != null)
            {
                _receivePong.Close();
                _receivePong = null;
            }

            if (_exitReceiving != null)
            {
                _exitReceiving.Close();
                _exitReceiving = null;
            }

            var result = sent && received;

            return result;
        }

        // As server
        private void closeServerResources()
        {
            if (_closeContext == null)
                return;

            _closeContext();
            _closeContext = null;
            _stream = null;
            _context = null;
        }

        private bool concatenateFragmentsInto(Stream dest)
        {
            while (true)
            {
                var frame = WebSocketFrame.Read(_stream, true);
                if (frame.IsFinal)
                {
                    /* FINAL */

                    // CONT
                    if (frame.IsContinuation)
                    {
                        dest.WriteBytes(frame.PayloadData.ApplicationData);
                        break;
                    }

                    // PING
                    if (frame.IsPing)
                    {
                        processPingFrame(frame);
                        continue;
                    }

                    // PONG
                    if (frame.IsPong)
                    {
                        processPongFrame(frame);
                        continue;
                    }

                    // CLOSE
                    if (frame.IsClose)
                        return processCloseFrame(frame);
                }
                else
                {
                    /* MORE */

                    // CONT
                    if (frame.IsContinuation)
                    {
                        dest.WriteBytes(frame.PayloadData.ApplicationData);
                        continue;
                    }
                }

                // ?
                return processUnsupportedFrame(
                  frame,
                  CloseStatusCode.IncorrectData,
                  "An incorrect data has been received while receiving fragmented data.");
            }

            return true;
        }

        // As server
        private HttpResponse createHandshakeCloseResponse(HttpStatusCode code)
        {
            var res = HttpResponse.CreateCloseResponse(code);
            res.Headers["Sec-WebSocket-Version"] = _version;

            return res;
        }

        // As server
        private HttpResponse createHandshakeResponse()
        {
            var res = HttpResponse.CreateWebSocketResponse();

            var headers = res.Headers;
            headers["Sec-WebSocket-Accept"] = CreateResponseKey(_base64Key);

            if (_protocol != null)
                headers["Sec-WebSocket-Protocol"] = _protocol;

            if (_extensions != null)
                headers["Sec-WebSocket-Extensions"] = _extensions;

            if (_cookies.Count > 0)
                res.SetCookies(_cookies);

            return res;
        }

        private MessageEventArgs dequeueFromMessageEventQueue()
        {
            lock (_forMessageEventQueue)
                return _messageEventQueue.Count > 0
                       ? _messageEventQueue.Dequeue()
                       : null;
        }

        private void enqueueToMessageEventQueue(MessageEventArgs e)
        {
            lock (_forMessageEventQueue)
                _messageEventQueue.Enqueue(e);
        }

        private void error(string message)
        {
            try
            {
                OnError.Emit(this, new ErrorEventArgs(message));
            }
            catch (Exception ex)
            {
            }
        }

        private void init()
        {
            _compression = CompressionMethod.None;
            _cookies = new CookieCollection();
            _forConn = new object();
            _forEvent = new object();
            _forSend = new object();
            _messageEventQueue = new Queue<MessageEventArgs>();
            _forMessageEventQueue = ((ICollection)_messageEventQueue).SyncRoot;
            _readyState = WebSocketState.Connecting;
        }

        private void open()
        {
            try
            {
                startReceiving();

                lock (_forEvent)
                {
                    try
                    {
                        OnOpen.Emit(this, EventArgs.Empty);
                    }
                    catch (Exception ex)
                    {
                        processException(ex, "An exception has occurred while OnOpen.");
                    }
                }
            }
            catch (Exception ex)
            {
                processException(ex, "An exception has occurred while opening.");
            }
        }

        private bool processCloseFrame(WebSocketFrame frame)
        {
            var payload = frame.PayloadData;
            close(payload, !payload.ContainsReservedCloseStatusCode, false);

            return false;
        }

        private bool processDataFrame(WebSocketFrame frame)
        {
            var e = frame.IsCompressed
                    ? new MessageEventArgs(
                        frame.Opcode, frame.PayloadData.ApplicationData.Decompress(_compression))
                    : new MessageEventArgs(frame.Opcode, frame.PayloadData);

            enqueueToMessageEventQueue(e);
            return true;
        }

        private void processException(Exception exception, string message)
        {
            var code = CloseStatusCode.Abnormal;
            var reason = message;
            if (exception is WebSocketException)
            {
                var wsex = (WebSocketException)exception;
                code = wsex.Code;
                reason = wsex.Message;
            }

            error(message ?? code.GetMessage());
            if (_readyState == WebSocketState.Connecting)
                Close(HttpStatusCode.BadRequest);
            else
                close(code, reason ?? code.GetMessage(), false);
        }

        private bool processFragmentedFrame(WebSocketFrame frame)
        {
            return frame.IsContinuation // Not first fragment
                   ? true
                   : processFragments(frame);
        }

        private bool processFragments(WebSocketFrame first)
        {
            using (var buff = new MemoryStream())
            {
                buff.WriteBytes(first.PayloadData.ApplicationData);
                if (!concatenateFragmentsInto(buff))
                    return false;

                byte[] data;
                if (_compression != CompressionMethod.None)
                {
                    data = buff.DecompressToArray(_compression);
                }
                else
                {
                    buff.Close();
                    data = buff.ToArray();
                }

                enqueueToMessageEventQueue(new MessageEventArgs(first.Opcode, data));
                return true;
            }
        }

        private bool processPingFrame(WebSocketFrame frame)
        {
            var mask = Mask.Unmask;

            return true;
        }

        private bool processPongFrame(WebSocketFrame frame)
        {
            _receivePong.Set();

            return true;
        }

        // As server
        private void processSecWebSocketExtensionsHeader(string value)
        {
            var buff = new StringBuilder(32);

            var compress = false;
            foreach (var extension in value.SplitHeaderValue(','))
            {
                var trimed = extension.Trim();
                var unprefixed = trimed.RemovePrefix("x-webkit-");
                if (!compress && unprefixed.IsCompressionExtension())
                {
                    var method = unprefixed.ToCompressionMethod();
                    if (method != CompressionMethod.None)
                    {
                        _compression = method;
                        compress = true;

                        buff.Append(trimed + ", ");
                    }
                }
            }

            var len = buff.Length;
            if (len > 0)
            {
                buff.Length = len - 2;
                _extensions = buff.ToString();
            }
        }

        private bool processUnsupportedFrame(WebSocketFrame frame, CloseStatusCode code, string reason)
        {
            processException(new WebSocketException(code, reason), null);

            return false;
        }

        private bool processWebSocketFrame(WebSocketFrame frame)
        {
            return frame.IsCompressed && _compression == CompressionMethod.None
                   ? processUnsupportedFrame(
                       frame,
                       CloseStatusCode.IncorrectData,
                       "A compressed data has been received without available decompression method.")
                   : frame.IsFragmented
                     ? processFragmentedFrame(frame)
                     : frame.IsData
                       ? processDataFrame(frame)
                       : frame.IsPing
                         ? processPingFrame(frame)
                         : frame.IsPong
                           ? processPongFrame(frame)
                           : frame.IsClose
                             ? processCloseFrame(frame)
                             : processUnsupportedFrame(frame, CloseStatusCode.PolicyViolation, null);
        }

        private bool send(Opcode opcode, Stream stream)
        {
            lock (_forSend)
            {
                var src = stream;
                var compressed = false;
                var sent = false;
                try
                {
                    if (_compression != CompressionMethod.None)
                    {
                        stream = stream.Compress(_compression);
                        compressed = true;
                    }

                    sent = send(opcode, Mask.Unmask, stream, compressed);
                    if (!sent)
                        error("Sending a data has been interrupted.");
                }
                catch (Exception ex)
                {
                    error("An exception has occurred while sending a data.");
                }
                finally
                {
                    if (compressed)
                        stream.Dispose();

                    src.Dispose();
                }

                return sent;
            }
        }

        private bool send(Opcode opcode, Mask mask, Stream stream, bool compressed)
        {
            var len = stream.Length;

            /* Not fragmented */

            if (len == 0)
                return send(Fin.Final, opcode, mask, new byte[0], compressed);

            var quo = len / FragmentLength;
            var rem = (int)(len % FragmentLength);

            byte[] buff = null;
            if (quo == 0)
            {
                buff = new byte[rem];
                return stream.Read(buff, 0, rem) == rem &&
                       send(Fin.Final, opcode, mask, buff, compressed);
            }

            buff = new byte[FragmentLength];
            if (quo == 1 && rem == 0)
                return stream.Read(buff, 0, FragmentLength) == FragmentLength &&
                       send(Fin.Final, opcode, mask, buff, compressed);

            /* Send fragmented */

            // Begin
            if (stream.Read(buff, 0, FragmentLength) != FragmentLength ||
                !send(Fin.More, opcode, mask, buff, compressed))
                return false;

            var n = rem == 0 ? quo - 2 : quo - 1;
            for (long i = 0; i < n; i++)
                if (stream.Read(buff, 0, FragmentLength) != FragmentLength ||
                    !send(Fin.More, Opcode.Cont, mask, buff, compressed))
                    return false;

            // End
            if (rem == 0)
                rem = FragmentLength;
            else
                buff = new byte[rem];

            return stream.Read(buff, 0, rem) == rem &&
                   send(Fin.Final, Opcode.Cont, mask, buff, compressed);
        }

        private bool send(Fin fin, Opcode opcode, Mask mask, byte[] data, bool compressed)
        {
            lock (_forConn)
            {
                if (_readyState != WebSocketState.Open)
                {
                    return false;
                }

                return writeBytes(
                  WebSocketFrame.CreateWebSocketFrame(fin, opcode, mask, data, compressed).ToByteArray());
            }
        }

        private void sendAsync(Opcode opcode, Stream stream, Action<bool> completed)
        {
            Func<Opcode, Stream, bool> sender = send;
            sender.BeginInvoke(
              opcode,
              stream,
              ar =>
              {
                  try
                  {
                      var sent = sender.EndInvoke(ar);
                      if (completed != null)
                          completed(sent);
                  }
                  catch (Exception ex)
                  {
                      error("An exception has occurred while callback.");
                  }
              },
              null);
        }

        // As server
        private bool sendHttpResponse(HttpResponse response)
        {
            return writeBytes(response.ToByteArray());
        }

        private void startReceiving()
        {
            if (_messageEventQueue.Count > 0)
                _messageEventQueue.Clear();

            _exitReceiving = new AutoResetEvent(false);
            _receivePong = new AutoResetEvent(false);

            Action receive = null;
            receive = () => WebSocketFrame.ReadAsync(
              _stream,
              true,
              frame =>
              {
                  if (processWebSocketFrame(frame) && _readyState != WebSocketState.Closed)
                  {
                      receive();

                      if (!frame.IsData)
                          return;

                      lock (_forEvent)
                      {
                          try
                          {
                              var e = dequeueFromMessageEventQueue();
                              if (e != null && _readyState == WebSocketState.Open)
                                  OnMessage.Emit(this, e);
                          }
                          catch (Exception ex)
                          {
                              processException(ex, "An exception has occurred while OnMessage.");
                          }
                      }
                  }
                  else if (_exitReceiving != null)
                  {
                      _exitReceiving.Set();
                  }
              },
              ex => processException(ex, "An exception has occurred while receiving a message."));

            receive();
        }

        // As client
        private bool validateSecWebSocketAcceptHeader(string value)
        {
            return value != null && value == CreateResponseKey(_base64Key);
        }

        // As client
        private bool validateSecWebSocketExtensionsHeader(string value)
        {
            var compress = _compression != CompressionMethod.None;
            if (value == null || value.Length == 0)
            {
                if (compress)
                    _compression = CompressionMethod.None;

                return true;
            }

            if (!compress)
                return false;

            var extensions = value.SplitHeaderValue(',');
            if (extensions.Contains(
                  extension => extension.Trim() != _compression.ToExtensionString()))
                return false;

            _extensions = value;
            return true;
        }

        // As server
        private bool validateSecWebSocketKeyHeader(string value)
        {
            if (value == null || value.Length == 0)
                return false;

            _base64Key = value;
            return true;
        }

        // As client
        private bool validateSecWebSocketProtocolHeader(string value)
        {
            if (value == null)
                return _protocols == null;

            if (_protocols == null || !_protocols.Contains(protocol => protocol == value))
                return false;

            _protocol = value;
            return true;
        }

        // As server
        private bool validateSecWebSocketVersionClientHeader(string value)
        {
            return value != null && value == _version;
        }

        // As client
        private bool validateSecWebSocketVersionServerHeader(string value)
        {
            return value == null || value == _version;
        }

        private bool writeBytes(byte[] data)
        {
            try
            {
                _stream.Write(data, 0, data.Length);
                return true;
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        #endregion

        #region Internal Methods

        // As server
        internal void Close(HttpResponse response)
        {
            _readyState = WebSocketState.Closing;

            sendHttpResponse(response);
            closeServerResources();

            _readyState = WebSocketState.Closed;
        }

        // As server
        internal void Close(HttpStatusCode code)
        {
            Close(createHandshakeCloseResponse(code));
        }

        // As server
        public void ConnectAsServer()
        {
            try
            {
                if (acceptHandshake())
                {
                    _readyState = WebSocketState.Open;
                    open();
                }
            }
            catch (Exception ex)
            {
                processException(ex, "An exception has occurred while connecting.");
            }
        }

        internal static string CreateResponseKey(string base64Key)
        {
            var buff = new StringBuilder(base64Key, 64);
            buff.Append(_guid);
            SHA1 sha1 = new SHA1CryptoServiceProvider();
            var src = sha1.ComputeHash(Encoding.UTF8.GetBytes(buff.ToString()));

            return Convert.ToBase64String(src);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Closes the WebSocket connection, and releases all associated resources.
        /// </summary>
        public void Close()
        {
            var msg = _readyState.CheckIfClosable();
            if (msg != null)
            {
                error(msg);

                return;
            }

            var send = _readyState == WebSocketState.Open;
            close(new PayloadData(), send, send);
        }

        /// <summary>
        /// Closes the WebSocket connection with the specified <see cref="CloseStatusCode"/>
        /// and <see cref="string"/>, and releases all associated resources.
        /// </summary>
        /// <remarks>
        /// This method emits a <see cref="OnError"/> event if the size
        /// of <paramref name="reason"/> is greater than 123 bytes.
        /// </remarks>
        /// <param name="code">
        /// One of the <see cref="CloseStatusCode"/> enum values, represents the status code
        /// indicating the reason for the close.
        /// </param>
        /// <param name="reason">
        /// A <see cref="string"/> that represents the reason for the close.
        /// </param>
        public void Close(CloseStatusCode code, string reason)
        {
            byte[] data = null;
            var msg = _readyState.CheckIfClosable() ??
                      (data = ((ushort)code).Append(reason)).CheckIfValidControlData("reason");

            if (msg != null)
            {
                error(msg);

                return;
            }

            var send = _readyState == WebSocketState.Open && !code.IsReserved();
            close(new PayloadData(data), send, send);
        }

        /// <summary>
        /// Sends a binary <paramref name="data"/> asynchronously using the WebSocket connection.
        /// </summary>
        /// <remarks>
        /// This method doesn't wait for the send to be complete.
        /// </remarks>
        /// <param name="data">
        /// An array of <see cref="byte"/> that represents the binary data to send.
        /// </param>
        /// <param name="completed">
        /// An Action&lt;bool&gt; delegate that references the method(s) called when the send is
        /// complete. A <see cref="bool"/> passed to this delegate is <c>true</c> if the send is
        /// complete successfully; otherwise, <c>false</c>.
        /// </param>
        public void SendAsync(byte[] data, Action<bool> completed)
        {
            var msg = _readyState.CheckIfOpen() ?? data.CheckIfValidSendData();
            if (msg != null)
            {
                error(msg);

                return;
            }

            sendAsync(Opcode.Binary, new MemoryStream(data), completed);
        }

        /// <summary>
        /// Sends a text <paramref name="data"/> asynchronously using the WebSocket connection.
        /// </summary>
        /// <remarks>
        /// This method doesn't wait for the send to be complete.
        /// </remarks>
        /// <param name="data">
        /// A <see cref="string"/> that represents the text data to send.
        /// </param>
        /// <param name="completed">
        /// An Action&lt;bool&gt; delegate that references the method(s) called when the send is
        /// complete. A <see cref="bool"/> passed to this delegate is <c>true</c> if the send is
        /// complete successfully; otherwise, <c>false</c>.
        /// </param>
        public void SendAsync(string data, Action<bool> completed)
        {
            var msg = _readyState.CheckIfOpen() ?? data.CheckIfValidSendData();
            if (msg != null)
            {
                error(msg);

                return;
            }

            sendAsync(Opcode.Text, new MemoryStream(Encoding.UTF8.GetBytes(data)), completed);
        }

        #endregion

        #region Explicit Interface Implementation

        /// <summary>
        /// Closes the WebSocket connection, and releases all associated resources.
        /// </summary>
        /// <remarks>
        /// This method closes the WebSocket connection with <see cref="CloseStatusCode.Away"/>.
        /// </remarks>
        void IDisposable.Dispose()
        {
            Close(CloseStatusCode.Away, null);
        }

        #endregion
    }
}