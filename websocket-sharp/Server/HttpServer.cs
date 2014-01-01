#region License
/*
 * HttpServer.cs
 *
 * A simple HTTP server that allows to accept the WebSocket connection requests.
 *
 * The MIT License
 *
 * Copyright (c) 2012-2013 sta.blockhead
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
  /// Provides a simple HTTP server that allows to accept the WebSocket
  /// connection requests.
  /// </summary>
  /// <remarks>
  /// The HttpServer class can provide the multi WebSocket services.
  /// </remarks>
  public class HttpServer
  {
    #region Private Fields

    private HttpListener                _listener;
    private Logger                      _logger;
    private int                         _port;
    private Thread                      _receiveRequestThread;
    private string                      _rootPath;
    private bool                        _secure;
    private WebSocketServiceHostManager _serviceHosts;
    private volatile ServerState        _state;
    private object                      _sync;
    private bool                        _windows;

    #endregion

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpServer"/> class that
    /// listens for incoming requests on port 80.
    /// </summary>
    public HttpServer ()
      : this (80)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpServer"/> class that
    /// listens for incoming requests on the specified <paramref name="port"/>.
    /// </summary>
    /// <param name="port">
    /// An <see cref="int"/> that contains a port number.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="port"/> is not between 1 and 65535.
    /// </exception>
    public HttpServer (int port)
      : this (port, port == 443 ? true : false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpServer"/> class that
    /// listens for incoming requests on the specified <paramref name="port"/>
    /// and <paramref name="secure"/>.
    /// </summary>
    /// <param name="port">
    /// An <see cref="int"/> that contains a port number.
    /// </param>
    /// <param name="secure">
    /// A <see cref="bool"/> that indicates providing a secure connection or not.
    /// (<c>true</c> indicates providing a secure connection.)
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="port"/> is not between 1 and 65535.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Pair of <paramref name="port"/> and <paramref name="secure"/> is invalid.
    /// </exception>
    public HttpServer (int port, bool secure)
    {
      if (!port.IsPortNumber ())
        throw new ArgumentOutOfRangeException (
          "port", "Must be between 1 and 65535: " + port);

      if ((port == 80 && secure) || (port == 443 && !secure))
        throw new ArgumentException (
          String.Format (
            "Invalid pair of 'port' and 'secure': {0}, {1}", port, secure));

      _port = port;
      _secure = secure;
      _listener = new HttpListener ();
      _logger = new Logger ();
      _serviceHosts = new WebSocketServiceHostManager (_logger);
      _state = ServerState.READY;
      _sync = new object ();

      var os = Environment.OSVersion;
      if (os.Platform != PlatformID.Unix && os.Platform != PlatformID.MacOSX)
        _windows = true;

      var prefix = String.Format ("http{0}://*:{1}/", _secure ? "s" : "", _port);
      _listener.Prefixes.Add (prefix);
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets or sets the scheme used to authenticate the clients.
    /// </summary>
    /// <value>
    /// One of the <see cref="WebSocketSharp.Net.AuthenticationSchemes"/> values
    /// that indicates the scheme used to authenticate the clients. The default
    /// value is <see cref="WebSocketSharp.Net.AuthenticationSchemes.Anonymous"/>.
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
    /// Gets or sets the certificate used to authenticate the server on the
    /// secure connection.
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
          _logger.Warn (
            "The server certificate associated with the port number already exists.");

        _listener.DefaultCertificate = value;
      }
    }

    /// <summary>
    /// Gets a value indicating whether the server has been started.
    /// </summary>
    /// <value>
    /// <c>true</c> if the server has been started; otherwise, <c>false</c>.
    /// </value>
    public bool IsListening {
      get {
        return _state == ServerState.START;
      }
    }

    /// <summary>
    /// Gets a value indicating whether the server provides secure connection.
    /// </summary>
    /// <value>
    /// <c>true</c> if the server provides secure connection; otherwise,
    /// <c>false</c>.
    /// </value>
    public bool IsSecure {
      get {
        return _secure;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the server cleans up the inactive
    /// WebSocket sessions periodically.
    /// </summary>
    /// <value>
    /// <c>true</c> if the server cleans up the inactive WebSocket sessions every
    /// 60 seconds; otherwise, <c>false</c>. The default value is <c>true</c>.
    /// </value>
    public bool KeepClean {
      get {
        return _serviceHosts.KeepClean;
      }

      set {
        _serviceHosts.KeepClean = value;
      }
    }

    /// <summary>
    /// Gets the logging functions.
    /// </summary>
    /// <remarks>
    /// The default logging level is the <see cref="LogLevel.ERROR"/>. If you
    /// change the current logging level, you set the <c>Log.Level</c> property
    /// to any of the <see cref="LogLevel"/> values.
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
    /// An <see cref="int"/> that contains a port number.
    /// </value>
    public int Port {
      get {
        return _port;
      }
    }

    /// <summary>
    /// Gets or sets the name of the realm associated with the
    /// <see cref="HttpServer"/>.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that contains the name of the realm.
    /// The default value is <c>SECRET AREA</c>.
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
    /// Gets or sets the document root path of server.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that contains the document root path of server.
    /// The default value is <c>./Public</c>.
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
    /// Gets or sets the delegate called to find the credentials for an identity
    /// used to authenticate a client.
    /// </summary>
    /// <value>
    /// A Func&lt;<see cref="IIdentity"/>, <see cref="NetworkCredential"/>&gt;
    /// delegate that references the method(s) used to find the credentials. The
    /// default value is a function that only returns <see langword="null"/>.
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
    /// Gets the functions for the WebSocket services provided by the
    /// <see cref="HttpServer"/>.
    /// </summary>
    /// <value>
    /// A <see cref="WebSocketServiceHostManager"/> that manages the WebSocket
    /// services.
    /// </value>
    public WebSocketServiceHostManager WebSocketServices {
      get {
        return _serviceHosts;
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

        _state = ServerState.SHUTDOWN;
      }

      _serviceHosts.Stop (
        ((ushort) CloseStatusCode.SERVER_ERROR).ToByteArrayInternally (ByteOrder.BIG),
        true);
      _listener.Abort ();

      _state = ServerState.STOP;
    }

    private void acceptHttpRequest (HttpListenerContext context)
    {
      var args = new HttpRequestEventArgs (context);
      var method = context.Request.HttpMethod;
      if (method == "GET" && OnGet != null) {
        OnGet (this, args);
        return;
      }

      if (method == "HEAD" && OnHead != null) {
        OnHead (this, args);
        return;
      }

      if (method == "POST" && OnPost != null) {
        OnPost (this, args);
        return;
      }

      if (method == "PUT" && OnPut != null) {
        OnPut (this, args);
        return;
      }

      if (method == "DELETE" && OnDelete != null) {
        OnDelete (this, args);
        return;
      }

      if (method == "OPTIONS" && OnOptions != null) {
        OnOptions (this, args);
        return;
      }

      if (method == "TRACE" && OnTrace != null) {
        OnTrace (this, args);
        return;
      }

      if (method == "CONNECT" && OnConnect != null) {
        OnConnect (this, args);
        return;
      }

      if (method == "PATCH" && OnPatch != null) {
        OnPatch (this, args);
        return;
      }

      context.Response.Close (HttpStatusCode.NotImplemented);
    }

    private void acceptRequestAsync (HttpListenerContext context)
    {
      ThreadPool.QueueUserWorkItem (
        state => {
          try {
            var authScheme = _listener.SelectAuthenticationScheme (context);
            if (authScheme != AuthenticationSchemes.Anonymous &&
                !authenticateRequest (authScheme, context))
              return;

            if (context.Request.IsUpgradeTo ("websocket")) {
              acceptWebSocketRequest (context.AcceptWebSocket (_logger));
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
      var path = context.Path;

      WebSocketServiceHost host;
      if (path == null ||
          !_serviceHosts.TryGetServiceHostInternally (path, out host)) {
        context.Close (HttpStatusCode.NotImplemented);
        return;
      }

      host.StartSession (context);
    }

    private bool authenticateRequest (
      AuthenticationSchemes scheme, HttpListenerContext context)
    {
      if (context.Request.IsAuthenticated)
        return true;

      if (scheme == AuthenticationSchemes.Basic)
        context.Response.CloseWithAuthChallenge (
          HttpUtility.CreateBasicAuthChallenge (_listener.Realm));
      else if (scheme == AuthenticationSchemes.Digest)
        context.Response.CloseWithAuthChallenge (
          HttpUtility.CreateDigestAuthChallenge (_listener.Realm));
      else
        context.Response.Close (HttpStatusCode.Forbidden);

      return false;
    }

    private bool canSet (string property)
    {
      if (_state == ServerState.START || _state == ServerState.SHUTDOWN) {
        _logger.Error (
          String.Format (
            "The '{0}' property cannot set a value because the server has already been started.",
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
          _logger.Warn (
            String.Format (
              "Receiving has been stopped.\nreason: {0}.", ex.Message));

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

    private void stopListener (int timeOut)
    {
      _listener.Close ();
      _receiveRequestThread.Join (timeOut);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Adds the specified typed WebSocket service with the specified
    /// <paramref name="servicePath"/>.
    /// </summary>
    /// <remarks>
    /// This method converts <paramref name="servicePath"/> to URL-decoded string
    /// and removes <c>'/'</c> from tail end of <paramref name="servicePath"/>.
    /// </remarks>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket
    /// service.
    /// </param>
    /// <typeparam name="TWithNew">
    /// The type of the WebSocket service. The TWithNew must inherit the
    /// <see cref="WebSocketService"/> class and must have a public parameterless
    /// constructor.
    /// </typeparam>
    public void AddWebSocketService<TWithNew> (string servicePath)
      where TWithNew : WebSocketService, new ()
    {
      AddWebSocketService<TWithNew> (servicePath, () => new TWithNew ());
    }

    /// <summary>
    /// Adds the specified typed WebSocket service with the specified
    /// <paramref name="servicePath"/> and <paramref name="serviceConstructor"/>.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   This method converts <paramref name="servicePath"/> to URL-decoded
    ///   string and removes <c>'/'</c> from tail end of
    ///   <paramref name="servicePath"/>.
    ///   </para>
    ///   <para>
    ///   <paramref name="serviceConstructor"/> returns a initialized specified
    ///   typed WebSocket service instance.
    ///   </para>
    /// </remarks>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket
    /// service.
    /// </param>
    /// <param name="serviceConstructor">
    /// A Func&lt;T&gt; delegate that references the method used to initialize
    /// a new WebSocket service instance (a new WebSocket session).
    /// </param>
    /// <typeparam name="T">
    /// The type of the WebSocket service. The T must inherit the
    /// <see cref="WebSocketService"/> class.
    /// </typeparam>
    public void AddWebSocketService<T> (string servicePath, Func<T> serviceConstructor)
      where T : WebSocketService
    {
      var msg = servicePath.CheckIfValidServicePath () ??
                (serviceConstructor == null
                 ? "'serviceConstructor' must not be null."
                 : null);

      if (msg != null) {
        _logger.Error (
          String.Format ("{0}\nservice path: {1}", msg, servicePath ?? ""));

        return;
      }

      var host = new WebSocketServiceHost<T> (
        servicePath, serviceConstructor, _logger);

      if (!KeepClean)
        host.KeepClean = false;

      _serviceHosts.Add (host.ServicePath, host);
    }

    /// <summary>
    /// Gets the contents of the file with the specified <paramref name="path"/>.
    /// </summary>
    /// <returns>
    /// An array of <see cref="byte"/> that contains the contents of the file if
    /// it exists; otherwise, <see langword="null"/>.
    /// </returns>
    /// <param name="path">
    /// A <see cref="string"/> that contains a virtual path to the file to get.
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
    /// Removes the WebSocket service with the specified
    /// <paramref name="servicePath"/>.
    /// </summary>
    /// <remarks>
    /// This method converts <paramref name="servicePath"/> to URL-decoded string
    /// and removes <c>'/'</c> from tail end of <paramref name="servicePath"/>.
    /// </remarks>
    /// <returns>
    /// <c>true</c> if the WebSocket service is successfully found and removed;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket
    /// service to find.
    /// </param>
    public bool RemoveWebSocketService (string servicePath)
    {
      var msg = servicePath.CheckIfValidServicePath ();
      if (msg != null) {
        _logger.Error (
          String.Format ("{0}\nservice path: {1}", msg, servicePath));

        return false;
      }

      return _serviceHosts.Remove (servicePath);
    }

    /// <summary>
    /// Starts receiving the HTTP requests.
    /// </summary>
    public void Start ()
    {
      lock (_sync) {
        var msg = _state.CheckIfStopped () ?? checkIfCertExists ();
        if (msg != null) {
          _logger.Error (
            String.Format (
              "{0}\nstate: {1}\nsecure: {2}", msg, _state, _secure));

          return;
        }

        _serviceHosts.Start ();
        _listener.Start ();
        startReceiving ();

        _state = ServerState.START;
      }
    }

    /// <summary>
    /// Stops receiving the HTTP requests.
    /// </summary>
    public void Stop ()
    {
      lock (_sync) {
        var msg = _state.CheckIfStarted ();
        if (msg != null) {
          _logger.Error (String.Format ("{0}\nstate: {1}", msg, _state));
          return;
        }

        _state = ServerState.SHUTDOWN;
      }

      _serviceHosts.Stop (new byte [0], true);
      stopListener (5000);

      _state = ServerState.STOP;
    }

    /// <summary>
    /// Stops receiving the HTTP requests with the specified <see cref="ushort"/>
    /// and <see cref="string"/> used to stop the WebSocket services.
    /// </summary>
    /// <param name="code">
    /// A <see cref="ushort"/> that contains a status code indicating the reason
    /// for stop.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that contains the reason for stop.
    /// </param>
    public void Stop (ushort code, string reason)
    {
      byte [] data = null;
      lock (_sync) {
        var msg = _state.CheckIfStarted () ??
                  code.CheckIfValidCloseStatusCode () ??
                  (data = code.Append (reason)).CheckIfValidCloseData ();

        if (msg != null) {
          _logger.Error (
            String.Format (
              "{0}\nstate: {1}\ncode: {2}\nreason: {3}", msg, _state, code, reason));

          return;
        }

        _state = ServerState.SHUTDOWN;
      }

      _serviceHosts.Stop (data, !code.IsReserved ());
      stopListener (5000);

      _state = ServerState.STOP;
    }

    /// <summary>
    /// Stops receiving the HTTP requests with the specified
    /// <see cref="CloseStatusCode"/> and <see cref="string"/> used to stop the
    /// WebSocket services.
    /// </summary>
    /// <param name="code">
    /// One of the <see cref="CloseStatusCode"/> values that represent the status
    /// codes indicating the reasons for stop.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that contains the reason for stop.
    /// </param>
    public void Stop (CloseStatusCode code, string reason)
    {
      byte [] data = null;
      lock (_sync) {
        var msg = _state.CheckIfStarted () ??
                  (data = ((ushort) code).Append (reason)).CheckIfValidCloseData ();

        if (msg != null) {
          _logger.Error (
            String.Format ("{0}\nstate: {1}\nreason: {2}", msg, _state, reason));

          return;
        }

        _state = ServerState.SHUTDOWN;
      }

      _serviceHosts.Stop (data, !code.IsReserved ());
      stopListener (5000);

      _state = ServerState.STOP;
    }

    #endregion
  }
}
