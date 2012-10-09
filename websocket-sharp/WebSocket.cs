#region MIT License
/**
 * WebSocket.cs
 *
 * A C# implementation of a WebSocket protocol client.
 * This code derived from WebSocket.java (http://github.com/adamac/Java-WebSocket-client).
 *
 * The MIT License
 *
 * Copyright (c) 2009 Adam MacBeth
 * Copyright (c) 2010-2012 sta.blockhead
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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Security.Cryptography;
using WebSocketSharp.Frame;
using WebSocketSharp.Net;
using WebSocketSharp.Net.Sockets;

namespace WebSocketSharp {

  public class WebSocket : IDisposable
  {
    #region Private Const Fields

    private const int    _fragmentLen = 1016; // Max value is int.MaxValue - 14.
    private const string _guid        = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    private const string _version     = "13";

    #endregion

    #region Private Fields

    private string                          _base64key;
    private HttpListenerContext             _baseContext;
    private string                          _binaryType;
    private WebSocketContext                _context;
    private System.Net.IPEndPoint           _endPoint;
    private AutoResetEvent                  _exitMessageLoop;
    private string                          _extensions;
    private Object                          _forClose;
    private Object                          _forSend;
    private bool                            _isClient;
    private bool                            _isSecure;
    private string                          _protocol;
    private string                          _protocols;
    private volatile WsState                _readyState;
    private AutoResetEvent                  _receivePong;
    private TcpClient                       _tcpClient;
    private Uri                             _uri;
    private SynchronizedCollection<WsFrame> _unTransmittedBuffer;
    private WsStream                        _wsStream;

    #endregion

    #region Private Constructor

    private WebSocket()
    {
      _binaryType          = String.Empty;
      _extensions          = String.Empty;
      _forClose            = new Object();
      _forSend             = new Object();
      _protocol            = String.Empty;
      _readyState          = WsState.CONNECTING;
      _receivePong         = new AutoResetEvent(false);
      _unTransmittedBuffer = new SynchronizedCollection<WsFrame>();
    }

    #endregion

    #region Internal Constructor

    internal WebSocket(TcpListenerWebSocketContext context)
      : this()
    {
      _uri       = context.RequestUri;
      _context   = context;
      _tcpClient = context.Client;
      _wsStream  = context.Stream;
      _endPoint  = (System.Net.IPEndPoint)_tcpClient.Client.LocalEndPoint;
      _isClient  = false;
      _isSecure  = context.IsSecureConnection;
    }

    internal WebSocket(Uri uri, HttpListenerWebSocketContext context)
      : this()
    {
      _uri         = uri;
      _context     = context;
      _baseContext = context.BaseContext;
      _wsStream    = context.Stream;
      _endPoint    = _baseContext.Connection.LocalEndPoint;
      _isClient    = false;
      _isSecure    = context.IsSecureConnection;
    }

    internal WebSocket(Uri uri, TcpClient tcpClient)
      : this()
    {
      _uri       = uri;
      _tcpClient = tcpClient;
      _wsStream  = WsStream.CreateServerStream(tcpClient);
      _endPoint  = (System.Net.IPEndPoint)tcpClient.Client.LocalEndPoint;
      _isClient  = false;
      _isSecure  = _wsStream.IsSecure;
    }

    #endregion

    #region Public Constructors

    public WebSocket(string url, params string[] protocols)
      : this()
    {
      var uri = url.ToUri();

      string msg;
      if (!uri.IsValidWsUri(out msg))
        throw new ArgumentException(msg, "url");

      _uri       = uri;
      _protocols = protocols.ToString(", ");
      _isClient  = true;
      _isSecure  = uri.Scheme == "wss" ? true : false;
    }

    public WebSocket(
      string                         url,
      EventHandler                   onOpen,
      EventHandler<MessageEventArgs> onMessage,
      EventHandler<ErrorEventArgs>   onError,
      EventHandler<CloseEventArgs>   onClose,
      params string[]                protocols)
      : this(url, protocols)
    {
      OnOpen    = onOpen;
      OnMessage = onMessage;
      OnError   = onError;
      OnClose   = onClose;

      Connect();
    }

    #endregion

    #region Properties

    public string BinaryType {
      get { return _binaryType; }
    }

    public ulong BufferedAmount {
      get {
        ulong bufferedAmount = 0;

        lock (_unTransmittedBuffer.SyncRoot)
        {
          foreach (WsFrame frame in _unTransmittedBuffer)
            bufferedAmount += frame.PayloadLength;
        }

        return bufferedAmount;
      }
    }

    public string Extensions {
      get { return _extensions; }
    }

    public bool IsAlive {
      get {
        if (_readyState != WsState.OPEN)
          return false;

        return Ping();
      }
    }

    public bool IsSecure {
      get {
        return _isSecure;
      }
    }

    public string Protocol {
      get { return _protocol; }
    }

    public WsState ReadyState {
      get { return _readyState; }

      private set {
        _readyState = value;

        switch (value)
        {
          case WsState.OPEN:
            startMessageThread();
            OnOpen.Emit(this, EventArgs.Empty);
            break;
          case WsState.CLOSING:
            break;
          case WsState.CLOSED:
            closeConnection();
            break;
        }
      }
    }

    public SynchronizedCollection<WsFrame> UnTransmittedBuffer {
      get { return _unTransmittedBuffer; }
    }

    public Uri Url {
      get { return _uri; }
      set {
        if (_readyState != WsState.CONNECTING || _isClient)
          return;

        _uri = value;
      }
    }

    #endregion

    #region Events

    public event EventHandler                   OnOpen;
    public event EventHandler<MessageEventArgs> OnMessage;
    public event EventHandler<ErrorEventArgs>   OnError;
    public event EventHandler<CloseEventArgs>   OnClose;

    #endregion

    #region Private Methods

    private void acceptHandshake()
    {
      var req = receiveOpeningHandshake();

      string msg;
      if (!isValidRequest(req, out msg))
      {
        error(msg);
        close(CloseStatusCode.HANDSHAKE_FAILURE, msg);
        return;
      }

      sendResponseHandshake();
      ReadyState = WsState.OPEN;
    }

    private void close(HttpStatusCode code)
    {
      if (_readyState != WsState.CONNECTING || _isClient)
        return;

      sendResponseHandshake(code);
      ReadyState = WsState.CLOSED;
    }

    private void close(PayloadData data)
    {
      #if DEBUG
      Console.WriteLine("WS: Info@close: Current thread IsBackground ?: {0}", Thread.CurrentThread.IsBackground);
      #endif
      lock(_forClose)
      {
        if (_readyState == WsState.CLOSING ||
            _readyState == WsState.CLOSED)
          return;

        if (_readyState == WsState.CONNECTING && !_isClient)
        {
          OnClose.Emit(this, new CloseEventArgs(data));
          close(HttpStatusCode.BadRequest);
          return;
        }

        ReadyState = WsState.CLOSING;
      }

      OnClose.Emit(this, new CloseEventArgs(data));
      var frame = createFrame(Fin.FINAL, Opcode.CLOSE, data);
      closeHandshake(frame);
      #if DEBUG
      Console.WriteLine("WS: Info@close: Exit close method.");
      #endif
    }

    private void close(CloseStatusCode code, string reason)
    {
      var data = new List<byte>(((ushort)code).ToBytes(ByteOrder.BIG));

      if (!String.IsNullOrEmpty(reason))
      {
        var buffer = Encoding.UTF8.GetBytes(reason);
        data.AddRange(buffer);
      }

      var payloadData = new PayloadData(data.ToArray());
      if (payloadData.Length > 125)
      {
        var msg = "Close frame must have a payload length of 125 bytes or less.";
        error(msg);
        return;
      }

      close(payloadData);
    }

    private void closeConnection()
    {
      if (_baseContext != null)
      {
        _baseContext.Response.Close();
        _wsStream    = null;
        _baseContext = null;
      }

      if (_wsStream != null)
      {
        _wsStream.Dispose();
        _wsStream = null;
      }

      if (_tcpClient != null)
      {
        _tcpClient.Close();
        _tcpClient = null;
      }
    }

    private void closeHandshake(WsFrame frame)
    {
      if (send(frame) && !Thread.CurrentThread.IsBackground)
        if (_exitMessageLoop != null)
          _exitMessageLoop.WaitOne(5 * 1000);

      ReadyState = WsState.CLOSED;
    }

    private void createClientStream()
    {
      var host = _uri.DnsSafeHost;
      var port = _uri.Port;
      if (port <= 0)
        port = IsSecure ? 443 : 80;

      _wsStream = WsStream.CreateClientStream(host, port, out _tcpClient);
    }

    private string createExpectedKey()
    {
      SHA1 sha1 = new SHA1CryptoServiceProvider();
      var  sb   = new StringBuilder(_base64key);

      sb.Append(_guid);
      var keySrc = sha1.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));

      return Convert.ToBase64String(keySrc);
    }

    private WsFrame createFrame(Fin fin, Opcode opcode, PayloadData payloadData)
    {
      return _isClient
             ? new WsFrame(fin, opcode, payloadData)
             : new WsFrame(fin, opcode, Mask.UNMASK, payloadData);
    }

    private RequestHandshake createOpeningHandshake()
    {
      var path = _uri.PathAndQuery;
      var host = _uri.DnsSafeHost;
      var port = ((System.Net.IPEndPoint)_tcpClient.Client.RemoteEndPoint).Port;
      if (port != 80)
        host += ":" + port;

      var keySrc = new byte[16];
      var rand   = new Random();
      rand.NextBytes(keySrc);
      _base64key = Convert.ToBase64String(keySrc);
      
      var req = new RequestHandshake(path);
      req.AddHeader("Host", host);
      req.AddHeader("Sec-WebSocket-Key", _base64key);
      if (!String.IsNullOrEmpty(_protocols))
        req.AddHeader("Sec-WebSocket-Protocol", _protocols);
      req.AddHeader("Sec-WebSocket-Version", _version);

      return req;
    }

    private ResponseHandshake createResponseHandshake()
    {
      var res = new ResponseHandshake();
      res.AddHeader("Sec-WebSocket-Accept", createExpectedKey());

      return res;
    }

    private ResponseHandshake createResponseHandshake(HttpStatusCode code)
    {
      var res = ResponseHandshake.CreateCloseResponse(code);
      res.AddHeader("Sec-WebSocket-Version", _version);

      return res;
    }

    private void doHandshake()
    {
      var res = sendOpeningHandshake();

      string msg;
      if (!isValidResponse(res, out msg))
      {
        error(msg);
        close(CloseStatusCode.HANDSHAKE_FAILURE, msg);
        return;
      }

      ReadyState = WsState.OPEN;
    }

    private void error(string message)
    {
      #if DEBUG
      var callerFrame = new StackFrame(1);
      var caller      = callerFrame.GetMethod();
      Console.WriteLine("WS: Error@{0}: {1}", caller.Name, message);
      #endif
      OnError.Emit(this, new ErrorEventArgs(message));
    }

    private bool isValidRequest(RequestHandshake request, out string message)
    {
      Func<string, Func<string, string, string>> func = s =>
      {
        return (e, a) =>
        {
          return String.Format("Invalid request {0} value: {1}(expected: {2})", s, a, e);
        };
      };

      if (!request.IsWebSocketRequest)
      {
        message = "Invalid WebSocket request.";
        return false;
      }

      if (!isValidRequestUri(request.RequestUri, func("Request URI"), out message))
        return false;

      if (_uri.IsAbsoluteUri)
        if (!isValidRequestHost(request.GetHeaderValues("Host")[0], func("Host"), out message))
          return false;

      if (!request.HeaderExists("Sec-WebSocket-Version", _version))
      {
        message = "Unsupported Sec-WebSocket-Version.";
        return false;
      }

      _base64key = request.GetHeaderValues("Sec-WebSocket-Key")[0];

      if (request.HeaderExists("Sec-WebSocket-Protocol"))
        _protocols = request.Headers["Sec-WebSocket-Protocol"];

      if (request.HeaderExists("Sec-WebSocket-Extensions"))
        _extensions = request.Headers["Sec-WebSocket-Extensions"];

      message = String.Empty;
      return true;
    }

    private bool isValidRequestHost(string value, Func<string, string, string> func, out string message)
    {
      var host = _uri.DnsSafeHost;
      var type = Uri.CheckHostName(host);

      var address = _endPoint.Address;
      var port    = _endPoint.Port;

      var expectedHost1 = host;
      var expectedHost2 = address.ToString();
      if (type != UriHostNameType.Dns)
        expectedHost2 = System.Net.Dns.GetHostEntry(address).HostName;

      if (port != 80)
      {
        expectedHost1 += ":" + port;
        expectedHost2 += ":" + port;
      }

      if (expectedHost1.NotEqualsDo(value, func, out message, false))
        if (expectedHost2.NotEqualsDo(value, func, out message, false))
          return false;

      message = String.Empty;
      return true;
    }

    private bool isValidRequestUri(Uri requestUri, Func<string, string, string> func, out string message)
    {
      if (_uri.IsAbsoluteUri && requestUri.IsAbsoluteUri)
        if (_uri.ToString().NotEqualsDo(requestUri.ToString(), func, out message, false))
          return false;

      if (_uri.IsAbsoluteUri && !requestUri.IsAbsoluteUri)
        if (_uri.PathAndQuery.NotEqualsDo(requestUri.ToString(), func, out message, false))
          return false;

      if (!_uri.IsAbsoluteUri && requestUri.IsAbsoluteUri)
        if (_uri.ToString().NotEqualsDo(requestUri.PathAndQuery, func, out message, false))
          return false;

      if (!_uri.IsAbsoluteUri && !requestUri.IsAbsoluteUri)
        if (_uri.ToString().NotEqualsDo(requestUri.ToString(), func, out message, false))
          return false;

      message = String.Empty;
      return true;
    }

    private bool isValidResponse(ResponseHandshake response, out string message)
    {
      if (!response.IsWebSocketResponse)
      {
        message = "Invalid WebSocket response.";
        return false;
      }

      if (!response.HeaderExists("Sec-WebSocket-Accept", createExpectedKey()))
      {
        message = "Invalid Sec-WebSocket-Accept value.";
        return false;
      }

      if (response.HeaderExists("Sec-WebSocket-Version"))
      {
        if (!response.HeaderExists("Sec-WebSocket-Version", _version))
        {
          message = "Unsupported Sec-WebSocket-Version.";
          return false;
        }
      }

      if (response.HeaderExists("Sec-WebSocket-Protocol"))
        _protocol = response.Headers["Sec-WebSocket-Protocol"];

      if (response.HeaderExists("Sec-WebSocket-Extensions"))
        _extensions = response.Headers["Sec-WebSocket-Extensions"];

      message = String.Empty;
      return true;
    }

    private void message()
    {
      try
      {
        var eventArgs = receive();
        if (eventArgs != null)
          OnMessage.Emit(this, eventArgs);
      }
      catch (WsReceivedTooBigMessageException ex)
      {
        close(CloseStatusCode.TOO_BIG, ex.Message);
      }
      catch (Exception)
      {
        close(CloseStatusCode.ABNORMAL, "An exception has occured.");
      }
    }

    private void messageLoopCallback(IAsyncResult ar)
    {
      Action messageInvoker = (Action)ar.AsyncState;
      messageInvoker.EndInvoke(ar);
      if (_readyState == WsState.OPEN)
      {
        messageInvoker.BeginInvoke(messageLoopCallback, messageInvoker);
        return;
      }

      _exitMessageLoop.Set();
    }

    private bool ping(string data, int millisecondsTimeout)
    {
      var buffer = Encoding.UTF8.GetBytes(data);
      if (buffer.Length > 125)
      {
        var msg = "Ping frame must have a payload length of 125 bytes or less.";
        error(msg);
        return false;
      }

      if (!send(Fin.FINAL, Opcode.PING, buffer))
        return false;

      return _receivePong.WaitOne(millisecondsTimeout);
    }

    private void pong(PayloadData data)
    {
      var frame = createFrame(Fin.FINAL, Opcode.PONG, data);
      send(frame);
    }

    private void pong(string data)
    {
      var payloadData = new PayloadData(data);
      pong(payloadData);
    }

    private WsFrame readFrame()
    {
      var frame = _wsStream.ReadFrame();
      if (frame == null)
      {
        var msg = "WebSocket data frame can not be read from network stream.";
        close(CloseStatusCode.ABNORMAL, msg);
      }

      return frame;
    }

    private WsFrame readFrameWithTimeout(int millisecondsTimeout)
    {
      if (!_wsStream.DataAvailable)
      {
        Thread.Sleep(millisecondsTimeout);
        if (!_wsStream.DataAvailable)
          return null;
      }

      return readFrame();
    }

    private string[] readHandshake()
    {
      return _wsStream.ReadHandshake();
    }

    private MessageEventArgs receive()
    {
      var frame = _isClient ? readFrame() : readFrameWithTimeout(1 * 100);
      if (frame == null)
        return null;

      if ((frame.Fin == Fin.FINAL && frame.Opcode == Opcode.CONT) ||
          (frame.Fin == Fin.MORE  && frame.Opcode == Opcode.CONT))
        return null;

      if (frame.Fin == Fin.MORE)
      {// MORE
        var merged = receiveFragmented(frame);
        if (merged == null)
          return null;

        return new MessageEventArgs(frame.Opcode, new PayloadData(merged));
      }

      if (frame.Opcode == Opcode.CLOSE)
      {// FINAL & CLOSE
        #if DEBUG
        Console.WriteLine("WS: Info@receive: Start closing handshake.");
        #endif
        close(frame.PayloadData);
        return null;
      }

      if (frame.Opcode == Opcode.PING)
      {// FINAL & PING
        #if DEBUG
        Console.WriteLine("WS: Info@receive: Return Pong.");
        #endif
        pong(frame.PayloadData);
        return null;
      }

      if (frame.Opcode == Opcode.PONG)
      {// FINAL & PONG
        _receivePong.Set();
      }

      // FINAL & (TEXT | BINARY | PONG)
      return new MessageEventArgs(frame.Opcode, frame.PayloadData);
    }

    private byte[] receiveFragmented(WsFrame firstFrame)
    {
      var buffer = new List<byte>(firstFrame.PayloadData.ToBytes());

      while (true)
      {
        var frame = readFrame();
        if (frame == null)
          return null;

        if (frame.Fin == Fin.MORE)
        {
          if (frame.Opcode == Opcode.CONT)
          {// MORE & CONT
            buffer.AddRange(frame.PayloadData.ToBytes());
            continue;
          }

          #if DEBUG
          Console.WriteLine("WS: Info@receiveFragmented: Start closing handshake.");
          #endif
          close(CloseStatusCode.INCORRECT_DATA, String.Empty);
          return null;
        }

        if (frame.Opcode == Opcode.CONT)
        {// FINAL & CONT
          buffer.AddRange(frame.PayloadData.ToBytes());
          break;
        }

        if (frame.Opcode == Opcode.CLOSE)
        {// FINAL & CLOSE
          #if DEBUG
          Console.WriteLine("WS: Info@receiveFragmented: Start closing handshake.");
          #endif
          close(frame.PayloadData);
          return null;
        }

        if (frame.Opcode == Opcode.PING)
        {// FINAL & PING
          #if DEBUG
          Console.WriteLine("WS: Info@receiveFragmented: Return Pong.");
          #endif
          pong(frame.PayloadData);
          continue;
        }

        if (frame.Opcode == Opcode.PONG)
        {// FINAL & PONG
          _receivePong.Set();
          OnMessage.Emit(this, new MessageEventArgs(frame.Opcode, frame.PayloadData));
          continue;
        }

        // FINAL & (TEXT | BINARY)
        #if DEBUG
        Console.WriteLine("WS: Info@receiveFragmented: Start closing handshake.");
        #endif
        close(CloseStatusCode.INCORRECT_DATA, String.Empty);
        return null;
      }

      return buffer.ToArray();
    }

    private RequestHandshake receiveOpeningHandshake()
    {
      var req = _context != null
              ? RequestHandshake.Parse(_context)
              : RequestHandshake.Parse(readHandshake());
      #if DEBUG
      Console.WriteLine("WS: Info@receiveOpeningHandshake: Opening handshake from client:\n");
      Console.WriteLine(req.ToString());
      #endif
      return req;
    }

    private ResponseHandshake receiveResponseHandshake()
    {
      var res = ResponseHandshake.Parse(readHandshake());
      #if DEBUG
      Console.WriteLine("WS: Info@receiveResponseHandshake: Response handshake from server:\n");
      Console.WriteLine(res.ToString());
      #endif
      return res;
    }

    private bool send(WsFrame frame)
    {
      if (_readyState == WsState.CONNECTING ||
          _readyState == WsState.CLOSED)
      {
        var msg = "Connection isn't established or has been closed.";
        error(msg);
        return false;
      }

      try
      {
        if (_unTransmittedBuffer.Count == 0)
        {
          if (_wsStream != null)
          {
            _wsStream.WriteFrame(frame);
            return true;
          }
        }

        if (_unTransmittedBuffer.Count > 0)
        {
          _unTransmittedBuffer.Add(frame);
          var msg = "Current data can not be sent because there is untransmitted data.";
          error(msg);
        }

        return false;
      }
      catch (Exception ex)
      {
        _unTransmittedBuffer.Add(frame);
        error(ex.Message);
        return false;
      }
    }

    private void send(Opcode opcode, byte[] data)
    {
      using (MemoryStream ms = new MemoryStream(data))
      {
        send(opcode, ms);
      }
    }

    private void send(Opcode opcode, Stream stream)
    {
      lock (_forSend)
      {
        if (_readyState != WsState.OPEN)
        {
          var msg = "Connection isn't established or has been closed.";
          error(msg);
          return;
        }

        var length = stream.Length;
        if (length <= _fragmentLen)
        {
          var buffer = stream.ReadBytes((int)length);
          send(Fin.FINAL, opcode, buffer);
          return;
        }

        sendFragmented(opcode, stream);
      }
    }

    private bool send(Fin fin, Opcode opcode, byte[] data)
    {
      var frame = createFrame(fin, opcode, new PayloadData(data));
      return send(frame);
    }

    private long sendFragmented(Opcode opcode, Stream stream)
    {
      var length = stream.Length;
      var quo    = length / _fragmentLen;
      var rem    = length % _fragmentLen;
      var count  = rem == 0 ? quo - 2 : quo - 1;

      // First
      var buffer   = new byte[_fragmentLen];
      long readLen = stream.Read(buffer, 0, _fragmentLen);
      send(Fin.MORE, opcode, buffer);

      // Mid
      count.Times(() =>
      {
        readLen += stream.Read(buffer, 0, _fragmentLen);
        send(Fin.MORE, Opcode.CONT, buffer);
      });

      // Final
      if (rem != 0)
        buffer = new byte[rem];
      readLen += stream.Read(buffer, 0, buffer.Length);
      send(Fin.FINAL, Opcode.CONT, buffer);

      return readLen;
    }

    private ResponseHandshake sendOpeningHandshake()
    {
      var req = createOpeningHandshake();
      sendOpeningHandshake(req);
      return receiveResponseHandshake();
    }

    private void sendOpeningHandshake(RequestHandshake request)
    {
      #if DEBUG
      Console.WriteLine("WS: Info@sendOpeningHandshake: Opening handshake from client:\n");
      Console.WriteLine(request.ToString());
      #endif
      writeHandshake(request);
    }

    private void sendResponseHandshake()
    {
      var res = createResponseHandshake();
      sendResponseHandshake(res);
    }

    private void sendResponseHandshake(HttpStatusCode code)
    {
      var res = createResponseHandshake(code);
      sendResponseHandshake(res);
    }

    private void sendResponseHandshake(ResponseHandshake response)
    {
      #if DEBUG
      Console.WriteLine("WS: Info@sendResponseHandshake: Response handshake from server:\n");
      Console.WriteLine(response.ToString());
      #endif
      writeHandshake(response);
    }

    private void startMessageThread()
    {
      _exitMessageLoop = new AutoResetEvent(false);
      Action messageInvoker = () =>
      {
        if (_readyState == WsState.OPEN)
          message();
      };

      messageInvoker.BeginInvoke(messageLoopCallback, messageInvoker);
    }

    private void writeHandshake(Handshake handshake)
    {
      _wsStream.WriteHandshake(handshake);
    }

    #endregion

    #region Internal Method

    internal void Close(HttpStatusCode code)
    {
      close(code);
    }

    #endregion

    #region Public Methods

    public void Close()
    {
      Close(CloseStatusCode.NORMAL);
    }

    public void Close(CloseStatusCode code)
    {
      Close(code, String.Empty);
    }

    public void Close(CloseStatusCode code, string reason)
    {
      close(code, reason);
    }

    public void Connect()
    {
      if (_readyState == WsState.OPEN)
      {
        Console.WriteLine("WS: Info@Connect: Connection has been established already.");
        return;
      }

      try
      {
        // As client
        if (_isClient)
        {
          createClientStream();
          doHandshake();
          return;
        }

        // As server
        acceptHandshake();
      }
      catch (Exception ex)
      {
        error(ex.Message);
        close(CloseStatusCode.HANDSHAKE_FAILURE, "An exception has occured.");
      }
    }

    public void Dispose()
    {
      Close(CloseStatusCode.AWAY);
    }

    public bool Ping()
    {
      return Ping(String.Empty);
    }

    public bool Ping(string data)
    {
      return ping(data, 5 * 1000);
    }

    public void Send(string data)
    {
      var buffer = Encoding.UTF8.GetBytes(data);
      send(Opcode.TEXT, buffer);
    }

    public void Send(byte[] data)
    {
      send(Opcode.BINARY, data);
    }

    public void Send(FileInfo file)
    {
      using (FileStream fs = file.OpenRead())
      {
        send(Opcode.BINARY, fs);
      }
    }

    #endregion
  }
}
