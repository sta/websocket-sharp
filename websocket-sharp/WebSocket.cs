#region License
/*
 * WebSocket.cs
 *
 * This code is derived from WebSocket.java
 * (http://github.com/adamac/Java-WebSocket-client).
 *
 * The MIT License
 *
 * Copyright (c) 2009 Adam MacBeth
 * Copyright (c) 2010-2025 sta.blockhead
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
  ///   <para>
  ///   This class provides a set of methods and properties for two-way
  ///   communication using the WebSocket protocol.
  ///   </para>
  ///   <para>
  ///   The WebSocket protocol is defined in
  ///   <see href="http://tools.ietf.org/html/rfc6455">RFC 6455</see>.
  ///   </para>
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
    private static readonly byte[]         _emptyBytes;
    private bool                           _enableRedirection;
    private string                         _extensions;
    private bool                           _extensionsRequested;
    private object                         _forMessageEventQueue;
    private object                         _forPing;
    private object                         _forSend;
    private object                         _forState;
    private MemoryStream                   _fragmentsBuffer;
    private bool                           _fragmentsCompressed;
    private Opcode                         _fragmentsOpcode;
    private const string                   _guid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    private Func<WebSocketContext, string> _handshakeRequestChecker;
    private bool                           _ignoreExtensions;
    private bool                           _inContinuation;
    private volatile bool                  _inMessage;
    private volatile Logger                _log;
    private static readonly int            _maxRetryCountForConnect;
    private Action<MessageEventArgs>       _message;
    private Queue<MessageEventArgs>        _messageEventQueue;
    private uint                           _nonceCount;
    private string                         _origin;
    private ManualResetEvent               _pongReceived;
    private bool                           _preAuth;
    private string                         _protocol;
    private string[]                       _protocols;
    private bool                           _protocolsRequested;
    private NetworkCredential              _proxyCredentials;
    private Uri                            _proxyUri;
    private volatile WebSocketState        _readyState;
    private ManualResetEvent               _receivingExited;
    private int                            _retryCountForConnect;
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
    /// Represents the length used to determine whether the data should
    /// be fragmented in sending.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   The data will be fragmented if its length is greater than
    ///   the value of this field.
    ///   </para>
    ///   <para>
    ///   If you would like to change the value, you must set it to
    ///   a value between 125 and <c>Int32.MaxValue - 14</c> inclusive.
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
      _emptyBytes = new byte[0];
      _maxRetryCountForConnect = 10;

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
      _log = context.Log;
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
      _log = context.Log;
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
    /// the specified URL and optionally subprotocols.
    /// </summary>
    /// <param name="url">
    ///   <para>
    ///   A <see cref="string"/> that specifies the URL to which to connect.
    ///   </para>
    ///   <para>
    ///   The scheme of the URL must be ws or wss.
    ///   </para>
    ///   <para>
    ///   The new instance uses a secure connection if the scheme is wss.
    ///   </para>
    /// </param>
    /// <param name="protocols">
    ///   <para>
    ///   An array of <see cref="string"/> that specifies the names of
    ///   the subprotocols if necessary.
    ///   </para>
    ///   <para>
    ///   Each value of the array must be a token defined in
    ///   <see href="http://tools.ietf.org/html/rfc2616#section-2.2">
    ///   RFC 2616</see>.
    ///   </para>
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="url"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="url"/> is an empty string.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="url"/> is an invalid WebSocket URL string.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="protocols"/> contains a value that is not a token.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="protocols"/> contains a value twice.
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
        if (!checkProtocols (protocols, out msg))
          throw new ArgumentException (msg, "protocols");

        _protocols = protocols;
      }

      _base64Key = CreateBase64Key ();
      _client = true;
      _log = new Logger ();
      _message = messagec;
      _retryCountForConnect = -1;
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

    // As server
    internal bool IgnoreExtensions {
      get {
        return _ignoreExtensions;
      }

      set {
        _ignoreExtensions = value;
      }
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets or sets the compression method used to compress a message.
    /// </summary>
    /// <remarks>
    /// The set operation works if the current state of the interface is
    /// New or Closed.
    /// </remarks>
    /// <value>
    ///   <para>
    ///   One of the <see cref="CompressionMethod"/> enum values.
    ///   </para>
    ///   <para>
    ///   It indicates the compression method used to compress a message.
    ///   </para>
    ///   <para>
    ///   The default value is <see cref="CompressionMethod.None"/>.
    ///   </para>
    /// </value>
    /// <exception cref="InvalidOperationException">
    /// The set operation is not available if the interface is not for
    /// the client.
    /// </exception>
    public CompressionMethod Compression {
      get {
        return _compression;
      }

      set {
        if (!_client) {
          var msg = "The interface is not for the client.";

          throw new InvalidOperationException (msg);
        }

        lock (_forState) {
          if (!canSet ())
            return;

          _compression = value;
        }
      }
    }

    /// <summary>
    /// Gets the HTTP cookies included in the handshake request/response.
    /// </summary>
    /// <value>
    ///   <para>
    ///   An <see cref="T:System.Collections.Generic.IEnumerable{WebSocketSharp.Net.Cookie}"/>
    ///   instance.
    ///   </para>
    ///   <para>
    ///   It provides an enumerator which supports the iteration over
    ///   the collection of the cookies.
    ///   </para>
    /// </value>
    public IEnumerable<Cookie> Cookies {
      get {
        lock (_cookies.SyncRoot) {
          foreach (var cookie in _cookies)
            yield return cookie;
        }
      }
    }

    /// <summary>
    /// Gets the credentials for the HTTP authentication (Basic/Digest).
    /// </summary>
    /// <value>
    ///   <para>
    ///   A <see cref="NetworkCredential"/> that represents the credentials
    ///   used to authenticate the client.
    ///   </para>
    ///   <para>
    ///   The default value is <see langword="null"/>.
    ///   </para>
    /// </value>
    public NetworkCredential Credentials {
      get {
        return _credentials;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the message event is
    /// emitted when the interface receives a ping.
    /// </summary>
    /// <value>
    ///   <para>
    ///   <c>true</c> if the interface emits the message event when
    ///   receives a ping; otherwise, <c>false</c>.
    ///   </para>
    ///   <para>
    ///   The default value is <c>false</c>.
    ///   </para>
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
    /// Gets or sets a value indicating whether the URL redirection for
    /// the handshake request is allowed.
    /// </summary>
    /// <remarks>
    /// The set operation works if the current state of the interface is
    /// New or Closed.
    /// </remarks>
    /// <value>
    ///   <para>
    ///   <c>true</c> if the interface allows the URL redirection for
    ///   the handshake request; otherwise, <c>false</c>.
    ///   </para>
    ///   <para>
    ///   The default value is <c>false</c>.
    ///   </para>
    /// </value>
    /// <exception cref="InvalidOperationException">
    /// The set operation is not available if the interface is not for
    /// the client.
    /// </exception>
    public bool EnableRedirection {
      get {
        return _enableRedirection;
      }

      set {
        if (!_client) {
          var msg = "The interface is not for the client.";

          throw new InvalidOperationException (msg);
        }

        lock (_forState) {
          if (!canSet ())
            return;

          _enableRedirection = value;
        }
      }
    }

    /// <summary>
    /// Gets the extensions selected by the server.
    /// </summary>
    /// <value>
    ///   <para>
    ///   A <see cref="string"/> that represents a list of the extensions
    ///   negotiated between the client and server.
    ///   </para>
    ///   <para>
    ///   An empty string if not specified or selected.
    ///   </para>
    /// </value>
    public string Extensions {
      get {
        return _extensions ?? String.Empty;
      }
    }

    /// <summary>
    /// Gets a value indicating whether the communication is possible.
    /// </summary>
    /// <value>
    /// <c>true</c> if the communication is possible; otherwise, <c>false</c>.
    /// </value>
    public bool IsAlive {
      get {
        return ping (_emptyBytes);
      }
    }

    /// <summary>
    /// Gets a value indicating whether the connection is secure.
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
    /// Gets the logging function.
    /// </summary>
    /// <remarks>
    /// The default logging level is <see cref="LogLevel.Error"/>.
    /// </remarks>
    /// <value>
    /// A <see cref="Logger"/> that provides the logging function.
    /// </value>
    public Logger Log {
      get {
        return _log;
      }

      internal set {
        _log = value;
      }
    }

    /// <summary>
    /// Gets or sets the value of the HTTP Origin header to send with
    /// the handshake request.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   The HTTP Origin header is defined in
    ///   <see href="http://tools.ietf.org/html/rfc6454#section-7">
    ///   Section 7 of RFC 6454</see>.
    ///   </para>
    ///   <para>
    ///   The interface sends the Origin header if this property has any.
    ///   </para>
    ///   <para>
    ///   The set operation works if the current state of the interface is
    ///   New or Closed.
    ///   </para>
    /// </remarks>
    /// <value>
    ///   <para>
    ///   A <see cref="string"/> that represents the value of the Origin
    ///   header to send.
    ///   </para>
    ///   <para>
    ///   The syntax is &lt;scheme&gt;://&lt;host&gt;[:&lt;port&gt;].
    ///   </para>
    ///   <para>
    ///   The default value is <see langword="null"/>.
    ///   </para>
    /// </value>
    /// <exception cref="InvalidOperationException">
    /// The set operation is not available if the interface is not for
    /// the client.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   The value specified for a set operation is not an absolute URI string.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   The value specified for a set operation includes the path segments.
    ///   </para>
    /// </exception>
    public string Origin {
      get {
        return _origin;
      }

      set {
        if (!_client) {
          var msg = "The interface is not for the client.";

          throw new InvalidOperationException (msg);
        }

        if (!value.IsNullOrEmpty ()) {
          Uri uri;

          if (!Uri.TryCreate (value, UriKind.Absolute, out uri)) {
            var msg = "Not an absolute URI string.";

            throw new ArgumentException (msg, "value");
          }

          if (uri.Segments.Length > 1) {
            var msg = "It includes the path segments.";

            throw new ArgumentException (msg, "value");
          }
        }

        lock (_forState) {
          if (!canSet ())
            return;

          _origin = !value.IsNullOrEmpty () ? value.TrimEnd ('/') : value;
        }
      }
    }

    /// <summary>
    /// Gets the name of subprotocol selected by the server.
    /// </summary>
    /// <value>
    ///   <para>
    ///   A <see cref="string"/> that will be one of the names of
    ///   subprotocols specified by client.
    ///   </para>
    ///   <para>
    ///   An empty string if not specified or selected.
    ///   </para>
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
    /// Gets the current state of the interface.
    /// </summary>
    /// <value>
    ///   <para>
    ///   One of the <see cref="WebSocketState"/> enum values.
    ///   </para>
    ///   <para>
    ///   It indicates the current state of the interface.
    ///   </para>
    ///   <para>
    ///   The default value is <see cref="WebSocketState.New"/>.
    ///   </para>
    /// </value>
    public WebSocketState ReadyState {
      get {
        return _readyState;
      }
    }

    /// <summary>
    /// Gets the configuration for secure connection.
    /// </summary>
    /// <remarks>
    /// The configuration is used when the interface attempts to connect,
    /// so it must be configured before any connect method is called.
    /// </remarks>
    /// <value>
    /// A <see cref="ClientSslConfiguration"/> that represents the
    /// configuration used to establish a secure connection.
    /// </value>
    /// <exception cref="InvalidOperationException">
    ///   <para>
    ///   The interface is not for the client.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   The interface does not use a secure connection.
    ///   </para>
    /// </exception>
    public ClientSslConfiguration SslConfiguration {
      get {
        if (!_client) {
          var msg = "The interface is not for the client.";

          throw new InvalidOperationException (msg);
        }

        if (!_secure) {
          var msg = "The interface does not use a secure connection.";

          throw new InvalidOperationException (msg);
        }

        return getSslConfiguration ();
      }
    }

    /// <summary>
    /// Gets the URL to which to connect.
    /// </summary>
    /// <value>
    ///   <para>
    ///   A <see cref="Uri"/> that represents the URL to which to connect.
    ///   </para>
    ///   <para>
    ///   Also it represents the URL requested by the client if the interface
    ///   is for the server.
    ///   </para>
    /// </value>
    public Uri Url {
      get {
        return _client ? _uri : _context.RequestUri;
      }
    }

    /// <summary>
    /// Gets or sets the time to wait for the response to the ping or close.
    /// </summary>
    /// <remarks>
    /// The set operation works if the current state of the interface is
    /// New or Closed.
    /// </remarks>
    /// <value>
    ///   <para>
    ///   A <see cref="TimeSpan"/> that represents the time to wait for
    ///   the response.
    ///   </para>
    ///   <para>
    ///   The default value is the same as 5 seconds if the interface is
    ///   for the client.
    ///   </para>
    /// </value>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value specified for a set operation is zero or less.
    /// </exception>
    public TimeSpan WaitTime {
      get {
        return _waitTime;
      }

      set {
        if (value <= TimeSpan.Zero) {
          var msg = "Zero or less.";

          throw new ArgumentOutOfRangeException ("value", msg);
        }

        lock (_forState) {
          if (!canSet ())
            return;

          _waitTime = value;
        }
      }
    }

    #endregion

    #region Public Events

    /// <summary>
    /// Occurs when the connection has been closed.
    /// </summary>
    public event EventHandler<CloseEventArgs> OnClose;

    /// <summary>
    /// Occurs when the interface gets an error.
    /// </summary>
    public event EventHandler<ErrorEventArgs> OnError;

    /// <summary>
    /// Occurs when the interface receives a message.
    /// </summary>
    public event EventHandler<MessageEventArgs> OnMessage;

    /// <summary>
    /// Occurs when the connection has been established.
    /// </summary>
    public event EventHandler OnOpen;

    #endregion

    #region Private Methods

    private void abort (string reason, Exception exception)
    {
      var code = exception is WebSocketException
                 ? ((WebSocketException) exception).Code
                 : (ushort) 1006;

      abort (code, reason);
    }

    private void abort (ushort code, string reason)
    {
      var data = new PayloadData (code, reason);

      close (data, false, false);
    }

    // As server
    private bool accept ()
    {
      lock (_forState) {
        if (_readyState == WebSocketState.Open) {
          _log.Trace ("The connection has already been established.");

          return false;
        }

        if (_readyState == WebSocketState.Closing) {
          _log.Error ("The close process is in progress.");

          error ("An error has occurred before accepting.", null);

          return false;
        }

        if (_readyState == WebSocketState.Closed) {
          _log.Error ("The connection has been closed.");

          error ("An error has occurred before accepting.", null);

          return false;
        }

        _readyState = WebSocketState.Connecting;

        var accepted = false;

        try {
          accepted = acceptHandshake ();
        }
        catch (Exception ex) {
          _log.Fatal (ex.Message);
          _log.Debug (ex.ToString ());

          abort (1011, "An exception has occurred while accepting.");
        }

        if (!accepted)
          return false;

        _readyState = WebSocketState.Open;

        return true;
      }
    }

    // As server
    private bool acceptHandshake ()
    {
      string msg;

      if (!checkHandshakeRequest (_context, out msg)) {
        _log.Error (msg);
        _log.Debug (_context.ToString ());

        refuseHandshake (1002, "A handshake error has occurred.");

        return false;
      }

      if (!customCheckHandshakeRequest (_context, out msg)) {
        _log.Error (msg);
        _log.Debug (_context.ToString ());

        refuseHandshake (1002, "A handshake error has occurred.");

        return false;
      }

      _base64Key = _context.Headers["Sec-WebSocket-Key"];

      if (_protocol != null) {
        var matched = _context
                      .SecWebSocketProtocols
                      .Contains (p => p == _protocol);

        if (!matched)
          _protocol = null;
      }

      if (!_ignoreExtensions) {
        var val = _context.Headers["Sec-WebSocket-Extensions"];

        processSecWebSocketExtensionsClientHeader (val);
      }

      createHandshakeResponse ().WriteTo (_stream);

      return true;
    }

    private bool canSet ()
    {
      return _readyState == WebSocketState.New
             || _readyState == WebSocketState.Closed;
    }

    // As server
    private bool checkHandshakeRequest (
      WebSocketContext context, out string message
    )
    {
      message = null;

      if (!context.IsWebSocketRequest) {
        message = "Not a WebSocket handshake request.";

        return false;
      }

      var headers = context.Headers;

      var key = headers["Sec-WebSocket-Key"];

      if (key == null) {
        message = "The Sec-WebSocket-Key header is non-existent.";

        return false;
      }

      if (key.Length == 0) {
        message = "The Sec-WebSocket-Key header is invalid.";

        return false;
      }

      var ver = headers["Sec-WebSocket-Version"];

      if (ver == null) {
        message = "The Sec-WebSocket-Version header is non-existent.";

        return false;
      }

      if (ver != _version) {
        message = "The Sec-WebSocket-Version header is invalid.";

        return false;
      }

      var subps = headers["Sec-WebSocket-Protocol"];

      if (subps != null) {
        if (subps.Length == 0) {
          message = "The Sec-WebSocket-Protocol header is invalid.";

          return false;
        }
      }

      if (!_ignoreExtensions) {
        var exts = headers["Sec-WebSocket-Extensions"];

        if (exts != null) {
          if (exts.Length == 0) {
            message = "The Sec-WebSocket-Extensions header is invalid.";

            return false;
          }
        }
      }

      return true;
    }

    // As client
    private bool checkHandshakeResponse (
      HttpResponse response, out string message
    )
    {
      message = null;

      if (response.IsRedirect) {
        message = "The redirection is indicated.";

        return false;
      }

      if (response.IsUnauthorized) {
        message = "The authentication is required.";

        return false;
      }

      if (!response.IsWebSocketResponse) {
        message = "Not a WebSocket handshake response.";

        return false;
      }

      var headers = response.Headers;

      var key = headers["Sec-WebSocket-Accept"];

      if (key == null) {
        message = "The Sec-WebSocket-Accept header is non-existent.";

        return false;
      }

      if (key != CreateResponseKey (_base64Key)) {
        message = "The Sec-WebSocket-Accept header is invalid.";

        return false;
      }

      var ver = headers["Sec-WebSocket-Version"];

      if (ver != null) {
        if (ver != _version) {
          message = "The Sec-WebSocket-Version header is invalid.";

          return false;
        }
      }

      var subp = headers["Sec-WebSocket-Protocol"];

      if (subp == null) {
        if (_protocolsRequested) {
          message = "The Sec-WebSocket-Protocol header is non-existent.";

          return false;
        }
      }
      else {
        var valid = _protocolsRequested
                    && subp.Length > 0
                    && _protocols.Contains (p => p == subp);

        if (!valid) {
          message = "The Sec-WebSocket-Protocol header is invalid.";

          return false;
        }
      }

      var exts = headers["Sec-WebSocket-Extensions"];

      if (exts != null) {
        if (!validateSecWebSocketExtensionsServerHeader (exts)) {
          message = "The Sec-WebSocket-Extensions header is invalid.";

          return false;
        }
      }

      return true;
    }

    private static bool checkProtocols (string[] protocols, out string message)
    {
      message = null;

      Func<string, bool> cond = p => p.IsNullOrEmpty () || !p.IsToken ();

      if (protocols.Contains (cond)) {
        message = "It contains a value that is not a token.";

        return false;
      }

      if (protocols.ContainsTwice ()) {
        message = "It contains a value twice.";

        return false;
      }

      return true;
    }

    // As client
    private bool checkProxyConnectResponse (
      HttpResponse response, out string message
    )
    {
      message = null;

      if (response.IsProxyAuthenticationRequired) {
        message = "The proxy authentication is required.";

        return false;
      }

      if (!response.IsSuccess) {
        message = "The proxy has failed a connection to the requested URL.";

        return false;
      }

      return true;
    }

    private bool checkReceivedFrame (WebSocketFrame frame, out string message)
    {
      message = null;

      if (frame.IsMasked) {
        if (_client) {
          message = "A frame from the server is masked.";

          return false;
        }
      }
      else {
        if (!_client) {
          message = "A frame from a client is not masked.";

          return false;
        }
      }

      if (frame.IsCompressed) {
        if (_compression == CompressionMethod.None) {
          message = "A frame is compressed without any agreement for it.";

          return false;
        }

        if (!frame.IsData) {
          message = "A non data frame is compressed.";

          return false;
        }
      }

      if (frame.IsData) {
        if (_inContinuation) {
          message = "A data frame was received while receiving continuation frames.";

          return false;
        }
      }

      if (frame.IsControl) {
        if (frame.Fin == Fin.More) {
          message = "A control frame is fragmented.";

          return false;
        }

        if (frame.PayloadLength > 125) {
          message = "The payload length of a control frame is greater than 125.";

          return false;
        }
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

    private void close (ushort code, string reason)
    {
      if (_readyState == WebSocketState.Closing) {
        _log.Trace ("The close process is already in progress.");

        return;
      }

      if (_readyState == WebSocketState.Closed) {
        _log.Trace ("The connection has already been closed.");

        return;
      }

      if (code == 1005) {
        close (PayloadData.Empty, true, false);

        return;
      }

      var data = new PayloadData (code, reason);
      var send = !code.IsReservedStatusCode ();

      close (data, send, false);
    }

    private void close (PayloadData payloadData, bool send, bool received)
    {
      lock (_forState) {
        if (_readyState == WebSocketState.Closing) {
          _log.Trace ("The close process is already in progress.");

          return;
        }

        if (_readyState == WebSocketState.Closed) {
          _log.Trace ("The connection has already been closed.");

          return;
        }

        send = send && _readyState == WebSocketState.Open;

        _readyState = WebSocketState.Closing;
      }

      _log.Trace ("Begin closing the connection.");

      var res = closeHandshake (payloadData, send, received);

      releaseResources ();

      _log.Trace ("End closing the connection.");

      _readyState = WebSocketState.Closed;

      var e = new CloseEventArgs (payloadData, res);

      try {
        OnClose.Emit (this, e);
      }
      catch (Exception ex) {
        _log.Error (ex.Message);
        _log.Debug (ex.ToString ());
      }
    }

    private void closeAsync (ushort code, string reason)
    {
      if (_readyState == WebSocketState.Closing) {
        _log.Trace ("The close process is already in progress.");

        return;
      }

      if (_readyState == WebSocketState.Closed) {
        _log.Trace ("The connection has already been closed.");

        return;
      }

      if (code == 1005) {
        closeAsync (PayloadData.Empty, true, false);

        return;
      }

      var data = new PayloadData (code, reason);
      var send = !code.IsReservedStatusCode ();

      closeAsync (data, send, false);
    }

    private void closeAsync (PayloadData payloadData, bool send, bool received)
    {
      Action<PayloadData, bool, bool> closer = close;

      closer.BeginInvoke (
        payloadData, send, received, ar => closer.EndInvoke (ar), null
      );
    }

    private bool closeHandshake (
      PayloadData payloadData, bool send, bool received
    )
    {
      var sent = false;

      if (send) {
        var frame = WebSocketFrame.CreateCloseFrame (payloadData, _client);
        var bytes = frame.ToArray ();

        sent = sendBytes (bytes);

        if (_client)
          frame.Unmask ();
      }

      var wait = !received && sent && _receivingExited != null;

      if (wait)
        received = _receivingExited.WaitOne (_waitTime);

      var ret = sent && received;

      var msg = String.Format (
                  "The closing was clean? {0} (sent: {1} received: {2})",
                  ret,
                  sent,
                  received
                );

      _log.Debug (msg);

      return ret;
    }

    // As client
    private bool connect ()
    {
      if (_readyState == WebSocketState.Connecting) {
        _log.Trace ("The connect process is in progress.");

        return false;
      }

      lock (_forState) {
        if (_readyState == WebSocketState.Open) {
          _log.Trace ("The connection has already been established.");

          return false;
        }

        if (_readyState == WebSocketState.Closing) {
          _log.Error ("The close process is in progress.");

          error ("An error has occurred before connecting.", null);

          return false;
        }

        if (_retryCountForConnect >= _maxRetryCountForConnect) {
          _log.Error ("An opportunity for reconnecting has been lost.");

          error ("An error has occurred before connecting.", null);

          return false;
        }

        _retryCountForConnect++;

        _readyState = WebSocketState.Connecting;

        var done = false;

        try {
          done = doHandshake ();
        }
        catch (Exception ex) {
          _log.Fatal (ex.Message);
          _log.Debug (ex.ToString ());

          abort ("An exception has occurred while connecting.", ex);
        }

        if (!done)
          return false;

        _retryCountForConnect = -1;

        _readyState = WebSocketState.Open;

        return true;
      }
    }
    
    // As client
    private AuthenticationResponse createAuthenticationResponse ()
    {
      if (_credentials == null)
        return null;

      if (_authChallenge != null) {
        var ret = new AuthenticationResponse (
                    _authChallenge, _credentials, _nonceCount
                  );

        _nonceCount = ret.NonceCount;

        return ret;
      }

      return _preAuth ? new AuthenticationResponse (_credentials) : null;
    }

    // As client
    private string createExtensions ()
    {
      var buff = new StringBuilder (80);

      if (_compression != CompressionMethod.None) {
        var str = _compression.ToExtensionString (
                    "server_no_context_takeover", "client_no_context_takeover"
                  );

        buff.AppendFormat ("{0}, ", str);
      }

      var len = buff.Length;

      if (len <= 2)
        return null;

      buff.Length = len - 2;

      return buff.ToString ();
    }

    // As server
    private HttpResponse createHandshakeFailureResponse ()
    {
      var ret = HttpResponse.CreateCloseResponse (HttpStatusCode.BadRequest);

      ret.Headers["Sec-WebSocket-Version"] = _version;

      return ret;
    }

    // As client
    private HttpRequest createHandshakeRequest ()
    {
      var ret = HttpRequest.CreateWebSocketHandshakeRequest (_uri);

      var headers = ret.Headers;

      headers["Sec-WebSocket-Key"] = _base64Key;
      headers["Sec-WebSocket-Version"] = _version;

      if (!_origin.IsNullOrEmpty ())
        headers["Origin"] = _origin;

      if (_protocols != null) {
        headers["Sec-WebSocket-Protocol"] = _protocols.ToString (", ");

        _protocolsRequested = true;
      }

      var exts = createExtensions ();

      if (exts != null) {
        headers["Sec-WebSocket-Extensions"] = exts;

        _extensionsRequested = true;
      }

      var ares = createAuthenticationResponse ();

      if (ares != null)
        headers["Authorization"] = ares.ToString ();

      if (_cookies.Count > 0)
        ret.SetCookies (_cookies);

      return ret;
    }

    // As server
    private HttpResponse createHandshakeResponse ()
    {
      var ret = HttpResponse.CreateWebSocketHandshakeResponse ();

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
    private bool customCheckHandshakeRequest (
      WebSocketContext context, out string message
    )
    {
      message = null;

      if (_handshakeRequestChecker == null)
        return true;

      message = _handshakeRequestChecker (context);

      return message == null;
    }

    private MessageEventArgs dequeueFromMessageEventQueue ()
    {
      lock (_forMessageEventQueue) {
        return _messageEventQueue.Count > 0
               ? _messageEventQueue.Dequeue ()
               : null;
      }
    }

    // As client
    private bool doHandshake ()
    {
      setClientStream ();

      var res = sendHandshakeRequest ();

      string msg;

      if (!checkHandshakeResponse (res, out msg)) {
        _log.Error (msg);
        _log.Debug (res.ToString ());

        abort (1002, "A handshake error has occurred.");

        return false;
      }

      if (_protocolsRequested)
        _protocol = res.Headers["Sec-WebSocket-Protocol"];

      if (_extensionsRequested) {
        var exts = res.Headers["Sec-WebSocket-Extensions"];

        if (exts == null)
          _compression = CompressionMethod.None;
        else
          _extensions = exts;
      }

      var cookies = res.Cookies;

      if (cookies.Count > 0)
        _cookies.SetOrRemove (cookies);

      return true;
    }

    private void enqueueToMessageEventQueue (MessageEventArgs e)
    {
      lock (_forMessageEventQueue)
        _messageEventQueue.Enqueue (e);
    }

    private void error (string message, Exception exception)
    {
      var e = new ErrorEventArgs (message, exception);

      try {
        OnError.Emit (this, e);
      }
      catch (Exception ex) {
        _log.Error (ex.Message);
        _log.Debug (ex.ToString ());
      }
    }

    private ClientSslConfiguration getSslConfiguration ()
    {
      if (_sslConfig == null)
        _sslConfig = new ClientSslConfiguration (_uri.DnsSafeHost);

      return _sslConfig;
    }

    private void init ()
    {
      _compression = CompressionMethod.None;
      _cookies = new CookieCollection ();
      _forPing = new object ();
      _forSend = new object ();
      _forState = new object ();
      _messageEventQueue = new Queue<MessageEventArgs> ();
      _forMessageEventQueue = ((ICollection) _messageEventQueue).SyncRoot;
      _readyState = WebSocketState.New;
    }

    private void message ()
    {
      MessageEventArgs e = null;

      lock (_forMessageEventQueue) {
        if (_inMessage)
          return;

        if (_messageEventQueue.Count == 0)
          return;

        if (_readyState != WebSocketState.Open)
          return;

        e = _messageEventQueue.Dequeue ();

        _inMessage = true;
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
          _log.Error (ex.Message);
          _log.Debug (ex.ToString ());

          error ("An exception has occurred during an OnMessage event.", ex);
        }

        lock (_forMessageEventQueue) {
          if (_messageEventQueue.Count == 0) {
            _inMessage = false;

            break;
          }

          if (_readyState != WebSocketState.Open) {
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
        _log.Error (ex.Message);
        _log.Debug (ex.ToString ());

        error ("An exception has occurred during an OnMessage event.", ex);
      }

      lock (_forMessageEventQueue) {
        if (_messageEventQueue.Count == 0) {
          _inMessage = false;

          return;
        }

        if (_readyState != WebSocketState.Open) {
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
        _log.Error (ex.Message);
        _log.Debug (ex.ToString ());

        error ("An exception has occurred during the OnOpen event.", ex);
      }

      MessageEventArgs e = null;

      lock (_forMessageEventQueue) {
        if (_messageEventQueue.Count == 0) {
          _inMessage = false;

          return;
        }

        if (_readyState != WebSocketState.Open) {
          _inMessage = false;

          return;
        }

        e = _messageEventQueue.Dequeue ();
      }

      _message.BeginInvoke (e, ar => _message.EndInvoke (ar), null);
    }

    private bool ping (byte[] data)
    {
      if (_readyState != WebSocketState.Open)
        return false;

      var received = _pongReceived;

      if (received == null)
        return false;

      lock (_forPing) {
        try {
          received.Reset ();

          var sent = send (Fin.Final, Opcode.Ping, data, false);

          if (!sent)
            return false;

          return received.WaitOne (_waitTime);
        }
        catch (ObjectDisposedException) {
          return false;
        }
      }
    }

    private bool processCloseFrame (WebSocketFrame frame)
    {
      var data = frame.PayloadData;
      var send = !data.HasReservedCode;

      close (data, send, true);

      return false;
    }

    private bool processDataFrame (WebSocketFrame frame)
    {
      var e = frame.IsCompressed
              ? new MessageEventArgs (
                  frame.Opcode,
                  frame.PayloadData.ApplicationData.Decompress (_compression)
                )
              : new MessageEventArgs (frame);

      enqueueToMessageEventQueue (e);

      return true;
    }

    private bool processFragmentFrame (WebSocketFrame frame)
    {
      if (!_inContinuation) {
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

          var e = new MessageEventArgs (_fragmentsOpcode, data);

          enqueueToMessageEventQueue (e);
        }

        _fragmentsBuffer = null;
        _inContinuation = false;
      }

      return true;
    }

    private bool processPingFrame (WebSocketFrame frame)
    {
      _log.Trace ("A ping was received.");

      var pong = WebSocketFrame.CreatePongFrame (frame.PayloadData, _client);

      lock (_forState) {
        if (_readyState != WebSocketState.Open) {
          _log.Trace ("A pong to this ping cannot be sent.");

          return true;
        }

        var bytes = pong.ToArray ();
        var sent = sendBytes (bytes);

        if (!sent)
          return false;
      }

      _log.Trace ("A pong to this ping has been sent.");

      if (_emitOnPing) {
        if (_client)
          pong.Unmask ();

        var e = new MessageEventArgs (frame);

        enqueueToMessageEventQueue (e);
      }

      return true;
    }

    private bool processPongFrame (WebSocketFrame frame)
    {
      _log.Trace ("A pong was received.");

      try {
        _pongReceived.Set ();
      }
      catch (NullReferenceException) {
        return false;
      }
      catch (ObjectDisposedException) {
        return false;
      }

      _log.Trace ("It has been signaled.");

      return true;
    }

    private bool processReceivedFrame (WebSocketFrame frame)
    {
      string msg;

      if (!checkReceivedFrame (frame, out msg)) {
        _log.Error (msg);
        _log.Debug (frame.ToString (false));

        abort (1002, "An error has occurred while receiving.");

        return false;
      }

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

      foreach (var elm in value.SplitHeaderValue (',')) {
        var ext = elm.Trim ();

        if (ext.Length == 0)
          continue;

        if (!comp) {
          if (ext.IsCompressionExtension (CompressionMethod.Deflate)) {
            _compression = CompressionMethod.Deflate;

            var str = _compression.ToExtensionString (
                        "client_no_context_takeover",
                        "server_no_context_takeover"
                      );

            buff.AppendFormat ("{0}, ", str);

            comp = true;
          }
        }
      }

      var len = buff.Length;

      if (len <= 2)
        return;

      buff.Length = len - 2;

      _extensions = buff.ToString ();
    }

    private bool processUnsupportedFrame (WebSocketFrame frame)
    {
      _log.Fatal ("An unsupported frame was received.");
      _log.Debug (frame.ToString (false));

      abort (1003, "There is no way to handle it.");

      return false;
    }

    // As server
    private void refuseHandshake (ushort code, string reason)
    {
      createHandshakeFailureResponse ().WriteTo (_stream);

      abort (code, reason);
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

      if (_pongReceived != null) {
        _pongReceived.Close ();

        _pongReceived = null;
      }

      if (_receivingExited != null) {
        _receivingExited.Close ();

        _receivingExited = null;
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
      if (_closeContext != null) {
        _closeContext ();

        _closeContext = null;
      }

      _stream = null;
      _context = null;
    }

    private bool send (byte[] rawFrame)
    {
      lock (_forState) {
        if (_readyState != WebSocketState.Open) {
          _log.Error ("The current state of the interface is not Open.");

          return false;
        }

        return sendBytes (rawFrame);
      }
    }

    private bool send (Opcode opcode, Stream sourceStream)
    {
      lock (_forSend) {
        var dataStream = sourceStream;
        var compressed = false;
        var sent = false;

        try {
          if (_compression != CompressionMethod.None) {
            dataStream = sourceStream.Compress (_compression);
            compressed = true;
          }

          sent = send (opcode, dataStream, compressed);

          if (!sent)
            error ("A send has failed.", null);
        }
        catch (Exception ex) {
          _log.Error (ex.Message);
          _log.Debug (ex.ToString ());

          error ("An exception has occurred during a send.", ex);
        }
        finally {
          if (compressed)
            dataStream.Dispose ();

          sourceStream.Dispose ();
        }

        return sent;
      }
    }

    private bool send (Opcode opcode, Stream dataStream, bool compressed)
    {
      var len = dataStream.Length;

      if (len == 0)
        return send (Fin.Final, opcode, _emptyBytes, false);

      var quo = len / FragmentLength;
      var rem = (int) (len % FragmentLength);

      byte[] buff = null;

      if (quo == 0) {
        buff = new byte[rem];

        return dataStream.Read (buff, 0, rem) == rem
               && send (Fin.Final, opcode, buff, compressed);
      }

      if (quo == 1 && rem == 0) {
        buff = new byte[FragmentLength];

        return dataStream.Read (buff, 0, FragmentLength) == FragmentLength
               && send (Fin.Final, opcode, buff, compressed);
      }

      /* Send fragments */

      // Begin

      buff = new byte[FragmentLength];

      var sent = dataStream.Read (buff, 0, FragmentLength) == FragmentLength
                 && send (Fin.More, opcode, buff, compressed);

      if (!sent)
        return false;

      // Continue

      var n = rem == 0 ? quo - 2 : quo - 1;

      for (long i = 0; i < n; i++) {
        sent = dataStream.Read (buff, 0, FragmentLength) == FragmentLength
               && send (Fin.More, Opcode.Cont, buff, false);

        if (!sent)
          return false;
      }

      // End

      if (rem == 0)
        rem = FragmentLength;
      else
        buff = new byte[rem];

      return dataStream.Read (buff, 0, rem) == rem
             && send (Fin.Final, Opcode.Cont, buff, false);
    }

    private bool send (Fin fin, Opcode opcode, byte[] data, bool compressed)
    {
      var frame = new WebSocketFrame (fin, opcode, data, compressed, _client);
      var rawFrame = frame.ToArray ();

      return send (rawFrame);
    }

    private void sendAsync (
      Opcode opcode, Stream sourceStream, Action<bool> completed
    )
    {
      Func<Opcode, Stream, bool> sender = send;

      sender.BeginInvoke (
        opcode,
        sourceStream,
        ar => {
          try {
            var sent = sender.EndInvoke (ar);

            if (completed != null)
              completed (sent);
          }
          catch (Exception ex) {
            _log.Error (ex.Message);
            _log.Debug (ex.ToString ());

            error (
              "An exception has occurred during the callback for an async send.",
              ex
            );
          }
        },
        null
      );
    }

    private bool sendBytes (byte[] bytes)
    {
      try {
        _stream.Write (bytes, 0, bytes.Length);
      }
      catch (Exception ex) {
        _log.Error (ex.Message);
        _log.Debug (ex.ToString ());

        return false;
      }

      return true;
    }

    // As client
    private HttpResponse sendHandshakeRequest ()
    {
      var req = createHandshakeRequest ();

      var timeout = 90000;
      var res = req.GetResponse (_stream, timeout);

      if (res.IsUnauthorized) {
        var val = res.Headers["WWW-Authenticate"];

        if (val.IsNullOrEmpty ()) {
          _log.Debug ("No authentication challenge is specified.");

          return res;
        }

        var achal = AuthenticationChallenge.Parse (val);

        if (achal == null) {
          _log.Debug ("An invalid authentication challenge is specified.");

          return res;
        }

        _authChallenge = achal;

        if (_credentials == null)
          return res;

        var ares = new AuthenticationResponse (
                     _authChallenge, _credentials, _nonceCount
                   );

        _nonceCount = ares.NonceCount;

        req.Headers["Authorization"] = ares.ToString ();

        if (res.CloseConnection) {
          releaseClientResources ();
          setClientStream ();
        }

        timeout = 15000;
        res = req.GetResponse (_stream, timeout);
      }

      if (res.IsRedirect) {
        if (!_enableRedirection)
          return res;

        var val = res.Headers["Location"];

        if (val.IsNullOrEmpty ()) {
          _log.Debug ("No URL to redirect is located.");

          return res;
        }

        Uri uri;
        string msg;

        if (!val.TryCreateWebSocketUri (out uri, out msg)) {
          _log.Debug ("An invalid URL to redirect is located.");

          return res;
        }

        releaseClientResources ();

        _uri = uri;
        _secure = uri.Scheme == "wss";

        setClientStream ();

        return sendHandshakeRequest ();
      }

      return res;
    }

    // As client
    private HttpResponse sendProxyConnectRequest ()
    {
      var req = HttpRequest.CreateConnectRequest (_uri);

      var timeout = 90000;
      var res = req.GetResponse (_stream, timeout);

      if (res.IsProxyAuthenticationRequired) {
        if (_proxyCredentials == null)
          return res;

        var val = res.Headers["Proxy-Authenticate"];

        if (val.IsNullOrEmpty ()) {
          _log.Debug ("No proxy authentication challenge is specified.");

          return res;
        }

        var achal = AuthenticationChallenge.Parse (val);

        if (achal == null) {
          _log.Debug ("An invalid proxy authentication challenge is specified.");

          return res;
        }

        var ares = new AuthenticationResponse (achal, _proxyCredentials, 0);

        req.Headers["Proxy-Authorization"] = ares.ToString ();

        if (res.CloseConnection) {
          releaseClientResources ();

          _tcpClient = new TcpClient (_proxyUri.DnsSafeHost, _proxyUri.Port);
          _stream = _tcpClient.GetStream ();
        }

        timeout = 15000;
        res = req.GetResponse (_stream, timeout);
      }

      return res;
    }

    // As client
    private void setClientStream ()
    {
      if (_proxyUri != null) {
        _tcpClient = new TcpClient (_proxyUri.DnsSafeHost, _proxyUri.Port);
        _stream = _tcpClient.GetStream ();

        var res = sendProxyConnectRequest ();

        string msg;

        if (!checkProxyConnectResponse (res, out msg))
          throw new WebSocketException (msg);
      }
      else {
        _tcpClient = new TcpClient (_uri.DnsSafeHost, _uri.Port);
        _stream = _tcpClient.GetStream ();
      }

      if (_secure) {
        var conf = getSslConfiguration ();
        var host = conf.TargetHost;

        if (host != _uri.DnsSafeHost) {
          var msg = "An invalid host name is specified.";

          throw new WebSocketException (
                  CloseStatusCode.TlsHandshakeFailure, msg
                );
        }

        try {
          var sslStream = new SslStream (
                            _stream,
                            false,
                            conf.ServerCertificateValidationCallback,
                            conf.ClientCertificateSelectionCallback
                          );

          sslStream.AuthenticateAsClient (
            host,
            conf.ClientCertificates,
            conf.EnabledSslProtocols,
            conf.CheckCertificateRevocation
          );

          _stream = sslStream;
        }
        catch (Exception ex) {
          throw new WebSocketException (
                  CloseStatusCode.TlsHandshakeFailure, ex
                );
        }
      }
    }

    private void startReceiving ()
    {
      if (_messageEventQueue.Count > 0)
        _messageEventQueue.Clear ();

      _pongReceived = new ManualResetEvent (false);
      _receivingExited = new ManualResetEvent (false);

      Action receive = null;
      receive =
        () =>
          WebSocketFrame.ReadFrameAsync (
            _stream,
            false,
            frame => {
              var cont = processReceivedFrame (frame)
                         && _readyState != WebSocketState.Closed;

              if (!cont) {
                var exited = _receivingExited;

                if (exited != null)
                  exited.Set ();

                return;
              }

              receive ();

              if (_inMessage)
                return;

              message ();
            },
            ex => {
              _log.Fatal (ex.Message);
              _log.Debug (ex.ToString ());

              abort ("An exception has occurred while receiving.", ex);
            }
          );

      receive ();
    }

    // As client
    private bool validateSecWebSocketExtensionsServerHeader (string value)
    {
      if (!_extensionsRequested)
        return false;

      if (value.Length == 0)
        return false;

      var comp = _compression != CompressionMethod.None;

      foreach (var elm in value.SplitHeaderValue (',')) {
        var ext = elm.Trim ();

        if (comp && ext.IsCompressionExtension (_compression)) {
          var param1 = "server_no_context_takeover";
          var param2 = "client_no_context_takeover";

          if (!ext.Contains (param1)) {
            // The server did not send back "server_no_context_takeover".

            return false;
          }

          var name = _compression.ToExtensionString ();
          var invalid = ext.SplitHeaderValue (';').Contains (
                          t => {
                            t = t.Trim ();

                            var valid = t == name
                                        || t == param1
                                        || t == param2;

                            return !valid;
                          }
                        );

          if (invalid)
            return false;

          comp = false;
        }
        else {
          return false;
        }
      }

      return true;
    }

    #endregion

    #region Internal Methods

    // As server
    internal void Accept ()
    {
      var accepted = accept ();

      if (!accepted)
        return;

      open ();
    }

    // As server
    internal void AcceptAsync ()
    {
      Func<bool> acceptor = accept;

      acceptor.BeginInvoke (
        ar => {
          var accepted = acceptor.EndInvoke (ar);

          if (!accepted)
            return;

          open ();
        },
        null
      );
    }

    // As server
    internal void Close (PayloadData payloadData, byte[] rawFrame)
    {
      lock (_forState) {
        if (_readyState == WebSocketState.Closing) {
          _log.Trace ("The close process is already in progress.");

          return;
        }

        if (_readyState == WebSocketState.Closed) {
          _log.Trace ("The connection has already been closed.");

          return;
        }

        _readyState = WebSocketState.Closing;
      }

      _log.Trace ("Begin closing the connection.");

      var sent = rawFrame != null && sendBytes (rawFrame);
      var received = sent && _receivingExited != null
                     ? _receivingExited.WaitOne (_waitTime)
                     : false;

      var res = sent && received;

      var msg = String.Format (
                  "The closing was clean? {0} (sent: {1} received: {2})",
                  res,
                  sent,
                  received
                );

      _log.Debug (msg);

      releaseServerResources ();
      releaseCommonResources ();

      _log.Trace ("End closing the connection.");

      _readyState = WebSocketState.Closed;

      var e = new CloseEventArgs (payloadData, res);

      try {
        OnClose.Emit (this, e);
      }
      catch (Exception ex) {
        _log.Error (ex.Message);
        _log.Debug (ex.ToString ());
      }
    }

    // As client
    internal static string CreateBase64Key ()
    {
      var key = new byte[16];

      RandomNumber.GetBytes (key);

      return Convert.ToBase64String (key);
    }

    internal static string CreateResponseKey (string base64Key)
    {
      SHA1 sha1 = new SHA1CryptoServiceProvider ();

      var src = base64Key + _guid;
      var bytes = src.GetUTF8EncodedBytes ();
      var key = sha1.ComputeHash (bytes);

      return Convert.ToBase64String (key);
    }

    // As server
    internal bool Ping (byte[] rawFrame)
    {
      if (_readyState != WebSocketState.Open)
        return false;

      var received = _pongReceived;

      if (received == null)
        return false;

      lock (_forPing) {
        try {
          received.Reset ();

          var sent = send (rawFrame);

          if (!sent)
            return false;

          return received.WaitOne (_waitTime);
        }
        catch (ObjectDisposedException) {
          return false;
        }
      }
    }

    // As server
    internal void Send (
      Opcode opcode, byte[] data, Dictionary<CompressionMethod, byte[]> cache
    )
    {
      lock (_forSend) {
        byte[] found;

        if (!cache.TryGetValue (_compression, out found)) {
          found = new WebSocketFrame (
                    Fin.Final,
                    opcode,
                    data.Compress (_compression),
                    _compression != CompressionMethod.None,
                    false
                  )
                  .ToArray ();

          cache.Add (_compression, found);
        }

        send (found);
      }
    }

    // As server
    internal void Send (
      Opcode opcode,
      Stream sourceStream,
      Dictionary<CompressionMethod, Stream> cache
    )
    {
      lock (_forSend) {
        Stream found;

        if (!cache.TryGetValue (_compression, out found)) {
          found = sourceStream.Compress (_compression);

          cache.Add (_compression, found);
        }
        else {
          found.Position = 0;
        }

        send (opcode, found, _compression != CompressionMethod.None);
      }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Closes the connection.
    /// </summary>
    /// <remarks>
    /// This method does nothing if the current state of the interface is
    /// Closing or Closed.
    /// </remarks>
    public void Close ()
    {
      close (1005, String.Empty);
    }

    /// <summary>
    /// Closes the connection with the specified status code.
    /// </summary>
    /// <remarks>
    /// This method does nothing if the current state of the interface is
    /// Closing or Closed.
    /// </remarks>
    /// <param name="code">
    ///   <para>
    ///   A <see cref="ushort"/> that specifies the status code indicating
    ///   the reason for the close.
    ///   </para>
    ///   <para>
    ///   The status codes are defined in
    ///   <see href="http://tools.ietf.org/html/rfc6455#section-7.4">
    ///   Section 7.4</see> of RFC 6455.
    ///   </para>
    /// </param>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="code"/> is 1011 (server error).
    ///   It cannot be used by a client.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="code"/> is 1010 (mandatory extension).
    ///   It cannot be used by a server.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="code"/> is less than 1000 or greater than 4999.
    /// </exception>
    public void Close (ushort code)
    {
      Close (code, String.Empty);
    }

    /// <summary>
    /// Closes the connection with the specified status code.
    /// </summary>
    /// <remarks>
    /// This method does nothing if the current state of the interface is
    /// Closing or Closed.
    /// </remarks>
    /// <param name="code">
    ///   <para>
    ///   One of the <see cref="CloseStatusCode"/> enum values.
    ///   </para>
    ///   <para>
    ///   It specifies the status code indicating the reason for the close.
    ///   </para>
    /// </param>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="code"/> is an undefined enum value.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="code"/> is <see cref="CloseStatusCode.ServerError"/>.
    ///   It cannot be used by a client.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="code"/> is <see cref="CloseStatusCode.MandatoryExtension"/>.
    ///   It cannot be used by a server.
    ///   </para>
    /// </exception>
    public void Close (CloseStatusCode code)
    {
      Close (code, String.Empty);
    }

    /// <summary>
    /// Closes the connection with the specified status code and reason.
    /// </summary>
    /// <remarks>
    /// This method does nothing if the current state of the interface is
    /// Closing or Closed.
    /// </remarks>
    /// <param name="code">
    ///   <para>
    ///   A <see cref="ushort"/> that specifies the status code indicating
    ///   the reason for the close.
    ///   </para>
    ///   <para>
    ///   The status codes are defined in
    ///   <see href="http://tools.ietf.org/html/rfc6455#section-7.4">
    ///   Section 7.4</see> of RFC 6455.
    ///   </para>
    /// </param>
    /// <param name="reason">
    ///   <para>
    ///   A <see cref="string"/> that specifies the reason for the close.
    ///   </para>
    ///   <para>
    ///   Its size must be 123 bytes or less in UTF-8.
    ///   </para>
    /// </param>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="code"/> is 1011 (server error).
    ///   It cannot be used by a client.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="code"/> is 1010 (mandatory extension).
    ///   It cannot be used by a server.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="code"/> is 1005 (no status) and
    ///   <paramref name="reason"/> is specified.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="reason"/> could not be UTF-8-encoded.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   <para>
    ///   <paramref name="code"/> is less than 1000 or greater than 4999.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   The size of <paramref name="reason"/> is greater than 123 bytes.
    ///   </para>
    /// </exception>
    public void Close (ushort code, string reason)
    {
      if (!code.IsCloseStatusCode ()) {
        var msg = "Less than 1000 or greater than 4999.";

        throw new ArgumentOutOfRangeException ("code", msg);
      }

      if (_client) {
        if (code == 1011) {
          var msg = "1011 cannot be used.";

          throw new ArgumentException (msg, "code");
        }
      }
      else {
        if (code == 1010) {
          var msg = "1010 cannot be used.";

          throw new ArgumentException (msg, "code");
        }
      }

      if (reason.IsNullOrEmpty ()) {
        close (code, String.Empty);

        return;
      }

      if (code == 1005) {
        var msg = "1005 cannot be used.";

        throw new ArgumentException (msg, "code");
      }

      byte[] bytes;

      if (!reason.TryGetUTF8EncodedBytes (out bytes)) {
        var msg = "It could not be UTF-8-encoded.";

        throw new ArgumentException (msg, "reason");
      }

      if (bytes.Length > 123) {
        var msg = "Its size is greater than 123 bytes.";

        throw new ArgumentOutOfRangeException ("reason", msg);
      }

      close (code, reason);
    }

    /// <summary>
    /// Closes the connection with the specified status code and reason.
    /// </summary>
    /// <remarks>
    /// This method does nothing if the current state of the interface is
    /// Closing or Closed.
    /// </remarks>
    /// <param name="code">
    ///   <para>
    ///   One of the <see cref="CloseStatusCode"/> enum values.
    ///   </para>
    ///   <para>
    ///   It specifies the status code indicating the reason for the close.
    ///   </para>
    /// </param>
    /// <param name="reason">
    ///   <para>
    ///   A <see cref="string"/> that specifies the reason for the close.
    ///   </para>
    ///   <para>
    ///   Its size must be 123 bytes or less in UTF-8.
    ///   </para>
    /// </param>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="code"/> is an undefined enum value.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="code"/> is <see cref="CloseStatusCode.ServerError"/>.
    ///   It cannot be used by a client.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="code"/> is <see cref="CloseStatusCode.MandatoryExtension"/>.
    ///   It cannot be used by a server.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="code"/> is <see cref="CloseStatusCode.NoStatus"/> and
    ///   <paramref name="reason"/> is specified.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="reason"/> could not be UTF-8-encoded.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The size of <paramref name="reason"/> is greater than 123 bytes.
    /// </exception>
    public void Close (CloseStatusCode code, string reason)
    {
      if (!code.IsDefined ()) {
        var msg = "An undefined enum value.";

        throw new ArgumentException (msg, "code");
      }

      if (_client) {
        if (code == CloseStatusCode.ServerError) {
          var msg = "ServerError cannot be used.";

          throw new ArgumentException (msg, "code");
        }
      }
      else {
        if (code == CloseStatusCode.MandatoryExtension) {
          var msg = "MandatoryExtension cannot be used.";

          throw new ArgumentException (msg, "code");
        }
      }

      if (reason.IsNullOrEmpty ()) {
        close ((ushort) code, String.Empty);

        return;
      }

      if (code == CloseStatusCode.NoStatus) {
        var msg = "NoStatus cannot be used.";

        throw new ArgumentException (msg, "code");
      }

      byte[] bytes;

      if (!reason.TryGetUTF8EncodedBytes (out bytes)) {
        var msg = "It could not be UTF-8-encoded.";

        throw new ArgumentException (msg, "reason");
      }

      if (bytes.Length > 123) {
        var msg = "Its size is greater than 123 bytes.";

        throw new ArgumentOutOfRangeException ("reason", msg);
      }

      close ((ushort) code, reason);
    }

    /// <summary>
    /// Closes the connection asynchronously.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   This method does not wait for the close to be complete.
    ///   </para>
    ///   <para>
    ///   This method does nothing if the current state of the interface is
    ///   Closing or Closed.
    ///   </para>
    /// </remarks>
    public void CloseAsync ()
    {
      closeAsync (1005, String.Empty);
    }

    /// <summary>
    /// Closes the connection asynchronously with the specified status code.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   This method does not wait for the close to be complete.
    ///   </para>
    ///   <para>
    ///   This method does nothing if the current state of the interface is
    ///   Closing or Closed.
    ///   </para>
    /// </remarks>
    /// <param name="code">
    ///   <para>
    ///   A <see cref="ushort"/> that specifies the status code indicating
    ///   the reason for the close.
    ///   </para>
    ///   <para>
    ///   The status codes are defined in
    ///   <see href="http://tools.ietf.org/html/rfc6455#section-7.4">
    ///   Section 7.4</see> of RFC 6455.
    ///   </para>
    /// </param>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="code"/> is 1011 (server error).
    ///   It cannot be used by a client.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="code"/> is 1010 (mandatory extension).
    ///   It cannot be used by a server.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="code"/> is less than 1000 or greater than 4999.
    /// </exception>
    public void CloseAsync (ushort code)
    {
      CloseAsync (code, String.Empty);
    }

    /// <summary>
    /// Closes the connection asynchronously with the specified status code.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   This method does not wait for the close to be complete.
    ///   </para>
    ///   <para>
    ///   This method does nothing if the current state of the interface is
    ///   Closing or Closed.
    ///   </para>
    /// </remarks>
    /// <param name="code">
    ///   <para>
    ///   One of the <see cref="CloseStatusCode"/> enum values.
    ///   </para>
    ///   <para>
    ///   It specifies the status code indicating the reason for the close.
    ///   </para>
    /// </param>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="code"/> is an undefined enum value.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="code"/> is <see cref="CloseStatusCode.ServerError"/>.
    ///   It cannot be used by a client.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="code"/> is <see cref="CloseStatusCode.MandatoryExtension"/>.
    ///   It cannot be used by a server.
    ///   </para>
    /// </exception>
    public void CloseAsync (CloseStatusCode code)
    {
      CloseAsync (code, String.Empty);
    }

    /// <summary>
    /// Closes the connection asynchronously with the specified status code and
    /// reason.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   This method does not wait for the close to be complete.
    ///   </para>
    ///   <para>
    ///   This method does nothing if the current state of the interface is
    ///   Closing or Closed.
    ///   </para>
    /// </remarks>
    /// <param name="code">
    ///   <para>
    ///   A <see cref="ushort"/> that specifies the status code indicating
    ///   the reason for the close.
    ///   </para>
    ///   <para>
    ///   The status codes are defined in
    ///   <see href="http://tools.ietf.org/html/rfc6455#section-7.4">
    ///   Section 7.4</see> of RFC 6455.
    ///   </para>
    /// </param>
    /// <param name="reason">
    ///   <para>
    ///   A <see cref="string"/> that specifies the reason for the close.
    ///   </para>
    ///   <para>
    ///   Its size must be 123 bytes or less in UTF-8.
    ///   </para>
    /// </param>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="code"/> is 1011 (server error).
    ///   It cannot be used by a client.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="code"/> is 1010 (mandatory extension).
    ///   It cannot be used by a server.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="code"/> is 1005 (no status) and
    ///   <paramref name="reason"/> is specified.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="reason"/> could not be UTF-8-encoded.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   <para>
    ///   <paramref name="code"/> is less than 1000 or greater than 4999.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   The size of <paramref name="reason"/> is greater than 123 bytes.
    ///   </para>
    /// </exception>
    public void CloseAsync (ushort code, string reason)
    {
      if (!code.IsCloseStatusCode ()) {
        var msg = "Less than 1000 or greater than 4999.";

        throw new ArgumentOutOfRangeException ("code", msg);
      }

      if (_client) {
        if (code == 1011) {
          var msg = "1011 cannot be used.";

          throw new ArgumentException (msg, "code");
        }
      }
      else {
        if (code == 1010) {
          var msg = "1010 cannot be used.";

          throw new ArgumentException (msg, "code");
        }
      }

      if (reason.IsNullOrEmpty ()) {
        closeAsync (code, String.Empty);

        return;
      }

      if (code == 1005) {
        var msg = "1005 cannot be used.";

        throw new ArgumentException (msg, "code");
      }

      byte[] bytes;

      if (!reason.TryGetUTF8EncodedBytes (out bytes)) {
        var msg = "It could not be UTF-8-encoded.";

        throw new ArgumentException (msg, "reason");
      }

      if (bytes.Length > 123) {
        var msg = "Its size is greater than 123 bytes.";

        throw new ArgumentOutOfRangeException ("reason", msg);
      }

      closeAsync (code, reason);
    }

    /// <summary>
    /// Closes the connection asynchronously with the specified status code and
    /// reason.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   This method does not wait for the close to be complete.
    ///   </para>
    ///   <para>
    ///   This method does nothing if the current state of the interface is
    ///   Closing or Closed.
    ///   </para>
    /// </remarks>
    /// <param name="code">
    ///   <para>
    ///   One of the <see cref="CloseStatusCode"/> enum values.
    ///   </para>
    ///   <para>
    ///   It specifies the status code indicating the reason for the close.
    ///   </para>
    /// </param>
    /// <param name="reason">
    ///   <para>
    ///   A <see cref="string"/> that specifies the reason for the close.
    ///   </para>
    ///   <para>
    ///   Its size must be 123 bytes or less in UTF-8.
    ///   </para>
    /// </param>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="code"/> is an undefined enum value.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="code"/> is <see cref="CloseStatusCode.ServerError"/>.
    ///   It cannot be used by a client.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="code"/> is <see cref="CloseStatusCode.MandatoryExtension"/>.
    ///   It cannot be used by a server.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="code"/> is <see cref="CloseStatusCode.NoStatus"/> and
    ///   <paramref name="reason"/> is specified.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="reason"/> could not be UTF-8-encoded.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The size of <paramref name="reason"/> is greater than 123 bytes.
    /// </exception>
    public void CloseAsync (CloseStatusCode code, string reason)
    {
      if (!code.IsDefined ()) {
        var msg = "An undefined enum value.";

        throw new ArgumentException (msg, "code");
      }

      if (_client) {
        if (code == CloseStatusCode.ServerError) {
          var msg = "ServerError cannot be used.";

          throw new ArgumentException (msg, "code");
        }
      }
      else {
        if (code == CloseStatusCode.MandatoryExtension) {
          var msg = "MandatoryExtension cannot be used.";

          throw new ArgumentException (msg, "code");
        }
      }

      if (reason.IsNullOrEmpty ()) {
        closeAsync ((ushort) code, String.Empty);

        return;
      }

      if (code == CloseStatusCode.NoStatus) {
        var msg = "NoStatus cannot be used.";

        throw new ArgumentException (msg, "code");
      }

      byte[] bytes;

      if (!reason.TryGetUTF8EncodedBytes (out bytes)) {
        var msg = "It could not be UTF-8-encoded.";

        throw new ArgumentException (msg, "reason");
      }

      if (bytes.Length > 123) {
        var msg = "Its size is greater than 123 bytes.";

        throw new ArgumentOutOfRangeException ("reason", msg);
      }

      closeAsync ((ushort) code, reason);
    }

    /// <summary>
    /// Establishes a connection.
    /// </summary>
    /// <remarks>
    /// This method does nothing if the current state of the interface is
    /// Connecting or Open.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    ///   <para>
    ///   The interface is not for the client.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   A series of reconnecting has failed.
    ///   </para>
    /// </exception>
    public void Connect ()
    {
      if (!_client) {
        var msg = "The interface is not for the client.";

        throw new InvalidOperationException (msg);
      }

      if (_retryCountForConnect >= _maxRetryCountForConnect) {
        var msg = "A series of reconnecting has failed.";

        throw new InvalidOperationException (msg);
      }

      var connected = connect ();

      if (!connected)
        return;

      open ();
    }

    /// <summary>
    /// Establishes a connection asynchronously.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   This method does not wait for the connect process to be complete.
    ///   </para>
    ///   <para>
    ///   This method does nothing if the current state of the interface is
    ///   Connecting or Open.
    ///   </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    ///   <para>
    ///   The interface is not for the client.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   A series of reconnecting has failed.
    ///   </para>
    /// </exception>
    public void ConnectAsync ()
    {
      if (!_client) {
        var msg = "The interface is not for the client.";

        throw new InvalidOperationException (msg);
      }

      if (_retryCountForConnect >= _maxRetryCountForConnect) {
        var msg = "A series of reconnecting has failed.";

        throw new InvalidOperationException (msg);
      }

      Func<bool> connector = connect;

      connector.BeginInvoke (
        ar => {
          var connected = connector.EndInvoke (ar);

          if (!connected)
            return;

          open ();
        },
        null
      );
    }

    /// <summary>
    /// Sends a ping to the remote endpoint.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the send has successfully done and a pong has been
    /// received within a time; otherwise, <c>false</c>.
    /// </returns>
    public bool Ping ()
    {
      return ping (_emptyBytes);
    }

    /// <summary>
    /// Sends a ping with the specified message to the remote endpoint.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the send has successfully done and a pong has been
    /// received within a time; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="message">
    ///   <para>
    ///   A <see cref="string"/> that specifies the message to send.
    ///   </para>
    ///   <para>
    ///   Its size must be 125 bytes or less in UTF-8.
    ///   </para>
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="message"/> could not be UTF-8-encoded.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The size of <paramref name="message"/> is greater than 125 bytes.
    /// </exception>
    public bool Ping (string message)
    {
      if (message.IsNullOrEmpty ())
        return ping (_emptyBytes);

      byte[] bytes;

      if (!message.TryGetUTF8EncodedBytes (out bytes)) {
        var msg = "It could not be UTF-8-encoded.";

        throw new ArgumentException (msg, "message");
      }

      if (bytes.Length > 125) {
        var msg = "Its size is greater than 125 bytes.";

        throw new ArgumentOutOfRangeException ("message", msg);
      }

      return ping (bytes);
    }

    /// <summary>
    /// Sends the specified data to the remote endpoint.
    /// </summary>
    /// <param name="data">
    /// An array of <see cref="byte"/> that specifies the binary data to send.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The current state of the interface is not Open.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="data"/> is <see langword="null"/>.
    /// </exception>
    public void Send (byte[] data)
    {
      if (_readyState != WebSocketState.Open) {
        var msg = "The current state of the interface is not Open.";

        throw new InvalidOperationException (msg);
      }

      if (data == null)
        throw new ArgumentNullException ("data");

      send (Opcode.Binary, new MemoryStream (data));
    }

    /// <summary>
    /// Sends the specified file to the remote endpoint.
    /// </summary>
    /// <param name="fileInfo">
    ///   <para>
    ///   A <see cref="FileInfo"/> that specifies the file to send.
    ///   </para>
    ///   <para>
    ///   The file is sent as the binary data.
    ///   </para>
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The current state of the interface is not Open.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="fileInfo"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   The file does not exist.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   The file could not be opened.
    ///   </para>
    /// </exception>
    public void Send (FileInfo fileInfo)
    {
      if (_readyState != WebSocketState.Open) {
        var msg = "The current state of the interface is not Open.";

        throw new InvalidOperationException (msg);
      }

      if (fileInfo == null)
        throw new ArgumentNullException ("fileInfo");

      if (!fileInfo.Exists) {
        var msg = "The file does not exist.";

        throw new ArgumentException (msg, "fileInfo");
      }

      FileStream stream;

      if (!fileInfo.TryOpenRead (out stream)) {
        var msg = "The file could not be opened.";

        throw new ArgumentException (msg, "fileInfo");
      }

      send (Opcode.Binary, stream);
    }

    /// <summary>
    /// Sends the specified data to the remote endpoint.
    /// </summary>
    /// <param name="data">
    /// A <see cref="string"/> that specifies the text data to send.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The current state of the interface is not Open.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="data"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="data"/> could not be UTF-8-encoded.
    /// </exception>
    public void Send (string data)
    {
      if (_readyState != WebSocketState.Open) {
        var msg = "The current state of the interface is not Open.";

        throw new InvalidOperationException (msg);
      }

      if (data == null)
        throw new ArgumentNullException ("data");

      byte[] bytes;

      if (!data.TryGetUTF8EncodedBytes (out bytes)) {
        var msg = "It could not be UTF-8-encoded.";

        throw new ArgumentException (msg, "data");
      }

      send (Opcode.Text, new MemoryStream (bytes));
    }

    /// <summary>
    /// Sends the data from the specified stream instance to the remote endpoint.
    /// </summary>
    /// <param name="stream">
    ///   <para>
    ///   A <see cref="Stream"/> instance from which to read the data to send.
    ///   </para>
    ///   <para>
    ///   The data is sent as the binary data.
    ///   </para>
    /// </param>
    /// <param name="length">
    /// An <see cref="int"/> that specifies the number of bytes to send.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The current state of the interface is not Open.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="stream"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="stream"/> cannot be read.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="length"/> is less than 1.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   No data could be read from <paramref name="stream"/>.
    ///   </para>
    /// </exception>
    public void Send (Stream stream, int length)
    {
      if (_readyState != WebSocketState.Open) {
        var msg = "The current state of the interface is not Open.";

        throw new InvalidOperationException (msg);
      }

      if (stream == null)
        throw new ArgumentNullException ("stream");

      if (!stream.CanRead) {
        var msg = "It cannot be read.";

        throw new ArgumentException (msg, "stream");
      }

      if (length < 1) {
        var msg = "Less than 1.";

        throw new ArgumentException (msg, "length");
      }

      var bytes = stream.ReadBytes (length);
      var len = bytes.Length;

      if (len == 0) {
        var msg = "No data could be read from it.";

        throw new ArgumentException (msg, "stream");
      }

      if (len < length) {
        var fmt = "Only {0} byte(s) of data could be read from the stream.";
        var msg = String.Format (fmt, len);

        _log.Warn (msg);
      }

      send (Opcode.Binary, new MemoryStream (bytes));
    }

    /// <summary>
    /// Sends the specified data to the remote endpoint asynchronously.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the send to be complete.
    /// </remarks>
    /// <param name="data">
    /// An array of <see cref="byte"/> that specifies the binary data to send.
    /// </param>
    /// <param name="completed">
    ///   <para>
    ///   An <see cref="T:System.Action{bool}"/> delegate.
    ///   </para>
    ///   <para>
    ///   The delegate invokes the method called when the send is complete.
    ///   </para>
    ///   <para>
    ///   The <see cref="bool"/> parameter passed to the method is <c>true</c>
    ///   if the send has successfully done; otherwise, <c>false</c>.
    ///   </para>
    ///   <para>
    ///   <see langword="null"/> if not necessary.
    ///   </para>
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The current state of the interface is not Open.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="data"/> is <see langword="null"/>.
    /// </exception>
    public void SendAsync (byte[] data, Action<bool> completed)
    {
      if (_readyState != WebSocketState.Open) {
        var msg = "The current state of the interface is not Open.";

        throw new InvalidOperationException (msg);
      }

      if (data == null)
        throw new ArgumentNullException ("data");

      sendAsync (Opcode.Binary, new MemoryStream (data), completed);
    }

    /// <summary>
    /// Sends the specified file to the remote endpoint asynchronously.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the send to be complete.
    /// </remarks>
    /// <param name="fileInfo">
    ///   <para>
    ///   A <see cref="FileInfo"/> that specifies the file to send.
    ///   </para>
    ///   <para>
    ///   The file is sent as the binary data.
    ///   </para>
    /// </param>
    /// <param name="completed">
    ///   <para>
    ///   An <see cref="T:System.Action{bool}"/> delegate.
    ///   </para>
    ///   <para>
    ///   The delegate invokes the method called when the send is complete.
    ///   </para>
    ///   <para>
    ///   The <see cref="bool"/> parameter passed to the method is <c>true</c>
    ///   if the send has successfully done; otherwise, <c>false</c>.
    ///   </para>
    ///   <para>
    ///   <see langword="null"/> if not necessary.
    ///   </para>
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The current state of the interface is not Open.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="fileInfo"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   The file does not exist.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   The file could not be opened.
    ///   </para>
    /// </exception>
    public void SendAsync (FileInfo fileInfo, Action<bool> completed)
    {
      if (_readyState != WebSocketState.Open) {
        var msg = "The current state of the interface is not Open.";

        throw new InvalidOperationException (msg);
      }

      if (fileInfo == null)
        throw new ArgumentNullException ("fileInfo");

      if (!fileInfo.Exists) {
        var msg = "The file does not exist.";

        throw new ArgumentException (msg, "fileInfo");
      }

      FileStream stream;

      if (!fileInfo.TryOpenRead (out stream)) {
        var msg = "The file could not be opened.";

        throw new ArgumentException (msg, "fileInfo");
      }

      sendAsync (Opcode.Binary, stream, completed);
    }

    /// <summary>
    /// Sends the specified data to the remote endpoint asynchronously.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the send to be complete.
    /// </remarks>
    /// <param name="data">
    /// A <see cref="string"/> that specifies the text data to send.
    /// </param>
    /// <param name="completed">
    ///   <para>
    ///   An <see cref="T:System.Action{bool}"/> delegate.
    ///   </para>
    ///   <para>
    ///   The delegate invokes the method called when the send is complete.
    ///   </para>
    ///   <para>
    ///   The <see cref="bool"/> parameter passed to the method is <c>true</c>
    ///   if the send has successfully done; otherwise, <c>false</c>.
    ///   </para>
    ///   <para>
    ///   <see langword="null"/> if not necessary.
    ///   </para>
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The current state of the interface is not Open.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="data"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="data"/> could not be UTF-8-encoded.
    /// </exception>
    public void SendAsync (string data, Action<bool> completed)
    {
      if (_readyState != WebSocketState.Open) {
        var msg = "The current state of the interface is not Open.";

        throw new InvalidOperationException (msg);
      }

      if (data == null)
        throw new ArgumentNullException ("data");

      byte[] bytes;

      if (!data.TryGetUTF8EncodedBytes (out bytes)) {
        var msg = "It could not be UTF-8-encoded.";

        throw new ArgumentException (msg, "data");
      }

      sendAsync (Opcode.Text, new MemoryStream (bytes), completed);
    }

    /// <summary>
    /// Sends the data from the specified stream instance to the remote
    /// endpoint asynchronously.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the send to be complete.
    /// </remarks>
    /// <param name="stream">
    ///   <para>
    ///   A <see cref="Stream"/> instance from which to read the data to send.
    ///   </para>
    ///   <para>
    ///   The data is sent as the binary data.
    ///   </para>
    /// </param>
    /// <param name="length">
    /// An <see cref="int"/> that specifies the number of bytes to send.
    /// </param>
    /// <param name="completed">
    ///   <para>
    ///   An <see cref="T:System.Action{bool}"/> delegate.
    ///   </para>
    ///   <para>
    ///   The delegate invokes the method called when the send is complete.
    ///   </para>
    ///   <para>
    ///   The <see cref="bool"/> parameter passed to the method is <c>true</c>
    ///   if the send has successfully done; otherwise, <c>false</c>.
    ///   </para>
    ///   <para>
    ///   <see langword="null"/> if not necessary.
    ///   </para>
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The current state of the interface is not Open.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="stream"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="stream"/> cannot be read.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="length"/> is less than 1.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   No data could be read from <paramref name="stream"/>.
    ///   </para>
    /// </exception>
    public void SendAsync (Stream stream, int length, Action<bool> completed)
    {
      if (_readyState != WebSocketState.Open) {
        var msg = "The current state of the interface is not Open.";

        throw new InvalidOperationException (msg);
      }

      if (stream == null)
        throw new ArgumentNullException ("stream");

      if (!stream.CanRead) {
        var msg = "It cannot be read.";

        throw new ArgumentException (msg, "stream");
      }

      if (length < 1) {
        var msg = "Less than 1.";

        throw new ArgumentException (msg, "length");
      }

      var bytes = stream.ReadBytes (length);
      var len = bytes.Length;

      if (len == 0) {
        var msg = "No data could be read from it.";

        throw new ArgumentException (msg, "stream");
      }

      if (len < length) {
        var fmt = "Only {0} byte(s) of data could be read from the stream.";
        var msg = String.Format (fmt, len);

        _log.Warn (msg);
      }

      sendAsync (Opcode.Binary, new MemoryStream (bytes), completed);
    }

    /// <summary>
    /// Sets an HTTP cookie to send with the handshake request.
    /// </summary>
    /// <remarks>
    /// This method works if the current state of the interface is
    /// New or Closed.
    /// </remarks>
    /// <param name="cookie">
    /// A <see cref="Cookie"/> that specifies the cookie to send.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The interface is not for the client.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="cookie"/> is <see langword="null"/>.
    /// </exception>
    public void SetCookie (Cookie cookie)
    {
      if (!_client) {
        var msg = "The interface is not for the client.";

        throw new InvalidOperationException (msg);
      }

      if (cookie == null)
        throw new ArgumentNullException ("cookie");

      lock (_forState) {
        if (!canSet ())
          return;

        lock (_cookies.SyncRoot)
          _cookies.SetOrRemove (cookie);
      }
    }

    /// <summary>
    /// Sets the credentials for the HTTP authentication (Basic/Digest).
    /// </summary>
    /// <remarks>
    /// This method works if the current state of the interface is
    /// New or Closed.
    /// </remarks>
    /// <param name="username">
    ///   <para>
    ///   A <see cref="string"/> that specifies the username associated
    ///   with the credentials.
    ///   </para>
    ///   <para>
    ///   <see langword="null"/> or an empty string if initializes
    ///   the credentials.
    ///   </para>
    /// </param>
    /// <param name="password">
    ///   <para>
    ///   A <see cref="string"/> that specifies the password for the
    ///   username associated with the credentials.
    ///   </para>
    ///   <para>
    ///   <see langword="null"/> or an empty string if not necessary.
    ///   </para>
    /// </param>
    /// <param name="preAuth">
    /// A <see cref="bool"/>: <c>true</c> if sends the credentials for
    /// the Basic authentication in advance with the first handshake
    /// request; otherwise, <c>false</c>.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The interface is not for the client.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="username"/> contains an invalid character.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="password"/> contains an invalid character.
    ///   </para>
    /// </exception>
    public void SetCredentials (string username, string password, bool preAuth)
    {
      if (!_client) {
        var msg = "The interface is not for the client.";

        throw new InvalidOperationException (msg);
      }

      if (!username.IsNullOrEmpty ()) {
        if (username.Contains (':') || !username.IsText ()) {
          var msg = "It contains an invalid character.";

          throw new ArgumentException (msg, "username");
        }
      }

      if (!password.IsNullOrEmpty ()) {
        if (!password.IsText ()) {
          var msg = "It contains an invalid character.";

          throw new ArgumentException (msg, "password");
        }
      }

      lock (_forState) {
        if (!canSet ())
          return;

        if (username.IsNullOrEmpty ()) {
          _credentials = null;
          _preAuth = false;

          return;
        }

        _credentials = new NetworkCredential (
                         username, password, _uri.PathAndQuery
                       );

        _preAuth = preAuth;
      }
    }

    /// <summary>
    /// Sets the URL of the HTTP proxy server through which to connect and
    /// the credentials for the HTTP proxy authentication (Basic/Digest).
    /// </summary>
    /// <remarks>
    /// This method works if the current state of the interface is
    /// New or Closed.
    /// </remarks>
    /// <param name="url">
    ///   <para>
    ///   A <see cref="string"/> that specifies the URL of the proxy
    ///   server through which to connect.
    ///   </para>
    ///   <para>
    ///   The syntax is http://&lt;host&gt;[:&lt;port&gt;].
    ///   </para>
    ///   <para>
    ///   <see langword="null"/> or an empty string if initializes
    ///   the URL and the credentials.
    ///   </para>
    /// </param>
    /// <param name="username">
    ///   <para>
    ///   A <see cref="string"/> that specifies the username associated
    ///   with the credentials.
    ///   </para>
    ///   <para>
    ///   <see langword="null"/> or an empty string if the credentials
    ///   are not necessary.
    ///   </para>
    /// </param>
    /// <param name="password">
    ///   <para>
    ///   A <see cref="string"/> that specifies the password for the
    ///   username associated with the credentials.
    ///   </para>
    ///   <para>
    ///   <see langword="null"/> or an empty string if not necessary.
    ///   </para>
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The interface is not for the client.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="url"/> is not an absolute URI string.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   The scheme of <paramref name="url"/> is not http.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="url"/> includes the path segments.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="username"/> contains an invalid character.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="password"/> contains an invalid character.
    ///   </para>
    /// </exception>
    public void SetProxy (string url, string username, string password)
    {
      if (!_client) {
        var msg = "The interface is not for the client.";

        throw new InvalidOperationException (msg);
      }

      Uri uri = null;

      if (!url.IsNullOrEmpty ()) {
        if (!Uri.TryCreate (url, UriKind.Absolute, out uri)) {
          var msg = "Not an absolute URI string.";

          throw new ArgumentException (msg, "url");
        }

        if (uri.Scheme != "http") {
          var msg = "The scheme part is not http.";

          throw new ArgumentException (msg, "url");
        }

        if (uri.Segments.Length > 1) {
          var msg = "It includes the path segments.";

          throw new ArgumentException (msg, "url");
        }
      }

      if (!username.IsNullOrEmpty ()) {
        if (username.Contains (':') || !username.IsText ()) {
          var msg = "It contains an invalid character.";

          throw new ArgumentException (msg, "username");
        }
      }

      if (!password.IsNullOrEmpty ()) {
        if (!password.IsText ()) {
          var msg = "It contains an invalid character.";

          throw new ArgumentException (msg, "password");
        }
      }

      lock (_forState) {
        if (!canSet ())
          return;

        if (url.IsNullOrEmpty ()) {
          _proxyUri = null;
          _proxyCredentials = null;

          return;
        }

        _proxyUri = uri;
        _proxyCredentials = !username.IsNullOrEmpty ()
                            ? new NetworkCredential (
                                username,
                                password,
                                String.Format (
                                  "{0}:{1}", _uri.DnsSafeHost, _uri.Port
                                )
                              )
                            : null;
      }
    }

    #endregion

    #region Explicit Interface Implementations

    /// <summary>
    /// Closes the connection and releases all associated resources.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   This method closes the connection with close status 1001 (going away).
    ///   </para>
    ///   <para>
    ///   This method does nothing if the current state of the interface is
    ///   Closing or Closed.
    ///   </para>
    /// </remarks>
    void IDisposable.Dispose ()
    {
      close (1001, String.Empty);
    }

    #endregion
  }
}
