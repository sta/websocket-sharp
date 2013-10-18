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

namespace WebSocketSharp
{
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

    private const string _guid    = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    private const string _version = "13";

    #endregion

    #region Private Fields

    private string                  _base64key;
    private RemoteCertificateValidationCallback
                                    _certValidationCallback;
    private bool                    _client;
    private Action                  _closeContext;
    private CompressionMethod       _compression;
    private WebSocketContext        _context;
    private CookieCollection        _cookies;
    private Func<CookieCollection, CookieCollection, bool>
                                    _cookiesValidation;
    private WsCredential            _credentials;
    private string                  _extensions;
    private AutoResetEvent          _exitReceiving;
    private object                  _forClose;
    private object                  _forSend;
    private volatile Logger         _logger;
    private string                  _origin;
    private bool                    _preAuth;
    private string                  _protocol;
    private string                  _protocols;
    private volatile WebSocketState _readyState;
    private AutoResetEvent          _receivePong;
    private bool                    _secure;
    private WsStream                _stream;
    private TcpClient               _tcpClient;
    private Uri                     _uri;

    #endregion

    #region Internal Const Fields

    internal const int FragmentLength = 1016; // Max value is int.MaxValue - 14.

    #endregion

    #region Private Constructors

    private WebSocket ()
    {
      _compression = CompressionMethod.NONE;
      _cookies = new CookieCollection ();
      _extensions = String.Empty;
      _forClose = new object ();
      _forSend = new object ();
      _origin = String.Empty;
      _preAuth = false;
      _protocol = String.Empty;
      _readyState = WebSocketState.CONNECTING;
    }

    #endregion

    #region Internal Constructors

    internal WebSocket (HttpListenerWebSocketContext context)
      : this ()
    {
      _stream = context.Stream;
      _closeContext = () => context.Close ();
      init (context);
    }

    internal WebSocket (TcpListenerWebSocketContext context)
      : this ()
    {
      _stream = context.Stream;
      _closeContext = () => context.Close ();
      init (context);
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
    /// <paramref name="url"/> is invalid.
    /// </exception>
    public WebSocket (string url, params string[] protocols)
      : this ()
    {
      if (url == null)
        throw new ArgumentNullException ("url");

      string msg;
      if (!url.TryCreateWebSocketUri (out _uri, out msg))
        throw new ArgumentException (msg, "url");

      _protocols = protocols.ToString (", ");
      _secure = _uri.Scheme == "wss" ? true : false;
      _client = true;
      _base64key = createBase64Key ();
      _logger = new Logger ();
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
    /// <paramref name="url"/> is invalid.
    /// </exception>
    public WebSocket (
      string                         url,
      EventHandler                   onOpen,
      EventHandler<MessageEventArgs> onMessage,
      EventHandler<ErrorEventArgs>   onError,
      EventHandler<CloseEventArgs>   onClose,
      params string []               protocols)
      : this (url, protocols)
    {
      OnOpen    = onOpen;
      OnMessage = onMessage;
      OnError   = onError;
      OnClose   = onClose;

      Connect ();
    }

    #endregion

    #region Internal Properties

    internal Func<CookieCollection, CookieCollection, bool> CookiesValidation {
      get {
        return _cookiesValidation;
      }

      set {
        _cookiesValidation = value;
      }
    }

    internal bool IsOpened {
      get {
        return _readyState == WebSocketState.OPEN || _readyState == WebSocketState.CLOSING;
      }
    }

    #endregion

    #region Public Properties

    public IEnumerable<KeyValuePair<string,string>> CustomHeaders { get; set; }

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
          var msg = "A WebSocket connection has already been established.";
          _logger.Error (msg);
          error (msg);

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
          msg = "A WebSocket connection has already been established.";
        }
        else if (value.IsNullOrEmpty ())
        {
          _origin = String.Empty;
          return;
        }
        else
        {
          var origin = new Uri (value);
          if (!origin.IsAbsoluteUri || origin.Segments.Length > 1)
            msg = "The syntax of value of Origin must be '<scheme>://<host>[:<port>]'.";
        }

        if (msg != null)
        {
          _logger.Error (msg);
          error (msg);

          return;
        }

        _origin = value.TrimEnd ('/');
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
    /// One of the <see cref="WebSocketState"/> values.
    /// The default value is <see cref="WebSocketState.CONNECTING"/>.
    /// </value>
    public WebSocketState ReadyState {
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
        if (_readyState == WebSocketState.CONNECTING && !_client)
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
      _logger.Debug (String.Format ("A WebSocket connection request from {0}:\n{1}",
        _context.UserEndPoint, _context));

      if (!validateConnectionRequest (_context))
      {
        var msg = "Invalid WebSocket connection request.";
        _logger.Error (msg);
        error (msg);
        Close (HttpStatusCode.BadRequest);

        return false;
      }

      _base64key = _context.SecWebSocketKey;

      if (_protocol.Length > 0 && !_context.Headers.Contains ("Sec-WebSocket-Protocol", _protocol))
        _protocol = String.Empty;

      var extensions = _context.Headers ["Sec-WebSocket-Extensions"];
      if (extensions != null && extensions.Length > 0)
        processRequestedExtensions (extensions);

      return send (createHandshakeResponse ());
    }

    private void close (CloseStatusCode code, string reason, bool wait)
    {
      close (new PayloadData (((ushort) code).Append (reason)), !code.IsReserved (), wait);
    }

    private void close (PayloadData payload, bool send, bool wait)
    {
      lock (_forClose)
      {
        if (_readyState == WebSocketState.CLOSING || _readyState == WebSocketState.CLOSED)
          return;

        _logger.Trace ("Start closing handshake.");

        _readyState = WebSocketState.CLOSING;
      }

      var args = new CloseEventArgs (payload);
      args.WasClean = _client
                    ? close (
                        send ? WsFrame.CreateCloseFrame (Mask.MASK, payload).ToByteArray () : null,
                        wait ? 5000 : 0,
                        closeClientResources)
                    : close (
                        send ? WsFrame.CreateCloseFrame (Mask.UNMASK, payload).ToByteArray () : null,
                        wait ? 1000 : 0,
                        closeServerResources);

      _readyState = WebSocketState.CLOSED;

      _logger.Trace ("End closing handshake.");
      OnClose.Emit (this, args);
    }

    private bool close (byte [] frameAsBytes, int timeOut, Func<bool> release)
    {
      var sent = frameAsBytes != null && _stream.Write (frameAsBytes);
      var received = timeOut == 0 || (sent && _exitReceiving.WaitOne (timeOut));
      var released = release ();
      var result = sent && received && released;
      _logger.Debug (String.Format (
        "Was clean?: {0}\nsent: {1} received: {2} released: {3}", result, sent, received, released));

      return result;
    }

    // As client
    private bool closeClientResources ()
    {
      try {
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

        return true;
      }
      catch (Exception ex) {
        _logger.Fatal (ex.ToString ());
        error ("An exception has occured.");

        return false;
      }
    }

    // As server
    private bool closeServerResources ()
    {
      try {
        if (_closeContext != null)
          _closeContext ();

        _stream = null;
        _context = null;

        return true;
      }
      catch (Exception ex) {
        _logger.Fatal (ex.ToString ());
        error ("An exception has occured.");

        return false;
      }
    }

    private bool concatenateFragments (Stream dest)
    {
      while (true)
      {
        var frame = _stream.ReadFrame ();
        if (frame == null)
          return processAbnormalFrame ();

        if (frame.IsCompressed)
          return processIncorrectFrame ();

        // MORE & CONT
        if (!frame.IsFinal && frame.IsContinuation)
        {
          dest.WriteBytes (frame.PayloadData.ApplicationData);
          continue;
        }

        // FINAL & CONT
        if (frame.IsFinal && frame.IsContinuation)
        {
          dest.WriteBytes (frame.PayloadData.ApplicationData);
          break;
        }

        // FINAL & PING
        if (frame.IsFinal && frame.IsPing)
        {
          processPingFrame (frame);
          continue;
        }

        // FINAL & PONG
        if (frame.IsFinal && frame.IsPong)
        {
          processPongFrame ();
          continue;
        }

        // FINAL & CLOSE
        if (frame.IsFinal && frame.IsClose)
          return processCloseFrame (frame);

        // ?
        return processIncorrectFrame ();
      }

      return true;
    }

    private bool connect ()
    {
      return _client
             ? doHandshake ()
             : acceptHandshake ();
    }

    // As client
    private static string createBase64Key ()
    {
      var src = new byte [16];
      var rand = new Random ();
      rand.NextBytes (src);

      return Convert.ToBase64String (src);
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

      if (CustomHeaders != null)
      {
        foreach (var header in CustomHeaders)
        {
          req.AddHeader(header.Key, header.Value);
        }
      }

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

      if (_protocol.Length > 0)
        res.AddHeader ("Sec-WebSocket-Protocol", _protocol);

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
      if (_compression != CompressionMethod.NONE)
        extensions.Append (_compression.ToCompressionExtension ());

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
      var res = sendHandshakeRequest ();
      var msg = res.IsUnauthorized
              ? String.Format ("An HTTP {0} authorization is required.", res.AuthChallenge.Scheme)
              : !validateConnectionResponse (res)
                ? "Invalid response to this WebSocket connection request."
                : String.Empty;

      if (msg.Length > 0)
      {
        _logger.Error (msg);
        error (msg);
        Close (CloseStatusCode.ABNORMAL);

        return false;
      }

      var protocol = res.Headers ["Sec-WebSocket-Protocol"];
      if (protocol != null && protocol.Length > 0)
        _protocol = protocol;

      processRespondedExtensions (res.Headers ["Sec-WebSocket-Extensions"]);

      var cookies = res.Cookies;
      if (cookies.Count > 0)
        _cookies.SetOrRemove (cookies);

      return true;
    }

    private void error (string message)
    {
      OnError.Emit (this, new ErrorEventArgs (message));
    }

    // As server
    private void init (WebSocketContext context)
    {
      _context = context;
      _uri = context.Path.ToUri ();
      _secure = context.IsSecureConnection;
      _client = false;
    }

    private void open ()
    {
      _readyState = WebSocketState.OPEN;

      OnOpen.Emit (this, EventArgs.Empty);
      startReceiving ();
    }

    private bool processAbnormalFrame ()
    {
      var code = CloseStatusCode.ABNORMAL;
      close (code, code.GetMessage (), false);

      return false;
    }

    private bool processCloseFrame (WsFrame frame)
    {
      var payload = frame.PayloadData;
      close (payload, !payload.ContainsReservedCloseStatusCode, false);

      return false;
    }

    private bool processDataFrame (WsFrame frame)
    {
      var args = frame.IsCompressed
               ? new MessageEventArgs (
                   frame.Opcode, frame.PayloadData.ApplicationData.Decompress (_compression))
               : new MessageEventArgs (frame.Opcode, frame.PayloadData);

      OnMessage.Emit (this, args);
      return true;
    }

    private bool processFragmentedFrame (WsFrame frame)
    {
      return frame.IsContinuation // Not first fragment
             ? true
             : frame.IsData
               ? processFragments (frame)
               : processIncorrectFrame ();
    }

    private bool processFragments (WsFrame first)
    {
      using (var concatenated = new MemoryStream ())
      {
        concatenated.WriteBytes (first.PayloadData.ApplicationData);
        if (!concatenateFragments (concatenated))
          return false;

        byte [] data;
        if (_compression != CompressionMethod.NONE)
        {
          data = concatenated.DecompressToArray (_compression);
        }
        else
        {
          concatenated.Close ();
          data = concatenated.ToArray ();
        }

        OnMessage.Emit (this, new MessageEventArgs (first.Opcode, data));
        return true;
      }
    }

    private bool processFrame (WsFrame frame)
    {
      return frame == null
             ? processAbnormalFrame ()
             : (!frame.IsData && frame.IsCompressed) ||
               (frame.IsCompressed && _compression == CompressionMethod.NONE)
               ? processIncorrectFrame ()
               : frame.IsFragmented
                 ? processFragmentedFrame (frame)
                 : frame.IsData
                   ? processDataFrame (frame)
                   : frame.IsPing
                     ? processPingFrame (frame)
                     : frame.IsPong
                       ? processPongFrame ()
                       : frame.IsClose
                         ? processCloseFrame (frame)
                         : processIncorrectFrame ();
    }

    private bool processIncorrectFrame ()
    {
      close (CloseStatusCode.INCORRECT_DATA, null, false);
      return false;
    }

    private bool processPingFrame (WsFrame frame)
    {
      if (send (WsFrame.CreatePongFrame (_client ? Mask.MASK : Mask.UNMASK, frame.PayloadData)))
        _logger.Trace ("Returned Pong.");

      return true;
    }

    private bool processPongFrame ()
    {
      _receivePong.Set ();
      _logger.Trace ("Received Pong.");

      return true;
    }

    // As server
    private void processRequestedExtensions (string extensions)
    {
      var comp = false;
      var buffer = new List<string> ();
      foreach (var e in extensions.SplitHeaderValue (','))
      {
        var extension = e.Trim ();
        var tmp = extension.RemovePrefix ("x-webkit-");
        if (!comp && tmp.IsCompressionExtension ())
        {
          var method = tmp.ToCompressionMethod ();
          if (method != CompressionMethod.NONE)
          {
            _compression = method;
            comp = true;
            buffer.Add (extension);
          }
        }
      }

      if (buffer.Count > 0)
        _extensions = buffer.ToArray ().ToString (", ");
    }

    // As client
    private void processRespondedExtensions (string extensions)
    {
      var comp = _compression != CompressionMethod.NONE ? true : false;
      var hasComp = false;
      if (extensions != null && extensions.Length > 0)
      {
        foreach (var e in extensions.SplitHeaderValue (','))
        {
          var extension = e.Trim ();
          if (comp && !hasComp && extension.Equals (_compression))
            hasComp = true;
        }

        _extensions = extensions;
      }

      if (comp && !hasComp)
        _compression = CompressionMethod.NONE;
    }

    // As client
    private HandshakeResponse receiveHandshakeResponse ()
    {
      var res = HandshakeResponse.Parse (_stream.ReadHandshake ());
      _logger.Debug ("A response to this WebSocket connection request:\n" + res.ToString ());

      return res;
    }

    private bool send (byte [] frameAsBytes)
    {
      if (_readyState != WebSocketState.OPEN)
      {
        var msg = "A WebSocket connection isn't established or has been closed.";
        _logger.Error (msg);
        error (msg);

        return false;
      }

      return _stream.Write (frameAsBytes);
    }

    // As client
    private void send (HandshakeRequest request)
    {
      _logger.Debug (String.Format (
        "A WebSocket connection request to {0}:\n{1}", _uri, request));
      _stream.WriteHandshake (request);
    }

    // As server
    private bool send (HandshakeResponse response)
    {
      _logger.Debug ("A response to a WebSocket connection request:\n" + response.ToString ());
      return _stream.WriteHandshake (response);
    }

    private bool send (WsFrame frame)
    {
      if (_readyState != WebSocketState.OPEN)
      {
        var msg = "A WebSocket connection isn't established or has been closed.";
        _logger.Error (msg);
        error (msg);

        return false;
      }

      return _stream.Write (frame.ToByteArray ());
    }

    private bool send (Opcode opcode, byte [] data)
    {
      lock (_forSend)
      {
        var sent = false;
        try {
          var compressed = false;
          if (_compression != CompressionMethod.NONE)
          {
            data = data.Compress (_compression);
            compressed = true;
          }

          sent = send (WsFrame.CreateFrame (
            Fin.FINAL, opcode, _client ? Mask.MASK : Mask.UNMASK, data, compressed));
        }
        catch (Exception ex) {
          _logger.Fatal (ex.ToString ());
          error ("An exception has occured.");
        }

        return sent;
      }
    }

    private bool send (Opcode opcode, Stream stream)
    {
      lock (_forSend)
      {
        var sent = false;

        var src = stream;
        var compressed = false;
        try {
          if (_compression != CompressionMethod.NONE)
          {
            stream = stream.Compress (_compression);
            compressed = true;
          }

          sent = sendFragmented (opcode, stream, _client ? Mask.MASK : Mask.UNMASK, compressed);
        }
        catch (Exception ex) {
          _logger.Fatal (ex.ToString ());
          error ("An exception has occured.");
        }
        finally {
          if (compressed)
            stream.Dispose ();

          src.Dispose ();
        }

        return sent;
      }
    }

    private void send (Opcode opcode, byte [] data, Action<bool> completed)
    {
      Func<Opcode, byte [], bool> sender = send;
      AsyncCallback callback = ar =>
      {
        try {
          var sent = sender.EndInvoke (ar);
          if (completed != null)
            completed (sent);
        }
        catch (Exception ex)
        {
          _logger.Fatal (ex.ToString ());
          error ("An exception has occured.");
        }
      };

      sender.BeginInvoke (opcode, data, callback, null);
    }

    private void send (Opcode opcode, Stream stream, Action<bool> completed)
    {
      Func<Opcode, Stream, bool> sender = send;
      AsyncCallback callback = ar =>
      {
        try {
          var sent = sender.EndInvoke (ar);
          if (completed != null)
            completed (sent);
        }
        catch (Exception ex)
        {
          _logger.Fatal (ex.ToString ());
          error ("An exception has occured.");
        }
      };

      sender.BeginInvoke (opcode, stream, callback, null);
    }

    private bool sendFragmented (Opcode opcode, Stream stream, Mask mask, bool compressed)
    {
      var len = stream.Length;
      if (sendFragmented (opcode, stream, len, mask, compressed) == len)
        return true;

      var msg = "Sending fragmented data is interrupted.";
      _logger.Error (msg);
      error (msg);
      close (CloseStatusCode.ABNORMAL, msg, false);

      return false;
    }

    private long sendFragmented (
      Opcode opcode, Stream stream, long length, Mask mask, bool compressed)
    {
      var quo = length / FragmentLength;
      var rem = (int) (length % FragmentLength);
      var count = rem == 0 ? quo - 2 : quo - 1;

      long sentLen = 0;
      int readLen = 0;
      byte [] buffer = null;

      // Not fragmented
      if (quo == 0)
      {
        buffer = new byte [rem];
        readLen = stream.Read (buffer, 0, rem);
        if (readLen == rem &&
            send (WsFrame.CreateFrame (Fin.FINAL, opcode, mask, buffer, compressed)))
          sentLen = readLen;

        return sentLen;
      }

      buffer = new byte [FragmentLength];

      // First
      readLen = stream.Read (buffer, 0, FragmentLength);
      if (readLen == FragmentLength &&
          send (WsFrame.CreateFrame (Fin.MORE, opcode, mask, buffer, compressed)))
        sentLen = readLen;
      else
        return sentLen;

      // Mid
      for (long i = 0; i < count; i++)
      {
        readLen = stream.Read (buffer, 0, FragmentLength);
        if (readLen == FragmentLength &&
            send (WsFrame.CreateFrame (Fin.MORE, Opcode.CONT, mask, buffer, compressed)))
          sentLen += readLen;
        else
          return sentLen;
      }

      // Final
      var tmpLen = FragmentLength;
      if (rem != 0)
        buffer = new byte [tmpLen = rem];

      readLen = stream.Read (buffer, 0, tmpLen);
      if (readLen == tmpLen &&
          send (WsFrame.CreateFrame (Fin.FINAL, Opcode.CONT, mask, buffer, compressed)))
        sentLen += readLen;

      return sentLen;
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

      Action receive = null;
      receive = () => _stream.ReadFrameAsync (
        frame =>
        {
          if (processFrame(frame))
            receive();
          else
            _exitReceiving.Set ();
        },
        ex =>
        {
          _logger.Fatal (ex.ToString ());
          error ("An exception has occured.");
          if (ex.GetType () == typeof (WebSocketException))
          {
            var wsex = (WebSocketException) ex;
            close (wsex.Code, wsex.Message, false);
          }
          else
            close (CloseStatusCode.ABNORMAL, null, false);
        });

      receive ();
    }

    // As server
    private bool validateConnectionRequest (WebSocketContext context)
    {
      string version;
      return context.IsWebSocketRequest &&
             validateHostHeader (context.Host) &&
             !context.SecWebSocketKey.IsNullOrEmpty () &&
             ((version = context.SecWebSocketVersion) != null && version == _version) &&
             validateCookies (context.CookieCollection, _cookies);
    }

    // As client
    private bool validateConnectionResponse (HandshakeResponse response)
    {
      string accept, version;
      return response.IsWebSocketResponse &&
             ((accept = response.Headers ["Sec-WebSocket-Accept"]) != null && accept == createResponseKey ()) &&
             ((version = response.Headers ["Sec-WebSocket-Version"]) == null || version == _version);
    }

    // As server
    private bool validateCookies (CookieCollection request, CookieCollection response)
    {
      return _cookiesValidation != null
             ? _cookiesValidation (request, response)
             : true;
    }

    // As server
    private bool validateHostHeader (string value)
    {
      if (value == null || value.Length == 0)
        return false;

      if (!_uri.IsAbsoluteUri)
        return true;

      var i = value.IndexOf (':');
      var host = i > 0 ? value.Substring (0, i) : value;
      var type = Uri.CheckHostName (host);

      return type != UriHostNameType.Dns ||
             Uri.CheckHostName (_uri.DnsSafeHost) != UriHostNameType.Dns ||
             host == _uri.DnsSafeHost;
    }

    #endregion

    #region Internal Methods

    // As server
    internal void Close (HttpStatusCode code)
    {
      _readyState = WebSocketState.CLOSING;

      send (createHandshakeResponse (code));
      closeServerResources ();
      
      _readyState = WebSocketState.CLOSED;
    }

    // As server
    internal void Close (CloseEventArgs args, byte [] frameAsBytes, int waitTimeOut)
    {
      lock (_forClose)
      {
        if (_readyState == WebSocketState.CLOSING || _readyState == WebSocketState.CLOSED)
          return;

        _readyState = WebSocketState.CLOSING;
      }

      args.WasClean = close (frameAsBytes, waitTimeOut, closeServerResources);

      _readyState = WebSocketState.CLOSED;

      OnClose.Emit (this, args);
    }

    internal bool Ping (byte [] frameAsBytes, int timeOut)
    {
      return send (frameAsBytes) &&
             _receivePong.WaitOne (timeOut);
    }

    // As server, used to broadcast
    internal void Send (Opcode opcode, byte [] data, Dictionary<CompressionMethod, byte []> cache)
    {
      lock (_forSend)
      {
        try {
          byte [] cached;
          if (!cache.TryGetValue (_compression, out cached))
          {
            cached = WsFrame.CreateFrame (
              Fin.FINAL,
              opcode,
              Mask.UNMASK,
              data.Compress (_compression),
              _compression != CompressionMethod.NONE).ToByteArray ();

            cache.Add (_compression, cached);
          }

          send (cached);
        }
        catch (Exception ex) {
          _logger.Fatal (ex.ToString ());
          error ("An exception has occured.");
        }
      }
    }

    // As server, used to broadcast
    internal void Send (Opcode opcode, Stream stream, Dictionary <CompressionMethod, Stream> cache)
    {
      lock (_forSend)
      {
        try {
          Stream cached;
          if (!cache.TryGetValue (_compression, out cached))
          {
            cached = stream.Compress (_compression);
            cache.Add (_compression, cached);
          }
          else
            cached.Position = 0;

          sendFragmented (opcode, cached, Mask.UNMASK, _compression != CompressionMethod.NONE);
        }
        catch (Exception ex) {
          _logger.Fatal (ex.ToString ());
          error ("An exception has occured.");
        }
      }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Closes the WebSocket connection and releases all associated resources.
    /// </summary>
    public void Close ()
    {
      close (new PayloadData (), _readyState == WebSocketState.OPEN, true);
    }

    /// <summary>
    /// Closes the WebSocket connection with the specified <see cref="ushort"/>,
    /// and releases all associated resources.
    /// </summary>
    /// <remarks>
    /// This method emits a <see cref="OnError"/> event if <paramref name="code"/> is not
    /// in the allowable range of the WebSocket close status code.
    /// </remarks>
    /// <param name="code">
    /// A <see cref="ushort"/> that indicates the status code for closure.
    /// </param>
    public void Close (ushort code)
    {
      var msg = code.CheckIfValidCloseStatusCode ();
      if (msg != null)
      {
        _logger.Error (String.Format ("{0}\ncode: {1}", msg, code));
        error (msg);

        return;
      }

      var send = _readyState == WebSocketState.OPEN && !code.IsReserved ();
      close (new PayloadData (code.ToByteArrayInternally (ByteOrder.BIG)), send, true);
    }

    /// <summary>
    /// Closes the WebSocket connection with the specified <see cref="CloseStatusCode"/>,
    /// and releases all associated resources.
    /// </summary>
    /// <param name="code">
    /// One of the <see cref="CloseStatusCode"/> values that indicate the status codes for closure.
    /// </param>
    public void Close (CloseStatusCode code)
    {
      var send = _readyState == WebSocketState.OPEN && !code.IsReserved ();
      close (new PayloadData (((ushort) code).ToByteArrayInternally (ByteOrder.BIG)), send, true);
    }

    /// <summary>
    /// Closes the WebSocket connection with the specified <see cref="ushort"/> and <see cref="string"/>,
    /// and releases all associated resources.
    /// </summary>
    /// <remarks>
    /// This method emits a <see cref="OnError"/> event if <paramref name="code"/> is not
    /// in the allowable range of the WebSocket close status code
    /// or the length of <paramref name="reason"/> is greater than 123 bytes.
    /// </remarks>
    /// <param name="code">
    /// A <see cref="ushort"/> that indicates the status code for closure.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that contains the reason for closure.
    /// </param>
    public void Close (ushort code, string reason)
    {
      byte [] data = null;
      var msg = code.CheckIfValidCloseStatusCode () ??
                (data = code.Append (reason)).CheckIfValidCloseData ();

      if (msg != null)
      {
        _logger.Error (String.Format ("{0}\ncode: {1}\nreason: {2}", msg, code, reason));
        error (msg);

        return;
      }

      var send = _readyState == WebSocketState.OPEN && !code.IsReserved ();
      close (new PayloadData (data), send, true);
    }

    /// <summary>
    /// Closes the WebSocket connection with the specified <see cref="CloseStatusCode"/> and
    /// <see cref="string"/>, and releases all associated resources.
    /// </summary>
    /// <remarks>
    /// This method emits a <see cref="OnError"/> event if the length of <paramref name="reason"/>
    /// is greater than 123 bytes.
    /// </remarks>
    /// <param name="code">
    /// One of the <see cref="CloseStatusCode"/> values that indicate the status codes for closure.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that contains the reason for closure.
    /// </param>
    public void Close (CloseStatusCode code, string reason)
    {
      var data = ((ushort) code).Append (reason);
      var msg = data.CheckIfValidCloseData ();
      if (msg != null)
      {
        _logger.Error (String.Format ("{0}\nreason: {1}", msg, reason));
        error (msg);

        return;
      }

      var send = _readyState == WebSocketState.OPEN && !code.IsReserved ();
      close (new PayloadData (data), send, true);
    }

    /// <summary>
    /// Establishes a WebSocket connection.
    /// </summary>
    public void Connect ()
    {
      if (IsOpened)
      {
        var msg = "A WebSocket connection has already been established.";
        _logger.Error (msg);
        error (msg);

        return;
      }

      try {
        if (connect ())
          open ();
      }
      catch (Exception ex) {
        _logger.Fatal (ex.ToString ());
        error ("An exception has occured.");
        if (_client)
          Close (CloseStatusCode.ABNORMAL);
        else
          Close (HttpStatusCode.BadRequest);
      }
    }

    /// <summary>
    /// Closes the WebSocket connection and releases all associated resources.
    /// </summary>
    /// <remarks>
    /// This method closes the WebSocket connection with the <see cref="CloseStatusCode.AWAY"/>.
    /// </remarks>
    public void Dispose ()
    {
      Close (CloseStatusCode.AWAY);
    }

    /// <summary>
    /// Sends a Ping using the WebSocket connection.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the <see cref="WebSocket"/> instance receives a Pong in a time;
    /// otherwise, <c>false</c>.
    /// </returns>
    public bool Ping ()
    {
      return _client
             ? Ping (WsFrame.CreatePingFrame (Mask.MASK).ToByteArray (), 5000)
             : Ping (WsFrame.EmptyUnmaskPingData, 1000);
    }

    /// <summary>
    /// Sends a Ping with the specified <paramref name="message"/> using the WebSocket connection.
    /// </summary>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to send.
    /// </param>
    /// <returns>
    /// <c>true</c> if the <see cref="WebSocket"/> instance receives a Pong in a time;
    /// otherwise, <c>false</c>.
    /// </returns>
    public bool Ping (string message)
    {
      if (message == null || message.Length == 0)
        return Ping ();

      var data = Encoding.UTF8.GetBytes (message);
      var msg = data.CheckIfValidPingData ();
      if (msg != null)
      {
        _logger.Error (msg);
        error (msg);

        return false;
      }

      return _client
             ? Ping (WsFrame.CreatePingFrame (Mask.MASK, data).ToByteArray (), 5000)
             : Ping (WsFrame.CreatePingFrame (Mask.UNMASK, data).ToByteArray (), 1000);
    }

    /// <summary>
    /// Sends a binary <paramref name="data"/> using the WebSocket connection.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the send to be complete.
    /// </remarks>
    /// <param name="data">
    /// An array of <see cref="byte"/> that contains a binary data to send.
    /// </param>
    public void Send (byte[] data)
    {
      Send (data, null);
    }

    /// <summary>
    /// Sends a binary data from the specified <see cref="FileInfo"/>
    /// using the WebSocket connection.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the send to be complete.
    /// </remarks>
    /// <param name="file">
    /// A <see cref="FileInfo"/> from which contains a binary data to send.
    /// </param>
    public void Send (FileInfo file)
    {
      Send (file, null);
    }

    /// <summary>
    /// Sends a text <paramref name="data"/> using the WebSocket connection.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the send to be complete.
    /// </remarks>
    /// <param name="data">
    /// A <see cref="string"/> that contains a text data to send.
    /// </param>
    public void Send (string data)
    {
      Send (data, null);
    }

    /// <summary>
    /// Sends a binary <paramref name="data"/> using the WebSocket connection.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the send to be complete.
    /// </remarks>
    /// <param name="data">
    /// An array of <see cref="byte"/> that contains a binary data to send.
    /// </param>
    /// <param name="completed">
    /// An Action&lt;bool&gt; delegate that references the method(s) called when
    /// the send is complete.
    /// A <see cref="bool"/> passed to this delegate is <c>true</c> if the send is complete
    /// successfully; otherwise, <c>false</c>.
    /// </param>
    public void Send (byte [] data, Action<bool> completed)
    {
      var msg = _readyState.CheckIfOpen () ?? data.CheckIfValidSendData ();
      if (msg != null)
      {
        _logger.Error (msg);
        error (msg);

        return;
      }

      if (data.LongLength <= FragmentLength)
        send (Opcode.BINARY, data, completed);
      else
        send (Opcode.BINARY, new MemoryStream (data), completed);
    }

    /// <summary>
    /// Sends a binary data from the specified <see cref="FileInfo"/>
    /// using the WebSocket connection.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the send to be complete.
    /// </remarks>
    /// <param name="file">
    /// A <see cref="FileInfo"/> from which contains a binary data to send.
    /// </param>
    /// <param name="completed">
    /// An Action&lt;bool&gt; delegate that references the method(s) called when
    /// the send is complete.
    /// A <see cref="bool"/> passed to this delegate is <c>true</c> if the send is complete
    /// successfully; otherwise, <c>false</c>.
    /// </param>
    public void Send (FileInfo file, Action<bool> completed)
    {
      var msg = _readyState.CheckIfOpen () ??
                (file == null ? "'file' must not be null." : null);

      if (msg != null)
      {
        _logger.Error (msg);
        error (msg);

        return;
      }

      send (Opcode.BINARY, file.OpenRead (), completed);
    }

    /// <summary>
    /// Sends a text <paramref name="data"/> using the WebSocket connection.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the send to be complete.
    /// </remarks>
    /// <param name="data">
    /// A <see cref="string"/> that contains a text data to send.
    /// </param>
    /// <param name="completed">
    /// An Action&lt;bool&gt; delegate that references the method(s) called when
    /// the send is complete.
    /// A <see cref="bool"/> passed to this delegate is <c>true</c> if the send is complete
    /// successfully; otherwise, <c>false</c>.
    /// </param>
    public void Send (string data, Action<bool> completed)
    {
      var msg = _readyState.CheckIfOpen () ?? data.CheckIfValidSendData ();
      if (msg != null)
      {
        _logger.Error (msg);
        error (msg);

        return;
      }

      var rawData = Encoding.UTF8.GetBytes (data);
      if (rawData.LongLength <= FragmentLength)
        send (Opcode.TEXT, rawData, completed);
      else
        send (Opcode.TEXT, new MemoryStream (rawData), completed);
    }

    /// <summary>
    /// Sends a binary data from the specified <see cref="Stream"/>
    /// using the WebSocket connection.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the send to be complete.
    /// </remarks>
    /// <param name="stream">
    /// A <see cref="Stream"/> object from which contains a binary data to send.
    /// </param>
    /// <param name="length">
    /// An <see cref="int"/> that contains the number of bytes to send.
    /// </param>
    public void Send (Stream stream, int length)
    {
      Send (stream, length, null);
    }

    /// <summary>
    /// Sends a binary data from the specified <see cref="Stream"/>
    /// using the WebSocket connection.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the send to be complete.
    /// </remarks>
    /// <param name="stream">
    /// A <see cref="Stream"/> object from which contains a binary data to send.
    /// </param>
    /// <param name="length">
    /// An <see cref="int"/> that contains the number of bytes to send.
    /// </param>
    /// <param name="completed">
    /// An Action&lt;bool&gt; delegate that references the method(s) called when
    /// the send is complete.
    /// A <see cref="bool"/> passed to this delegate is <c>true</c> if the send is
    /// complete successfully; otherwise, <c>false</c>.
    /// </param>
    public void Send (Stream stream, int length, Action<bool> completed)
    {
      var msg = _readyState.CheckIfOpen () ??
                stream.CheckIfCanRead () ??
                (length < 1 ? "'length' must be greater than 0." : null);

      if (msg != null)
      {
        _logger.Error (msg);
        error (msg);

        return;
      }

      stream.ReadBytesAsync (
        length,
        data =>
        {
          var len = data.Length;
          if (len == 0)
          {
            var err = "A data cannot be read from 'stream'.";
            _logger.Error (err);
            error (err);

            return;
          }

          if (len < length)
            _logger.Warn (String.Format (
              "A data with 'length' cannot be read from 'stream'.\nexpected: {0} actual: {1}",
              length,
              len));

          var sent = len <= FragmentLength
                   ? send (Opcode.BINARY, data)
                   : send (Opcode.BINARY, new MemoryStream (data));

          if (completed != null)
            completed (sent);
        },
        ex =>
        {
          _logger.Fatal (ex.ToString ());
          error ("An exception has occured.");        
        });
    }

    /// <summary>
    /// Sets a <see cref="Cookie"/> used in the WebSocket opening handshake.
    /// </summary>
    /// <param name="cookie">
    /// A <see cref="Cookie"/> that contains an HTTP Cookie to set.
    /// </param>
    public void SetCookie (Cookie cookie)
    {
      var msg = IsOpened
              ? "A WebSocket connection has already been established."
              : cookie == null
                ? "'cookie' must not be null."
                : null;

      if (msg != null)
      {
        _logger.Error (msg);
        error (msg);

        return;
      }

      lock (_cookies.SyncRoot)
      {
        _cookies.SetOrRemove (cookie);
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
    public void SetCredentials (string userName, string password, bool preAuth)
    {
      string msg = null;
      if (IsOpened)
      {
        msg = "A WebSocket connection has already been established.";
      }
      else if (userName == null)
      {
        _credentials = null;
        _preAuth = false;

        return;
      }
      else
      {
        msg = userName.Length > 0 && (userName.Contains (':') || !userName.IsText ())
            ? "'userName' contains an invalid character."
            : !password.IsNullOrEmpty () && !password.IsText ()
              ? "'password' contains an invalid character."
              : null;
      }

      if (msg != null)
      {
        _logger.Error (msg);
        error (msg);

        return;
      }

      _credentials = new WsCredential (userName, password, _uri.PathAndQuery);
      _preAuth = preAuth;
    }

    #endregion
  }
}
