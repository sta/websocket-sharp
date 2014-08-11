#region License
/*
 * HttpServer.cs
 *
 * A simple HTTP server that allows to accept the WebSocket connection requests.
 *
 * The MIT License
 *
 * Copyright (c) 2012-2014 sta.blockhead
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
 *   Juan Manuel Lallana <juan.manuel.lallana@gmail.com>
 */
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Threading;
using WebSocketSharp.Net;
using WebSocketSharp.Net.WebSockets;

namespace WebSocketSharp.Server
{
  /// <summary>
  /// Provides a simple HTTP server that allows to accept the WebSocket connection requests.
  /// </summary>
  /// <remarks>
  /// The HttpServer class can provide the multi WebSocket services.
  /// </remarks>
  public class HttpServer
  {
    #region Private Fields

    private HttpListener            _listener;
    private Logger                  _logger;
    private int                     _port;
    private Thread                  _receiveRequestThread;
    private string                  _rootPath;
    private bool                    _secure;
    private WebSocketServiceManager _services;
    private volatile ServerState    _state;
    private object                  _sync;
    private bool                    _windows;

    #endregion

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpServer"/> class.
    /// </summary>
    /// <remarks>
    /// An instance initialized by this constructor listens for the incoming requests on port 80.
    /// </remarks>
    public HttpServer ()
      : this (80)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpServer"/> class with the specified
    /// <paramref name="port"/>.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   An instance initialized by this constructor listens for the incoming requests on
    ///   <paramref name="port"/>.
    ///   </para>
    ///   <para>
    ///   And if <paramref name="port"/> is 443, that instance provides a secure connection.
    ///   </para>
    /// </remarks>
    /// <param name="port">
    /// An <see cref="int"/> that represents the port number on which to listen.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="port"/> isn't between 1 and 65535.
    /// </exception>
    public HttpServer (int port)
      : this (port, port == 443)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpServer"/> class with the specified
    /// <paramref name="port"/> and <paramref name="secure"/>.
    /// </summary>
    /// <remarks>
    /// An instance initialized by this constructor listens for the incoming requests on
    /// <paramref name="port"/>.
    /// </remarks>
    /// <param name="port">
    /// An <see cref="int"/> that represents the port number on which to listen.
    /// </param>
    /// <param name="secure">
    /// A <see cref="bool"/> that indicates providing a secure connection or not. (<c>true</c>
    /// indicates providing a secure connection.)
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="port"/> isn't between 1 and 65535.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Pair of <paramref name="port"/> and <paramref name="secure"/> is invalid.
    /// </exception>
    public HttpServer(int port, bool secure)
        : this(port, secure, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpServer"/> class with the specified
    /// <paramref name="port"/> and <paramref name="secure"/>.
    /// </summary>
    /// <remarks>
    /// An instance initialized by this constructor listens for the incoming requests on
    /// <paramref name="port"/>.
    /// </remarks>
    /// <param name="port">
    /// An <see cref="int"/> that represents the port number on which to listen.
    /// </param>
    /// <param name="secure">
    /// A <see cref="bool"/> that indicates providing a secure connection or not. (<c>true</c>
    /// indicates providing a secure connection.)
    /// </param>
    /// <param name="prefix">
    /// A <see cref="string"/> that provides a custom url prefix, if desired. (<c>true</c>
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="port"/> isn't between 1 and 65535.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Pair of <paramref name="port"/> and <paramref name="secure"/> is invalid.
    /// </exception>
    public HttpServer (int port, bool secure, string prefix)
    {
      if (!port.IsPortNumber ())
        throw new ArgumentOutOfRangeException ("port", "Must be between 1 and 65535: " + port);

      if ((port == 80 && secure) || (port == 443 && !secure))
        throw new ArgumentException (
          String.Format ("Invalid pair of 'port' and 'secure': {0}, {1}", port, secure));

      _port = port;
      _secure = secure;
      _listener = new HttpListener ();
      _logger = new Logger ();
      _services = new WebSocketServiceManager (_logger);
      _state = ServerState.Ready;
      _sync = new object ();

      var os = Environment.OSVersion;
      if (os.Platform != PlatformID.Unix && os.Platform != PlatformID.MacOSX)
        _windows = true;

      if (string.IsNullOrEmpty(prefix))
        prefix = String.Format("http{0}://*:{1}/", _secure ? "s" : "", _port);

      _listener.Prefixes.Add(prefix);
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets or sets the scheme used to authenticate the clients.
    /// </summary>
    /// <value>
    /// One of the <see cref="WebSocketSharp.Net.AuthenticationSchemes"/> enum values, indicates
    /// the scheme used to authenticate the clients.
    /// The default value is <see cref="WebSocketSharp.Net.AuthenticationSchemes.Anonymous"/>.
    /// </value>
    public AuthenticationSchemes AuthenticationSchemes {
      get {
        return _listener.AuthenticationSchemes;
      }

      set {
        if (!canSet ("AuthenticationSchemes"))
          return;

        _listener.AuthenticationSchemes = value;
      }
    }

    /// <summary>
    /// Gets or sets the certificate used to authenticate the server on the secure connection.
    /// </summary>
    /// <value>
    /// A <see cref="X509Certificate2"/> used to authenticate the server.
    /// </value>
    public X509Certificate2 Certificate {
      get {
        return _listener.DefaultCertificate;
      }

      set {
        if (!canSet ("Certificate"))
          return;

        if (EndPointListener.CertificateExists (_port, _listener.CertificateFolderPath))
          _logger.Warn ("The server certificate associated with the port number already exists.");

        _listener.DefaultCertificate = value;
      }
    }

    /// <summary>
    /// Gets a value indicating whether the server has started.
    /// </summary>
    /// <value>
    /// <c>true</c> if the server has started; otherwise, <c>false</c>.
    /// </value>
    public bool IsListening {
      get {
        return _state == ServerState.Start;
      }
    }

    /// <summary>
    /// Gets a value indicating whether the server provides a secure connection.
    /// </summary>
    /// <value>
    /// <c>true</c> if the server provides a secure connection; otherwise, <c>false</c>.
    /// </value>
    public bool IsSecure {
      get {
        return _secure;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the server cleans up the inactive sessions in the
    /// WebSocket services periodically.
    /// </summary>
    /// <value>
    /// <c>true</c> if the server cleans up the inactive sessions every 60 seconds; otherwise,
    /// <c>false</c>. The default value is <c>true</c>.
    /// </value>
    public bool KeepClean {
      get {
        return _services.KeepClean;
      }

      set {
        _services.KeepClean = value;
      }
    }

    /// <summary>
    /// Gets the logging functions.
    /// </summary>
    /// <remarks>
    /// The default logging level is <see cref="LogLevel.Error"/>. If you would like to change it,
    /// you should set the <c>Log.Level</c> property to any of the <see cref="LogLevel"/> enum
    /// values.
    /// </remarks>
    /// <value>
    /// A <see cref="Logger"/> that provides the logging functions.
    /// </value>
    public Logger Log {
      get {
        return _logger;
      }
    }

    /// <summary>
    /// Gets the port on which to listen for incoming requests.
    /// </summary>
    /// <value>
    /// An <see cref="int"/> that represents the port number on which to listen.
    /// </value>
    public int Port {
      get {
        return _port;
      }
    }

    /// <summary>
    /// Gets or sets the name of the realm associated with the server.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the name of the realm. The default value is
    /// <c>SECRET AREA</c>.
    /// </value>
    public string Realm {
      get {
        return _listener.Realm;
      }

      set {
        if (!canSet ("Realm"))
          return;

        _listener.Realm = value;
      }
    }

    /// <summary>
    /// Gets or sets the document root path of the server.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the document root path of the server. The default
    /// value is <c>./Public</c>.
    /// </value>
    public string RootPath {
      get {
        return _rootPath.IsNullOrEmpty ()
               ? (_rootPath = "./Public")
               : _rootPath;
      }

      set {
        if (!canSet ("RootPath"))
          return;

        _rootPath = value;
      }
    }

    /// <summary>
    /// Gets or sets the delegate called to find the credentials for an identity used to
    /// authenticate a client.
    /// </summary>
    /// <value>
    /// A Func&lt;<see cref="IIdentity"/>, <see cref="NetworkCredential"/>&gt; delegate that
    /// references the method(s) used to find the credentials. The default value is a function
    /// that only returns <see langword="null"/>.
    /// </value>
    public Func<IIdentity, NetworkCredential> UserCredentialsFinder {
      get {
        return _listener.UserCredentialsFinder;
      }

      set {
        if (!canSet ("UserCredentialsFinder"))
          return;

        _listener.UserCredentialsFinder = value;
      }
    }

    /// <summary>
    /// Gets the access to the WebSocket services provided by the server.
    /// </summary>
    /// <value>
    /// A <see cref="WebSocketServiceManager"/> that manages the WebSocket services.
    /// </value>
    public WebSocketServiceManager WebSocketServices {
      get {
        return _services;
      }
    }

    #endregion

    #region Public Events

    /// <summary>
    /// Occurs when the server receives an HTTP CONNECT request.
    /// </summary>
    public event EventHandler<HttpRequestEventArgs> OnConnect;

    /// <summary>
    /// Occurs when the server receives an HTTP DELETE request.
    /// </summary>
    public event EventHandler<HttpRequestEventArgs> OnDelete;

    /// <summary>
    /// Occurs when the server receives an HTTP GET request.
    /// </summary>
    public event EventHandler<HttpRequestEventArgs> OnGet;

    /// <summary>
    /// Occurs when the server receives an HTTP request.
    /// </summary>
    public event EventHandler<HttpRequestEventArgs> OnRequest;
    
      /// <summary>
    /// Occurs when the server receives an HTTP HEAD request.
    /// </summary>
    public event EventHandler<HttpRequestEventArgs> OnHead;

    /// <summary>
    /// Occurs when the server receives an HTTP OPTIONS request.
    /// </summary>
    public event EventHandler<HttpRequestEventArgs> OnOptions;

    /// <summary>
    /// Occurs when the server receives an HTTP PATCH request.
    /// </summary>
    public event EventHandler<HttpRequestEventArgs> OnPatch;

    /// <summary>
    /// Occurs when the server receives an HTTP POST request.
    /// </summary>
    public event EventHandler<HttpRequestEventArgs> OnPost;

    /// <summary>
    /// Occurs when the server receives an HTTP PUT request.
    /// </summary>
    public event EventHandler<HttpRequestEventArgs> OnPut;

    /// <summary>
    /// Occurs when the server receives an HTTP TRACE request.
    /// </summary>
    public event EventHandler<HttpRequestEventArgs> OnTrace;

    #endregion

    #region Private Methods

    private void abort ()
    {
      lock (_sync) {
        if (!IsListening)
          return;

        _state = ServerState.ShuttingDown;
      }

      _services.Stop (
        ((ushort) CloseStatusCode.ServerError).ToByteArrayInternally (ByteOrder.Big), true);

      _listener.Abort ();
      _state = ServerState.Stop;
    }

    private void acceptHttpRequest (HttpListenerContext context)
    {
      var args = new HttpRequestEventArgs (context);
      var method = context.Request.HttpMethod;

      if (method == "GET") {
        if (OnGet != null) {
          OnGet (this, args);
          return;
        }
      }
      else if (method == "HEAD") {
        if (OnHead != null) {
          OnHead (this, args);
          return;
        }
      }
      else if (method == "POST") {
        if (OnPost != null) {
          OnPost (this, args);
          return;
        }
      }
      else if (method == "PUT") {
        if (OnPut != null) {
          OnPut (this, args);
          return;
        }
      }
      else if (method == "DELETE") {
        if (OnDelete != null) {
          OnDelete (this, args);
          return;
        }
      }
      else if (method == "OPTIONS") {
        if (OnOptions != null) {
          OnOptions (this, args);
          return;
        }
      }
      else if (method == "TRACE") {
        if (OnTrace != null) {
          OnTrace (this, args);
          return;
        }
      }
      else if (method == "CONNECT") {
        if (OnConnect != null) {
          OnConnect (this, args);
          return;
        }
      }
      else if (method == "PATCH") {
        if (OnPatch != null) {
          OnPatch (this, args);
          return;
        }
      }

      context.Response.StatusCode = (int) HttpStatusCode.NotImplemented;
    }

    private void acceptRequestAsync (HttpListenerContext context)
    {
        if (OnRequest != null)
        {
            OnRequest(this, new HttpRequestEventArgs(context));
            return;
        }

        ThreadPool.QueueUserWorkItem(
        state => {
          try {
            var authScheme = _listener.SelectAuthenticationScheme (context);
            if (authScheme != AuthenticationSchemes.Anonymous &&
                !authenticateRequest (authScheme, context))
              return;

            if (context.Request.IsUpgradeTo ("websocket")) {
              acceptWebSocketRequest (context.AcceptWebSocket (null, _logger));
              return;
            }

            acceptHttpRequest (context);
            context.Response.Close ();
          }
          catch (Exception ex) {
            _logger.Fatal (ex.ToString ());
            context.Connection.Close (true);
          }
        });
    }

    private void acceptWebSocketRequest (HttpListenerWebSocketContext context)
    {
      WebSocketServiceHost host;
      if (!_services.TryGetServiceHostInternally (context.RequestUri.AbsolutePath, out host)) {
        context.Close (HttpStatusCode.NotImplemented);
        return;
      }

      host.StartSession (context);
    }

    private bool authenticateRequest (AuthenticationSchemes scheme, HttpListenerContext context)
    {
      if (context.Request.IsAuthenticated)
        return true;

      if (scheme == AuthenticationSchemes.Basic)
        context.Response.CloseWithAuthChallenge (
          AuthenticationChallenge.CreateBasicChallenge (_listener.Realm).ToBasicString ());
      else if (scheme == AuthenticationSchemes.Digest)
        context.Response.CloseWithAuthChallenge (
          AuthenticationChallenge.CreateDigestChallenge (_listener.Realm).ToDigestString ());
      else
        context.Response.Close (HttpStatusCode.Forbidden);

      return false;
    }

    private bool canSet (string property)
    {
      if (_state == ServerState.Start || _state == ServerState.ShuttingDown) {
        _logger.Error (
          String.Format (
            "Set operation of {0} isn't available because the server has already started.",
            property));

        return false;
      }

      return true;
    }

    private string checkIfCertExists ()
    {
      return _secure &&
             !EndPointListener.CertificateExists (_port, _listener.CertificateFolderPath) &&
             Certificate == null
             ? "The secure connection requires a server certificate."
             : null;
    }

    private void receiveRequest ()
    {
      while (true) {
        try {
          acceptRequestAsync (_listener.GetContext ());
        }
        catch (HttpListenerException ex) {
          _logger.Warn ("Receiving has been stopped.\nreason: " + ex.Message);
          break;
        }
        catch (Exception ex) {
          _logger.Fatal (ex.ToString ());
          break;
        }
      }

      if (IsListening)
        abort ();
    }

    private void startReceiving ()
    {
      _receiveRequestThread = new Thread (new ThreadStart (receiveRequest));
      _receiveRequestThread.IsBackground = true;
      _receiveRequestThread.Start ();
    }

    private void stopListener (int millisecondsTimeout)
    {
      _listener.Close ();
      _receiveRequestThread.Join (millisecondsTimeout);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Adds the specified typed WebSocket service with the specified <paramref name="path"/>.
    /// </summary>
    /// <remarks>
    /// This method converts <paramref name="path"/> to URL-decoded string and removes <c>'/'</c>
    /// from tail end of <paramref name="path"/>.
    /// </remarks>
    /// <param name="path">
    /// A <see cref="string"/> that represents the absolute path to the WebSocket service to add.
    /// </param>
    /// <typeparam name="TWithNew">
    /// The type of the WebSocket service.
    /// The TWithNew must inherit the <see cref="WebSocketService"/> class and must have a public
    /// parameterless constructor.
    /// </typeparam>
    public void AddWebSocketService<TWithNew> (string path)
      where TWithNew : WebSocketService, new ()
    {
      AddWebSocketService<TWithNew> (path, () => new TWithNew ());
    }

    /// <summary>
    /// Adds the specified typed WebSocket service with the specified <paramref name="path"/> and
    /// <paramref name="constructor"/>.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   This method converts <paramref name="path"/> to URL-decoded string and removes <c>'/'</c>
    ///   from tail end of <paramref name="path"/>.
    ///   </para>
    ///   <para>
    ///   <paramref name="constructor"/> returns a initialized specified typed
    ///   <see cref="WebSocketService"/> instance.
    ///   </para>
    /// </remarks>
    /// <param name="path">
    /// A <see cref="string"/> that represents the absolute path to the WebSocket service to add.
    /// </param>
    /// <param name="constructor">
    /// A Func&lt;T&gt; delegate that references the method used to initialize a new specified
    /// typed <see cref="WebSocketService"/> instance (a new <see cref="IWebSocketSession"/>
    /// instance).
    /// </param>
    /// <typeparam name="T">
    /// The type of the WebSocket service. The T must inherit the <see cref="WebSocketService"/>
    /// class.
    /// </typeparam>
    public void AddWebSocketService<T> (string path, Func<T> constructor)
      where T : WebSocketService
    {
      var msg = path.CheckIfValidServicePath () ??
                (constructor == null ? "'constructor' must not be null." : null);

      if (msg != null) {
        _logger.Error (String.Format ("{0}\nservice path: {1}", msg, path));
        return;
      }

      var host = new WebSocketServiceHost<T> (path, constructor, _logger);
      if (!KeepClean)
        host.KeepClean = false;

      _services.Add (host.Path, host);
    }

    /// <summary>
    /// Gets the contents of the file with the specified <paramref name="path"/>.
    /// </summary>
    /// <returns>
    /// An array of <see cref="byte"/> that receives the contents of the file if it exists;
    /// otherwise, <see langword="null"/>.
    /// </returns>
    /// <param name="path">
    /// A <see cref="string"/> that represents the virtual path to the file to find.
    /// </param>
    public byte [] GetFile (string path)
    {
      var filePath = RootPath + path;
      if (_windows)
        filePath = filePath.Replace ("/", "\\");

      return File.Exists (filePath)
             ? File.ReadAllBytes (filePath)
             : null;
    }

    /// <summary>
    /// Removes the WebSocket service with the specified <paramref name="path"/>.
    /// </summary>
    /// <remarks>
    /// This method converts <paramref name="path"/> to URL-decoded string and removes <c>'/'</c>
    /// from tail end of <paramref name="path"/>.
    /// </remarks>
    /// <returns>
    /// <c>true</c> if the WebSocket service is successfully found and removed; otherwise,
    /// <c>false</c>.
    /// </returns>
    /// <param name="path">
    /// A <see cref="string"/> that represents the absolute path to the WebSocket service to find.
    /// </param>
    public bool RemoveWebSocketService (string path)
    {
      var msg = path.CheckIfValidServicePath ();
      if (msg != null) {
        _logger.Error (String.Format ("{0}\nservice path: {1}", msg, path));
        return false;
      }

      return _services.Remove (path);
    }

    /// <summary>
    /// Starts receiving the HTTP requests.
    /// </summary>
    public void Start ()
    {
      lock (_sync) {
        var msg = _state.CheckIfStartable () ?? checkIfCertExists ();
        if (msg != null) {
          _logger.Error (String.Format ("{0}\nstate: {1}\nsecure: {2}", msg, _state, _secure));
          return;
        }

        _services.Start ();
        _listener.Start ();
        startReceiving ();

        _state = ServerState.Start;
      }
    }

    /// <summary>
    /// Stops receiving the HTTP requests.
    /// </summary>
    public void Stop ()
    {
      lock (_sync) {
        var msg = _state.CheckIfStart ();
        if (msg != null) {
          _logger.Error (String.Format ("{0}\nstate: {1}", msg, _state));
          return;
        }

        _state = ServerState.ShuttingDown;
      }

      _services.Stop (new byte [0], true);
      stopListener (5000);

      _state = ServerState.Stop;
    }

    /// <summary>
    /// Stops receiving the HTTP requests with the specified <see cref="ushort"/> and
    /// <see cref="string"/> used to stop the WebSocket services.
    /// </summary>
    /// <param name="code">
    /// A <see cref="ushort"/> that represents the status code indicating the reason for stop.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that represents the reason for stop.
    /// </param>
    public void Stop (ushort code, string reason)
    {
      byte [] data = null;
      lock (_sync) {
        var msg = _state.CheckIfStart () ??
                  code.CheckIfValidCloseStatusCode () ??
                  (data = code.Append (reason)).CheckIfValidControlData ("reason");

        if (msg != null) {
          _logger.Error (
            String.Format ("{0}\nstate: {1}\ncode: {2}\nreason: {3}", msg, _state, code, reason));

          return;
        }

        _state = ServerState.ShuttingDown;
      }

      _services.Stop (data, !code.IsReserved ());
      stopListener (5000);

      _state = ServerState.Stop;
    }

    /// <summary>
    /// Stops receiving the HTTP requests with the specified <see cref="CloseStatusCode"/> and
    /// <see cref="string"/> used to stop the WebSocket services.
    /// </summary>
    /// <param name="code">
    /// One of the <see cref="CloseStatusCode"/> enum values, represents the status code indicating
    /// the reasons for stop.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that represents the reason for stop.
    /// </param>
    public void Stop (CloseStatusCode code, string reason)
    {
      byte [] data = null;
      lock (_sync) {
        var msg = _state.CheckIfStart () ??
                  (data = ((ushort) code).Append (reason)).CheckIfValidControlData ("reason");

        if (msg != null) {
          _logger.Error (String.Format ("{0}\nstate: {1}\nreason: {2}", msg, _state, reason));
          return;
        }

        _state = ServerState.ShuttingDown;
      }

      _services.Stop (data, !code.IsReserved ());
      stopListener (5000);

      _state = ServerState.Stop;
    }

    #endregion
  }
}
