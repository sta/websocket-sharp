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
 * - Juan Manuel Lallana <juan.manuel.lallana@gmail.com>
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
  /// The HttpServer class can provide multiple WebSocket services.
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
      : this (80, false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpServer"/> class with the specified
    /// <paramref name="port"/>.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   An instance initialized by this constructor listens for the incoming requests
    ///   on <paramref name="port"/>.
    ///   </para>
    ///   <para>
    ///   If <paramref name="port"/> is 443, that instance provides a secure connection.
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
    /// An instance initialized by this constructor listens for the incoming requests
    /// on <paramref name="port"/>.
    /// </remarks>
    /// <param name="port">
    /// An <see cref="int"/> that represents the port number on which to listen.
    /// </param>
    /// <param name="secure">
    /// A <see cref="bool"/> that indicates providing a secure connection or not.
    /// (<c>true</c> indicates providing a secure connection.)
    /// </param>
    /// <exception cref="ArgumentException">
    /// Pair of <paramref name="port"/> and <paramref name="secure"/> is invalid.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="port"/> isn't between 1 and 65535.
    /// </exception>
    public HttpServer (int port, bool secure)
    {
      if (!port.IsPortNumber ())
        throw new ArgumentOutOfRangeException ("port", "Not between 1 and 65535: " + port);

      if ((port == 80 && secure) || (port == 443 && !secure))
        throw new ArgumentException (
          String.Format ("An invalid pair of 'port' and 'secure': {0}, {1}", port, secure));

      _port = port;
      _secure = secure;
      _listener = new HttpListener ();
      _logger = new Logger ();
      _services = new WebSocketServiceManager (_logger);
      _state = ServerState.Ready;
      _sync = new object ();

      var os = Environment.OSVersion;
      _windows = os.Platform != PlatformID.Unix && os.Platform != PlatformID.MacOSX;

      var prefix = String.Format ("http{0}://*:{1}/", _secure ? "s" : "", _port);
      _listener.Prefixes.Add (prefix);
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets or sets the scheme used to authenticate the clients.
    /// </summary>
    /// <value>
    /// One of the <see cref="WebSocketSharp.Net.AuthenticationSchemes"/> enum values,
    /// indicates the scheme used to authenticate the clients. The default value is
    /// <see cref="WebSocketSharp.Net.AuthenticationSchemes.Anonymous"/>.
    /// </value>
    public AuthenticationSchemes AuthenticationSchemes {
      get {
        return _listener.AuthenticationSchemes;
      }

      set {
        var msg = _state.CheckIfStartable ();
        if (msg != null) {
          _logger.Error (msg);
          return;
        }

        _listener.AuthenticationSchemes = value;
      }
    }

    /// <summary>
    /// Gets or sets the certificate used to authenticate the server on the secure connection.
    /// </summary>
    /// <value>
    /// A <see cref="X509Certificate2"/> that represents the certificate used to authenticate
    /// the server.
    /// </value>
    public X509Certificate2 Certificate {
      get {
        return _listener.DefaultCertificate;
      }

      set {
        var msg = _state.CheckIfStartable ();
        if (msg != null) {
          _logger.Error (msg);
          return;
        }

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
    /// Gets or sets a value indicating whether the server cleans up the inactive sessions
    /// in the WebSocket services periodically.
    /// </summary>
    /// <value>
    /// <c>true</c> if the server cleans up the inactive sessions every 60 seconds;
    /// otherwise, <c>false</c>. The default value is <c>true</c>.
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
    /// A <see cref="string"/> that represents the name of the realm.
    /// The default value is <c>"SECRET AREA"</c>.
    /// </value>
    public string Realm {
      get {
        return _listener.Realm;
      }

      set {
        var msg = _state.CheckIfStartable ();
        if (msg != null) {
          _logger.Error (msg);
          return;
        }

        _listener.Realm = value;
      }
    }

    /// <summary>
    /// Gets or sets the document root path of the server.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the document root path of the server.
    /// The default value is <c>"./Public"</c>.
    /// </value>
    public string RootPath {
      get {
        return _rootPath != null && _rootPath.Length > 0
               ? _rootPath
               : (_rootPath = "./Public");
      }

      set {
        var msg = _state.CheckIfStartable ();
        if (msg != null) {
          _logger.Error (msg);
          return;
        }

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
        var msg = _state.CheckIfStartable ();
        if (msg != null) {
          _logger.Error (msg);
          return;
        }

        _listener.UserCredentialsFinder = value;
      }
    }

    /// <summary>
    /// Gets or sets the wait time for the response to the WebSocket Ping or Close.
    /// </summary>
    /// <value>
    /// A <see cref="TimeSpan"/> that represents the wait time. The default value is
    /// the same as 1 second.
    /// </value>
    public TimeSpan WaitTime {
      get {
        return _services.WaitTime;
      }

      set {
        var msg = _state.CheckIfStartable () ?? value.CheckIfValidWaitTime ();
        if (msg != null) {
          _logger.Error (msg);
          return;
        }

        _services.WaitTime = value;
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

      _services.Stop (new CloseEventArgs (CloseStatusCode.ServerError), true, false);
      _listener.Abort ();

      _state = ServerState.Stop;
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

    private string checkIfCertificateExists ()
    {
      return _secure &&
             !EndPointListener.CertificateExists (_port, _listener.CertificateFolderPath) &&
             _listener.DefaultCertificate == null
             ? "The secure connection requires a server certificate."
             : null;
    }

    private void processHttpRequest (HttpListenerContext context)
    {
      var method = context.Request.HttpMethod;
      var evt = method == "GET"
                ? OnGet
                : method == "HEAD"
                  ? OnHead
                  : method == "POST"
                    ? OnPost
                    : method == "PUT"
                      ? OnPut
                      : method == "DELETE"
                        ? OnDelete
                        : method == "OPTIONS"
                          ? OnOptions
                          : method == "TRACE"
                            ? OnTrace
                            : method == "CONNECT"
                              ? OnConnect
                              : method == "PATCH"
                                ? OnPatch
                                : null;

      if (evt != null)
        evt (this, new HttpRequestEventArgs (context));
      else
        context.Response.StatusCode = (int) HttpStatusCode.NotImplemented;

      context.Response.Close ();
    }

    private void processWebSocketRequest (HttpListenerWebSocketContext context)
    {
      WebSocketServiceHost host;
      if (!_services.InternalTryGetServiceHost (context.RequestUri.AbsolutePath, out host)) {
        context.Close (HttpStatusCode.NotImplemented);
        return;
      }

      host.StartSession (context);
    }

    private void receiveRequest ()
    {
      while (true) {
        try {
          var ctx = _listener.GetContext ();
          ThreadPool.QueueUserWorkItem (
            state => {
              try {
                var schm = _listener.SelectAuthenticationScheme (ctx);
                if (schm != AuthenticationSchemes.Anonymous &&
                    !authenticateRequest (schm, ctx))
                  return;

                if (ctx.Request.IsUpgradeTo ("websocket")) {
                  processWebSocketRequest (ctx.AcceptWebSocket (null, _logger));
                  return;
                }

                processHttpRequest (ctx);
              }
              catch (Exception ex) {
                _logger.Fatal (ex.ToString ());
                ctx.Connection.Close (true);
              }
            });
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
      _listener.Start ();
      _receiveRequestThread = new Thread (new ThreadStart (receiveRequest));
      _receiveRequestThread.IsBackground = true;
      _receiveRequestThread.Start ();
    }

    private void stopReceiving (int millisecondsTimeout)
    {
      _listener.Close ();
      _receiveRequestThread.Join (millisecondsTimeout);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Adds a WebSocket service with the specified behavior and <paramref name="path"/>.
    /// </summary>
    /// <remarks>
    /// This method converts <paramref name="path"/> to URL-decoded string,
    /// and removes <c>'/'</c> from tail end of <paramref name="path"/>.
    /// </remarks>
    /// <param name="path">
    /// A <see cref="string"/> that represents the absolute path to the service to add.
    /// </param>
    /// <typeparam name="TBehaviorWithNew">
    /// The type of the behavior of the service to add. The TBehaviorWithNew must inherit
    /// the <see cref="WebSocketBehavior"/> class, and must have a public parameterless
    /// constructor.
    /// </typeparam>
    public void AddWebSocketService<TBehaviorWithNew> (string path)
      where TBehaviorWithNew : WebSocketBehavior, new ()
    {
      AddWebSocketService<TBehaviorWithNew> (path, () => new TBehaviorWithNew ());
    }

    /// <summary>
    /// Adds the WebSocket service with the specified behavior, <paramref name="path"/>,
    /// and <paramref name="initializer"/>.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   This method converts <paramref name="path"/> to URL-decoded string,
    ///   and removes <c>'/'</c> from tail end of <paramref name="path"/>.
    ///   </para>
    ///   <para>
    ///   <paramref name="initializer"/> returns an initialized specified typed
    ///   <see cref="WebSocketBehavior"/> instance.
    ///   </para>
    /// </remarks>
    /// <param name="path">
    /// A <see cref="string"/> that represents the absolute path to the service to add.
    /// </param>
    /// <param name="initializer">
    /// A Func&lt;T&gt; delegate that references the method used to initialize a new specified
    /// typed <see cref="WebSocketBehavior"/> instance (a new <see cref="IWebSocketSession"/>
    /// instance).
    /// </param>
    /// <typeparam name="TBehavior">
    /// The type of the behavior of the service to add. The TBehavior must inherit
    /// the <see cref="WebSocketBehavior"/> class.
    /// </typeparam>
    public void AddWebSocketService<TBehavior> (string path, Func<TBehavior> initializer)
      where TBehavior : WebSocketBehavior
    {
      var msg = path.CheckIfValidServicePath () ??
                (initializer == null ? "'initializer' is null." : null);

      if (msg != null) {
        _logger.Error (msg);
        return;
      }

      _services.Add<TBehavior> (path, initializer);
    }

    /// <summary>
    /// Gets the contents of the file with the specified <paramref name="path"/>.
    /// </summary>
    /// <returns>
    /// An array of <see cref="byte"/> that receives the contents of the file,
    /// or <see langword="null"/> if it doesn't exist.
    /// </returns>
    /// <param name="path">
    /// A <see cref="string"/> that represents the virtual path to the file to find.
    /// </param>
    public byte[] GetFile (string path)
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
    /// This method converts <paramref name="path"/> to URL-decoded string,
    /// and removes <c>'/'</c> from tail end of <paramref name="path"/>.
    /// </remarks>
    /// <returns>
    /// <c>true</c> if the service is successfully found and removed; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="path">
    /// A <see cref="string"/> that represents the absolute path to the service to find.
    /// </param>
    public bool RemoveWebSocketService (string path)
    {
      var msg = path.CheckIfValidServicePath ();
      if (msg != null) {
        _logger.Error (msg);
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
        var msg = _state.CheckIfStartable () ?? checkIfCertificateExists ();
        if (msg != null) {
          _logger.Error (msg);
          return;
        }

        _services.Start ();
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
          _logger.Error (msg);
          return;
        }

        _state = ServerState.ShuttingDown;
      }

      _services.Stop (new CloseEventArgs (), true, true);
      stopReceiving (5000);

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
      CloseEventArgs e = null;
      lock (_sync) {
        var msg =
          _state.CheckIfStart () ??
          code.CheckIfValidCloseStatusCode () ??
          (e = new CloseEventArgs (code, reason)).RawData.CheckIfValidControlData ("reason");

        if (msg != null) {
          _logger.Error (msg);
          return;
        }

        _state = ServerState.ShuttingDown;
      }

      var send = !code.IsReserved ();
      _services.Stop (e, send, send);

      stopReceiving (5000);

      _state = ServerState.Stop;
    }

    /// <summary>
    /// Stops receiving the HTTP requests with the specified <see cref="CloseStatusCode"/> and
    /// <see cref="string"/> used to stop the WebSocket services.
    /// </summary>
    /// <param name="code">
    /// One of the <see cref="CloseStatusCode"/> enum values, represents the status code
    /// indicating the reasons for stop.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that represents the reason for stop.
    /// </param>
    public void Stop (CloseStatusCode code, string reason)
    {
      CloseEventArgs e = null;
      lock (_sync) {
        var msg =
          _state.CheckIfStart () ??
          (e = new CloseEventArgs (code, reason)).RawData.CheckIfValidControlData ("reason");

        if (msg != null) {
          _logger.Error (msg);
          return;
        }

        _state = ServerState.ShuttingDown;
      }

      var send = !code.IsReserved ();
      _services.Stop (e, send, send);

      stopReceiving (5000);

      _state = ServerState.Stop;
    }

    #endregion
  }
}
