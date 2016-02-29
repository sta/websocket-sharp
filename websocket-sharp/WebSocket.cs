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
 * Copyright (c) 2010-2016 sta.blockhead
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

#region Contributors
/*
 * Contributors:
 * - Frank Razenberg <frank@zzattack.org>
 * - David Wood <dpwood@gmail.com>
 * - Liryna <liryna.stark@gmail.com>
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

    private AuthenticationChallenge        _authChallenge;
    private string                         _base64Key;
    private bool                           _client;
    private Action                         _closeContext;
    private CompressionMethod              _compression;
    private WebSocketContext               _context;
    private CookieCollection               _cookies;
    private NetworkCredential              _credentials;
    private bool                           _emitOnPing;
    private bool                           _enableRedirection;
    private AutoResetEvent                 _exitReceiving;
    private string                         _extensions;
    private bool                           _extensionsRequested;
    private object                         _forConn;
    private object                         _forMessageEventQueue;
    private object                         _forSend;
    private MemoryStream                   _fragmentsBuffer;
    private bool                           _fragmentsCompressed;
    private Opcode                         _fragmentsOpcode;
    private const string                   _guid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    private Func<WebSocketContext, string> _handshakeRequestChecker;
    private bool                           _ignoreExtensions;
    private bool                           _inContinuation;
    private volatile bool                  _inMessage;
    private volatile Logger                _logger;
    private Action<MessageEventArgs>       _message;
    private Queue<MessageEventArgs>        _messageEventQueue;
    private uint                           _nonceCount;
    private string                         _origin;
    private bool                           _preAuth;
    private string                         _protocol;
    private string[]                       _protocols;
    private bool                           _protocolsRequested;
    private NetworkCredential              _proxyCredentials;
    private Uri                            _proxyUri;
    private volatile WebSocketState        _readyState;
    private AutoResetEvent                 _receivePong;
    private bool                           _secure;
    private ClientSslConfiguration         _sslConfig;
    private Stream                         _stream;
    private TcpClient                      _tcpClient;
    private Uri                            _uri;
    private const string                   _version = "13";
    private TimeSpan                       _waitTime;

    #endregion

    #region Internal Fields

    /// <summary>
    /// Represents the empty array of <see cref="byte"/> used internally.
    /// </summary>
    internal static readonly byte[] EmptyBytes;

    /// <summary>
    /// Represents the length used to determine whether the data should be fragmented in sending.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   The data will be fragmented if that length is greater than the value of this field.
    ///   </para>
    ///   <para>
    ///   If you would like to change the value, you must set it to a value between <c>125</c> and
    ///   <c>Int32.MaxValue - 14</c> inclusive.
    ///   </para>
    /// </remarks>
    internal static readonly int FragmentLength;

    /// <summary>
    /// Represents the random number generator used internally.
    /// </summary>
    internal static readonly RandomNumberGenerator RandomNumber;

    #endregion

    #region Static Constructor

    static WebSocket ()
    {
      EmptyBytes = new byte[0];
      FragmentLength = 1016;
      RandomNumber = new RNGCryptoServiceProvider ();
    }

    #endregion

    #region Internal Constructors

    // As server
    internal WebSocket (HttpListenerWebSocketContext context, string protocol)
    {
      _context = context;
      _protocol = protocol;

      _closeContext = context.Close;
      _logger = context.Log;
      _message = messages;
      _secure = context.IsSecureConnection;
      _stream = context.Stream;
      _waitTime = TimeSpan.FromSeconds (1);

      init ();
    }

    // As server
    internal WebSocket (TcpListenerWebSocketContext context, string protocol)
    {
      _context = context;
      _protocol = protocol;

      _closeContext = context.Close;
      _logger = context.Log;
      _message = messages;
      _secure = context.IsSecureConnection;
      _stream = context.Stream;
      _waitTime = TimeSpan.FromSeconds (1);

      init ();
    }

    #endregion

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocket"/> class with
    /// the specified WebSocket URL and subprotocols.
    /// </summary>
    /// <param name="url">
    /// A <see cref="string"/> that represents the WebSocket URL to connect.
    /// </param>
    /// <param name="protocols">
    /// An array of <see cref="string"/> that contains the WebSocket subprotocols if any.
    /// Each value of <paramref name="protocols"/> must be a token defined in
    /// <see href="http://tools.ietf.org/html/rfc2616#section-2.2">RFC 2616</see>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="url"/> is <see langword="null"/>.
    /// </exception>
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
    public WebSocket (string url, params string[] protocols)
    {
      if (url == null)
        throw new ArgumentNullException ("url");

      if (url.Length == 0)
        throw new ArgumentException ("An empty string.", "url");

      string msg;
      if (!url.TryCreateWebSocketUri (out _uri, out msg))
        throw new ArgumentException (msg, "url");

      if (protocols != null && protocols.Length > 0) {
        msg = protocols.CheckIfValidProtocols ();
        if (msg != null)
          throw new ArgumentException (msg, "protocols");

        _protocols = protocols;
      }

      _base64Key = CreateBase64Key ();
      _client = true;
      _logger = new Logger ();
      _message = messagec;
      _secure = _uri.Scheme == "wss";
      _waitTime = TimeSpan.FromSeconds (5);

      init ();
    }

    #endregion

    #region Internal Properties

    internal CookieCollection CookieCollection {
      get {
        return _cookies;
      }
    }

    // As server
    internal Func<WebSocketContext, string> CustomHandshakeRequestChecker {
      get {
        return _handshakeRequestChecker;
      }

      set {
        _handshakeRequestChecker = value;
      }
    }

    internal bool HasMessage {
      get {
        lock (_forMessageEventQueue)
          return _messageEventQueue.Count > 0;
      }
    }

    // As server
    internal bool IgnoreExtensions {
      get {
        return _ignoreExtensions;
      }

      set {
        _ignoreExtensions = value;
      }
    }

    internal bool IsConnected {
      get {
        return _readyState == WebSocketState.Open || _readyState == WebSocketState.Closing;
      }
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets or sets the compression method used to compress a message on the WebSocket connection.
    /// </summary>
    /// <value>
    /// One of the <see cref="CompressionMethod"/> enum values, specifies the compression method
    /// used to compress a message. The default value is <see cref="CompressionMethod.None"/>.
    /// </value>
    public CompressionMethod Compression {
      get {
        return _compression;
      }

      set {
        lock (_forConn) {
          var msg = checkIfAvailable (true, false, true, false, false, true);
          if (msg != null) {
            _logger.Error (msg);
            error ("An error has occurred in setting the compression.", null);

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
    /// An <see cref="T:System.Collections.Generic.IEnumerable{WebSocketSharp.Net.Cookie}"/>
    /// instance that provides an enumerator which supports the iteration over the collection of
    /// the cookies.
    /// </value>
    public IEnumerable<Cookie> Cookies {
      get {
        lock (_cookies.SyncRoot)
          foreach (Cookie cookie in _cookies)
            yield return cookie;
      }
    }

    /// <summary>
    /// Gets the credentials for the HTTP authentication (Basic/Digest).
    /// </summary>
    /// <value>
    /// A <see cref="NetworkCredential"/> that represents the credentials for
    /// the authentication. The default value is <see langword="null"/>.
    /// </value>
    public NetworkCredential Credentials {
      get {
        return _credentials;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the <see cref="WebSocket"/> emits
    /// a <see cref="OnMessage"/> event when receives a ping.
    /// </summary>
    /// <value>
    /// <c>true</c> if the <see cref="WebSocket"/> emits a <see cref="OnMessage"/> event
    /// when receives a ping; otherwise, <c>false</c>. The default value is <c>false</c>.
    /// </value>
    public bool EmitOnPing {
      get {
        return _emitOnPing;
      }

      set {
        _emitOnPing = value;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the <see cref="WebSocket"/> redirects
    /// the connection request to the new URL located in the connection response.
    /// </summary>
    /// <value>
    /// <c>true</c> if the <see cref="WebSocket"/> redirects the connection request to
    /// the new URL; otherwise, <c>false</c>. The default value is <c>false</c>.
    /// </value>
    public bool EnableRedirection {
      get {
        return _enableRedirection;
      }

      set {
        lock (_forConn) {
          var msg = checkIfAvailable (true, false, true, false, false, true);
          if (msg != null) {
            _logger.Error (msg);
            error ("An error has occurred in setting the enable redirection.", null);

            return;
          }

          _enableRedirection = value;
        }
      }
    }

    /// <summary>
    /// Gets the WebSocket extensions selected by the server.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the extensions if any.
    /// The default value is <see cref="String.Empty"/>.
    /// </value>
    public string Extensions {
      get {
        return _extensions ?? String.Empty;
      }
    }

    /// <summary>
    /// Gets a value indicating whether the WebSocket connection is alive.
    /// </summary>
    /// <value>
    /// <c>true</c> if the connection is alive; otherwise, <c>false</c>.
    /// </value>
    public bool IsAlive {
      get {
        return Ping ();
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
    /// The default logging level is <see cref="LogLevel.Error"/>. If you would like to change it,
    /// you should set this <c>Log.Level</c> property to any of the <see cref="LogLevel"/> enum
    /// values.
    /// </remarks>
    /// <value>
    /// A <see cref="Logger"/> that provides the logging functions.
    /// </value>
    public Logger Log {
      get {
        return _logger;
      }

      internal set {
        _logger = value;
      }
    }

    /// <summary>
    /// Gets or sets the value of the HTTP Origin header to send with
    /// the WebSocket connection request to the server.
    /// </summary>
    /// <remarks>
    /// The <see cref="WebSocket"/> sends the Origin header if this property has any.
    /// </remarks>
    /// <value>
    ///   <para>
    ///   A <see cref="string"/> that represents the value of
    ///   the <see href="http://tools.ietf.org/html/rfc6454#section-7">Origin</see> header to send.
    ///   The default value is <see langword="null"/>.
    ///   </para>
    ///   <para>
    ///   The Origin header has the following syntax:
    ///   <c>&lt;scheme&gt;://&lt;host&gt;[:&lt;port&gt;]</c>
    ///   </para>
    /// </value>
    public string Origin {
      get {
        return _origin;
      }

      set {
        lock (_forConn) {
          var msg = checkIfAvailable (true, false, true, false, false, true);
          if (msg == null) {
            if (value.IsNullOrEmpty ()) {
              _origin = value;
              return;
            }

            Uri origin;
            if (!Uri.TryCreate (value, UriKind.Absolute, out origin) || origin.Segments.Length > 1)
              msg = "The syntax of an origin must be '<scheme>://<host>[:<port>]'.";
          }

          if (msg != null) {
            _logger.Error (msg);
            error ("An error has occurred in setting the origin.", null);

            return;
          }

          _origin = value.TrimEnd ('/');
        }
      }
    }

    /// <summary>
    /// Gets the WebSocket subprotocol selected by the server.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the subprotocol if any.
    /// The default value is <see cref="String.Empty"/>.
    /// </value>
    public string Protocol {
      get {
        return _protocol ?? String.Empty;
      }

      internal set {
        _protocol = value;
      }
    }

    /// <summary>
    /// Gets the state of the WebSocket connection.
    /// </summary>
    /// <value>
    /// One of the <see cref="WebSocketState"/> enum values, indicates the state of the connection.
    /// The default value is <see cref="WebSocketState.Connecting"/>.
    /// </value>
    public WebSocketState ReadyState {
      get {
        return _readyState;
      }
    }

    /// <summary>
    /// Gets or sets the SSL configuration used to authenticate the server and
    /// optionally the client for secure connection.
    /// </summary>
    /// <value>
    /// A <see cref="ClientSslConfiguration"/> that represents the configuration used
    /// to authenticate the server and optionally the client for secure connection,
    /// or <see langword="null"/> if the <see cref="WebSocket"/> is used in a server.
    /// </value>
    public ClientSslConfiguration SslConfiguration {
      get {
        return _client
               ? (_sslConfig ?? (_sslConfig = new ClientSslConfiguration (_uri.DnsSafeHost)))
               : null;
      }

      set {
        lock (_forConn) {
          var msg = checkIfAvailable (true, false, true, false, false, true);
          if (msg != null) {
            _logger.Error (msg);
            error ("An error has occurred in setting the ssl configuration.", null);

            return;
          }

          _sslConfig = value;
        }
      }
    }

    /// <summary>
    /// Gets the WebSocket URL used to connect, or accepted.
    /// </summary>
    /// <value>
    /// A <see cref="Uri"/> that represents the URL used to connect, or accepted.
    /// </value>
    public Uri Url {
      get {
        return _client ? _uri : _context.RequestUri;
      }
    }

    /// <summary>
    /// Gets or sets the wait time for the response to the Ping or Close.
    /// </summary>
    /// <value>
    /// A <see cref="TimeSpan"/> that represents the wait time. The default value is the same as
    /// 5 seconds, or 1 second if the <see cref="WebSocket"/> is used in a server.
    /// </value>
    public TimeSpan WaitTime {
      get {
        return _waitTime;
      }

      set {
        lock (_forConn) {
          var msg = checkIfAvailable (true, true, true, false, false, true) ??
                    value.CheckIfValidWaitTime ();

          if (msg != null) {
            _logger.Error (msg);
            error ("An error has occurred in setting the wait time.", null);

            return;
          }

          _waitTime = value;
        }
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
    private bool accept ()
    {
      lock (_forConn) {
        var msg = _readyState.CheckIfAvailable (true, false, false, false);
        if (msg != null) {
          _logger.Error (msg);
          error ("An error has occurred in accepting.", null);

          return false;
        }

        try {
          if (acceptHandshake ()) {
            _readyState = WebSocketState.Open;
            return true;
          }
        }
        catch (Exception ex) {
          _logger.Fatal (ex.ToString ());
          fatal ("An exception has occurred while accepting.", ex);
        }

        return false;
      }
    }

    // As server
    private bool acceptHandshake ()
    {
      _logger.Debug (String.Format ("A request from {0}:\n{1}", _context.UserEndPoint, _context));

      string msg;
      if (!checkHandshakeRequest (_context, out msg)) {
        sendHttpResponse (createHandshakeFailureResponse (HttpStatusCode.BadRequest));

        _logger.Fatal (msg);
        fatal ("An error has occurred while accepting.", CloseStatusCode.ProtocolError);

        return false;
      }

      if (!customCheckHandshakeRequest (_context, out msg)) {
        sendHttpResponse (createHandshakeFailureResponse (HttpStatusCode.BadRequest));

        _logger.Fatal (msg);
        fatal ("An error has occurred while accepting.", CloseStatusCode.PolicyViolation);

        return false;
      }

      _base64Key = _context.Headers["Sec-WebSocket-Key"];

      if (_protocol != null)
        processSecWebSocketProtocolHeader (_context.SecWebSocketProtocols);

      if (!_ignoreExtensions)
        processSecWebSocketExtensionsClientHeader (_context.Headers["Sec-WebSocket-Extensions"]);

      return sendHttpResponse (createHandshakeResponse ());
    }

    // As server
    private bool checkHandshakeRequest (WebSocketContext context, out string message)
    {
      message = null;

      if (context.RequestUri == null) {
        message = "Specifies an invalid Request-URI.";
        return false;
      }

      if (!context.IsWebSocketRequest) {
        message = "Not a WebSocket handshake request.";
        return false;
      }

      var headers = context.Headers;
      if (!validateSecWebSocketKeyHeader (headers["Sec-WebSocket-Key"])) {
        message = "Includes no Sec-WebSocket-Key header, or it has an invalid value.";
        return false;
      }

      if (!validateSecWebSocketVersionClientHeader (headers["Sec-WebSocket-Version"])) {
        message = "Includes no Sec-WebSocket-Version header, or it has an invalid value.";
        return false;
      }

      if (!validateSecWebSocketProtocolClientHeader (headers["Sec-WebSocket-Protocol"])) {
        message = "Includes an invalid Sec-WebSocket-Protocol header.";
        return false;
      }

      if (!_ignoreExtensions
          && !validateSecWebSocketExtensionsClientHeader (headers["Sec-WebSocket-Extensions"])
      ) {
        message = "Includes an invalid Sec-WebSocket-Extensions header.";
        return false;
      }

      return true;
    }

    // As client
    private bool checkHandshakeResponse (HttpResponse response, out string message)
    {
      message = null;

      if (response.IsRedirect) {
        message = "Indicates the redirection.";
        return false;
      }

      if (response.IsUnauthorized) {
        message = "Requires the authentication.";
        return false;
      }

      if (!response.IsWebSocketResponse) {
        message = "Not a WebSocket handshake response.";
        return false;
      }

      var headers = response.Headers;
      if (!validateSecWebSocketAcceptHeader (headers["Sec-WebSocket-Accept"])) {
        message = "Includes no Sec-WebSocket-Accept header, or it has an invalid value.";
        return false;
      }

      if (!validateSecWebSocketProtocolServerHeader (headers["Sec-WebSocket-Protocol"])) {
        message = "Includes no Sec-WebSocket-Protocol header, or it has an invalid value.";
        return false;
      }

      if (!validateSecWebSocketExtensionsServerHeader (headers["Sec-WebSocket-Extensions"])) {
        message = "Includes an invalid Sec-WebSocket-Extensions header.";
        return false;
      }

      if (!validateSecWebSocketVersionServerHeader (headers["Sec-WebSocket-Version"])) {
        message = "Includes an invalid Sec-WebSocket-Version header.";
        return false;
      }

      return true;
    }

    private string checkIfAvailable (
      bool client, bool server, bool connecting, bool open, bool closing, bool closed)
    {
      return !client && _client
             ? "This operation isn't available in: client"
             : !server && !_client
               ? "This operation isn't available in: server"
               : _readyState.CheckIfAvailable (connecting, open, closing, closed);
    }

    private bool checkIfAvailable (
      bool connecting, bool open, bool closing, bool closed, out string message
    )
    {
      message = null;

      if (!connecting && _readyState == WebSocketState.Connecting) {
        message = "This operation isn't available in: connecting";
        return false;
      }

      if (!open && _readyState == WebSocketState.Open) {
        message = "This operation isn't available in: open";
        return false;
      }

      if (!closing && _readyState == WebSocketState.Closing) {
        message = "This operation isn't available in: closing";
        return false;
      }

      if (!closed && _readyState == WebSocketState.Closed) {
        message = "This operation isn't available in: closed";
        return false;
      }

      return true;
    }

    private bool checkIfAvailable (
      bool client,
      bool server,
      bool connecting,
      bool open,
      bool closing,
      bool closed,
      out string message
    )
    {
      message = null;

      if (!client && _client) {
        message = "This operation isn't available in: client";
        return false;
      }

      if (!server && !_client) {
        message = "This operation isn't available in: server";
        return false;
      }

      return checkIfAvailable (connecting, open, closing, closed, out message);
    }

    private bool checkReceivedFrame (WebSocketFrame frame, out string message)
    {
      message = null;

      var masked = frame.IsMasked;
      if (_client && masked) {
        message = "A frame from the server is masked.";
        return false;
      }

      if (!_client && !masked) {
        message = "A frame from a client isn't masked.";
        return false;
      }

      if (_inContinuation && frame.IsData) {
        message = "A data frame has been received while receiving continuation frames.";
        return false;
      }

      if (frame.IsCompressed && _compression == CompressionMethod.None) {
        message = "A compressed frame has been received without any agreement for it.";
        return false;
      }

      if (frame.Rsv2 == Rsv.On) {
        message = "The RSV2 of a frame is non-zero without any negotiation for it.";
        return false;
      }

      if (frame.Rsv3 == Rsv.On) {
        message = "The RSV3 of a frame is non-zero without any negotiation for it.";
        return false;
      }

      return true;
    }

    private void close (CloseEventArgs e, bool send, bool receive, bool received)
    {
      lock (_forConn) {
        if (_readyState == WebSocketState.Closing) {
          _logger.Info ("The closing is already in progress.");
          return;
        }

        if (_readyState == WebSocketState.Closed) {
          _logger.Info ("The connection has been closed.");
          return;
        }

        send = send && _readyState == WebSocketState.Open;
        receive = receive && send;

        _readyState = WebSocketState.Closing;
      }

      _logger.Trace ("Begin closing the connection.");

      var bytes = send ? WebSocketFrame.CreateCloseFrame (e.PayloadData, _client).ToArray () : null;
      e.WasClean = closeHandshake (bytes, receive, received);
      releaseResources ();

      _logger.Trace ("End closing the connection.");

      _readyState = WebSocketState.Closed;
      try {
        OnClose.Emit (this, e);
      }
      catch (Exception ex) {
        _logger.Error (ex.ToString ());
        error ("An exception has occurred during the OnClose event.", ex);
      }
    }

    private void closeAsync (CloseEventArgs e, bool send, bool receive, bool received)
    {
      Action<CloseEventArgs, bool, bool, bool> closer = close;
      closer.BeginInvoke (e, send, receive, received, ar => closer.EndInvoke (ar), null);
    }

    private bool closeHandshake (byte[] frameAsBytes, bool receive, bool received)
    {
      var sent = frameAsBytes != null && sendBytes (frameAsBytes);
      received = received ||
                 (receive && sent && _exitReceiving != null && _exitReceiving.WaitOne (_waitTime));

      var ret = sent && received;
      _logger.Debug (
        String.Format ("Was clean?: {0}\n  sent: {1}\n  received: {2}", ret, sent, received));

      return ret;
    }

    // As client
    private bool connect ()
    {
      lock (_forConn) {
        string msg;
        if (!checkIfAvailable (true, false, false, true, out msg)) {
          _logger.Error (msg);
          error ("An error has occurred in connecting.", null);

          return false;
        }

        try {
          _readyState = WebSocketState.Connecting;
          if (!doHandshake ())
            return false;

          _readyState = WebSocketState.Open;
          return true;
        }
        catch (Exception ex) {
          _logger.Fatal (ex.ToString ());
          fatal ("An exception has occurred while connecting.", ex);

          return false;
        }
      }
    }

    // As client
    private string createExtensions ()
    {
      var buff = new StringBuilder (80);

      if (_compression != CompressionMethod.None) {
        var str = _compression.ToExtensionString (
          "server_no_context_takeover", "client_no_context_takeover");

        buff.AppendFormat ("{0}, ", str);
      }

      var len = buff.Length;
      if (len > 2) {
        buff.Length = len - 2;
        return buff.ToString ();
      }

      return null;
    }

    // As server
    private HttpResponse createHandshakeFailureResponse (HttpStatusCode code)
    {
      var ret = HttpResponse.CreateCloseResponse (code);
      ret.Headers["Sec-WebSocket-Version"] = _version;

      return ret;
    }

    // As client
    private HttpRequest createHandshakeRequest ()
    {
      var ret = HttpRequest.CreateWebSocketRequest (_uri);

      var headers = ret.Headers;
      if (!_origin.IsNullOrEmpty ())
        headers["Origin"] = _origin;

      headers["Sec-WebSocket-Key"] = _base64Key;

      _protocolsRequested = _protocols != null;
      if (_protocolsRequested)
        headers["Sec-WebSocket-Protocol"] = _protocols.ToString (", ");

      _extensionsRequested = _compression != CompressionMethod.None;
      if (_extensionsRequested)
        headers["Sec-WebSocket-Extensions"] = createExtensions ();

      headers["Sec-WebSocket-Version"] = _version;

      AuthenticationResponse authRes = null;
      if (_authChallenge != null && _credentials != null) {
        authRes = new AuthenticationResponse (_authChallenge, _credentials, _nonceCount);
        _nonceCount = authRes.NonceCount;
      }
      else if (_preAuth) {
        authRes = new AuthenticationResponse (_credentials);
      }

      if (authRes != null)
        headers["Authorization"] = authRes.ToString ();

      if (_cookies.Count > 0)
        ret.SetCookies (_cookies);

      return ret;
    }

    // As server
    private HttpResponse createHandshakeResponse ()
    {
      var ret = HttpResponse.CreateWebSocketResponse ();

      var headers = ret.Headers;
      headers["Sec-WebSocket-Accept"] = CreateResponseKey (_base64Key);

      if (_protocol != null)
        headers["Sec-WebSocket-Protocol"] = _protocol;

      if (_extensions != null)
        headers["Sec-WebSocket-Extensions"] = _extensions;

      if (_cookies.Count > 0)
        ret.SetCookies (_cookies);

      return ret;
    }

    // As server
    private bool customCheckHandshakeRequest (WebSocketContext context, out string message)
    {
      message = null;
      return _handshakeRequestChecker == null
             || (message = _handshakeRequestChecker (context)) == null;
    }

    private MessageEventArgs dequeueFromMessageEventQueue ()
    {
      lock (_forMessageEventQueue)
        return _messageEventQueue.Count > 0 ? _messageEventQueue.Dequeue () : null;
    }

    // As client
    private bool doHandshake ()
    {
      setClientStream ();
      var res = sendHandshakeRequest ();

      string msg;
      if (!checkHandshakeResponse (res, out msg)) {
        _logger.Fatal (msg);
        fatal ("An error has occurred while connecting.", CloseStatusCode.ProtocolError);

        return false;
      }

      if (_protocolsRequested)
        _protocol = res.Headers["Sec-WebSocket-Protocol"];

      if (_extensionsRequested)
        processSecWebSocketExtensionsServerHeader (res.Headers["Sec-WebSocket-Extensions"]);

      processCookies (res.Cookies);

      return true;
    }

    private void enqueueToMessageEventQueue (MessageEventArgs e)
    {
      lock (_forMessageEventQueue)
        _messageEventQueue.Enqueue (e);
    }

    private void error (string message, Exception exception)
    {
      try {
        OnError.Emit (this, new ErrorEventArgs (message, exception));
      }
      catch (Exception ex) {
        _logger.Error (ex.ToString ());
      }
    }

    private void fatal (string message, Exception exception)
    {
      var code = exception is WebSocketException
                 ? ((WebSocketException) exception).Code
                 : CloseStatusCode.Abnormal;

      fatal (message, code);
    }

    private void fatal (string message, CloseStatusCode code)
    {
      close (new CloseEventArgs (code, message), !code.IsReserved (), false, false);
    }

    private void init ()
    {
      _compression = CompressionMethod.None;
      _cookies = new CookieCollection ();
      _forConn = new object ();
      _forSend = new object ();
      _messageEventQueue = new Queue<MessageEventArgs> ();
      _forMessageEventQueue = ((ICollection) _messageEventQueue).SyncRoot;
      _readyState = WebSocketState.Connecting;
    }

    private void message ()
    {
      MessageEventArgs e = null;
      lock (_forMessageEventQueue) {
        if (_inMessage || _messageEventQueue.Count == 0 || _readyState != WebSocketState.Open)
          return;

        _inMessage = true;
        e = _messageEventQueue.Dequeue ();
      }

      _message (e);
    }

    private void messagec (MessageEventArgs e)
    {
      do {
        try {
          OnMessage.Emit (this, e);
        }
        catch (Exception ex) {
          _logger.Error (ex.ToString ());
          error ("An exception has occurred during an OnMessage event.", ex);
        }

        lock (_forMessageEventQueue) {
          if (_messageEventQueue.Count == 0 || _readyState != WebSocketState.Open) {
            _inMessage = false;
            break;
          }

          e = _messageEventQueue.Dequeue ();
        }
      }
      while (true);
    }

    private void messages (MessageEventArgs e)
    {
      try {
        OnMessage.Emit (this, e);
      }
      catch (Exception ex) {
        _logger.Error (ex.ToString ());
        error ("An exception has occurred during an OnMessage event.", ex);
      }

      lock (_forMessageEventQueue) {
        if (_messageEventQueue.Count == 0 || _readyState != WebSocketState.Open) {
          _inMessage = false;
          return;
        }

        e = _messageEventQueue.Dequeue ();
      }

      ThreadPool.QueueUserWorkItem (state => messages (e));
    }

    private void open ()
    {
      _inMessage = true;
      startReceiving ();
      try {
        OnOpen.Emit (this, EventArgs.Empty);
      }
      catch (Exception ex) {
        _logger.Error (ex.ToString ());
        error ("An exception has occurred during the OnOpen event.", ex);
      }

      MessageEventArgs e = null;
      lock (_forMessageEventQueue) {
        if (_messageEventQueue.Count == 0 || _readyState != WebSocketState.Open) {
          _inMessage = false;
          return;
        }

        e = _messageEventQueue.Dequeue ();
      }

      _message.BeginInvoke (e, ar => _message.EndInvoke (ar), null);
    }

    private bool processCloseFrame (WebSocketFrame frame)
    {
      var payload = frame.PayloadData;
      close (new CloseEventArgs (payload), !payload.IncludesReservedCloseStatusCode, false, true);

      return false;
    }

    // As client
    private void processCookies (CookieCollection cookies)
    {
      if (cookies.Count == 0)
        return;

      _cookies.SetOrRemove (cookies);
    }

    private bool processDataFrame (WebSocketFrame frame)
    {
      enqueueToMessageEventQueue (
        frame.IsCompressed
        ? new MessageEventArgs (
            frame.Opcode, frame.PayloadData.ApplicationData.Decompress (_compression))
        : new MessageEventArgs (frame));

      return true;
    }

    private bool processFragmentFrame (WebSocketFrame frame)
    {
      if (!_inContinuation) {
        // Must process first fragment.
        if (frame.IsContinuation)
          return true;

        _fragmentsOpcode = frame.Opcode;
        _fragmentsCompressed = frame.IsCompressed;
        _fragmentsBuffer = new MemoryStream ();
        _inContinuation = true;
      }

      _fragmentsBuffer.WriteBytes (frame.PayloadData.ApplicationData, 1024);
      if (frame.IsFinal) {
        using (_fragmentsBuffer) {
          var data = _fragmentsCompressed
                     ? _fragmentsBuffer.DecompressToArray (_compression)
                     : _fragmentsBuffer.ToArray ();

          enqueueToMessageEventQueue (new MessageEventArgs (_fragmentsOpcode, data));
        }

        _fragmentsBuffer = null;
        _inContinuation = false;
      }

      return true;
    }

    private bool processPingFrame (WebSocketFrame frame)
    {
      if (send (new WebSocketFrame (Opcode.Pong, frame.PayloadData, _client).ToArray ()))
        _logger.Trace ("Returned a pong.");

      if (_emitOnPing)
        enqueueToMessageEventQueue (new MessageEventArgs (frame));

      return true;
    }

    private bool processPongFrame (WebSocketFrame frame)
    {
      _receivePong.Set ();
      _logger.Trace ("Received a pong.");

      return true;
    }

    private bool processReceivedFrame (WebSocketFrame frame)
    {
      string msg;
      if (!checkReceivedFrame (frame, out msg))
        throw new WebSocketException (CloseStatusCode.ProtocolError, msg);

      frame.Unmask ();
      return frame.IsFragment
             ? processFragmentFrame (frame)
             : frame.IsData
               ? processDataFrame (frame)
               : frame.IsPing
                 ? processPingFrame (frame)
                 : frame.IsPong
                   ? processPongFrame (frame)
                   : frame.IsClose
                     ? processCloseFrame (frame)
                     : processUnsupportedFrame (frame);
    }

    // As server
    private void processSecWebSocketExtensionsClientHeader (string value)
    {
      if (value == null)
        return;

      var buff = new StringBuilder (80);

      var comp = false;
      foreach (var e in value.SplitHeaderValue (',')) {
        var ext = e.Trim ();
        if (!comp && ext.IsCompressionExtension (CompressionMethod.Deflate)) {
          _compression = CompressionMethod.Deflate;
          buff.AppendFormat (
            "{0}, ",
            _compression.ToExtensionString (
              "client_no_context_takeover", "server_no_context_takeover"
            )
          );

          comp = true;
        }
      }

      var len = buff.Length;
      if (len > 2) {
        buff.Length = len - 2;
        _extensions = buff.ToString ();
      }
    }

    // As client
    private void processSecWebSocketExtensionsServerHeader (string value)
    {
      if (value == null) {
        _compression = CompressionMethod.None;
        return;
      }

      _extensions = value;
    }

    // As server
    private void processSecWebSocketProtocolHeader (IEnumerable<string> values)
    {
      if (values.Contains (p => p == _protocol))
        return;

      _protocol = null;
    }

    private bool processUnsupportedFrame (WebSocketFrame frame)
    {
      _logger.Fatal ("An unsupported frame:" + frame.PrintToString (false));
      fatal ("There is no way to handle it.", CloseStatusCode.PolicyViolation);

      return false;
    }

    // As client
    private void releaseClientResources ()
    {
      if (_stream != null) {
        _stream.Dispose ();
        _stream = null;
      }

      if (_tcpClient != null) {
        _tcpClient.Close ();
        _tcpClient = null;
      }
    }

    private void releaseCommonResources ()
    {
      if (_fragmentsBuffer != null) {
        _fragmentsBuffer.Dispose ();
        _fragmentsBuffer = null;
        _inContinuation = false;
      }

      if (_receivePong != null) {
        _receivePong.Close ();
        _receivePong = null;
      }

      if (_exitReceiving != null) {
        _exitReceiving.Close ();
        _exitReceiving = null;
      }
    }

    private void releaseResources ()
    {
      if (_client)
        releaseClientResources ();
      else
        releaseServerResources ();

      releaseCommonResources ();
    }

    // As server
    private void releaseServerResources ()
    {
      if (_closeContext == null)
        return;

      _closeContext ();
      _closeContext = null;
      _stream = null;
      _context = null;
    }

    private bool send (byte[] frameAsBytes)
    {
      lock (_forConn) {
        if (_readyState != WebSocketState.Open) {
          _logger.Error ("The sending has been interrupted.");
          return false;
        }

        return sendBytes (frameAsBytes);
      }
    }

    private bool send (Opcode opcode, Stream stream)
    {
      lock (_forSend) {
        var src = stream;
        var compressed = false;
        var sent = false;
        try {
          if (_compression != CompressionMethod.None) {
            stream = stream.Compress (_compression);
            compressed = true;
          }

          sent = send (opcode, stream, compressed);
          if (!sent)
            error ("The sending has been interrupted.", null);
        }
        catch (Exception ex) {
          _logger.Error (ex.ToString ());
          error ("An exception has occurred while sending data.", ex);
        }
        finally {
          if (compressed)
            stream.Dispose ();

          src.Dispose ();
        }

        return sent;
      }
    }

    private bool send (Opcode opcode, Stream stream, bool compressed)
    {
      var len = stream.Length;

      /* Not fragmented */

      if (len == 0)
        return send (Fin.Final, opcode, EmptyBytes, compressed);

      var quo = len / FragmentLength;
      var rem = (int) (len % FragmentLength);

      byte[] buff = null;
      if (quo == 0) {
        buff = new byte[rem];
        return stream.Read (buff, 0, rem) == rem &&
               send (Fin.Final, opcode, buff, compressed);
      }

      buff = new byte[FragmentLength];
      if (quo == 1 && rem == 0)
        return stream.Read (buff, 0, FragmentLength) == FragmentLength &&
               send (Fin.Final, opcode, buff, compressed);

      /* Send fragmented */

      // Begin
      if (stream.Read (buff, 0, FragmentLength) != FragmentLength ||
          !send (Fin.More, opcode, buff, compressed))
        return false;

      var n = rem == 0 ? quo - 2 : quo - 1;
      for (long i = 0; i < n; i++)
        if (stream.Read (buff, 0, FragmentLength) != FragmentLength ||
            !send (Fin.More, Opcode.Cont, buff, compressed))
          return false;

      // End
      if (rem == 0)
        rem = FragmentLength;
      else
        buff = new byte[rem];

      return stream.Read (buff, 0, rem) == rem && send (Fin.Final, Opcode.Cont, buff, compressed);
    }

    private bool send (Fin fin, Opcode opcode, byte[] data, bool compressed)
    {
      lock (_forConn) {
        if (_readyState != WebSocketState.Open) {
          _logger.Error ("The sending has been interrupted.");
          return false;
        }

        return sendBytes (new WebSocketFrame (fin, opcode, data, compressed, _client).ToArray ());
      }
    }

    private void sendAsync (Opcode opcode, Stream stream, Action<bool> completed)
    {
      Func<Opcode, Stream, bool> sender = send;
      sender.BeginInvoke (
        opcode,
        stream,
        ar => {
          try {
            var sent = sender.EndInvoke (ar);
            if (completed != null)
              completed (sent);
          }
          catch (Exception ex) {
            _logger.Error (ex.ToString ());
            error ("An exception has occurred during a send callback.", ex);
          }
        },
        null);
    }

    private bool sendBytes (byte[] bytes)
    {
      try {
        _stream.Write (bytes, 0, bytes.Length);
        return true;
      }
      catch (Exception ex) {
        _logger.Error (ex.ToString ());
        return false;
      }
    }

    // As client
    private HttpResponse sendHandshakeRequest ()
    {
      var req = createHandshakeRequest ();
      var res = sendHttpRequest (req, 90000);
      if (res.IsUnauthorized) {
        var chal = res.Headers["WWW-Authenticate"];
        _logger.Warn (String.Format ("Received an authentication requirement for '{0}'.", chal));
        if (chal.IsNullOrEmpty ()) {
          _logger.Error ("No authentication challenge is specified.");
          return res;
        }

        _authChallenge = AuthenticationChallenge.Parse (chal);
        if (_authChallenge == null) {
          _logger.Error ("An invalid authentication challenge is specified.");
          return res;
        }

        if (_credentials != null &&
            (!_preAuth || _authChallenge.Scheme == AuthenticationSchemes.Digest)) {
          if (res.HasConnectionClose) {
            releaseClientResources ();
            setClientStream ();
          }

          var authRes = new AuthenticationResponse (_authChallenge, _credentials, _nonceCount);
          _nonceCount = authRes.NonceCount;
          req.Headers["Authorization"] = authRes.ToString ();
          res = sendHttpRequest (req, 15000);
        }
      }

      if (res.IsRedirect) {
        var url = res.Headers["Location"];
        _logger.Warn (String.Format ("Received a redirection to '{0}'.", url));
        if (_enableRedirection) {
          if (url.IsNullOrEmpty ()) {
            _logger.Error ("No url to redirect is located.");
            return res;
          }

          Uri uri;
          string msg;
          if (!url.TryCreateWebSocketUri (out uri, out msg)) {
            _logger.Error ("An invalid url to redirect is located: " + msg);
            return res;
          }

          releaseClientResources ();

          _uri = uri;
          _secure = uri.Scheme == "wss";

          setClientStream ();
          return sendHandshakeRequest ();
        }
      }

      return res;
    }

    // As client
    private HttpResponse sendHttpRequest (HttpRequest request, int millisecondsTimeout)
    {
      _logger.Debug ("A request to the server:\n" + request.ToString ());
      var res = request.GetResponse (_stream, millisecondsTimeout);
      _logger.Debug ("A response to this request:\n" + res.ToString ());

      return res;
    }

    // As server
    private bool sendHttpResponse (HttpResponse response)
    {
      _logger.Debug ("A response to this request:\n" + response.ToString ());
      return sendBytes (response.ToByteArray ());
    }

    // As client
    private void sendProxyConnectRequest ()
    {
      var req = HttpRequest.CreateConnectRequest (_uri);
      var res = sendHttpRequest (req, 90000);
      if (res.IsProxyAuthenticationRequired) {
        var chal = res.Headers["Proxy-Authenticate"];
        _logger.Warn (
          String.Format ("Received a proxy authentication requirement for '{0}'.", chal));

        if (chal.IsNullOrEmpty ())
          throw new WebSocketException ("No proxy authentication challenge is specified.");

        var authChal = AuthenticationChallenge.Parse (chal);
        if (authChal == null)
          throw new WebSocketException ("An invalid proxy authentication challenge is specified.");

        if (_proxyCredentials != null) {
          if (res.HasConnectionClose) {
            releaseClientResources ();
            _tcpClient = new TcpClient (_proxyUri.DnsSafeHost, _proxyUri.Port);
            _stream = _tcpClient.GetStream ();
          }

          var authRes = new AuthenticationResponse (authChal, _proxyCredentials, 0);
          req.Headers["Proxy-Authorization"] = authRes.ToString ();
          res = sendHttpRequest (req, 15000);
        }

        if (res.IsProxyAuthenticationRequired)
          throw new WebSocketException ("A proxy authentication is required.");
      }

      if (res.StatusCode[0] != '2')
        throw new WebSocketException (
          "The proxy has failed a connection to the requested host and port.");
    }

    // As client
    private void setClientStream ()
    {
      if (_proxyUri != null) {
        _tcpClient = new TcpClient (_proxyUri.DnsSafeHost, _proxyUri.Port);
        _stream = _tcpClient.GetStream ();
        sendProxyConnectRequest ();
      }
      else {
        _tcpClient = new TcpClient (_uri.DnsSafeHost, _uri.Port);
        _stream = _tcpClient.GetStream ();
      }

      if (_secure) {
        var conf = SslConfiguration;
        var host = conf.TargetHost;
        if (host != _uri.DnsSafeHost)
          throw new WebSocketException (
            CloseStatusCode.TlsHandshakeFailure, "An invalid host name is specified.");

        try {
          var sslStream = new SslStream (
            _stream,
            false,
            conf.ServerCertificateValidationCallback,
            conf.ClientCertificateSelectionCallback);

          sslStream.AuthenticateAsClient (
            host,
            conf.ClientCertificates,
            conf.EnabledSslProtocols,
            conf.CheckCertificateRevocation);

          _stream = sslStream;
        }
        catch (Exception ex) {
          throw new WebSocketException (CloseStatusCode.TlsHandshakeFailure, ex);
        }
      }
    }

    private void startReceiving ()
    {
      if (_messageEventQueue.Count > 0)
        _messageEventQueue.Clear ();

      _exitReceiving = new AutoResetEvent (false);
      _receivePong = new AutoResetEvent (false);

      Action receive = null;
      receive =
        () =>
          WebSocketFrame.ReadFrameAsync (
            _stream,
            false,
            frame => {
              if (!processReceivedFrame (frame) || _readyState == WebSocketState.Closed) {
                var exit = _exitReceiving;
                if (exit != null)
                  exit.Set ();

                return;
              }

              // Receive next asap because the Ping or Close needs a response to it.
              receive ();

              if (_inMessage || !HasMessage || _readyState != WebSocketState.Open)
                return;

              message ();
            },
            ex => {
              _logger.Fatal (ex.ToString ());
              fatal ("An exception has occurred while receiving.", ex);
            }
          );

      receive ();
    }

    // As client
    private bool validateSecWebSocketAcceptHeader (string value)
    {
      return value != null && value == CreateResponseKey (_base64Key);
    }

    // As server
    private bool validateSecWebSocketExtensionsClientHeader (string value)
    {
      return value == null || value.Length > 0;
    }

    // As client
    private bool validateSecWebSocketExtensionsServerHeader (string value)
    {
      if (value == null)
        return true;

      if (value.Length == 0)
        return false;

      if (!_extensionsRequested)
        return false;

      var comp = _compression != CompressionMethod.None;
      foreach (var e in value.SplitHeaderValue (',')) {
        var ext = e.Trim ();
        if (comp && ext.IsCompressionExtension (_compression)) {
          if (!ext.Contains ("server_no_context_takeover")) {
            _logger.Error ("The server hasn't sent back 'server_no_context_takeover'.");
            return false;
          }

          if (!ext.Contains ("client_no_context_takeover"))
            _logger.Warn ("The server hasn't sent back 'client_no_context_takeover'.");

          var method = _compression.ToExtensionString ();
          var invalid =
            ext.SplitHeaderValue (';').Contains (
              t => {
                t = t.Trim ();
                return t != method
                       && t != "server_no_context_takeover"
                       && t != "client_no_context_takeover";
              }
            );

          if (invalid)
            return false;
        }
        else {
          return false;
        }
      }

      return true;
    }

    // As server
    private bool validateSecWebSocketKeyHeader (string value)
    {
      return value != null && value.Length > 0;
    }

    // As server
    private bool validateSecWebSocketProtocolClientHeader (string value)
    {
      return value == null || value.Length > 0;
    }

    // As client
    private bool validateSecWebSocketProtocolServerHeader (string value)
    {
      if (value == null)
        return !_protocolsRequested;

      if (value.Length == 0)
        return false;

      return _protocolsRequested && _protocols.Contains (p => p == value);
    }

    // As server
    private bool validateSecWebSocketVersionClientHeader (string value)
    {
      return value != null && value == _version;
    }

    // As client
    private bool validateSecWebSocketVersionServerHeader (string value)
    {
      return value == null || value == _version;
    }

    #endregion

    #region Internal Methods

    internal static string CheckCloseParameters (ushort code, string reason, bool client)
    {
      return !code.IsCloseStatusCode ()
             ? "An invalid close status code."
             : code == (ushort) CloseStatusCode.NoStatus
               ? (!reason.IsNullOrEmpty () ? "NoStatus cannot have a reason." : null)
               : code == (ushort) CloseStatusCode.MandatoryExtension && !client
                 ? "MandatoryExtension cannot be used by a server."
                 : code == (ushort) CloseStatusCode.ServerError && client
                   ? "ServerError cannot be used by a client."
                   : !reason.IsNullOrEmpty () && reason.UTF8Encode ().Length > 123
                     ? "A reason has greater than the allowable max size."
                     : null;
    }

    internal static string CheckCloseParameters (CloseStatusCode code, string reason, bool client)
    {
      return code == CloseStatusCode.NoStatus
             ? (!reason.IsNullOrEmpty () ? "NoStatus cannot have a reason." : null)
             : code == CloseStatusCode.MandatoryExtension && !client
               ? "MandatoryExtension cannot be used by a server."
               : code == CloseStatusCode.ServerError && client
                 ? "ServerError cannot be used by a client."
                 : !reason.IsNullOrEmpty () && reason.UTF8Encode ().Length > 123
                   ? "A reason has greater than the allowable max size."
                   : null;
    }

    internal static string CheckPingParameter (string message, out byte[] bytes)
    {
      bytes = message.UTF8Encode ();
      return bytes.Length > 125 ? "A message has greater than the allowable max size." : null;
    }

    internal static string CheckSendParameter (byte[] data)
    {
      return data == null ? "'data' is null." : null;
    }

    internal static string CheckSendParameter (FileInfo file)
    {
      return file == null ? "'file' is null." : null;
    }

    internal static string CheckSendParameter (string data)
    {
      return data == null ? "'data' is null." : null;
    }

    internal static string CheckSendParameters (Stream stream, int length)
    {
      return stream == null
             ? "'stream' is null."
             : !stream.CanRead
               ? "'stream' cannot be read."
               : length < 1
                 ? "'length' is less than 1."
                 : null;
    }

    // As server
    internal void Close (HttpResponse response)
    {
      _readyState = WebSocketState.Closing;

      sendHttpResponse (response);
      releaseServerResources ();

      _readyState = WebSocketState.Closed;
    }

    // As server
    internal void Close (HttpStatusCode code)
    {
      Close (createHandshakeFailureResponse (code));
    }

    // As server
    internal void Close (CloseEventArgs e, byte[] frameAsBytes, bool receive)
    {
      lock (_forConn) {
        if (_readyState == WebSocketState.Closing) {
          _logger.Info ("The closing is already in progress.");
          return;
        }

        if (_readyState == WebSocketState.Closed) {
          _logger.Info ("The connection has been closed.");
          return;
        }

        _readyState = WebSocketState.Closing;
      }

      e.WasClean = closeHandshake (frameAsBytes, receive, false);
      releaseServerResources ();
      releaseCommonResources ();

      _readyState = WebSocketState.Closed;
      try {
        OnClose.Emit (this, e);
      }
      catch (Exception ex) {
        _logger.Error (ex.ToString ());
      }
    }

    // As client
    internal static string CreateBase64Key ()
    {
      var src = new byte[16];
      RandomNumber.GetBytes (src);

      return Convert.ToBase64String (src);
    }

    internal static string CreateResponseKey (string base64Key)
    {
      var buff = new StringBuilder (base64Key, 64);
      buff.Append (_guid);
      SHA1 sha1 = new SHA1CryptoServiceProvider ();
      var src = sha1.ComputeHash (buff.ToString ().UTF8Encode ());

      return Convert.ToBase64String (src);
    }

    // As server
    internal void InternalAccept ()
    {
      try {
        if (acceptHandshake ()) {
          _readyState = WebSocketState.Open;
          open ();
        }
      }
      catch (Exception ex) {
        _logger.Fatal (ex.ToString ());
        fatal ("An exception has occurred while accepting.", ex);
      }
    }

    internal bool Ping (byte[] frameAsBytes, TimeSpan timeout)
    {
      if (_readyState != WebSocketState.Open)
        return false;

      if (!send (frameAsBytes))
        return false;

      var receivePong = _receivePong;
      if (receivePong == null)
        return false;

      return receivePong.WaitOne (timeout);
    }

    // As server, used to broadcast
    internal void Send (Opcode opcode, byte[] data, Dictionary<CompressionMethod, byte[]> cache)
    {
      lock (_forSend) {
        lock (_forConn) {
          if (_readyState != WebSocketState.Open) {
            _logger.Error ("The sending has been interrupted.");
            return;
          }

          try {
            byte[] found;
            if (!cache.TryGetValue (_compression, out found)) {
              found =
                new WebSocketFrame (
                  Fin.Final,
                  opcode,
                  data.Compress (_compression),
                  _compression != CompressionMethod.None,
                  false
                )
                .ToArray ();

              cache.Add (_compression, found);
            }

            sendBytes (found);
          }
          catch (Exception ex) {
            _logger.Error (ex.ToString ());
          }
        }
      }
    }

    // As server, used to broadcast
    internal void Send (Opcode opcode, Stream stream, Dictionary <CompressionMethod, Stream> cache)
    {
      lock (_forSend) {
        try {
          Stream found;
          if (!cache.TryGetValue (_compression, out found)) {
            found = stream.Compress (_compression);
            cache.Add (_compression, found);
          }
          else {
            found.Position = 0;
          }

          send (opcode, found, _compression != CompressionMethod.None);
        }
        catch (Exception ex) {
          _logger.Error (ex.ToString ());
        }
      }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Accepts the WebSocket connection request.
    /// </summary>
    /// <remarks>
    /// This method isn't available in a client.
    /// </remarks>
    public void Accept ()
    {
      var msg = checkIfAvailable (false, true, true, false, false, false);
      if (msg != null) {
        _logger.Error (msg);
        error ("An error has occurred in accepting.", null);

        return;
      }

      if (accept ())
        open ();
    }

    /// <summary>
    /// Accepts the WebSocket connection request asynchronously.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   This method doesn't wait for the accept to be complete.
    ///   </para>
    ///   <para>
    ///   This method isn't available in a client.
    ///   </para>
    /// </remarks>
    public void AcceptAsync ()
    {
      var msg = checkIfAvailable (false, true, true, false, false, false);
      if (msg != null) {
        _logger.Error (msg);
        error ("An error has occurred in accepting.", null);

        return;
      }

      Func<bool> acceptor = accept;
      acceptor.BeginInvoke (
        ar => {
          if (acceptor.EndInvoke (ar))
            open ();
        },
        null);
    }

    /// <summary>
    /// Closes the WebSocket connection, and releases all associated resources.
    /// </summary>
    public void Close ()
    {
      var msg = _readyState.CheckIfAvailable (true, true, false, false);
      if (msg != null) {
        _logger.Error (msg);
        error ("An error has occurred in closing the connection.", null);

        return;
      }

      close (new CloseEventArgs (), true, true, false);
    }

    /// <summary>
    /// Closes the WebSocket connection with the specified <see cref="ushort"/>,
    /// and releases all associated resources.
    /// </summary>
    /// <remarks>
    /// This method emits a <see cref="OnError"/> event if <paramref name="code"/> isn't in
    /// the allowable range of the close status code.
    /// </remarks>
    /// <param name="code">
    /// A <see cref="ushort"/> that represents the status code indicating the reason for the close.
    /// </param>
    public void Close (ushort code)
    {
      var msg = _readyState.CheckIfAvailable (true, true, false, false) ??
                CheckCloseParameters (code, null, _client);

      if (msg != null) {
        _logger.Error (msg);
        error ("An error has occurred in closing the connection.", null);

        return;
      }

      if (code == (ushort) CloseStatusCode.NoStatus) {
        close (new CloseEventArgs (), true, true, false);
        return;
      }

      var send = !code.IsReserved ();
      close (new CloseEventArgs (code), send, send, false);
    }

    /// <summary>
    /// Closes the WebSocket connection with the specified <see cref="CloseStatusCode"/>,
    /// and releases all associated resources.
    /// </summary>
    /// <param name="code">
    /// One of the <see cref="CloseStatusCode"/> enum values, represents the status code indicating
    /// the reason for the close.
    /// </param>
    public void Close (CloseStatusCode code)
    {
      var msg = _readyState.CheckIfAvailable (true, true, false, false) ??
                CheckCloseParameters (code, null, _client);

      if (msg != null) {
        _logger.Error (msg);
        error ("An error has occurred in closing the connection.", null);

        return;
      }

      if (code == CloseStatusCode.NoStatus) {
        close (new CloseEventArgs (), true, true, false);
        return;
      }

      var send = !code.IsReserved ();
      close (new CloseEventArgs (code), send, send, false);
    }

    /// <summary>
    /// Closes the WebSocket connection with the specified <see cref="ushort"/> and
    /// <see cref="string"/>, and releases all associated resources.
    /// </summary>
    /// <remarks>
    /// This method emits a <see cref="OnError"/> event if <paramref name="code"/> isn't in
    /// the allowable range of the close status code or the size of <paramref name="reason"/> is
    /// greater than 123 bytes.
    /// </remarks>
    /// <param name="code">
    /// A <see cref="ushort"/> that represents the status code indicating the reason for the close.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that represents the reason for the close.
    /// </param>
    public void Close (ushort code, string reason)
    {
      var msg = _readyState.CheckIfAvailable (true, true, false, false) ??
                CheckCloseParameters (code, reason, _client);

      if (msg != null) {
        _logger.Error (msg);
        error ("An error has occurred in closing the connection.", null);

        return;
      }

      if (code == (ushort) CloseStatusCode.NoStatus) {
        close (new CloseEventArgs (), true, true, false);
        return;
      }

      var send = !code.IsReserved ();
      close (new CloseEventArgs (code, reason), send, send, false);
    }

    /// <summary>
    /// Closes the WebSocket connection with the specified <see cref="CloseStatusCode"/> and
    /// <see cref="string"/>, and releases all associated resources.
    /// </summary>
    /// <remarks>
    /// This method emits a <see cref="OnError"/> event if the size of <paramref name="reason"/> is
    /// greater than 123 bytes.
    /// </remarks>
    /// <param name="code">
    /// One of the <see cref="CloseStatusCode"/> enum values, represents the status code indicating
    /// the reason for the close.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that represents the reason for the close.
    /// </param>
    public void Close (CloseStatusCode code, string reason)
    {
      var msg = _readyState.CheckIfAvailable (true, true, false, false) ??
                CheckCloseParameters (code, reason, _client);

      if (msg != null) {
        _logger.Error (msg);
        error ("An error has occurred in closing the connection.", null);

        return;
      }

      if (code == CloseStatusCode.NoStatus) {
        close (new CloseEventArgs (), true, true, false);
        return;
      }

      var send = !code.IsReserved ();
      close (new CloseEventArgs (code, reason), send, send, false);
    }

    /// <summary>
    /// Closes the WebSocket connection asynchronously, and releases all associated resources.
    /// </summary>
    /// <remarks>
    /// This method doesn't wait for the close to be complete.
    /// </remarks>
    public void CloseAsync ()
    {
      var msg = _readyState.CheckIfAvailable (true, true, false, false);
      if (msg != null) {
        _logger.Error (msg);
        error ("An error has occurred in closing the connection.", null);

        return;
      }

      closeAsync (new CloseEventArgs (), true, true, false);
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
    ///   This method emits a <see cref="OnError"/> event if <paramref name="code"/> isn't in
    ///   the allowable range of the close status code.
    ///   </para>
    /// </remarks>
    /// <param name="code">
    /// A <see cref="ushort"/> that represents the status code indicating the reason for the close.
    /// </param>
    public void CloseAsync (ushort code)
    {
      var msg = _readyState.CheckIfAvailable (true, true, false, false) ??
                CheckCloseParameters (code, null, _client);

      if (msg != null) {
        _logger.Error (msg);
        error ("An error has occurred in closing the connection.", null);

        return;
      }

      if (code == (ushort) CloseStatusCode.NoStatus) {
        closeAsync (new CloseEventArgs (), true, true, false);
        return;
      }

      var send = !code.IsReserved ();
      closeAsync (new CloseEventArgs (code), send, send, false);
    }

    /// <summary>
    /// Closes the WebSocket connection asynchronously with the specified
    /// <see cref="CloseStatusCode"/>, and releases all associated resources.
    /// </summary>
    /// <remarks>
    /// This method doesn't wait for the close to be complete.
    /// </remarks>
    /// <param name="code">
    /// One of the <see cref="CloseStatusCode"/> enum values, represents the status code indicating
    /// the reason for the close.
    /// </param>
    public void CloseAsync (CloseStatusCode code)
    {
      var msg = _readyState.CheckIfAvailable (true, true, false, false) ??
                CheckCloseParameters (code, null, _client);

      if (msg != null) {
        _logger.Error (msg);
        error ("An error has occurred in closing the connection.", null);

        return;
      }

      if (code == CloseStatusCode.NoStatus) {
        closeAsync (new CloseEventArgs (), true, true, false);
        return;
      }

      var send = !code.IsReserved ();
      closeAsync (new CloseEventArgs (code), send, send, false);
    }

    /// <summary>
    /// Closes the WebSocket connection asynchronously with the specified <see cref="ushort"/> and
    /// <see cref="string"/>, and releases all associated resources.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   This method doesn't wait for the close to be complete.
    ///   </para>
    ///   <para>
    ///   This method emits a <see cref="OnError"/> event if <paramref name="code"/> isn't in
    ///   the allowable range of the close status code or the size of <paramref name="reason"/> is
    ///   greater than 123 bytes.
    ///   </para>
    /// </remarks>
    /// <param name="code">
    /// A <see cref="ushort"/> that represents the status code indicating the reason for the close.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that represents the reason for the close.
    /// </param>
    public void CloseAsync (ushort code, string reason)
    {
      var msg = _readyState.CheckIfAvailable (true, true, false, false) ??
                CheckCloseParameters (code, reason, _client);

      if (msg != null) {
        _logger.Error (msg);
        error ("An error has occurred in closing the connection.", null);

        return;
      }

      if (code == (ushort) CloseStatusCode.NoStatus) {
        closeAsync (new CloseEventArgs (), true, true, false);
        return;
      }

      var send = !code.IsReserved ();
      closeAsync (new CloseEventArgs (code, reason), send, send, false);
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
    ///   This method emits a <see cref="OnError"/> event if the size of
    ///   <paramref name="reason"/> is greater than 123 bytes.
    ///   </para>
    /// </remarks>
    /// <param name="code">
    /// One of the <see cref="CloseStatusCode"/> enum values, represents the status code indicating
    /// the reason for the close.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that represents the reason for the close.
    /// </param>
    public void CloseAsync (CloseStatusCode code, string reason)
    {
      var msg = _readyState.CheckIfAvailable (true, true, false, false) ??
                CheckCloseParameters (code, reason, _client);

      if (msg != null) {
        _logger.Error (msg);
        error ("An error has occurred in closing the connection.", null);

        return;
      }

      if (code == CloseStatusCode.NoStatus) {
        closeAsync (new CloseEventArgs (), true, true, false);
        return;
      }

      var send = !code.IsReserved ();
      closeAsync (new CloseEventArgs (code, reason), send, send, false);
    }

    /// <summary>
    /// Establishes a WebSocket connection.
    /// </summary>
    /// <remarks>
    /// This method isn't available in a server.
    /// </remarks>
    public void Connect ()
    {
      string msg;
      if (!checkIfAvailable (true, false, true, false, false, true, out msg)) {
        _logger.Error (msg);
        error ("An error has occurred in connecting.", null);

        return;
      }

      if (connect ())
        open ();
    }

    /// <summary>
    /// Establishes a WebSocket connection asynchronously.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   This method doesn't wait for the connect to be complete.
    ///   </para>
    ///   <para>
    ///   This method isn't available in a server.
    ///   </para>
    /// </remarks>
    public void ConnectAsync ()
    {
      string msg;
      if (!checkIfAvailable (true, false, true, false, false, true, out msg)) {
        _logger.Error (msg);
        error ("An error has occurred in connecting.", null);

        return;
      }

      Func<bool> connector = connect;
      connector.BeginInvoke (
        ar => {
          if (connector.EndInvoke (ar))
            open ();
        },
        null
      );
    }

    /// <summary>
    /// Sends a ping using the WebSocket connection.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the <see cref="WebSocket"/> receives a pong to this ping in a time;
    /// otherwise, <c>false</c>.
    /// </returns>
    public bool Ping ()
    {
      var bytes = _client
                  ? WebSocketFrame.CreatePingFrame (true).ToArray ()
                  : WebSocketFrame.EmptyPingBytes;

      return Ping (bytes, _waitTime);
    }

    /// <summary>
    /// Sends a ping with the specified <paramref name="message"/> using the WebSocket connection.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the <see cref="WebSocket"/> receives a pong to this ping in a time;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <param name="message">
    /// A <see cref="string"/> that represents the message to send.
    /// </param>
    public bool Ping (string message)
    {
      if (message == null || message.Length == 0)
        return Ping ();

      byte[] data;
      var msg = CheckPingParameter (message, out data);
      if (msg != null) {
        _logger.Error (msg);
        error ("An error has occurred in sending a ping.", null);

        return false;
      }

      return Ping (WebSocketFrame.CreatePingFrame (data, _client).ToArray (), _waitTime);
    }

    /// <summary>
    /// Sends binary <paramref name="data"/> using the WebSocket connection.
    /// </summary>
    /// <param name="data">
    /// An array of <see cref="byte"/> that represents the binary data to send.
    /// </param>
    public void Send (byte[] data)
    {
      var msg = _readyState.CheckIfAvailable (false, true, false, false) ??
                CheckSendParameter (data);

      if (msg != null) {
        _logger.Error (msg);
        error ("An error has occurred in sending data.", null);

        return;
      }

      send (Opcode.Binary, new MemoryStream (data));
    }

    /// <summary>
    /// Sends the specified <paramref name="file"/> as binary data using the WebSocket connection.
    /// </summary>
    /// <param name="file">
    /// A <see cref="FileInfo"/> that represents the file to send.
    /// </param>
    public void Send (FileInfo file)
    {
      var msg = _readyState.CheckIfAvailable (false, true, false, false) ??
                CheckSendParameter (file);

      if (msg != null) {
        _logger.Error (msg);
        error ("An error has occurred in sending data.", null);

        return;
      }

      send (Opcode.Binary, file.OpenRead ());
    }

    /// <summary>
    /// Sends text <paramref name="data"/> using the WebSocket connection.
    /// </summary>
    /// <param name="data">
    /// A <see cref="string"/> that represents the text data to send.
    /// </param>
    public void Send (string data)
    {
      var msg = _readyState.CheckIfAvailable (false, true, false, false) ??
                CheckSendParameter (data);

      if (msg != null) {
        _logger.Error (msg);
        error ("An error has occurred in sending data.", null);

        return;
      }

      send (Opcode.Text, new MemoryStream (data.UTF8Encode ()));
    }

    /// <summary>
    /// Sends binary <paramref name="data"/> asynchronously using the WebSocket connection.
    /// </summary>
    /// <remarks>
    /// This method doesn't wait for the send to be complete.
    /// </remarks>
    /// <param name="data">
    /// An array of <see cref="byte"/> that represents the binary data to send.
    /// </param>
    /// <param name="completed">
    /// An <c>Action&lt;bool&gt;</c> delegate that references the method(s) called when
    /// the send is complete. A <see cref="bool"/> passed to this delegate is <c>true</c>
    /// if the send is complete successfully.
    /// </param>
    public void SendAsync (byte[] data, Action<bool> completed)
    {
      var msg = _readyState.CheckIfAvailable (false, true, false, false) ??
                CheckSendParameter (data);

      if (msg != null) {
        _logger.Error (msg);
        error ("An error has occurred in sending data.", null);

        return;
      }

      sendAsync (Opcode.Binary, new MemoryStream (data), completed);
    }

    /// <summary>
    /// Sends the specified <paramref name="file"/> as binary data asynchronously using
    /// the WebSocket connection.
    /// </summary>
    /// <remarks>
    /// This method doesn't wait for the send to be complete.
    /// </remarks>
    /// <param name="file">
    /// A <see cref="FileInfo"/> that represents the file to send.
    /// </param>
    /// <param name="completed">
    /// An <c>Action&lt;bool&gt;</c> delegate that references the method(s) called when
    /// the send is complete. A <see cref="bool"/> passed to this delegate is <c>true</c>
    /// if the send is complete successfully.
    /// </param>
    public void SendAsync (FileInfo file, Action<bool> completed)
    {
      var msg = _readyState.CheckIfAvailable (false, true, false, false) ??
                CheckSendParameter (file);

      if (msg != null) {
        _logger.Error (msg);
        error ("An error has occurred in sending data.", null);

        return;
      }

      sendAsync (Opcode.Binary, file.OpenRead (), completed);
    }

    /// <summary>
    /// Sends text <paramref name="data"/> asynchronously using the WebSocket connection.
    /// </summary>
    /// <remarks>
    /// This method doesn't wait for the send to be complete.
    /// </remarks>
    /// <param name="data">
    /// A <see cref="string"/> that represents the text data to send.
    /// </param>
    /// <param name="completed">
    /// An <c>Action&lt;bool&gt;</c> delegate that references the method(s) called when
    /// the send is complete. A <see cref="bool"/> passed to this delegate is <c>true</c>
    /// if the send is complete successfully.
    /// </param>
    public void SendAsync (string data, Action<bool> completed)
    {
      var msg = _readyState.CheckIfAvailable (false, true, false, false) ??
                CheckSendParameter (data);

      if (msg != null) {
        _logger.Error (msg);
        error ("An error has occurred in sending data.", null);

        return;
      }

      sendAsync (Opcode.Text, new MemoryStream (data.UTF8Encode ()), completed);
    }

    /// <summary>
    /// Sends binary data from the specified <see cref="Stream"/> asynchronously using
    /// the WebSocket connection.
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
    /// An <c>Action&lt;bool&gt;</c> delegate that references the method(s) called when
    /// the send is complete. A <see cref="bool"/> passed to this delegate is <c>true</c>
    /// if the send is complete successfully.
    /// </param>
    public void SendAsync (Stream stream, int length, Action<bool> completed)
    {
      var msg = _readyState.CheckIfAvailable (false, true, false, false) ??
                CheckSendParameters (stream, length);

      if (msg != null) {
        _logger.Error (msg);
        error ("An error has occurred in sending data.", null);

        return;
      }

      stream.ReadBytesAsync (
        length,
        data => {
          var len = data.Length;
          if (len == 0) {
            _logger.Error ("The data cannot be read from 'stream'.");
            error ("An error has occurred in sending data.", null);

            return;
          }

          if (len < length)
            _logger.Warn (
              String.Format (
                "The length of the data is less than 'length':\n  expected: {0}\n  actual: {1}",
                length,
                len
              )
            );

          var sent = send (Opcode.Binary, new MemoryStream (data));
          if (completed != null)
            completed (sent);
        },
        ex => {
          _logger.Error (ex.ToString ());
          error ("An exception has occurred while sending data.", ex);
        }
      );
    }

    /// <summary>
    /// Sets an HTTP <paramref name="cookie"/> to send with
    /// the WebSocket connection request to the server.
    /// </summary>
    /// <param name="cookie">
    /// A <see cref="Cookie"/> that represents the cookie to send.
    /// </param>
    public void SetCookie (Cookie cookie)
    {
      lock (_forConn) {
        var msg = checkIfAvailable (true, false, true, false, false, true) ??
                  (cookie == null ? "'cookie' is null." : null);

        if (msg != null) {
          _logger.Error (msg);
          error ("An error has occurred in setting a cookie.", null);

          return;
        }

        lock (_cookies.SyncRoot)
          _cookies.SetOrRemove (cookie);
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
    public void SetCredentials (string username, string password, bool preAuth)
    {
      lock (_forConn) {
        var msg = checkIfAvailable (true, false, true, false, false, true);
        if (msg == null) {
          if (username.IsNullOrEmpty ()) {
            _credentials = null;
            _preAuth = false;
            _logger.Warn ("The credentials were set back to the default.");

            return;
          }

          msg = username.Contains (':') || !username.IsText ()
                ? "'username' contains an invalid character."
                : !password.IsNullOrEmpty () && !password.IsText ()
                  ? "'password' contains an invalid character."
                  : null;
        }

        if (msg != null) {
          _logger.Error (msg);
          error ("An error has occurred in setting the credentials.", null);

          return;
        }

        _credentials = new NetworkCredential (username, password, _uri.PathAndQuery);
        _preAuth = preAuth;
      }
    }

    /// <summary>
    /// Sets an HTTP proxy server URL to connect through, and if necessary,
    /// a pair of <paramref name="username"/> and <paramref name="password"/> for
    /// the proxy server authentication (Basic/Digest).
    /// </summary>
    /// <param name="url">
    /// A <see cref="string"/> that represents the proxy server URL to connect through.
    /// </param>
    /// <param name="username">
    /// A <see cref="string"/> that represents the user name used to authenticate.
    /// </param>
    /// <param name="password">
    /// A <see cref="string"/> that represents the password for <paramref name="username"/>
    /// used to authenticate.
    /// </param>
    public void SetProxy (string url, string username, string password)
    {
      lock (_forConn) {
        var msg = checkIfAvailable (true, false, true, false, false, true);
        if (msg == null) {
          if (url.IsNullOrEmpty ()) {
            _proxyUri = null;
            _proxyCredentials = null;
            _logger.Warn ("The proxy url and credentials were set back to the default.");

            return;
          }

          Uri uri;
          if (!Uri.TryCreate (url, UriKind.Absolute, out uri) ||
              uri.Scheme != "http" ||
              uri.Segments.Length > 1) {
            msg = "The syntax of a proxy url must be 'http://<host>[:<port>]'.";
          }
          else {
            _proxyUri = uri;

            if (username.IsNullOrEmpty ()) {
              _proxyCredentials = null;
              _logger.Warn ("The proxy credentials were set back to the default.");

              return;
            }

            msg = username.Contains (':') || !username.IsText ()
                  ? "'username' contains an invalid character."
                  : !password.IsNullOrEmpty () && !password.IsText ()
                    ? "'password' contains an invalid character."
                    : null;
          }
        }

        if (msg != null) {
          _logger.Error (msg);
          error ("An error has occurred in setting the proxy.", null);

          return;
        }

        _proxyCredentials = new NetworkCredential (
          username, password, String.Format ("{0}:{1}", _uri.DnsSafeHost, _uri.Port));
      }
    }

    #endregion

    #region Explicit Interface Implementations

    /// <summary>
    /// Closes the WebSocket connection, and releases all associated resources.
    /// </summary>
    /// <remarks>
    /// This method closes the connection with <see cref="CloseStatusCode.Away"/>.
    /// </remarks>
    void IDisposable.Dispose ()
    {
      close (new CloseEventArgs (CloseStatusCode.Away), true, true, false);
    }

    #endregion
  }
}
