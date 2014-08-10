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

        private AuthenticationChallenge _authChallenge;
        private string _base64Key;
        private RemoteCertificateValidationCallback
                                        _certValidationCallback;
        private bool _client;
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

        #region Public Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="WebSocket"/> class with the specified
        /// WebSocket URL and subprotocols.
        /// </summary>
        /// <param name="url">
        /// A <see cref="string"/> that represents the WebSocket URL to connect.
        /// </param>
        /// <param name="protocols">
        /// An array of <see cref="string"/> that contains the WebSocket subprotocols if any.
        /// Each value of <paramref name="protocols"/> must be a token defined in
        /// <see href="http://tools.ietf.org/html/rfc2616#section-2.2">RFC 2616</see>.
        /// </param>
        /// <exception cref="ArgumentException">
        ///   <para>
        ///   <paramref name="url"/> is invalid.
        ///   </para>
        ///   <para>
        ///   -or-
        ///   </para>
        ///   <para>
        ///   <paramref name="protocols"/> is invalid.
        ///   </para>
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="url"/> is <see langword="null"/>.
        /// </exception>
        public WebSocket(string url, params string[] protocols)
        {
            if (url == null)
                throw new ArgumentNullException("url");

            string msg;
            if (!url.TryCreateWebSocketUri(out _uri, out msg))
                throw new ArgumentException(msg, "url");

            if (protocols != null && protocols.Length > 0)
            {
                msg = protocols.CheckIfValidProtocols();
                if (msg != null)
                    throw new ArgumentException(msg, "protocols");

                _protocols = protocols;
            }

            _base64Key = CreateBase64Key();
            _client = true;
            _secure = _uri.Scheme == "wss";

            init();
        }

        #endregion

        #region Internal Properties

        internal CookieCollection CookieCollection
        {
            get
            {
                return _cookies;
            }
        }

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

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets or sets the compression method used to compress the message on the WebSocket
        /// connection.
        /// </summary>
        /// <value>
        /// One of the <see cref="CompressionMethod"/> enum values, indicates the compression method
        /// used to compress the message. The default value is <see cref="CompressionMethod.None"/>.
        /// </value>
        public CompressionMethod Compression
        {
            get
            {
                return _compression;
            }

            set
            {
                lock (_forConn)
                {
                    var msg = checkIfAvailable("Set operation of Compression", false, false);
                    if (msg != null)
                    {
                        error(msg);

                        return;
                    }

                    _compression = value;
                }
            }
        }

        /// <summary>
        /// Gets the HTTP cookies included in the WebSocket connection request and response.
        /// </summary>
        /// <value>
        /// An IEnumerable&lt;Cookie&gt; instance that provides an enumerator which supports the
        /// iteration over the collection of the cookies.
        /// </value>
        public IEnumerable<Cookie> Cookies
        {
            get
            {
                lock (_cookies.SyncRoot)
                    foreach (Cookie cookie in _cookies)
                        yield return cookie;
            }
        }

        /// <summary>
        /// Gets the credentials for the HTTP authentication (Basic/Digest).
        /// </summary>
        /// <value>
        /// A <see cref="NetworkCredential"/> that represents the credentials for the HTTP
        /// authentication. The default value is <see langword="null"/>.
        /// </value>
        public NetworkCredential Credentials
        {
            get
            {
                return _credentials;
            }
        }

        /// <summary>
        /// Gets the WebSocket extensions selected by the server.
        /// </summary>
        /// <value>
        /// A <see cref="string"/> that represents the extensions if any. The default value is
        /// <see cref="String.Empty"/>.
        /// </value>
        public string Extensions
        {
            get
            {
                return _extensions ?? String.Empty;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the WebSocket connection is alive.
        /// </summary>
        /// <value>
        /// <c>true</c> if the connection is alive; otherwise, <c>false</c>.
        /// </value>
        public bool IsAlive
        {
            get
            {
                return Ping();
            }
        }

        /// <summary>
        /// Gets a value indicating whether the WebSocket connection is secure.
        /// </summary>
        /// <value>
        /// <c>true</c> if the connection is secure; otherwise, <c>false</c>.
        /// </value>
        public bool IsSecure
        {
            get
            {
                return _secure;
            }
        }

        /// <summary>
        /// Gets or sets the value of the Origin header to send with the WebSocket connection request
        /// to the server.
        /// </summary>
        /// <remarks>
        /// The <see cref="WebSocket"/> sends the Origin header if this property has any.
        /// </remarks>
        /// <value>
        ///   <para>
        ///   A <see cref="string"/> that represents the value of the
        ///   <see href="http://tools.ietf.org/html/rfc6454#section-7">HTTP Origin
        ///   header</see> to send. The default value is <see langword="null"/>.
        ///   </para>
        ///   <para>
        ///   The Origin header has the following syntax:
        ///   <c>&lt;scheme&gt;://&lt;host&gt;[:&lt;port&gt;]</c>
        ///   </para>
        /// </value>
        public string Origin
        {
            get
            {
                return _origin;
            }

            set
            {
                lock (_forConn)
                {
                    var msg = checkIfAvailable("Set operation of Origin", false, false);
                    if (msg == null)
                    {
                        if (value.IsNullOrEmpty())
                        {
                            _origin = value;
                            return;
                        }

                        Uri origin;
                        if (!Uri.TryCreate(value, UriKind.Absolute, out origin) || origin.Segments.Length > 1)
                            msg = "The syntax of Origin must be '<scheme>://<host>[:<port>]'.";
                    }

                    if (msg != null)
                    {
                        error(msg);

                        return;
                    }

                    _origin = value.TrimEnd('/');
                }
            }
        }

        /// <summary>
        /// Gets the WebSocket subprotocol selected by the server.
        /// </summary>
        /// <value>
        /// A <see cref="string"/> that represents the subprotocol if any. The default value is
        /// <see cref="String.Empty"/>.
        /// </value>
        public string Protocol
        {
            get
            {
                return _protocol ?? String.Empty;
            }

            internal set
            {
                _protocol = value;
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

        /// <summary>
        /// Gets or sets the callback used to validate the certificate supplied by the server.
        /// </summary>
        /// <remarks>
        /// If the value of this property is <see langword="null"/>, the validation does nothing with
        /// the server certificate, always returns valid.
        /// </remarks>
        /// <value>
        /// A <see cref="RemoteCertificateValidationCallback"/> delegate that references the method(s)
        /// used to validate the server certificate. The default value is <see langword="null"/>.
        /// </value>
        public RemoteCertificateValidationCallback ServerCertificateValidationCallback
        {
            get
            {
                return _certValidationCallback;
            }

            set
            {
                lock (_forConn)
                {
                    var msg = checkIfAvailable(
                      "Set operation of ServerCertificateValidationCallback", false, false);

                    if (msg != null)
                    {
                        error(msg);

                        return;
                    }

                    _certValidationCallback = value;
                }
            }
        }

        /// <summary>
        /// Gets the WebSocket URL to connect.
        /// </summary>
        /// <value>
        /// A <see cref="Uri"/> that represents the WebSocket URL to connect.
        /// </value>
        public Uri Url
        {
            get
            {
                return _client
                       ? _uri
                       : _context.RequestUri;
            }
        }

        #endregion

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

        private string checkIfAvailable(
          string operation, bool availableAsServer, bool availableAsConnected)
        {
            return !_client && !availableAsServer
                   ? operation + " isn't available as a server."
                   : !availableAsConnected
                     ? _readyState.CheckIfConnectable()
                     : null;
        }

        private string checkIfCanConnect()
        {
            return !_client && _readyState == WebSocketState.Closed
                   ? "Connect isn't available to reconnect as a server."
                   : _readyState.CheckIfConnectable();
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

        // As client
        private string checkIfValidHandshakeResponse(HttpResponse response)
        {
            var headers = response.Headers;
            return response.IsUnauthorized
                   ? "HTTP authentication is required."
                   : !response.IsWebSocketResponse
                     ? "Not WebSocket connection response."
                     : !validateSecWebSocketAcceptHeader(headers["Sec-WebSocket-Accept"])
                       ? "Invalid Sec-WebSocket-Accept header."
                       : !validateSecWebSocketProtocolHeader(headers["Sec-WebSocket-Protocol"])
                         ? "Invalid Sec-WebSocket-Protocol header."
                         : !validateSecWebSocketExtensionsHeader(headers["Sec-WebSocket-Extensions"])
                           ? "Invalid Sec-WebSocket-Extensions header."
                           : !validateSecWebSocketVersionServerHeader(headers["Sec-WebSocket-Version"])
                             ? "Invalid Sec-WebSocket-Version header."
                             : null;
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
              _client
              ? closeHandshake(
                  send ? WebSocketFrame.CreateCloseFrame(Mask.Mask, payload).ToByteArray() : null,
                  wait ? 5000 : 0,
                  closeClientResources)
              : closeHandshake(
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

        private void closeAsync(PayloadData payload, bool send, bool wait)
        {
            Action<PayloadData, bool, bool> closer = close;
            closer.BeginInvoke(payload, send, wait, ar => closer.EndInvoke(ar), null);
        }

        // As client
        private void closeClientResources()
        {
            if (_stream != null)
            {
                _stream.Dispose();
                _stream = null;
            }

            if (_tcpClient != null)
            {
                _tcpClient.Close();
                _tcpClient = null;
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

        private bool connect()
        {
            lock (_forConn)
            {
                var msg = _readyState.CheckIfConnectable();
                if (msg != null)
                {
                    error(msg);

                    return false;
                }

                try
                {
                    if (_client ? doHandshake() : acceptHandshake())
                    {
                        _readyState = WebSocketState.Open;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    processException(ex, "An exception has occurred while connecting.");
                }

                return false;
            }
        }

        // As client
        private string createExtensions()
        {
            var buff = new StringBuilder(32);

            if (_compression != CompressionMethod.None)
                buff.Append(_compression.ToExtensionString());

            return buff.Length > 0
                   ? buff.ToString()
                   : null;
        }

        // As server
        private HttpResponse createHandshakeCloseResponse(HttpStatusCode code)
        {
            var res = HttpResponse.CreateCloseResponse(code);
            res.Headers["Sec-WebSocket-Version"] = _version;

            return res;
        }

        // As client
        private HttpRequest createHandshakeRequest()
        {
            var req = HttpRequest.CreateWebSocketRequest(_uri);

            var headers = req.Headers;
            if (!_origin.IsNullOrEmpty())
                headers["Origin"] = _origin;

            headers["Sec-WebSocket-Key"] = _base64Key;

            if (_protocols != null)
                headers["Sec-WebSocket-Protocol"] = _protocols.ToString(", ");

            var extensions = createExtensions();
            if (extensions != null)
                headers["Sec-WebSocket-Extensions"] = extensions;

            headers["Sec-WebSocket-Version"] = _version;

            AuthenticationResponse authRes = null;
            if (_authChallenge != null && _credentials != null)
            {
                authRes = new AuthenticationResponse(_authChallenge, _credentials, _nonceCount);
                _nonceCount = authRes.NonceCount;
            }
            else if (_preAuth)
            {
                authRes = new AuthenticationResponse(_credentials);
            }

            if (authRes != null)
                headers["Authorization"] = authRes.ToString();

            if (_cookies.Count > 0)
                req.SetCookies(_cookies);

            return req;
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

        // As client
        private bool doHandshake()
        {
            setClientStream();
            var res = sendHandshakeRequest();
            var msg = checkIfValidHandshakeResponse(res);
            if (msg != null)
            {
                msg = "An error has occurred while connecting.";
                error(msg);
                close(CloseStatusCode.Abnormal, msg, false);

                return false;
            }

            var cookies = res.Cookies;
            if (cookies.Count > 0)
                _cookies.SetOrRemove(cookies);

            return true;
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
            if (_readyState == WebSocketState.Connecting && !_client)
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
            var mask = _client ? Mask.Mask : Mask.Unmask;

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

        private bool send(byte[] frameAsBytes)
        {
            lock (_forConn)
            {
                if (_readyState != WebSocketState.Open)
                {
                    return false;
                }

                return writeBytes(frameAsBytes);
            }
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

                    sent = send(opcode, _client ? Mask.Mask : Mask.Unmask, stream, compressed);
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

        // As client
        private HttpResponse sendHandshakeRequest()
        {
            var req = createHandshakeRequest();
            var res = sendHttpRequest(req, 90000);
            if (res.IsUnauthorized)
            {
                _authChallenge = res.AuthenticationChallenge;
                if (_credentials != null &&
                    (!_preAuth || _authChallenge.Scheme == AuthenticationSchemes.Digest))
                {
                    if (res.Headers.Contains("Connection", "close"))
                    {
                        closeClientResources();
                        setClientStream();
                    }

                    var authRes = new AuthenticationResponse(_authChallenge, _credentials, _nonceCount);
                    _nonceCount = authRes.NonceCount;
                    req.Headers["Authorization"] = authRes.ToString();
                    res = sendHttpRequest(req, 15000);
                }
            }

            return res;
        }

        // As client
        private HttpResponse sendHttpRequest(HttpRequest request, int millisecondsTimeout)
        {
            var res = request.GetResponse(_stream, millisecondsTimeout);

            return res;
        }

        // As server
        private bool sendHttpResponse(HttpResponse response)
        {
            return writeBytes(response.ToByteArray());
        }

        // As client
        private HttpResponse sendProxyConnectRequest()
        {
            var req = HttpRequest.CreateConnectRequest(_uri);
            var res = sendHttpRequest(req, 90000);
            if (res.IsProxyAuthenticationRequired)
            {
                var authChal = res.ProxyAuthenticationChallenge;
                if (authChal != null && _proxyCredentials != null)
                {
                    if (res.Headers.Contains("Connection", "close"))
                    {
                        closeClientResources();
                        _tcpClient = new TcpClient(_proxyUri.DnsSafeHost, _proxyUri.Port);
                        _stream = _tcpClient.GetStream();
                    }

                    var authRes = new AuthenticationResponse(authChal, _proxyCredentials, 0);
                    req.Headers["Proxy-Authorization"] = authRes.ToString();
                    res = sendHttpRequest(req, 15000);
                }
            }

            return res;
        }

        // As client
        private void setClientStream()
        {
            var proxy = _proxyUri != null;
            _tcpClient = proxy
                         ? new TcpClient(_proxyUri.DnsSafeHost, _proxyUri.Port)
                         : new TcpClient(_uri.DnsSafeHost, _uri.Port);

            _stream = _tcpClient.GetStream();

            if (proxy)
            {
                var res = sendProxyConnectRequest();
                if (res.IsProxyAuthenticationRequired)
                    throw new WebSocketException("Proxy authentication is required.");

                if (res.StatusCode[0] != '2')
                    throw new WebSocketException(
                      "The proxy has failed a connection to the requested host and port.");
            }

            if (_secure)
            {
                var sslStream = new SslStream(
                  _stream,
                  false,
                  _certValidationCallback ?? ((sender, certificate, chain, sslPolicyErrors) => true));

                sslStream.AuthenticateAsClient(_uri.DnsSafeHost);
                _stream = sslStream;
            }
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
        internal void Close(CloseEventArgs e, byte[] frameAsBytes, int millisecondsTimeout)
        {
            lock (_forConn)
            {
                if (_readyState == WebSocketState.Closing || _readyState == WebSocketState.Closed)
                {
                    return;
                }

                _readyState = WebSocketState.Closing;
            }

            e.WasClean = closeHandshake(frameAsBytes, millisecondsTimeout, closeServerResources);

            _readyState = WebSocketState.Closed;
            try
            {
                OnClose.Emit(this, e);
            }
            catch (Exception ex)
            {
            }
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

        // As client
        internal static string CreateBase64Key()
        {
            var src = new byte[16];
            var rand = new Random();
            rand.NextBytes(src);

            return Convert.ToBase64String(src);
        }

        internal static string CreateResponseKey(string base64Key)
        {
            var buff = new StringBuilder(base64Key, 64);
            buff.Append(_guid);
            SHA1 sha1 = new SHA1CryptoServiceProvider();
            var src = sha1.ComputeHash(Encoding.UTF8.GetBytes(buff.ToString()));

            return Convert.ToBase64String(src);
        }

        internal bool Ping(byte[] frameAsBytes, int millisecondsTimeout)
        {
            try
            {
                AutoResetEvent pong;
                return _readyState == WebSocketState.Open &&
                       send(frameAsBytes) &&
                       (pong = _receivePong) != null &&
                       pong.WaitOne(millisecondsTimeout);
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        // As server, used to broadcast
        internal void Send(Opcode opcode, byte[] data, Dictionary<CompressionMethod, byte[]> cache)
        {
            lock (_forSend)
            {
                lock (_forConn)
                {
                    if (_readyState != WebSocketState.Open)
                    {
                        return;
                    }

                    try
                    {
                        byte[] cached;
                        if (!cache.TryGetValue(_compression, out cached))
                        {
                            cached = WebSocketFrame.CreateWebSocketFrame(
                              Fin.Final,
                              opcode,
                              Mask.Unmask,
                              data.Compress(_compression),
                              _compression != CompressionMethod.None)
                              .ToByteArray();

                            cache.Add(_compression, cached);
                        }

                        writeBytes(cached);
                    }
                    catch (Exception ex)
                    {
                    }
                }
            }
        }

        // As server, used to broadcast
        internal void Send(Opcode opcode, Stream stream, Dictionary<CompressionMethod, Stream> cache)
        {
            lock (_forSend)
            {
                try
                {
                    Stream cached;
                    if (!cache.TryGetValue(_compression, out cached))
                    {
                        cached = stream.Compress(_compression);
                        cache.Add(_compression, cached);
                    }
                    else
                    {
                        cached.Position = 0;
                    }

                    send(opcode, Mask.Unmask, cached, _compression != CompressionMethod.None);
                }
                catch (Exception ex)
                {
                }
            }
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
        /// Closes the WebSocket connection with the specified <see cref="ushort"/>,
        /// and releases all associated resources.
        /// </summary>
        /// <remarks>
        /// This method emits a <see cref="OnError"/> event if <paramref name="code"/>
        /// isn't in the allowable range of the WebSocket close status code.
        /// </remarks>
        /// <param name="code">
        /// A <see cref="ushort"/> that represents the status code indicating the reason for the close.
        /// </param>
        public void Close(ushort code)
        {
            Close(code, null);
        }

        /// <summary>
        /// Closes the WebSocket connection with the specified <see cref="CloseStatusCode"/>,
        /// and releases all associated resources.
        /// </summary>
        /// <param name="code">
        /// One of the <see cref="CloseStatusCode"/> enum values, represents the status code
        /// indicating the reason for the close.
        /// </param>
        public void Close(CloseStatusCode code)
        {
            Close(code, null);
        }

        /// <summary>
        /// Closes the WebSocket connection with the specified <see cref="ushort"/>
        /// and <see cref="string"/>, and releases all associated resources.
        /// </summary>
        /// <remarks>
        /// This method emits a <see cref="OnError"/> event if <paramref name="code"/>
        /// isn't in the allowable range of the WebSocket close status code or the size
        /// of <paramref name="reason"/> is greater than 123 bytes.
        /// </remarks>
        /// <param name="code">
        /// A <see cref="ushort"/> that represents the status code indicating the reason for the close.
        /// </param>
        /// <param name="reason">
        /// A <see cref="string"/> that represents the reason for the close.
        /// </param>
        public void Close(ushort code, string reason)
        {
            byte[] data = null;
            var msg = _readyState.CheckIfClosable() ??
                      code.CheckIfValidCloseStatusCode() ??
                      (data = code.Append(reason)).CheckIfValidControlData("reason");

            if (msg != null)
            {
                error(msg);

                return;
            }

            var send = _readyState == WebSocketState.Open && !code.IsReserved();
            close(new PayloadData(data), send, send);
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
        /// Closes the WebSocket connection asynchronously, and releases all associated resources.
        /// </summary>
        /// <remarks>
        /// This method doesn't wait for the close to be complete.
        /// </remarks>
        public void CloseAsync()
        {
            var msg = _readyState.CheckIfClosable();
            if (msg != null)
            {
                error(msg);

                return;
            }

            var send = _readyState == WebSocketState.Open;
            closeAsync(new PayloadData(), send, send);
        }

        /// <summary>
        /// Closes the WebSocket connection asynchronously with the specified <see cref="ushort"/>,
        /// and releases all associated resources.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///   This method doesn't wait for the close to be complete.
        ///   </para>
        ///   <para>
        ///   This method emits a <see cref="OnError"/> event if <paramref name="code"/>
        ///   isn't in the allowable range of the WebSocket close status code.
        ///   </para>
        /// </remarks>
        /// <param name="code">
        /// A <see cref="ushort"/> that represents the status code indicating the reason for the close.
        /// </param>
        public void CloseAsync(ushort code)
        {
            CloseAsync(code, null);
        }

        /// <summary>
        /// Closes the WebSocket connection asynchronously with the specified
        /// <see cref="CloseStatusCode"/>, and releases all associated resources.
        /// </summary>
        /// <remarks>
        /// This method doesn't wait for the close to be complete.
        /// </remarks>
        /// <param name="code">
        /// One of the <see cref="CloseStatusCode"/> enum values, represents the status code
        /// indicating the reason for the close.
        /// </param>
        public void CloseAsync(CloseStatusCode code)
        {
            CloseAsync(code, null);
        }

        /// <summary>
        /// Closes the WebSocket connection asynchronously with the specified <see cref="ushort"/>
        /// and <see cref="string"/>, and releases all associated resources.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///   This method doesn't wait for the close to be complete.
        ///   </para>
        ///   <para>
        ///   This method emits a <see cref="OnError"/> event if <paramref name="code"/>
        ///   isn't in the allowable range of the WebSocket close status code or the size
        ///   of <paramref name="reason"/> is greater than 123 bytes.
        ///   </para>
        /// </remarks>
        /// <param name="code">
        /// A <see cref="ushort"/> that represents the status code indicating the reason for the close.
        /// </param>
        /// <param name="reason">
        /// A <see cref="string"/> that represents the reason for the close.
        /// </param>
        public void CloseAsync(ushort code, string reason)
        {
            byte[] data = null;
            var msg = _readyState.CheckIfClosable() ??
                      code.CheckIfValidCloseStatusCode() ??
                      (data = code.Append(reason)).CheckIfValidControlData("reason");

            if (msg != null)
            {
                error(msg);

                return;
            }

            var send = _readyState == WebSocketState.Open && !code.IsReserved();
            closeAsync(new PayloadData(data), send, send);
        }

        /// <summary>
        /// Closes the WebSocket connection asynchronously with the specified
        /// <see cref="CloseStatusCode"/> and <see cref="string"/>, and releases
        /// all associated resources.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///   This method doesn't wait for the close to be complete.
        ///   </para>
        ///   <para>
        ///   This method emits a <see cref="OnError"/> event if the size
        ///   of <paramref name="reason"/> is greater than 123 bytes.
        ///   </para>
        /// </remarks>
        /// <param name="code">
        /// One of the <see cref="CloseStatusCode"/> enum values, represents the status code
        /// indicating the reason for the close.
        /// </param>
        /// <param name="reason">
        /// A <see cref="string"/> that represents the reason for the close.
        /// </param>
        public void CloseAsync(CloseStatusCode code, string reason)
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
            closeAsync(new PayloadData(data), send, send);
        }

        /// <summary>
        /// Establishes a WebSocket connection.
        /// </summary>
        public void Connect()
        {
            var msg = checkIfCanConnect();
            if (msg != null)
            {
                error(msg);

                return;
            }

            if (connect())
                open();
        }

        /// <summary>
        /// Establishes a WebSocket connection asynchronously.
        /// </summary>
        /// <remarks>
        /// This method doesn't wait for the connect to be complete.
        /// </remarks>
        public void ConnectAsync()
        {
            var msg = checkIfCanConnect();
            if (msg != null)
            {
                error(msg);

                return;
            }

            Func<bool> connector = connect;
            connector.BeginInvoke(
              ar =>
              {
                  if (connector.EndInvoke(ar))
                      open();
              },
              null);
        }

        /// <summary>
        /// Sends a Ping using the WebSocket connection.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the <see cref="WebSocket"/> receives a Pong to this Ping in a time;
        /// otherwise, <c>false</c>.
        /// </returns>
        public bool Ping()
        {
            return _client
                   ? Ping(WebSocketFrame.CreatePingFrame(Mask.Mask).ToByteArray(), 5000)
                   : Ping(WebSocketFrame.EmptyUnmaskPingData, 1000);
        }

        /// <summary>
        /// Sends a Ping with the specified <paramref name="message"/> using the WebSocket connection.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the <see cref="WebSocket"/> receives a Pong to this Ping in a time;
        /// otherwise, <c>false</c>.
        /// </returns>
        /// <param name="message">
        /// A <see cref="string"/> that represents the message to send.
        /// </param>
        public bool Ping(string message)
        {
            if (message == null || message.Length == 0)
                return Ping();

            var data = Encoding.UTF8.GetBytes(message);
            var msg = data.CheckIfValidControlData("message");
            if (msg != null)
            {
                error(msg);

                return false;
            }

            return _client
                   ? Ping(WebSocketFrame.CreatePingFrame(Mask.Mask, data).ToByteArray(), 5000)
                   : Ping(WebSocketFrame.CreatePingFrame(Mask.Unmask, data).ToByteArray(), 1000);
        }

        /// <summary>
        /// Sends a binary <paramref name="data"/> using the WebSocket connection.
        /// </summary>
        /// <param name="data">
        /// An array of <see cref="byte"/> that represents the binary data to send.
        /// </param>
        public void Send(byte[] data)
        {
            var msg = _readyState.CheckIfOpen() ?? data.CheckIfValidSendData();
            if (msg != null)
            {
                error(msg);

                return;
            }

            send(Opcode.Binary, new MemoryStream(data));
        }

        /// <summary>
        /// Sends the specified <paramref name="file"/> as a binary data
        /// using the WebSocket connection.
        /// </summary>
        /// <param name="file">
        /// A <see cref="FileInfo"/> that represents the file to send.
        /// </param>
        public void Send(FileInfo file)
        {
            var msg = _readyState.CheckIfOpen() ?? file.CheckIfValidSendData();
            if (msg != null)
            {
                error(msg);

                return;
            }

            send(Opcode.Binary, file.OpenRead());
        }

        /// <summary>
        /// Sends a text <paramref name="data"/> using the WebSocket connection.
        /// </summary>
        /// <param name="data">
        /// A <see cref="string"/> that represents the text data to send.
        /// </param>
        public void Send(string data)
        {
            var msg = _readyState.CheckIfOpen() ?? data.CheckIfValidSendData();
            if (msg != null)
            {
                error(msg);

                return;
            }

            send(Opcode.Text, new MemoryStream(Encoding.UTF8.GetBytes(data)));
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
        /// Sends the specified <paramref name="file"/> as a binary data asynchronously
        /// using the WebSocket connection.
        /// </summary>
        /// <remarks>
        /// This method doesn't wait for the send to be complete.
        /// </remarks>
        /// <param name="file">
        /// A <see cref="FileInfo"/> that represents the file to send.
        /// </param>
        /// <param name="completed">
        /// An Action&lt;bool&gt; delegate that references the method(s) called when the send is
        /// complete. A <see cref="bool"/> passed to this delegate is <c>true</c> if the send is
        /// complete successfully; otherwise, <c>false</c>.
        /// </param>
        public void SendAsync(FileInfo file, Action<bool> completed)
        {
            var msg = _readyState.CheckIfOpen() ?? file.CheckIfValidSendData();
            if (msg != null)
            {
                error(msg);

                return;
            }

            sendAsync(Opcode.Binary, file.OpenRead(), completed);
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

        /// <summary>
        /// Sends a binary data from the specified <see cref="Stream"/> asynchronously
        /// using the WebSocket connection.
        /// </summary>
        /// <remarks>
        /// This method doesn't wait for the send to be complete.
        /// </remarks>
        /// <param name="stream">
        /// A <see cref="Stream"/> from which contains the binary data to send.
        /// </param>
        /// <param name="length">
        /// An <see cref="int"/> that represents the number of bytes to send.
        /// </param>
        /// <param name="completed">
        /// An Action&lt;bool&gt; delegate that references the method(s) called when the send is
        /// complete. A <see cref="bool"/> passed to this delegate is <c>true</c> if the send is
        /// complete successfully; otherwise, <c>false</c>.
        /// </param>
        public void SendAsync(Stream stream, int length, Action<bool> completed)
        {
            var msg = _readyState.CheckIfOpen() ??
                      stream.CheckIfCanRead() ??
                      (length < 1 ? "'length' must be greater than 0." : null);

            if (msg != null)
            {
                error(msg);

                return;
            }

            stream.ReadBytesAsync(
              length,
              data =>
              {
                  var len = data.Length;
                  if (len == 0)
                  {
                      msg = "A data cannot be read from 'stream'.";
                      error(msg);

                      return;
                  }

                  var sent = send(Opcode.Binary, new MemoryStream(data));
                  if (completed != null)
                      completed(sent);
              },
              ex => error("An exception has occurred while sending a data."));
        }

        /// <summary>
        /// Sets an HTTP <paramref name="cookie"/> to send with the WebSocket connection request
        /// to the server.
        /// </summary>
        /// <param name="cookie">
        /// A <see cref="Cookie"/> that represents the cookie to send.
        /// </param>
        public void SetCookie(Cookie cookie)
        {
            lock (_forConn)
            {
                var msg = checkIfAvailable("SetCookie", false, false) ??
                          (cookie == null ? "'cookie' must not be null." : null);

                if (msg != null)
                {
                    error(msg);

                    return;
                }

                lock (_cookies.SyncRoot)
                {
                    _cookies.SetOrRemove(cookie);
                }
            }
        }

        /// <summary>
        /// Sets a pair of <paramref name="username"/> and <paramref name="password"/> for
        /// the HTTP authentication (Basic/Digest).
        /// </summary>
        /// <param name="username">
        /// A <see cref="string"/> that represents the user name used to authenticate.
        /// </param>
        /// <param name="password">
        /// A <see cref="string"/> that represents the password for <paramref name="username"/>
        /// used to authenticate.
        /// </param>
        /// <param name="preAuth">
        /// <c>true</c> if the <see cref="WebSocket"/> sends the Basic authentication credentials
        /// with the first connection request to the server; otherwise, <c>false</c>.
        /// </param>
        public void SetCredentials(string username, string password, bool preAuth)
        {
            lock (_forConn)
            {
                var msg = checkIfAvailable("SetCredentials", false, false);
                if (msg == null)
                {
                    if (username.IsNullOrEmpty())
                    {
                        _credentials = null;
                        _preAuth = false;

                        return;
                    }

                    msg = username.Contains(':') || !username.IsText()
                          ? "'username' contains an invalid character."
                          : !password.IsNullOrEmpty() && !password.IsText()
                            ? "'password' contains an invalid character."
                            : null;
                }

                if (msg != null)
                {
                    error(msg);

                    return;
                }

                _credentials = new NetworkCredential(username, password, _uri.PathAndQuery);
                _preAuth = preAuth;
            }
        }

        /// <summary>
        /// Sets the HTTP Proxy server URL to connect through, and a pair of <paramref name="username"/>
        /// and <paramref name="password"/> for the proxy server authentication (Basic/Digest).
        /// </summary>
        /// <param name="url">
        /// A <see cref="string"/> that represents the HTTP Proxy server URL to connect through.
        /// </param>
        /// <param name="username">
        /// A <see cref="string"/> that represents the user name used to authenticate.
        /// </param>
        /// <param name="password">
        /// A <see cref="string"/> that represents the password for <paramref name="username"/>
        /// used to authenticate.
        /// </param>
        public void SetHttpProxy(string url, string username, string password)
        {
            lock (_forConn)
            {
                var msg = checkIfAvailable("SetHttpProxy", false, false);
                if (msg == null)
                {
                    if (url.IsNullOrEmpty())
                    {
                        _proxyUri = null;
                        _proxyCredentials = null;

                        return;
                    }

                    Uri uri;
                    if (!Uri.TryCreate(url, UriKind.Absolute, out uri) ||
                        uri.Scheme != "http" ||
                        uri.Segments.Length > 1)
                    {
                        msg = "The syntax of proxy url must be 'http://<host>[:<port>]'.";
                    }
                    else
                    {
                        _proxyUri = uri;

                        if (username.IsNullOrEmpty())
                        {
                            _proxyCredentials = null;

                            return;
                        }

                        msg = username.Contains(':') || !username.IsText()
                              ? "'username' contains an invalid character."
                              : !password.IsNullOrEmpty() && !password.IsText()
                                ? "'password' contains an invalid character."
                                : null;
                    }
                }

                if (msg != null)
                {
                    error(msg);

                    return;
                }

                _proxyCredentials = new NetworkCredential(
                  username, password, String.Format("{0}:{1}", _uri.DnsSafeHost, _uri.Port));
            }
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