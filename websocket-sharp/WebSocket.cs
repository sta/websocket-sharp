#region MIT License
/**
 * WebSocket.cs
 *
 * A C# implementation of the WebSocket interface.
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
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using WebSocketSharp.Frame;
using WebSocketSharp.Net;
using WebSocketSharp.Net.Sockets;

namespace WebSocketSharp {

  /// <summary>
  /// Implements the WebSocket interface.
  /// </summary>
  /// <remarks>
  /// The WebSocket class provides methods and properties for two-way communication using the WebSocket protocol (RFC 6455).
  /// </remarks>
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
    private WebSocketContext                _context;
    private System.Net.IPEndPoint           _endPoint;
    private string                          _extensions;
    private AutoResetEvent                  _exitMessageLoop;
    private Object                          _forClose;
    private Object                          _forSend;
    private bool                            _isClient;
    private bool                            _isSecure;
    private string                          _protocol;
    private string                          _protocols;
    private NameValueCollection             _queryString;
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
      _extensions          = String.Empty;
      _forClose            = new Object();
      _forSend             = new Object();
      _protocol            = String.Empty;
      _readyState          = WsState.CONNECTING;
      _unTransmittedBuffer = new SynchronizedCollection<WsFrame>();
    }

    #endregion

    #region Internal Constructor

    internal WebSocket(HttpListenerWebSocketContext context)
      : this()
    {
      _uri         = context.Path.ToUri();
      _context     = context;
      _baseContext = context.BaseContext;
      _wsStream    = context.Stream;
      _endPoint    = context.ServerEndPoint;
      _isClient    = false;
      _isSecure    = context.IsSecureConnection;
    }

    internal WebSocket(TcpListenerWebSocketContext context)
      : this()
    {
      _uri       = context.Path.ToUri();
      _context   = context;
      _tcpClient = context.Client;
      _wsStream  = context.Stream;
      _endPoint  = context.ServerEndPoint;
      _isClient  = false;
      _isSecure  = context.IsSecureConnection;
    }

    #endregion

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketSharp.WebSocket"/> class with the specified WebSocket URL and subprotocols.
    /// </summary>
    /// <param name="url">
    /// A <see cref="string"/> that contains the WebSocket URL.
    /// </param>
    /// <param name="protocols">
    /// An array of <see cref="string"/> that contains the WebSocket subprotocols if any.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="url"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="url"/> is not valid WebSocket URL.
    /// </exception>
    public WebSocket(string url, params string[] protocols)
      : this()
    {
      if (url.IsNull())
        throw new ArgumentNullException("url");

      Uri    uri;
      string msg;
      if (!tryCreateUri(url, out uri, out msg))
        throw new ArgumentException(msg, "url");

      _uri       = uri;
      _protocols = protocols.ToString(", ");
      _isClient  = true;
      _isSecure  = uri.Scheme == "wss" ? true : false;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketSharp.WebSocket"/> class with the specified WebSocket URL, OnOpen, OnMessage, OnError, OnClose event handlers and subprotocols.
    /// </summary>
    /// <param name="url">
    /// A <see cref="string"/> that contains the WebSocket URL.
    /// </param>
    /// <param name="onOpen">
    /// An OnOpen event handler.
    /// </param>
    /// <param name="onMessage">
    /// An OnMessage event handler.
    /// </param>
    /// <param name="onError">
    /// An OnError event handler.
    /// </param>
    /// <param name="onClose">
    /// An OnClose event handler.
    /// </param>
    /// <param name="protocols">
    /// An array of <see cref="string"/> that contains the WebSocket subprotocols if any.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="url"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="url"/> is not valid WebSocket URL.
    /// </exception>
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

    #region Internal Property

    internal NameValueCollection QueryString {
      get {
        return _queryString;
      }
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the amount of untransmitted data.
    /// </summary>
    /// <value>
    /// The number of bytes of untransmitted data.
    /// </value>
    public ulong BufferedAmount {
      get {
        lock (_unTransmittedBuffer.SyncRoot)
        {
          ulong bufferedAmount = 0;
          foreach (WsFrame frame in _unTransmittedBuffer)
            bufferedAmount += frame.PayloadLength;

          return bufferedAmount;
        }
      }
    }

    /// <summary>
    /// Gets the extensions selected by the server.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that contains the extensions if any. By default, <c>String.Empty</c>. (Currently this will only ever be the <c>String.Empty</c>.)
    /// </value>
    public string Extensions {
      get {
        return _extensions;
      }
    }

    /// <summary>
    /// Gets a value indicating whether a connection is alive.
    /// </summary>
    /// <value>
    /// <c>true</c> if the connection is alive; otherwise, <c>false</c>.
    /// </value>
    public bool IsAlive {
      get {
        if (_readyState != WsState.OPEN)
          return false;

        return Ping();
      }
    }

    /// <summary>
    /// Gets a value indicating whether a connection is secure.
    /// </summary>
    /// <value>
    /// <c>true</c> if the connection is secure; otherwise, <c>false</c>.
    /// </value>
    public bool IsSecure {
      get {
        return _isSecure;
      }
    }

    /// <summary>
    /// Gets the subprotocol selected by the server.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that contains the subprotocol if any. By default, <c>String.Empty</c>.
    /// </value>
    public string Protocol {
      get {
        return _protocol;
      }
    }

    /// <summary>
    /// Gets the state of the connection.
    /// </summary>
    /// <value>
    /// A <see cref="WebSocketSharp.WsState"/>. By default, <c>WsState.CONNECTING</c>.
    /// </value>
    public WsState ReadyState {
      get {
        return _readyState;
      }
    }

    /// <summary>
    /// Gets the untransmitted WebSocket frames.
    /// </summary>
    /// <value>
    /// A <c>IList&lt;WsFrame&gt;</c> that contains the untransmitted WebSocket frames.
    /// </value>
    public IList<WsFrame> UnTransmittedBuffer {
      get {
        return _unTransmittedBuffer;
      }
    }

    /// <summary>
    /// Gets or sets the WebSocket URL.
    /// </summary>
    /// <value>
    /// A <see cref="Uri"/> that contains the WebSocket URL.
    /// </value>
    public Uri Url {
      get { return _uri; }
      set {
        if (_readyState == WsState.CONNECTING && !_isClient)
          _uri = value;
      }
    }

    #endregion

    #region Events

    /// <summary>
    /// Occurs when the WebSocket connection has been established.
    /// </summary>
    public event EventHandler OnOpen;

    /// <summary>
    /// Occurs when the WebSocket receives a data frame.
    /// </summary>
    public event EventHandler<MessageEventArgs> OnMessage;

    /// <summary>
    /// Occurs when the WebSocket gets an error.
    /// </summary>
    public event EventHandler<ErrorEventArgs> OnError;

    /// <summary>
    /// Occurs when the WebSocket receives a Close frame or the Close method is called.
    /// </summary>
    public event EventHandler<CloseEventArgs> OnClose;

    #endregion

    #region Private Methods

    // As Server
    private void acceptHandshake()
    {
      var req = receiveOpeningHandshake();

      string msg;
      if (!isValidRequest(req, out msg))
      {
        onError(msg);
        close(CloseStatusCode.HANDSHAKE_FAILURE, msg);
        return;
      }

      sendResponseHandshake();
      onOpen();
    }

    private bool canSendAsCloseFrame(PayloadData data)
    {
      if (data.Length >= 2)
      {
        var code = data.ToBytes().SubArray(0, 2).To<ushort>(ByteOrder.BIG);
        if (code == (ushort)CloseStatusCode.NO_STATUS_CODE ||
            code == (ushort)CloseStatusCode.ABNORMAL       ||
            code == (ushort)CloseStatusCode.HANDSHAKE_FAILURE)
          return false;
      }

      return true;
    }

    private void close(HttpStatusCode code)
    {
      if (_readyState != WsState.CONNECTING || _isClient)
        return;

      sendResponseHandshake(code);
      closeConnection();
    }

    private void close(PayloadData data)
    {
      #if DEBUG
      Console.WriteLine("WS: Info@close: Current thread IsBackground ?: {0}", Thread.CurrentThread.IsBackground);
      #endif
      lock(_forClose)
      {
        // Whether the closing handshake has been started already ?
        if (_readyState == WsState.CLOSING ||
            _readyState == WsState.CLOSED)
          return;

        // Whether the closing handshake as server is started before the connection has been established ?
        if (_readyState == WsState.CONNECTING && !_isClient)
        {
          sendResponseHandshake(HttpStatusCode.BadRequest);
          onClose(new CloseEventArgs(data));
          
          return;
        }

        // Whether a close status code that must not be set for send is used ?
        if (!canSendAsCloseFrame(data))
        {
          onClose(new CloseEventArgs(data));
          return;
        }

        _readyState = WsState.CLOSING;
      }

      closeHandshake(data);
      #if DEBUG
      Console.WriteLine("WS: Info@close: Exit close method.");
      #endif
    }

    private void close(CloseStatusCode code, string reason)
    {
      close((ushort)code, reason);
    }

    private void close(ushort code, string reason)
    {
      var data = new List<byte>(code.ToBytes(ByteOrder.BIG));
      if (!reason.IsNullOrEmpty())
      {
        var buffer = Encoding.UTF8.GetBytes(reason);
        data.AddRange(buffer);
      }

      var payloadData = new PayloadData(data.ToArray());
      if (payloadData.Length > 125)
      {
        var msg = "Close frame must have a payload length of 125 bytes or less.";
        onError(msg);
        return;
      }

      close(payloadData);
    }

    private bool closeConnection()
    {
      _readyState = WsState.CLOSED;

      try
      {
        if (!_baseContext.IsNull())
        {
          _baseContext.Response.Close();
          _wsStream    = null;
          _baseContext = null;
        }

        if (!_wsStream.IsNull())
        {
          _wsStream.Dispose();
          _wsStream = null;
        }

        if (!_tcpClient.IsNull())
        {
          _tcpClient.Close();
          _tcpClient = null;
        }

        return true;
      }
      catch (Exception ex)
      {
        onError(ex.Message);
        return false;
      }
    }

    private void closeHandshake(PayloadData data)
    {
      var args  = new CloseEventArgs(data);
      var frame = createFrame(Fin.FINAL, Opcode.CLOSE, data);
      if (send(frame) && !Thread.CurrentThread.IsBackground)
        if (!_exitMessageLoop.IsNull())
          _exitMessageLoop.WaitOne(5 * 1000);

      onClose(args);
    }

    // As Client
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

    // As Client
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
      if (!_protocols.IsNullOrEmpty())
        req.AddHeader("Sec-WebSocket-Protocol", _protocols);
      req.AddHeader("Sec-WebSocket-Version", _version);

      return req;
    }

    // As Server
    private ResponseHandshake createResponseHandshake()
    {
      var res = new ResponseHandshake();
      res.AddHeader("Sec-WebSocket-Accept", createExpectedKey());

      return res;
    }

    // As Server
    private ResponseHandshake createResponseHandshake(HttpStatusCode code)
    {
      var res = ResponseHandshake.CreateCloseResponse(code);
      res.AddHeader("Sec-WebSocket-Version", _version);

      return res;
    }

    // As Client
    private void doHandshake()
    {
      var res = sendOpeningHandshake();

      string msg;
      if (!isValidResponse(res, out msg))
      {
        onError(msg);
        close(CloseStatusCode.HANDSHAKE_FAILURE, msg);
        return;
      }

      onOpen();
    }

    private bool isValidCloseStatusCode(ushort code, out string message)
    {
      if (code < 1000)
      {
        message = "Close status codes in the range 0-999 are not used: " + code;
        return false;
      }

      if (code > 4999)
      {
        message = "Out of reserved close status code range: " + code;
        return false;
      }

      message = String.Empty;
      return true;
    }

    // As Server
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

      _queryString = request.QueryString;

      message = String.Empty;
      return true;
    }

    // As Server
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

    // As Client
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
        onMessage(receive());
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

    private void onClose(CloseEventArgs eventArgs)
    {
      if (closeConnection())
        eventArgs.WasClean = true;

      OnClose.Emit(this, eventArgs);
    }

    private void onError(string message)
    {
      #if DEBUG
      var callerFrame = new StackFrame(1);
      var caller      = callerFrame.GetMethod();
      Console.WriteLine("WS: Error@{0}: {1}", caller.Name, message);
      #endif
      OnError.Emit(this, new ErrorEventArgs(message));
    }

    private void onMessage(MessageEventArgs eventArgs)
    {
      if (!eventArgs.IsNull())
        OnMessage.Emit(this, eventArgs);
    }

    private void onOpen()
    {
      _readyState = WsState.OPEN;
      startMessageThread();
      OnOpen.Emit(this, EventArgs.Empty);
    }

    private bool ping(string message, int millisecondsTimeout)
    {
      var buffer = Encoding.UTF8.GetBytes(message);
      if (buffer.Length > 125)
      {
        var msg = "Ping frame must have a payload length of 125 bytes or less.";
        onError(msg);
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
      if (frame.IsNull())
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
      if (frame.IsNull())
        return null;

      if ((frame.Fin == Fin.FINAL && frame.Opcode == Opcode.CONT) ||
          (frame.Fin == Fin.MORE  && frame.Opcode == Opcode.CONT))
        return null;

      if (frame.Fin == Fin.MORE)
      {// MORE
        var merged = receiveFragmented(frame);
        if (merged.IsNull())
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
        if (frame.IsNull())
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

    // As Server
    private RequestHandshake receiveOpeningHandshake()
    {
      var req = RequestHandshake.Parse(_context);
      #if DEBUG
      Console.WriteLine("WS: Info@receiveOpeningHandshake: Opening handshake from client:\n");
      Console.WriteLine(req.ToString());
      #endif
      return req;
    }

    // As Client
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
        onError(msg);
        return false;
      }

      try
      {
        if (_unTransmittedBuffer.Count == 0)
        {
          if (!_wsStream.IsNull())
          {
            _wsStream.WriteFrame(frame);
            return true;
          }
        }

        if (_unTransmittedBuffer.Count > 0)
        {
          _unTransmittedBuffer.Add(frame);
          var msg = "Current data can not be sent because there is untransmitted data.";
          onError(msg);
        }

        return false;
      }
      catch (Exception ex)
      {
        _unTransmittedBuffer.Add(frame);
        onError(ex.Message);
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
          onError(msg);
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

    // As Client
    private ResponseHandshake sendOpeningHandshake()
    {
      var req = createOpeningHandshake();
      sendOpeningHandshake(req);

      return receiveResponseHandshake();
    }

    // As Client
    private void sendOpeningHandshake(RequestHandshake request)
    {
      #if DEBUG
      Console.WriteLine("WS: Info@sendOpeningHandshake: Opening handshake from client:\n");
      Console.WriteLine(request.ToString());
      #endif
      writeHandshake(request);
    }

    // As Server
    private void sendResponseHandshake()
    {
      var res = createResponseHandshake();
      sendResponseHandshake(res);
    }

    // As Server
    private void sendResponseHandshake(HttpStatusCode code)
    {
      var res = createResponseHandshake(code);
      sendResponseHandshake(res);
    }

    // As Server
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
      _receivePong     = new AutoResetEvent(false);
      _exitMessageLoop = new AutoResetEvent(false);
      Action messageInvoker = () =>
      {
        if (_readyState == WsState.OPEN)
          message();
      };

      messageInvoker.BeginInvoke(messageLoopCallback, messageInvoker);
    }

    private bool tryCreateUri(string uriString, out Uri result, out string message)
    {
      return uriString.TryCreateWebSocketUri(out result, out message);
    }

    private void writeHandshake(Handshake handshake)
    {
      _wsStream.WriteHandshake(handshake);
    }

    #endregion

    #region Internal Method

    // As Server
    internal void Close(HttpStatusCode code)
    {
      close(code);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Closes the connection and releases all associated resources after sends a Close control frame.
    /// </summary>
    public void Close()
    {
      var data = new PayloadData(new byte[]{});
      close(data);
    }

    /// <summary>
    /// Closes the connection and releases all associated resources after sends a Close control frame.
    /// </summary>
    /// <param name="code">
    /// A <see cref="WebSocketSharp.Frame.CloseStatusCode"/> that contains a status code indicating a reason for closure.
    /// </param>
    public void Close(CloseStatusCode code)
    {
      Close(code, String.Empty);
    }

    /// <summary>
    /// Closes the connection and releases all associated resources after sends a Close control frame.
    /// </summary>
    /// <param name="code">
    /// A <see cref="ushort"/> that contains a status code indicating a reason for closure.
    /// </param>
    public void Close(ushort code)
    {
      Close(code, String.Empty);
    }

    /// <summary>
    /// Closes the connection and releases all associated resources after sends a Close control frame.
    /// </summary>
    /// <param name="code">
    /// A <see cref="WebSocketSharp.Frame.CloseStatusCode"/> that contains a status code indicating a reason for closure.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that contains a reason for closure.
    /// </param>
    public void Close(CloseStatusCode code, string reason)
    {
      Close((ushort)code, reason);
    }

    /// <summary>
    /// Closes the connection and releases all associated resources after sends a Close control frame.
    /// </summary>
    /// <param name="code">
    /// A <see cref="ushort"/> that contains a status code indicating a reason for closure.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that contains a reason for closure.
    /// </param>
    public void Close(ushort code, string reason)
    {
      string msg;
      if (!isValidCloseStatusCode(code, out msg))
      {
        onError(msg);
        return;
      }

      close(code, reason);
    }

    /// <summary>
    /// Establishes a connection.
    /// </summary>
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
        onError(ex.Message);
        close(CloseStatusCode.HANDSHAKE_FAILURE, "An exception has occured.");
      }
    }

    /// <summary>
    /// Closes the connection and releases all associated resources after sends a Close control frame.
    /// </summary>
    /// <remarks>
    /// Call <see cref="Dispose"/> when you are finished using the <see cref="WebSocketSharp.WebSocket"/>. The
    /// <see cref="Dispose"/> method leaves the <see cref="WebSocketSharp.WebSocket"/> in an unusable state. After
    /// calling <see cref="Dispose"/>, you must release all references to the <see cref="WebSocketSharp.WebSocket"/> so
    /// the garbage collector can reclaim the memory that the <see cref="WebSocketSharp.WebSocket"/> was occupying.
    /// </remarks>
    public void Dispose()
    {
      Close(CloseStatusCode.AWAY);
    }

    /// <summary>
    /// Sends a Ping frame using the connection.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the WebSocket receives a Pong frame in a time; otherwise, <c>false</c>.
    /// </returns>
    public bool Ping()
    {
      return Ping(String.Empty);
    }

    /// <summary>
    /// Sends a Ping frame with a message using the connection.
    /// </summary>
    /// <param name="message">
    /// A <see cref="string"/> that contains the message to be sent.
    /// </param>
    /// <returns>
    /// <c>true</c> if the WebSocket receives a Pong frame in a time; otherwise, <c>false</c>.
    /// </returns>
    public bool Ping(string message)
    {
      if (message.IsNull())
        message = String.Empty;

      return ping(message, 5 * 1000);
    }

    /// <summary>
    /// Sends a text data using the connection.
    /// </summary>
    /// <param name="data">
    /// A <see cref="string"/> that contains the text data to be sent.
    /// </param>
    public void Send(string data)
    {
      if (data.IsNull())
      {
        onError("'data' must not be null.");
        return;
      }

      var buffer = Encoding.UTF8.GetBytes(data);
      send(Opcode.TEXT, buffer);
    }

    /// <summary>
    /// Sends a binary data using the connection.
    /// </summary>
    /// <param name="data">
    /// An array of <see cref="byte"/> that contains the binary data to be sent.
    /// </param>
    public void Send(byte[] data)
    {
      if (data.IsNull())
      {
        onError("'data' must not be null.");
        return;
      }

      send(Opcode.BINARY, data);
    }

    /// <summary>
    /// Sends a binary data using the connection.
    /// </summary>
    /// <param name="file">
    /// A <see cref="FileInfo"/> that contains the binary data to be sent.
    /// </param>
    public void Send(FileInfo file)
    {
      if (file.IsNull())
      {
        onError("'file' must not be null.");
        return;
      }

      using (FileStream fs = file.OpenRead())
      {
        send(Opcode.BINARY, fs);
      }
    }

    #endregion
  }
}
