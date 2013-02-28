#region MIT License
/*
 * WebSocket.cs
 *
 * A C# implementation of the WebSocket interface.
 * This code derived from WebSocket.java (http://github.com/adamac/Java-WebSocket-client).
 *
 * The MIT License
 *
 * Copyright (c) 2009 Adam MacBeth
 * Copyright (c) 2010-2013 sta.blockhead
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
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using WebSocketSharp.Net;
using WebSocketSharp.Net.WebSockets;

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

    private string           _base64key;
    private Action           _closeContext;
    private WebSocketContext _context;
    private string           _extensions;
    private AutoResetEvent   _exitMessageLoop;
    private Object           _forClose;
    private Object           _forSend;
    private bool             _isClient;
    private bool             _isSecure;
    private string           _protocol;
    private string           _protocols;
    private volatile WsState _readyState;
    private AutoResetEvent   _receivePong;
    private TcpClient        _tcpClient;
    private Uri              _uri;
    private WsStream         _wsStream;

    #endregion

    #region Private Constructor

    private WebSocket()
    {
      _extensions = String.Empty;
      _forClose   = new Object();
      _forSend    = new Object();
      _protocol   = String.Empty;
      _readyState = WsState.CONNECTING;
    }

    #endregion

    #region Internal Constructors

    internal WebSocket(HttpListenerWebSocketContext context)
      : this()
    {
      _wsStream     = context.Stream;
      _closeContext = () => context.Close();
      init(context);
    }

    internal WebSocket(TcpListenerWebSocketContext context)
      : this()
    {
      _wsStream     = context.Stream;
      _closeContext = () => context.Close();
      init(context);
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
      _base64key = createBase64Key();
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

    #region Public Properties

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
    /// One of the <see cref="WebSocketSharp.WsState"/>. By default, <c>WsState.CONNECTING</c>.
    /// </value>
    public WsState ReadyState {
      get {
        return _readyState;
      }
    }

    /// <summary>
    /// Gets the WebSocket URL.
    /// </summary>
    /// <value>
    /// A <see cref="Uri"/> that contains the WebSocket URL.
    /// </value>
    public Uri Url {
      get {
        return _uri;
      }

      internal set {
        if (_readyState == WsState.CONNECTING && !_isClient)
          _uri = value;
      }
    }

    #endregion

    #region Events

    /// <summary>
    /// Occurs when the WebSocket receives a Close frame or the Close method is called.
    /// </summary>
    public event EventHandler<CloseEventArgs> OnClose;

    /// <summary>
    /// Occurs when the WebSocket gets an error.
    /// </summary>
    public event EventHandler<ErrorEventArgs> OnError;

    /// <summary>
    /// Occurs when the WebSocket receives a data frame.
    /// </summary>
    public event EventHandler<MessageEventArgs> OnMessage;

    /// <summary>
    /// Occurs when the WebSocket connection has been established.
    /// </summary>
    public event EventHandler OnOpen;

    #endregion

    #region Private Methods

    // As Server
    private bool acceptHandshake()
    {
      if (!receiveOpeningHandshake())
        return false;

      sendResponseHandshake();
      return true;
    }

    private bool canSendAsCloseFrame(PayloadData data)
    {
      if (data.Length >= 2)
      {
        var code = data.ToBytes().SubArray(0, 2).To<ushort>(ByteOrder.BIG);
        if (code == (ushort)CloseStatusCode.NO_STATUS_CODE ||
            code == (ushort)CloseStatusCode.ABNORMAL       ||
            code == (ushort)CloseStatusCode.TLS_HANDSHAKE_FAILURE)
          return false;
      }

      return true;
    }

    private void close(HttpStatusCode code)
    {
      if (_readyState != WsState.CONNECTING || _isClient)
        return;

      sendResponseHandshake(code);
      closeResources();
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

        _readyState = WsState.CLOSING;
      }

      // Whether a close status code that must not be set for send is used ?
      if (!canSendAsCloseFrame(data))
      {
        onClose(new CloseEventArgs(data));
        return;
      }

      closeHandshake(data);
      #if DEBUG
      Console.WriteLine("WS: Info@close: Exits close method.");
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
        var msg = "A Close frame must have a payload length of 125 bytes or less.";
        onError(msg);
        return;
      }

      close(payloadData);
    }

    private void closeHandshake(PayloadData data)
    {
      var args  = new CloseEventArgs(data);
      var frame = createFrame(Fin.FINAL, Opcode.CLOSE, data);
      if (send(frame))
        args.WasClean = true;

      onClose(args);
    }

    private bool closeResources()
    {
      _readyState = WsState.CLOSED;

      try
      {
        if (_isClient)
          closeResourcesAsClient();
        else
          closeResourcesAsServer();

        return true;
      }
      catch (Exception ex)
      {
        onError(ex.Message);
        return false;
      }
    }

    // As Client
    private void closeResourcesAsClient()
    {
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
    }

    // As Server
    private void closeResourcesAsServer()
    {
      if (!_context.IsNull() && !_closeContext.IsNull())
      {
        _closeContext();
        _wsStream = null;
        _context  = null;
      }
    }

    private bool connect()
    {
      return _isClient
             ? doHandshake()
             : acceptHandshake();
    }

    // As Client
    private string createBase64Key()
    {
      var src  = new byte[16];
      var rand = new Random();
      rand.NextBytes(src);

      return Convert.ToBase64String(src);
    }

    // As Client
    private void createClientStream()
    {
      var host = _uri.DnsSafeHost;
      var port = _uri.Port > 0
               ? _uri.Port
               : _isSecure
                 ? 443
                 : 80;

      _tcpClient = new TcpClient(host, port);
      _wsStream  = WsStream.CreateClientStream(_tcpClient, host, _isSecure);
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
      res.AddHeader("Sec-WebSocket-Accept", createResponseKey());

      return res;
    }

    // As Server
    private ResponseHandshake createResponseHandshake(HttpStatusCode code)
    {
      var res = ResponseHandshake.CreateCloseResponse(code);
      res.AddHeader("Sec-WebSocket-Version", _version);

      return res;
    }

    private string createResponseKey()
    {
      SHA1 sha1 = new SHA1CryptoServiceProvider();
      var sb = new StringBuilder(_base64key);
      sb.Append(_guid);
      var src = sha1.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));

      return Convert.ToBase64String(src);
    }

    // As Client
    private bool doHandshake()
    {
      createClientStream();
      return sendOpeningHandshake();
    }

    // As Server
    private void init(WebSocketContext context)
    {
      _context  = context;
      _uri      = context.Path.ToUri();
      _isSecure = context.IsSecureConnection;
      _isClient = false;
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

    private bool isValidFrame(WsFrame frame)
    {
      if (frame.IsNull())
      {
        var msg = "The WebSocket frame can not be read from the network stream.";
        close(CloseStatusCode.ABNORMAL, msg);

        return false;
      }

      return true;
    }

    // As Server
    private bool isValidRequest()
    {
      return !_context.IsValid
             ? false
             : !isValidRequestHostHeader()
               ? false
               : _context.Headers.Exists("Sec-WebSocket-Version", _version);
    }

    // As Server
    private bool isValidRequestHostHeader()
    {
      var authority = _context.Headers["Host"];
      if (authority.IsNullOrEmpty() || !_uri.IsAbsoluteUri)
        return true;

      var i = authority.IndexOf(':');
      var host = i > 0
               ? authority.Substring(0, i)
               : authority;
      var type = Uri.CheckHostName(host);

      return type != UriHostNameType.Dns
             ? true
             : Uri.CheckHostName(_uri.DnsSafeHost) != UriHostNameType.Dns
               ? true
               : host == _uri.DnsSafeHost;
    }

    // As Client
    private bool isValidResponse(ResponseHandshake response)
    {
      return !response.IsWebSocketResponse
             ? false
             : !response.HeaderExists("Sec-WebSocket-Accept", createResponseKey())
               ? false
               : !response.HeaderExists("Sec-WebSocket-Version") || response.HeaderExists("Sec-WebSocket-Version", _version);
    }

    private void onClose(CloseEventArgs eventArgs)
    {
      if (!Thread.CurrentThread.IsBackground)
        if (!_exitMessageLoop.IsNull())
          _exitMessageLoop.WaitOne(5 * 1000);

      if (!closeResources())
        eventArgs.WasClean = false;

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
      startMessageLoop();
      OnOpen.Emit(this, EventArgs.Empty);
    }

    private bool ping(string message, int millisecondsTimeout)
    {
      var buffer = Encoding.UTF8.GetBytes(message);
      if (buffer.Length > 125)
      {
        var msg = "A Ping frame must have a payload length of 125 bytes or less.";
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
      return isValidFrame(frame) ? frame : null;
    }

    private string[] readHandshake()
    {
      return _wsStream.ReadHandshake();
    }

    private MessageEventArgs receive(WsFrame frame)
    {
      if (!isValidFrame(frame))
        return null;

      if ((frame.Fin == Fin.FINAL && frame.Opcode == Opcode.CONT) ||
          (frame.Fin == Fin.MORE  && frame.Opcode == Opcode.CONT))
        return null;

      if (frame.Fin == Fin.MORE)
      {// MORE
        var merged = receiveFragmented(frame);
        return !merged.IsNull()
               ? new MessageEventArgs(frame.Opcode, new PayloadData(merged))
               : null;
      }

      if (frame.Opcode == Opcode.CLOSE)
      {// FINAL & CLOSE
        #if DEBUG
        Console.WriteLine("WS: Info@receive: Starts closing handshake.");
        #endif
        close(frame.PayloadData);
        return null;
      }

      if (frame.Opcode == Opcode.PING)
      {// FINAL & PING
        #if DEBUG
        Console.WriteLine("WS: Info@receive: Returns Pong.");
        #endif
        pong(frame.PayloadData);
        return null;
      }

      if (frame.Opcode == Opcode.PONG)
      {// FINAL & PONG
        #if DEBUG
        Console.WriteLine("WS: Info@receive: Receives Pong.");
        #endif
        _receivePong.Set();
        return null;
      }

      // FINAL & (TEXT | BINARY)
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
          Console.WriteLine("WS: Info@receiveFragmented: Starts closing handshake.");
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
          Console.WriteLine("WS: Info@receiveFragmented: Starts closing handshake.");
          #endif
          close(frame.PayloadData);
          return null;
        }

        if (frame.Opcode == Opcode.PING)
        {// FINAL & PING
          #if DEBUG
          Console.WriteLine("WS: Info@receiveFragmented: Returns Pong.");
          #endif
          pong(frame.PayloadData);
          continue;
        }

        if (frame.Opcode == Opcode.PONG)
        {// FINAL & PONG
          #if DEBUG
          Console.WriteLine("WS: Info@receiveFragmented: Receives Pong.");
          #endif
          _receivePong.Set();
          continue;
        }

        // FINAL & (TEXT | BINARY)
        #if DEBUG
        Console.WriteLine("WS: Info@receiveFragmented: Starts closing handshake.");
        #endif
        close(CloseStatusCode.INCORRECT_DATA, String.Empty);
        return null;
      }

      return buffer.ToArray();
    }

    // As Server
    private bool receiveOpeningHandshake()
    {
      #if DEBUG
      var req = RequestHandshake.Parse(_context);
      Console.WriteLine("WS: Info@receiveOpeningHandshake: Opening handshake from client:\n");
      Console.WriteLine(req.ToString());
      #endif
      if (!isValidRequest())
      {
        onError("Invalid WebSocket connection request.");
        close(HttpStatusCode.BadRequest);
        return false;
      }

      _base64key = _context.SecWebSocketKey;
      if (_context.Headers.Exists("Sec-WebSocket-Protocol"))
        _protocols = _context.Headers["Sec-WebSocket-Protocol"];

      if (_context.Headers.Exists("Sec-WebSocket-Extensions"))
        _extensions = _context.Headers["Sec-WebSocket-Extensions"];

      return true;
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
        onError("The WebSocket connection isn't established or has been closed.");
        return false;
      }

      try
      {
        if (_wsStream.IsNull())
          return false;

        _wsStream.WriteFrame(frame);
        return true;
      }
      catch (Exception ex)
      {
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
        try
        {
          if (_readyState != WsState.OPEN)
          {
            onError("The WebSocket connection isn't established or has been closed.");
            return;
          }

          var length = stream.Length;
          if (length <= _fragmentLen)
            send(Fin.FINAL, opcode, stream.ReadBytes((int)length));
          else
            sendFragmented(opcode, stream);
        }
        catch (Exception ex)
        {
          onError(ex.Message);
        }
      }
    }

    private bool send(Fin fin, Opcode opcode, byte[] data)
    {
      var frame = createFrame(fin, opcode, new PayloadData(data));
      return send(frame);
    }

    private void sendAsync(Opcode opcode, byte[] data, Action completed)
    {
      sendAsync(opcode, new MemoryStream(data), completed);
    }

    private void sendAsync(Opcode opcode, Stream stream, Action completed)
    {
      Action<Opcode, Stream> action = send;

      AsyncCallback callback = (ar) =>
      {
        try
        {
          action.EndInvoke(ar);
          if (!completed.IsNull())
            completed();
        }
        catch (Exception ex)
        {
          onError(ex.Message);
        }
        finally
        {
          stream.Close();
        }
      };

      action.BeginInvoke(opcode, stream, callback, null);
    }

    private long sendFragmented(Opcode opcode, Stream stream)
    {
      var length = stream.Length;
      var quo    = length / _fragmentLen;
      var rem    = length % _fragmentLen;
      var count  = rem == 0 ? quo - 2 : quo - 1;

      long readLen = 0;
      var  tmpLen  = 0;
      var  buffer  = new byte[_fragmentLen];

      // First
      tmpLen = stream.Read(buffer, 0, _fragmentLen);
      if (send(Fin.MORE, opcode, buffer))
        readLen += tmpLen;
      else
        return 0;

      // Mid
      for (long i = 0; i < count; i++)
      {
        tmpLen = stream.Read(buffer, 0, _fragmentLen);
        if (send(Fin.MORE, Opcode.CONT, buffer))
          readLen += tmpLen;
        else
          return readLen;
      }

      // Final
      if (rem != 0)
        buffer = new byte[rem];
      tmpLen = stream.Read(buffer, 0, buffer.Length);
      if (send(Fin.FINAL, Opcode.CONT, buffer))
        readLen += tmpLen;

      return readLen;
    }

    // As Client
    private bool sendOpeningHandshake()
    {
      var req = createOpeningHandshake();
      sendRequestHandshake(req);

      var res = receiveResponseHandshake();
      if (!isValidResponse(res))
      {
        var msg = "Invalid response to the WebSocket connection request.";
        onError(msg);
        close(CloseStatusCode.ABNORMAL, msg);
        return false;
      }

      if (res.HeaderExists("Sec-WebSocket-Protocol"))
        _protocol = res.Headers["Sec-WebSocket-Protocol"];

      if (res.HeaderExists("Sec-WebSocket-Extensions"))
        _extensions = res.Headers["Sec-WebSocket-Extensions"];

      return true;
    }

    // As Client
    private void sendRequestHandshake(RequestHandshake request)
    {
      #if DEBUG
      Console.WriteLine("WS: Info@sendRequestHandshake: Request handshake from client:\n");
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

    private void startMessageLoop()
    {
      _exitMessageLoop = new AutoResetEvent(false);
      _receivePong     = new AutoResetEvent(false);

      Action<WsFrame> completed = null;
      completed = (frame) =>
      {
        try
        {
          onMessage(receive(frame));
          if (_readyState == WsState.OPEN)
            _wsStream.ReadFrameAsync(completed);
          else
            _exitMessageLoop.Set();
        }
        catch (WsReceivedTooBigMessageException ex)
        {
          close(CloseStatusCode.TOO_BIG, ex.Message);
        }
        catch (Exception)
        {
          close(CloseStatusCode.ABNORMAL, "An exception has occured.");
        }
      };

      _wsStream.ReadFrameAsync(completed);
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
    /// A <see cref="CloseStatusCode"/> that contains a status code indicating a reason for closure.
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
    /// A <see cref="CloseStatusCode"/> that contains a status code indicating a reason for closure.
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
    /// Establishes a WebSocket connection.
    /// </summary>
    public void Connect()
    {
      if (_readyState == WsState.OPEN)
      {
        Console.WriteLine("WS: Info@Connect: The WebSocket connection has been established already.");
        return;
      }

      try
      {
        if (connect())
          onOpen();
      }
      catch (Exception)
      {
        var msg = "An exception has occured.";
        onError(msg);
        close(CloseStatusCode.ABNORMAL, msg);
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

      return _isClient
             ? ping(message, 5 * 1000)
             : ping(message, 1 * 1000);
    }

    /// <summary>
    /// Sends a binary data using the connection.
    /// </summary>
    /// <param name="data">
    /// An array of <see cref="byte"/> that contains a binary data to send.
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
    /// Sends a text data using the connection.
    /// </summary>
    /// <param name="data">
    /// A <see cref="string"/> that contains a text data to send.
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
    /// <param name="file">
    /// A <see cref="FileInfo"/> that contains a binary data to send.
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

    /// <summary>
    /// Sends a binary data asynchronously using the connection.
    /// </summary>
    /// <param name="data">
    /// An array of <see cref="byte"/> that contains a binary data to send.
    /// </param>
    /// <param name="completed">
    /// An <see cref="Action"/> delegate that contains the method(s) that is called when an asynchronous operation completes.
    /// </param>
    public void SendAsync(byte[] data, Action completed)
    {
      if (data.IsNull())
      {
        onError("'data' must not be null.");
        return;
      }

      sendAsync(Opcode.BINARY, data, completed);
    }

    /// <summary>
    /// Sends a text data asynchronously using the connection.
    /// </summary>
    /// <param name="data">
    /// A <see cref="string"/> that contains a text data to send.
    /// </param>
    /// <param name="completed">
    /// An <see cref="Action"/> delegate that contains the method(s) that is called when an asynchronous operation completes.
    /// </param>
    public void SendAsync(string data, Action completed)
    {
      if (data.IsNull())
      {
        onError("'data' must not be null.");
        return;
      }

      var buffer = Encoding.UTF8.GetBytes(data);
      sendAsync(Opcode.TEXT, buffer, completed);
    }

    /// <summary>
    /// Sends a binary data asynchronously using the connection.
    /// </summary>
    /// <param name="file">
    /// A <see cref="FileInfo"/> that contains a binary data to send.
    /// </param>
    /// <param name="completed">
    /// An <see cref="Action"/> delegate that contains the method(s) that is called when an asynchronous operation completes.
    /// </param>
    public void SendAsync(FileInfo file, Action completed)
    {
      if (file.IsNull())
      {
        onError("'file' must not be null.");
        return;
      }

      sendAsync(Opcode.BINARY, file.OpenRead(), completed);
    }

    #endregion
  }
}
