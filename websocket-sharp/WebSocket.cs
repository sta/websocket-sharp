#region License
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
using System.Net.Security;
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
  /// The WebSocket class provides a set of methods and properties for two-way communication
  /// using the WebSocket protocol (<see href="http://tools.ietf.org/html/rfc6455">RFC 6455</see>).
  /// </remarks>
  public class WebSocket : IDisposable
  {
    #region Private Const Fields

    private const int    _fragmentLen = 1016; // Max value is int.MaxValue - 14.
    private const string _guid        = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    private const string _version     = "13";

    #endregion

    #region Private Fields

    private string            _base64key;
    private RemoteCertificateValidationCallback
                              _certValidationCallback;
    private bool              _client;
    private Action            _closeContext;
    private CookieCollection  _cookies;
    private CompressionMethod _compression;
    private WebSocketContext  _context;
    private WsCredential      _credentials;
    private string            _extensions;
    private AutoResetEvent    _exitReceiving;
    private object            _forClose;
    private object            _forFrame;
    private object            _forSend;
    private volatile Logger   _logger;
    private string            _origin;
    private bool              _preAuth;
    private string            _protocol;
    private string            _protocols;
    private volatile WsState  _readyState;
    private AutoResetEvent    _receivePong;
    private bool              _secure;
    private WsStream          _stream;
    private TcpClient         _tcpClient;
    private Uri               _uri;

    #endregion

    #region Private Constructors

    private WebSocket()
    {
      _compression = CompressionMethod.NONE;
      _cookies = new CookieCollection();
      _extensions = String.Empty;
      _forClose = new object();
      _forFrame = new object();
      _forSend = new object();
      _logger = new Logger();
      _origin = String.Empty;
      _preAuth = false;
      _protocol = String.Empty;
      _readyState = WsState.CONNECTING;
    }

    #endregion

    #region Internal Constructors

    internal WebSocket(HttpListenerWebSocketContext context)
      : this()
    {
      _stream = context.Stream;
      _closeContext = () => context.Close();
      init(context);
    }

    internal WebSocket(TcpListenerWebSocketContext context)
      : this()
    {
      _stream = context.Stream;
      _closeContext = () => context.Close();
      init(context);
    }

    #endregion

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocket"/> class with the specified WebSocket URL
    /// and subprotocols.
    /// </summary>
    /// <param name="url">
    /// A <see cref="string"/> that contains a WebSocket URL to connect.
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
      if (url == null)
        throw new ArgumentNullException("url");

      Uri uri;
      string msg;
      if (!url.TryCreateWebSocketUri(out uri, out msg))
        throw new ArgumentException(msg, "url");

      _uri = uri;
      _protocols = protocols.ToString(", ");
      _client = true;
      _secure = uri.Scheme == "wss" ? true : false;
      _base64key = createBase64Key();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocket"/> class with the specified WebSocket URL,
    /// OnOpen, OnMessage, OnError, OnClose event handlers and subprotocols.
    /// </summary>
    /// <remarks>
    /// This constructor initializes a new instance of the <see cref="WebSocket"/> class and
    /// establishes a WebSocket connection.
    /// </remarks>
    /// <param name="url">
    /// A <see cref="string"/> that contains a WebSocket URL to connect.
    /// </param>
    /// <param name="onOpen">
    /// An <see cref="OnOpen"/> event handler.
    /// </param>
    /// <param name="onMessage">
    /// An <see cref="OnMessage"/> event handler.
    /// </param>
    /// <param name="onError">
    /// An <see cref="OnError"/> event handler.
    /// </param>
    /// <param name="onClose">
    /// An <see cref="OnClose"/> event handler.
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

    #region Internal Properties

    internal CookieCollection CookieCollection {
      get {
        return _cookies;
      }
    }

    internal bool IsOpened {
      get {
        return _readyState == WsState.OPEN || _readyState == WsState.CLOSING;
      }
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets or sets the compression method used to compress the payload data of the WebSocket Data frame.
    /// </summary>
    /// <value>
    /// One of the <see cref="CompressionMethod"/> values that indicates the compression method to use.
    /// The default is <see cref="CompressionMethod.NONE"/>.
    /// </value>
    public CompressionMethod Compression {
      get {
        return _compression;
      }

      set {
        if (IsOpened)
        {
          var msg = "The WebSocket connection has already been established.";
          _logger.Error(msg);
          error(msg);

          return;
        }

        _compression = value;
      }
    }

    /// <summary>
    /// Gets the cookies used in the WebSocket opening handshake.
    /// </summary>
    /// <value>
    /// An IEnumerable&lt;Cookie&gt; interface that provides an enumerator which supports the iteration
    /// over the collection of cookies.
    /// </value>
    public IEnumerable<Cookie> Cookies {
      get {
        lock (_cookies.SyncRoot)
        {
          return from Cookie cookie in _cookies
                 select cookie;
        }
      }
    }

    /// <summary>
    /// Gets the credentials for HTTP authentication (Basic/Digest).
    /// </summary>
    /// <value>
    /// A <see cref="WsCredential"/> that contains the credentials for HTTP authentication.
    /// </value>
    public WsCredential Credentials {
      get {
        return _credentials;
      }
    }

    /// <summary>
    /// Gets the WebSocket extensions selected by the server.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that contains the extensions if any. The default is <see cref="String.Empty"/>.
    /// </value>
    public string Extensions {
      get {
        return _extensions;
      }
    }

    /// <summary>
    /// Gets a value indicating whether the WebSocket connection is alive.
    /// </summary>
    /// <value>
    /// <c>true</c> if the WebSocket connection is alive; otherwise, <c>false</c>.
    /// </value>
    public bool IsAlive {
      get {
        return _readyState == WsState.OPEN
               ? ping(new byte[]{})
               : false;
      }
    }

    /// <summary>
    /// Gets a value indicating whether the WebSocket connection is secure.
    /// </summary>
    /// <value>
    /// <c>true</c> if the connection is secure; otherwise, <c>false</c>.
    /// </value>
    public bool IsSecure {
      get {
        return _secure;
      }
    }

    /// <summary>
    /// Gets the logging functions.
    /// </summary>
    /// <remarks>
    /// The default logging level is the <see cref="LogLevel.ERROR"/>.
    /// If you want to change the current logging level, you set the <c>Log.Level</c> property
    /// to one of the <see cref="LogLevel"/> values which you want.
    /// </remarks>
    /// <value>
    /// A <see cref="Logger"/> that provides the logging functions.
    /// </value>
    public Logger Log {
      get {
        return _logger;
      }

      internal set {
        if (value == null)
          return;

        _logger = value;
      }
    }

    /// <summary>
    /// Gets or sets the value of the Origin header used in the WebSocket opening handshake.
    /// </summary>
    /// <remarks>
    /// A <see cref="WebSocket"/> instance does not send the Origin header in the WebSocket opening handshake
    /// if the value of this property is <see cref="String.Empty"/>.
    /// </remarks>
    /// <value>
    ///   <para>
    ///   A <see cref="string"/> that contains the value of the <see href="http://tools.ietf.org/html/rfc6454#section-7">HTTP Origin header</see> to send.
    ///   The default is <see cref="String.Empty"/>.
    ///   </para>
    ///   <para>
    ///   The value of the Origin header has the following syntax: <c>&lt;scheme&gt;://&lt;host&gt;[:&lt;port&gt;]</c>
    ///   </para>
    /// </value>
    public string Origin {
      get {
        return _origin;
      }

      set {
        string msg = null;
        if (IsOpened)
        {
          msg = "The WebSocket connection has already been established.";
        }
        else if (value.IsNullOrEmpty())
        {
          _origin = String.Empty;
          return;
        }
        else
        {
          var origin = new Uri(value);
          if (!origin.IsAbsoluteUri || origin.Segments.Length > 1)
            msg = "The syntax of value of Origin must be '<scheme>://<host>[:<port>]'.";
        }

        if (msg != null)
        {
          _logger.Error(msg);
          error(msg);

          return;
        }

        _origin = value.TrimEnd('/');
      }
    }

    /// <summary>
    /// Gets the WebSocket subprotocol selected by the server.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that contains the subprotocol if any. The default is <see cref="String.Empty"/>.
    /// </value>
    public string Protocol {
      get {
        return _protocol;
      }
    }

    /// <summary>
    /// Gets the state of the WebSocket connection.
    /// </summary>
    /// <value>
    /// One of the <see cref="WebSocketSharp.WsState"/> values. The default is <see cref="WsState.CONNECTING"/>.
    /// </value>
    public WsState ReadyState {
      get {
        return _readyState;
      }
    }

    /// <summary>
    /// Gets or sets the callback used to validate the certificate supplied by the server.
    /// </summary>
    /// <remarks>
    /// If the value of this property is <see langword="null"/>, the validation does nothing
    /// with the server certificate, always returns valid.
    /// </remarks>
    /// <value>
    /// A <see cref="RemoteCertificateValidationCallback"/> delegate that references the method(s)
    /// used to validate the server certificate. The default is <see langword="null"/>.
    /// </value>
    public RemoteCertificateValidationCallback ServerCertificateValidationCallback {
      get {
        return _certValidationCallback;
      }

      set {
        _certValidationCallback = value;
      }
    }

    /// <summary>
    /// Gets the WebSocket URL to connect.
    /// </summary>
    /// <value>
    /// A <see cref="Uri"/> that contains the WebSocket URL to connect.
    /// </value>
    public Uri Url {
      get {
        return _uri;
      }

      internal set {
        if (_readyState == WsState.CONNECTING && !_client)
          _uri = value;
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
    /// Occurs when the <see cref="WebSocket"/> receives a data frame.
    /// </summary>
    public event EventHandler<MessageEventArgs> OnMessage;

    /// <summary>
    /// Occurs when the WebSocket connection has been established.
    /// </summary>
    public event EventHandler OnOpen;

    #endregion

    #region Private Methods

    // As server
    private bool acceptHandshake ()
    {
      return processHandshakeRequest ()
             ? send (createHandshakeResponse ())
             : false;
    }

    private void close (CloseEventArgs eventArgs)
    {
      if (!Thread.CurrentThread.IsBackground && _exitReceiving != null)
        if (!_exitReceiving.WaitOne (5 * 1000))
          eventArgs.WasClean = false;

      if (!closeResources ())
        eventArgs.WasClean = false;

      _readyState = WsState.CLOSED;
      OnClose.Emit (this, eventArgs);
    }

    private void close (PayloadData data)
    {
      _logger.Debug ("Is this thread background?: " + Thread.CurrentThread.IsBackground);
      CloseEventArgs args = null;
      lock (_forClose)
      {
        if (_readyState == WsState.CLOSING || _readyState == WsState.CLOSED)
          return;

        var state = _readyState;
        _readyState = WsState.CLOSING;
        args = new CloseEventArgs (data);
        if (state == WsState.CONNECTING)
        {
          if (!_client)
            args.WasClean = send (createHandshakeResponse (HttpStatusCode.BadRequest));
        }
        else
        {
          if (!data.ContainsReservedCloseStatusCode)
            args.WasClean = send (createControlFrame (Opcode.CLOSE, data, _client));
        }
      }

      close (args);
      _logger.Trace ("Exit close method.");
    }

    private void close (ushort code, string reason)
    {
      var data = code.Append (reason);
      if (data.Length > 125)
      {
        var msg = "The payload length of a Close frame must be 125 bytes or less.";
        _logger.Error (msg);
        error (msg);

        return;
      }

      close (new PayloadData (data));
    }

    // As client
    private void closeClientResources ()
    {
      if (_stream != null)
      {
        _stream.Dispose ();
        _stream = null;
      }

      if (_tcpClient != null)
      {
        _tcpClient.Close ();
        _tcpClient = null;
      }
    }

    private bool closeResources ()
    {
      try {
        if (_client)
          closeClientResources ();
        else
          closeServerResources ();

        return true;
      }
      catch (Exception ex) {
        _logger.Fatal (ex.Message);
        error ("An exception has occured.");

        return false;
      }
    }

    // As server
    private void closeServerResources ()
    {
      if (_context != null && _closeContext != null)
      {
        _closeContext ();
        _stream = null;
        _context = null;
      }
    }

    private bool concatenateFragments(Stream dest)
    {
      Func<WsFrame, bool> processContinuation = contFrame =>
      {
        if (!contFrame.IsContinuation)
          return false;

        dest.WriteBytes(contFrame.PayloadData.ApplicationData);
        return true;
      };

      while (true)
      {
        var frame = _stream.ReadFrame();
        if (processAbnormal(frame))
          return false;

        if (!frame.IsFinal)
        {
          // MORE & CONT
          if (processContinuation(frame))
            continue;
        }
        else
        {
          // FINAL & CONT
          if (processContinuation(frame))
            break;

          // FINAL & PING
          if (processPing(frame))
            continue;

          // FINAL & PONG
          if (processPong(frame))
            continue;

          // FINAL & CLOSE
          if (processClose(frame))
            return false;
        }

        // ?
        processIncorrectFrame();
        return false;
      }

      return true;
    }

    private bool connect()
    {
      return _client
             ? doHandshake()
             : acceptHandshake();
    }

    // As client
    private static string createBase64Key()
    {
      var src = new byte[16];
      var rand = new Random();
      rand.NextBytes(src);

      return Convert.ToBase64String(src);
    }

    private static string createCompressionExtension(CompressionMethod method)
    {
      return createCurrentCompressionExtension(method);
    }

    private static WsFrame createControlFrame(Opcode opcode, PayloadData payloadData, bool client)
    {
      var mask = client ? Mask.MASK : Mask.UNMASK;
      var frame = new WsFrame(Fin.FINAL, opcode, mask, payloadData);

      return frame;
    }

    private static string createCurrentCompressionExtension(CompressionMethod method)
    {
      return method != CompressionMethod.NONE
             ? String.Format("permessage-{0}", method.ToString().ToLower())
             : String.Empty;
    }

    private static string createDeprecatedCompressionExtension(CompressionMethod method)
    {
      return method != CompressionMethod.NONE
             ? String.Format("permessage-compress; method={0}", method.ToString().ToLower())
             : String.Empty;
    }

    private static WsFrame createFrame(
      Fin fin, Opcode opcode, PayloadData payloadData, bool compressed, bool client)
    {
      var mask = client ? Mask.MASK : Mask.UNMASK;
      var frame = new WsFrame(fin, opcode, mask, payloadData, compressed);

      return frame;
    }

    // As client
    private HandshakeRequest createHandshakeRequest ()
    {
      var path = _uri.PathAndQuery;
      var host = _uri.Port == 80
               ? _uri.DnsSafeHost
               : _uri.Authority;

      var req = new HandshakeRequest (path);
      req.AddHeader ("Host", host);

      if (_origin.Length > 0)
        req.AddHeader ("Origin", _origin);

      req.AddHeader ("Sec-WebSocket-Key", _base64key);

      if (!_protocols.IsNullOrEmpty ())
        req.AddHeader ("Sec-WebSocket-Protocol", _protocols);

      var extensions = createRequestExtensions ();
      if (extensions.Length > 0)
        req.AddHeader ("Sec-WebSocket-Extensions", extensions);

      req.AddHeader ("Sec-WebSocket-Version", _version);

      if (_preAuth && _credentials != null)
        req.SetAuthorization (new AuthenticationResponse (_credentials));

      if (_cookies.Count > 0)
        req.SetCookies (_cookies);

      return req;
    }

    // As server
    private HandshakeResponse createHandshakeResponse ()
    {
      var res = new HandshakeResponse ();
      res.AddHeader ("Sec-WebSocket-Accept", createResponseKey ());
      if (_extensions.Length > 0)
        res.AddHeader ("Sec-WebSocket-Extensions", _extensions);

      if (_cookies.Count > 0)
        res.SetCookies (_cookies);

      return res;
    }

    // As server
    private HandshakeResponse createHandshakeResponse (HttpStatusCode code)
    {
      var res = HandshakeResponse.CreateCloseResponse (code);
      res.AddHeader ("Sec-WebSocket-Version", _version);

      return res;
    }

    // As client
    private string createRequestExtensions ()
    {
      var extensions = new StringBuilder (64);
      var comp = createCompressionExtension (_compression);
      if (comp.Length > 0)
        extensions.Append (comp);

      return extensions.Length > 0
             ? extensions.ToString ()
             : String.Empty;
    }

    private string createResponseKey ()
    {
      var buffer = new StringBuilder (_base64key, 64);
      buffer.Append (_guid);
      SHA1 sha1 = new SHA1CryptoServiceProvider ();
      var src = sha1.ComputeHash (Encoding.UTF8.GetBytes (buffer.ToString ()));

      return Convert.ToBase64String (src);
    }

    // As client
    private bool doHandshake ()
    {
      setClientStream ();
      return processHandshakeResponse (sendHandshakeRequest ());
    }

    private void error(string message)
    {
      OnError.Emit(this, new ErrorEventArgs(message));
    }

    private static CompressionMethod getCompressionMethod(string value)
    {
      var deprecated = createDeprecatedCompressionExtension(CompressionMethod.DEFLATE);
      if (value.Equals(deprecated))
        return CompressionMethod.DEFLATE;

      foreach (CompressionMethod method in Enum.GetValues(typeof(CompressionMethod)))
        if (isCompressionExtension(value, method))
          return method;

      return CompressionMethod.NONE;
    }

    // As server
    private void init(WebSocketContext context)
    {
      _context = context;
      _uri = context.Path.ToUri();
      _secure = context.IsSecureConnection;
      _client = false;
    }

    private static bool isCompressionExtension(string value)
    {
      return value.StartsWith("permessage-");
    }

    private static bool isCompressionExtension(string value, CompressionMethod method)
    {
      var expected = createCompressionExtension(method);
      return expected.Length > 0
             ? value.Equals(expected)
             : false;
    }

    // As server
    private bool isValidHostHeader()
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

    // As server
    private bool isValidRequesHandshake()
    {
      return !_context.IsValid
             ? false
             : !isValidHostHeader()
               ? false
               : _context.Headers.Contains("Sec-WebSocket-Version", _version);
    }

    // As client
    private bool isValidHandshakeResponse (HandshakeResponse response)
    {
      return !response.IsWebSocketResponse
             ? false
             : !response.ContainsHeader ("Sec-WebSocket-Accept", createResponseKey ())
               ? false
               : !response.ContainsHeader ("Sec-WebSocket-Version") ||
                 response.ContainsHeader ("Sec-WebSocket-Version", _version);
    }

    private void open()
    {
      _readyState = WsState.OPEN;
      startReceiving();
      OnOpen.Emit(this, EventArgs.Empty);
    }

    private bool ping(byte[] data)
    {
      var frame = createControlFrame(Opcode.PING, new PayloadData(data), _client);
      var timeOut = _client ? 5000 : 1000;

      return send(frame)
             ? _receivePong.WaitOne(timeOut)
             : false;
    }

    private void pong(PayloadData data)
    {
      var frame = createControlFrame(Opcode.PONG, data, _client);
      send(frame);
    }

    private bool processAbnormal(WsFrame frame)
    {
      if (frame != null)
        return false;

      _logger.Trace("Start closing handshake.");
      var code = CloseStatusCode.ABNORMAL;
      Close(code, code.GetMessage());

      return true;
    }

    private bool processClose(WsFrame frame)
    {
      if (!frame.IsClose)
        return false;

      _logger.Trace("Start closing handshake.");
      close(frame.PayloadData);

      return true;
    }

    private bool processData(WsFrame frame)
    {
      if (!frame.IsData)
        return false;

      if (frame.IsCompressed && _compression == CompressionMethod.NONE)
        return false;

      var args = frame.IsCompressed
               ? new MessageEventArgs(
                   frame.Opcode, frame.PayloadData.ApplicationData.Decompress(_compression))
               : new MessageEventArgs(frame.Opcode, frame.PayloadData);

      OnMessage.Emit(this, args);
      return true;
    }

    private bool processFragmented(WsFrame frame)
    {
      // Not first fragment
      if (frame.IsContinuation)
        return true;

      // Not fragmented
      if (frame.IsFinal)
        return false;

      bool incorrect = !frame.IsData ||
                       (frame.IsCompressed && _compression == CompressionMethod.NONE);

      if (!incorrect)
        processFragments(frame);
      else
        processIncorrectFrame();

      return true;
    }

    private void processFragments(WsFrame first)
    {
      using (var concatenated = new MemoryStream())
      {
        concatenated.WriteBytes(first.PayloadData.ApplicationData);
        if (!concatenateFragments(concatenated))
          return;

        byte[] data;
        if (_compression != CompressionMethod.NONE)
        {
          data = concatenated.DecompressToArray(_compression);
        }
        else
        {
          concatenated.Close();
          data = concatenated.ToArray();
        }

        OnMessage.Emit(this, new MessageEventArgs(first.Opcode, data));
      }
    }

    private void processFrame(WsFrame frame)
    {
      bool processed = processAbnormal(frame) ||
                       processFragmented(frame) ||
                       processData(frame) ||
                       processPing(frame) ||
                       processPong(frame) ||
                       processClose(frame);

      if (!processed)
        processIncorrectFrame();
    }

    private void processIncorrectFrame()
    {
      _logger.Trace("Start closing handshake.");
      Close(CloseStatusCode.INCORRECT_DATA);
    }

    private bool processPing(WsFrame frame)
    {
      if (!frame.IsPing)
        return false;

      _logger.Trace("Return Pong.");
      pong(frame.PayloadData);

      return true;
    }

    private bool processPong(WsFrame frame)
    {
      if (!frame.IsPong)
        return false;

      _logger.Trace("Receive Pong.");
      _receivePong.Set();

      return true;
    }

    // As server
    private void processRequestExtensions(string extensions)
    {
      if (extensions.IsNullOrEmpty())
        return;

      var comp = false;
      var buffer = new List<string>();
      foreach (var extension in extensions.SplitHeaderValue(','))
      {
        var e = extension.Trim();
        var tmp = e.RemovePrefix("x-webkit-");
        if (!comp && isCompressionExtension(tmp))
        {
          var method = getCompressionMethod(tmp);
          if (method != CompressionMethod.NONE)
          {
            _compression = method;
            comp = true;
            buffer.Add(e);
          }
        }
      }

      if (buffer.Count > 0)
        _extensions = buffer.ToArray().ToString(", ");
    }

    // As server
    private bool processHandshakeRequest ()
    {
      var req = HandshakeRequest.Parse (_context);
      _logger.Debug ("A handshake request from a client:\n" + req.ToString ());
      if (!isValidRequesHandshake ())
      {
        var msg = "Invalid WebSocket connection request.";
        _logger.Error (msg);
        error (msg);
        Close (HttpStatusCode.BadRequest);

        return false;
      }

      _base64key = _context.SecWebSocketKey;

      var protocols = _context.Headers ["Sec-WebSocket-Protocol"];
      if (!protocols.IsNullOrEmpty ())
        _protocols = protocols;

      processRequestExtensions (_context.Headers ["Sec-WebSocket-Extensions"]);

      return true;
    }

    // As client
    private void processResponseCookies(CookieCollection cookies)
    {
      if (cookies.Count > 0)
        _cookies.SetOrRemove(cookies);
    }

    // As client
    private void processResponseExtensions(string extensions)
    {
      var checkComp = _compression != CompressionMethod.NONE
                    ? true
                    : false;

      var comp = false;
      if (!extensions.IsNullOrEmpty())
      {
        foreach (var extension in extensions.SplitHeaderValue(','))
        {
          var e = extension.Trim();
          if (checkComp &&
              !comp &&
              isCompressionExtension(e, _compression))
            comp = true;
        }

        _extensions = extensions;
      }

      if (checkComp && !comp)
        _compression = CompressionMethod.NONE;
    }

    // As client
    private bool processHandshakeResponse (HandshakeResponse response)
    {
      var msg = response.IsUnauthorized
              ? String.Format ("An HTTP {0} authorization is required.", response.AuthChallenge.Scheme)
              : !isValidHandshakeResponse (response)
                ? "Invalid response to this WebSocket connection request."
                : String.Empty;

      if (msg.Length > 0)
      {
        _logger.Error (msg);
        error (msg);
        Close (CloseStatusCode.ABNORMAL, msg);

        return false;
      }

      processResponseProtocol (response.Headers ["Sec-WebSocket-Protocol"]);
      processResponseExtensions (response.Headers ["Sec-WebSocket-Extensions"]);
      processResponseCookies (response.Cookies);

      return true;
    }

    // As client
    private void processResponseProtocol(string protocol)
    {
      if (!protocol.IsNullOrEmpty())
        _protocol = protocol;
    }

    // As client
    private HandshakeResponse receiveHandshakeResponse ()
    {
      var res = HandshakeResponse.Parse (_stream.ReadHandshake ());
      _logger.Debug ("A handshake response from the server:\n" + res.ToString ());

      return res;
    }

    // As client
    private void send (HandshakeRequest request)
    {
      _logger.Debug ("A handshake Request to the server:\n" + request.ToString ());
      _stream.WriteHandshake (request);
    }

    // As server
    private bool send (HandshakeResponse response)
    {
      _logger.Debug ("A handshake response to a client:\n" + response.ToString ());
      return _stream.WriteHandshake (response);
    }

    private bool send (WsFrame frame)
    {
      lock (_forFrame)
      {
        var ready = _stream == null
                    ? false
                    : _readyState == WsState.OPEN
                      ? true
                      : _readyState == WsState.CLOSING
                        ? frame.IsClose
                        : false;

        if (!ready)
        {
          var msg = "The WebSocket connection isn't established or has been closed.";
          _logger.Error (msg);
          error (msg);

          return false;
        }

        return _stream.WriteFrame (frame);
      }
    }

    private void send(Opcode opcode, Stream stream)
    {
      var data = stream;
      var compressed = false;
      try
      {
        if (_readyState != WsState.OPEN)
        {
          var msg = "The WebSocket connection isn't established or has been closed.";
          _logger.Error(msg);
          error(msg);

          return;
        }

        if (_compression != CompressionMethod.NONE)
        {
          data = data.Compress(_compression);
          compressed = true;
        }

        var length = data.Length;
        lock (_forSend)
        {
          if (length <= _fragmentLen)
            send(Fin.FINAL, opcode, data.ReadBytes((int)length), compressed);
          else
            sendFragmented(opcode, data, compressed);
        }
      }
      catch (Exception ex)
      {
        _logger.Fatal(ex.Message);
        error("An exception has occured.");
      }
      finally
      {
        if (compressed)
          data.Dispose();

        stream.Dispose();
      }
    }

    private bool send(Fin fin, Opcode opcode, byte[] data, bool compressed)
    {
      var frame = createFrame(fin, opcode, new PayloadData(data), compressed, _client);
      return send(frame);
    }

    private void sendAsync(Opcode opcode, Stream stream, Action completed)
    {
      Action<Opcode, Stream> sender = send;
      AsyncCallback callback = ar =>
      {
        try
        {
          sender.EndInvoke(ar);
          if (completed != null)
            completed();
        }
        catch (Exception ex)
        {
          _logger.Fatal(ex.Message);
          error("An exception has occured.");
        }
      };

      sender.BeginInvoke(opcode, stream, callback, null);
    }

    private long sendFragmented(Opcode opcode, Stream stream, bool compressed)
    {
      var length = stream.Length;
      var quo    = length / _fragmentLen;
      var rem    = length % _fragmentLen;
      var count  = rem == 0 ? quo - 2 : quo - 1;

      long readLen = 0;
      var tmpLen = 0;
      var buffer = new byte[_fragmentLen];

      // First
      tmpLen = stream.Read(buffer, 0, _fragmentLen);
      if (send(Fin.MORE, opcode, buffer, compressed))
        readLen += tmpLen;
      else
        return 0;

      // Mid
      for (long i = 0; i < count; i++)
      {
        tmpLen = stream.Read(buffer, 0, _fragmentLen);
        if (send(Fin.MORE, Opcode.CONT, buffer, compressed))
          readLen += tmpLen;
        else
          return readLen;
      }

      // Final
      if (rem != 0)
        buffer = new byte[rem];
      tmpLen = stream.Read(buffer, 0, buffer.Length);
      if (send(Fin.FINAL, Opcode.CONT, buffer, compressed))
        readLen += tmpLen;

      return readLen;
    }

    // As client
    private HandshakeResponse sendHandshakeRequest ()
    {
      var req = createHandshakeRequest ();
      var res = sendHandshakeRequest (req);
      if (!_preAuth && res.IsUnauthorized && _credentials != null)
      {
        var challenge = res.AuthChallenge;
        req.SetAuthorization (new AuthenticationResponse (_credentials, challenge));
        res = sendHandshakeRequest (req);
      }

      return res;
    }

    // As client
    private HandshakeResponse sendHandshakeRequest (HandshakeRequest request)
    {
      send (request);
      return receiveHandshakeResponse ();
    }

    // As client
    private void setClientStream ()
    {
      var host = _uri.DnsSafeHost;
      var port = _uri.Port;
      _tcpClient = new TcpClient (host, port);
      _stream = WsStream.CreateClientStream (_tcpClient, _secure, host, _certValidationCallback);
    }

    private void startReceiving ()
    {
      _exitReceiving = new AutoResetEvent (false);
      _receivePong = new AutoResetEvent (false);

      Action<WsFrame> completed = null;
      completed = frame =>
      {
        try {
          processFrame (frame);
          if (_readyState == WsState.OPEN)
            _stream.ReadFrameAsync (completed);
          else
            _exitReceiving.Set ();
        }
        catch (WebSocketException ex) {
          _logger.Fatal (ex.Message);
          Close (ex.Code, ex.Message);
        }
        catch (Exception ex) {
          _logger.Fatal (ex.Message);
          Close (CloseStatusCode.ABNORMAL, "An exception has occured.");
        }
      };

      _stream.ReadFrameAsync (completed);
    }

    #endregion

    #region Internal Methods

    // As server
    internal void Close (HttpStatusCode code)
    {
      _readyState = WsState.CLOSING;
      send (createHandshakeResponse (code));
      closeResources ();
      _readyState = WsState.CLOSED;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Closes the WebSocket connection and releases all associated resources.
    /// </summary>
    public void Close ()
    {
      close (new PayloadData ());
    }

    /// <summary>
    /// Closes the WebSocket connection with the specified <paramref name="code"/> and
    /// releases all associated resources.
    /// </summary>
    /// <remarks>
    /// This Close method emits a <see cref="OnError"/> event if <paramref name="code"/> is not
    /// in the allowable range of the WebSocket close status code.
    /// </remarks>
    /// <param name="code">
    /// A <see cref="ushort"/> that indicates the status code for closure.
    /// </param>
    public void Close (ushort code)
    {
      Close (code, String.Empty);
    }

    /// <summary>
    /// Closes the WebSocket connection with the specified <paramref name="code"/> and
    /// releases all associated resources.
    /// </summary>
    /// <param name="code">
    /// One of the <see cref="CloseStatusCode"/> values that indicates the status code for closure.
    /// </param>
    public void Close (CloseStatusCode code)
    {
      close ((ushort) code, String.Empty);
    }

    /// <summary>
    /// Closes the WebSocket connection with the specified <paramref name="code"/> and
    /// <paramref name="reason"/>, and releases all associated resources.
    /// </summary>
    /// <remarks>
    /// This Close method emits a <see cref="OnError"/> event if <paramref name="code"/> is not
    /// in the allowable range of the WebSocket close status code.
    /// </remarks>
    /// <param name="code">
    /// A <see cref="ushort"/> that indicates the status code for closure.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that contains the reason for closure.
    /// </param>
    public void Close (ushort code, string reason)
    {
      if (!code.IsCloseStatusCode ())
      {
        var msg = String.Format ("Invalid close status code: {0}", code);
        _logger.Error (msg);
        error (msg);

        return;
      }

      close (code, reason);
    }

    /// <summary>
    /// Closes the WebSocket connection with the specified <paramref name="code"/> and
    /// <paramref name="reason"/>, and releases all associated resources.
    /// </summary>
    /// <param name="code">
    /// One of the <see cref="CloseStatusCode"/> values that indicates the status code for closure.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that contains the reason for closure.
    /// </param>
    public void Close (CloseStatusCode code, string reason)
    {
      close ((ushort) code, reason);
    }

    /// <summary>
    /// Establishes a WebSocket connection.
    /// </summary>
    public void Connect()
    {
      if (IsOpened)
      {
        var msg = "The WebSocket connection has already been established.";
        _logger.Error(msg);
        error(msg);

        return;
      }

      try
      {
        if (connect())
          open();
      }
      catch (Exception ex)
      {
        _logger.Fatal(ex.Message);
        var msg = "An exception has occured.";
        error(msg);
        Close(CloseStatusCode.ABNORMAL, msg);
      }
    }

    /// <summary>
    /// Closes the WebSocket connection and releases all associated resources.
    /// </summary>
    /// <remarks>
    /// This method closes the WebSocket connection with the <see cref="CloseStatusCode.AWAY"/>.
    /// </remarks>
    public void Dispose()
    {
      Close(CloseStatusCode.AWAY);
    }

    /// <summary>
    /// Sends a Ping using the WebSocket connection.
    /// </summary>
    /// <returns>
    /// <c>true</c> if a <see cref="WebSocket"/> instance receives a Pong in a time; otherwise, <c>false</c>.
    /// </returns>
    public bool Ping()
    {
      return ping(new byte[]{});
    }

    /// <summary>
    /// Sends a Ping with the specified <paramref name="message"/> using the WebSocket connection.
    /// </summary>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to send with a Ping.
    /// </param>
    /// <returns>
    /// <c>true</c> if a <see cref="WebSocket"/> instance receives a Pong in a time; otherwise, <c>false</c>.
    /// </returns>
    public bool Ping(string message)
    {
      if (message.IsNullOrEmpty())
        return ping(new byte[]{});

      var data = Encoding.UTF8.GetBytes(message);
      if (data.Length > 125)
      {
        var msg = "The payload length of a Ping frame must be 125 bytes or less.";
        _logger.Error(msg);
        error(msg);

        return false;
      }

      return ping(data);
    }

    /// <summary>
    /// Sends a binary <paramref name="data"/> using the WebSocket connection.
    /// </summary>
    /// <param name="data">
    /// An array of <see cref="byte"/> that contains a binary data to send.
    /// </param>
    public void Send(byte[] data)
    {
      if (data == null)
      {
        var msg = "'data' must not be null.";
        _logger.Error(msg);
        error(msg);

        return;
      }

      var stream = new MemoryStream(data);
      send(Opcode.BINARY, stream);
    }

    /// <summary>
    /// Sends a text <paramref name="data"/> using the WebSocket connection.
    /// </summary>
    /// <param name="data">
    /// A <see cref="string"/> that contains a text data to send.
    /// </param>
    public void Send(string data)
    {
      if (data == null)
      {
        var msg = "'data' must not be null.";
        _logger.Error(msg);
        error(msg);

        return;
      }

      var stream = new MemoryStream(Encoding.UTF8.GetBytes(data));
      send(Opcode.TEXT, stream);
    }

    /// <summary>
    /// Sends a binary data using the WebSocket connection.
    /// </summary>
    /// <param name="file">
    /// A <see cref="FileInfo"/> that contains a binary data to send.
    /// </param>
    public void Send(FileInfo file)
    {
      if (file == null)
      {
        var msg = "'file' must not be null.";
        _logger.Error(msg);
        error(msg);

        return;
      }

      send(Opcode.BINARY, file.OpenRead());
    }

    /// <summary>
    /// Sends a binary <paramref name="data"/> asynchronously using the WebSocket connection.
    /// </summary>
    /// <param name="data">
    /// An array of <see cref="byte"/> that contains a binary data to send.
    /// </param>
    /// <param name="completed">
    /// An <see cref="Action"/> delegate that references the method(s) called when
    /// the asynchronous operation completes.
    /// </param>
    public void SendAsync(byte[] data, Action completed)
    {
      if (data == null)
      {
        var msg = "'data' must not be null.";
        _logger.Error(msg);
        error(msg);

        return;
      }

      var stream = new MemoryStream(data);
      sendAsync(Opcode.BINARY, stream, completed);
    }

    /// <summary>
    /// Sends a text <paramref name="data"/> asynchronously using the WebSocket connection.
    /// </summary>
    /// <param name="data">
    /// A <see cref="string"/> that contains a text data to send.
    /// </param>
    /// <param name="completed">
    /// An <see cref="Action"/> delegate that references the method(s) called when
    /// the asynchronous operation completes.
    /// </param>
    public void SendAsync(string data, Action completed)
    {
      if (data == null)
      {
        var msg = "'data' must not be null.";
        _logger.Error(msg);
        error(msg);

        return;
      }

      var stream = new MemoryStream(Encoding.UTF8.GetBytes(data));
      sendAsync(Opcode.TEXT, stream, completed);
    }

    /// <summary>
    /// Sends a binary data asynchronously using the WebSocket connection.
    /// </summary>
    /// <param name="file">
    /// A <see cref="FileInfo"/> that contains a binary data to send.
    /// </param>
    /// <param name="completed">
    /// An <see cref="Action"/> delegate that references the method(s) called when
    /// the asynchronous operation completes.
    /// </param>
    public void SendAsync(FileInfo file, Action completed)
    {
      if (file == null)
      {
        var msg = "'file' must not be null.";
        _logger.Error(msg);
        error(msg);

        return;
      }

      sendAsync(Opcode.BINARY, file.OpenRead(), completed);
    }

    /// <summary>
    /// Sets a <see cref="Cookie"/> used in the WebSocket opening handshake.
    /// </summary>
    /// <param name="cookie">
    /// A <see cref="Cookie"/> that contains an HTTP Cookie to set.
    /// </param>
    public void SetCookie(Cookie cookie)
    {
      var msg = IsOpened
              ? "The WebSocket connection has already been established."
              : cookie == null
                ? "'cookie' must not be null."
                : null;

      if (msg != null)
      {
        _logger.Error(msg);
        error(msg);

        return;
      }

      lock (_cookies.SyncRoot)
      {
        _cookies.SetOrRemove(cookie);
      }
    }

    /// <summary>
    /// Sets the credentials for HTTP authentication (Basic/Digest).
    /// </summary>
    /// <param name="userName">
    /// A <see cref="string"/> that contains a user name associated with the credentials.
    /// </param>
    /// <param name="password">
    /// A <see cref="string"/> that contains a password for <paramref name="userName"/> associated with the credentials.
    /// </param>
    /// <param name="preAuth">
    /// <c>true</c> if sends the credentials as a Basic authorization with the first request handshake;
    /// otherwise, <c>false</c>.
    /// </param>
    public void SetCredentials(string userName, string password, bool preAuth)
    {
      string msg = null;
      if (IsOpened)
      {
        msg = "The WebSocket connection has already been established.";
      }
      else if (userName == null)
      {
        _credentials = null;
        _preAuth = false;

        return;
      }
      else
      {
        msg = userName.Length > 0 && (userName.Contains(':') || !userName.IsText())
            ? "'userName' contains an invalid character."
            : !password.IsNullOrEmpty() && !password.IsText()
              ? "'password' contains an invalid character."
              : null;
      }

      if (msg != null)
      {
        _logger.Error(msg);
        error(msg);

        return;
      }

      _credentials = new WsCredential(userName, password, _uri.PathAndQuery);
      _preAuth = preAuth;
    }

    #endregion
  }
}
