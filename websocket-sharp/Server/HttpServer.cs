#region License
/*
 * HttpServer.cs
 *
 * A simple HTTP server that allows to accept the WebSocket connection requests.
 *
 * The MIT License
 *
 * Copyright (c) 2012-2016 sta.blockhead
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
 * - Liryna <liryna.stark@gmail.com>
 * - Rohan Singh <rohan-singh@hotmail.com>
 */
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
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

    private System.Net.IPAddress    _address;
    private string                  _hostname;
    private HttpListener            _listener;
    private Logger                  _log;
    private int                     _port;
    private Thread                  _receiveThread;
    private string                  _rootPath;
    private bool                    _secure;
    private WebSocketServiceManager _services;
    private volatile ServerState    _state;
    private object                  _sync;

    #endregion

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpServer"/> class.
    /// </summary>
    /// <remarks>
    /// An instance initialized by this constructor listens for the incoming requests on port 80.
    /// </remarks>
    public HttpServer ()
    {
      init ("*", System.Net.IPAddress.Any, 80, false);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpServer"/> class with
    /// the specified <paramref name="port"/>.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   An instance initialized by this constructor listens for the incoming requests on
    ///   <paramref name="port"/>.
    ///   </para>
    ///   <para>
    ///   If <paramref name="port"/> is 443, that instance provides a secure connection.
    ///   </para>
    /// </remarks>
    /// <param name="port">
    /// An <see cref="int"/> that represents the port number on which to listen.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="port"/> isn't between 1 and 65535 inclusive.
    /// </exception>
    public HttpServer (int port)
      : this (port, port == 443)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpServer"/> class with
    /// the specified HTTP URL.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   An instance initialized by this constructor listens for the incoming requests on
    ///   the host name and port in <paramref name="url"/>.
    ///   </para>
    ///   <para>
    ///   If <paramref name="url"/> doesn't include a port, either port 80 or 443 is used on
    ///   which to listen. It's determined by the scheme (http or https) in <paramref name="url"/>.
    ///   (Port 80 if the scheme is http.)
    ///   </para>
    /// </remarks>
    /// <param name="url">
    /// A <see cref="string"/> that represents the HTTP URL of the server.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="url"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="url"/> is empty.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="url"/> is invalid.
    ///   </para>
    /// </exception>
    public HttpServer (string url)
    {
      if (url == null)
        throw new ArgumentNullException ("url");

      if (url.Length == 0)
        throw new ArgumentException ("An empty string.", "url");

      Uri uri;
      string msg;
      if (!tryCreateUri (url, out uri, out msg))
        throw new ArgumentException (msg, "url");

      var host = getHost (uri);
      var addr = host.ToIPAddress ();
      if (!addr.IsLocal ())
        throw new ArgumentException ("The host part isn't a local host name: " + url, "url");

      init (host, addr, uri.Port, uri.Scheme == "https");
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpServer"/> class with
    /// the specified <paramref name="port"/> and <paramref name="secure"/>.
    /// </summary>
    /// <remarks>
    /// An instance initialized by this constructor listens for the incoming requests on
    /// <paramref name="port"/>.
    /// </remarks>
    /// <param name="port">
    /// An <see cref="int"/> that represents the port number on which to listen.
    /// </param>
    /// <param name="secure">
    /// A <see cref="bool"/> that indicates providing a secure connection or not.
    /// (<c>true</c> indicates providing a secure connection.)
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="port"/> isn't between 1 and 65535 inclusive.
    /// </exception>
    public HttpServer (int port, bool secure)
    {
      if (!port.IsPortNumber ())
        throw new ArgumentOutOfRangeException (
          "port", "Not between 1 and 65535 inclusive: " + port);

      init ("*", System.Net.IPAddress.Any, port, secure);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpServer"/> class with
    /// the specified <paramref name="address"/> and <paramref name="port"/>.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   An instance initialized by this constructor listens for the incoming requests on
    ///   <paramref name="address"/> and <paramref name="port"/>.
    ///   </para>
    ///   <para>
    ///   If <paramref name="port"/> is 443, that instance provides a secure connection.
    ///   </para>
    /// </remarks>
    /// <param name="address">
    /// A <see cref="System.Net.IPAddress"/> that represents the local IP address of the server.
    /// </param>
    /// <param name="port">
    /// An <see cref="int"/> that represents the port number on which to listen.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="address"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="address"/> isn't a local IP address.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="port"/> isn't between 1 and 65535 inclusive.
    /// </exception>
    public HttpServer (System.Net.IPAddress address, int port)
      : this (address, port, port == 443)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpServer"/> class with
    /// the specified <paramref name="address"/>, <paramref name="port"/>,
    /// and <paramref name="secure"/>.
    /// </summary>
    /// <remarks>
    /// An instance initialized by this constructor listens for the incoming requests on
    /// <paramref name="address"/> and <paramref name="port"/>.
    /// </remarks>
    /// <param name="address">
    /// A <see cref="System.Net.IPAddress"/> that represents the local IP address of the server.
    /// </param>
    /// <param name="port">
    /// An <see cref="int"/> that represents the port number on which to listen.
    /// </param>
    /// <param name="secure">
    /// A <see cref="bool"/> that indicates providing a secure connection or not.
    /// (<c>true</c> indicates providing a secure connection.)
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="address"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="address"/> isn't a local IP address.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="port"/> isn't between 1 and 65535 inclusive.
    /// </exception>
    public HttpServer (System.Net.IPAddress address, int port, bool secure)
    {
      if (address == null)
        throw new ArgumentNullException ("address");

      if (!address.IsLocal ())
        throw new ArgumentException ("Not a local IP address: " + address, "address");

      if (!port.IsPortNumber ())
        throw new ArgumentOutOfRangeException (
          "port", "Not between 1 and 65535 inclusive: " + port);

      init (null, address, port, secure);
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the IP address of the server.
    /// </summary>
    /// <value>
    /// A <see cref="System.Net.IPAddress"/> that represents the local
    /// IP address on which to listen for incoming requests.
    /// </value>
    public System.Net.IPAddress Address {
      get {
        return _address;
      }
    }

    /// <summary>
    /// Gets or sets the scheme used to authenticate the clients.
    /// </summary>
    /// <remarks>
    /// The set operation does nothing if the server has already
    /// started or it is shutting down.
    /// </remarks>
    /// <value>
    ///   <para>
    ///   One of the <see cref="WebSocketSharp.Net.AuthenticationSchemes"/>
    ///   enum values.
    ///   </para>
    ///   <para>
    ///   It represents the scheme used to authenticate the clients.
    ///   </para>
    ///   <para>
    ///   The default value is
    ///   <see cref="WebSocketSharp.Net.AuthenticationSchemes.Anonymous"/>.
    ///   </para>
    /// </value>
    public AuthenticationSchemes AuthenticationSchemes {
      get {
        return _listener.AuthenticationSchemes;
      }

      set {
        string msg;
        if (!canSet (out msg)) {
          _log.Warn (msg);
          return;
        }

        lock (_sync) {
          if (!canSet (out msg)) {
            _log.Warn (msg);
            return;
          }

          _listener.AuthenticationSchemes = value;
        }
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
    /// Gets a value indicating whether the server provides
    /// secure connections.
    /// </summary>
    /// <value>
    /// <c>true</c> if the server provides secure connections;
    /// otherwise, <c>false</c>.
    /// </value>
    public bool IsSecure {
      get {
        return _secure;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the server cleans up
    /// the inactive sessions periodically.
    /// </summary>
    /// <remarks>
    /// The set operation does nothing if the server has already
    /// started or it is shutting down.
    /// </remarks>
    /// <value>
    ///   <para>
    ///   <c>true</c> if the server cleans up the inactive sessions
    ///   every 60 seconds; otherwise, <c>false</c>.
    ///   </para>
    ///   <para>
    ///   The default value is <c>true</c>.
    ///   </para>
    /// </value>
    public bool KeepClean {
      get {
        return _services.KeepClean;
      }

      set {
        string msg;
        if (!canSet (out msg)) {
          _log.Warn (msg);
          return;
        }

        lock (_sync) {
          if (!canSet (out msg)) {
            _log.Warn (msg);
            return;
          }

          _services.KeepClean = value;
        }
      }
    }

    /// <summary>
    /// Gets the logging function for the server.
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
    }

    /// <summary>
    /// Gets the port of the server.
    /// </summary>
    /// <value>
    /// An <see cref="int"/> that represents the number of the port
    /// on which to listen for incoming requests.
    /// </value>
    public int Port {
      get {
        return _port;
      }
    }

    /// <summary>
    /// Gets or sets the realm used for authentication.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   "SECRET AREA" is used as the realm if the value is
    ///   <see langword="null"/> or an empty string.
    ///   </para>
    ///   <para>
    ///   The set operation does nothing if the server has
    ///   already started or it is shutting down.
    ///   </para>
    /// </remarks>
    /// <value>
    ///   <para>
    ///   A <see cref="string"/> or <see langword="null"/> by default.
    ///   </para>
    ///   <para>
    ///   That string represents the name of the realm.
    ///   </para>
    /// </value>
    public string Realm {
      get {
        return _listener.Realm;
      }

      set {
        string msg;
        if (!canSet (out msg)) {
          _log.Warn (msg);
          return;
        }

        lock (_sync) {
          if (!canSet (out msg)) {
            _log.Warn (msg);
            return;
          }

          _listener.Realm = value;
        }
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the server is allowed to
    /// be bound to an address that is already in use.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   You should set this property to <c>true</c> if you would
    ///   like to resolve to wait for socket in TIME_WAIT state.
    ///   </para>
    ///   <para>
    ///   The set operation does nothing if the server has already
    ///   started or it is shutting down.
    ///   </para>
    /// </remarks>
    /// <value>
    ///   <para>
    ///   <c>true</c> if the server is allowed to be bound to an address
    ///   that is already in use; otherwise, <c>false</c>.
    ///   </para>
    ///   <para>
    ///   The default value is <c>false</c>.
    ///   </para>
    /// </value>
    public bool ReuseAddress {
      get {
        return _listener.ReuseAddress;
      }

      set {
        string msg;
        if (!canSet (out msg)) {
          _log.Warn (msg);
          return;
        }

        lock (_sync) {
          if (!canSet (out msg)) {
            _log.Warn (msg);
            return;
          }

          _listener.ReuseAddress = value;
        }
      }
    }

    /// <summary>
    /// Gets or sets the path to the document folder of the server.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   '/' or '\' is trimmed from the end of the value if any.
    ///   </para>
    ///   <para>
    ///   The set operation does nothing if the server has already
    ///   started or it is shutting down.
    ///   </para>
    /// </remarks>
    /// <value>
    ///   <para>
    ///   A <see cref="string"/> that represents a path to the folder
    ///   from which to find the requested file.
    ///   </para>
    ///   <para>
    ///   The default value is "./Public".
    ///   </para>
    /// </value>
    /// <exception cref="ArgumentNullException">
    /// The value specified for a set operation is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The value specified for a set operation is an empty string.
    /// </exception>
    public string RootPath {
      get {
        return _rootPath;
      }

      set {
        if (value == null)
          throw new ArgumentNullException ("value");

        if (value.Length == 0)
          throw new ArgumentException ("An empty string.", "value");

        string msg;
        if (!canSet (out msg)) {
          _log.Warn (msg);
          return;
        }

        lock (_sync) {
          if (!canSet (out msg)) {
            _log.Warn (msg);
            return;
          }

          _rootPath = value.TrimSlashOrBackslashFromEnd ();
        }
      }
    }

    /// <summary>
    /// Gets the configuration for secure connections.
    /// </summary>
    /// <remarks>
    /// The configuration will be referenced when the server starts.
    /// So you must configure it before calling the start method.
    /// </remarks>
    /// <value>
    /// A <see cref="ServerSslConfiguration"/> that represents
    /// the configuration used to provide secure connections.
    /// </value>
    public ServerSslConfiguration SslConfiguration {
      get {
        return _listener.SslConfiguration;
      }
    }

    /// <summary>
    /// Gets or sets the delegate used to find the credentials
    /// for an identity.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   No credentials are found if the method invoked by
    ///   the delegate returns <see langword="null"/> or
    ///   the value is <see langword="null"/>.
    ///   </para>
    ///   <para>
    ///   The set operation does nothing if the server has
    ///   already started or it is shutting down.
    ///   </para>
    /// </remarks>
    /// <value>
    ///   <para>
    ///   A <c>Func&lt;<see cref="IIdentity"/>,
    ///   <see cref="NetworkCredential"/>&gt;</c> delegate or
    ///   <see langword="null"/> if not needed.
    ///   </para>
    ///   <para>
    ///   That delegate invokes the method called for finding
    ///   the credentials used to authenticate a client.
    ///   </para>
    ///   <para>
    ///   The default value is <see langword="null"/>.
    ///   </para>
    /// </value>
    public Func<IIdentity, NetworkCredential> UserCredentialsFinder {
      get {
        return _listener.UserCredentialsFinder;
      }

      set {
        string msg;
        if (!canSet (out msg)) {
          _log.Warn (msg);
          return;
        }

        lock (_sync) {
          if (!canSet (out msg)) {
            _log.Warn (msg);
            return;
          }

          _listener.UserCredentialsFinder = value;
        }
      }
    }

    /// <summary>
    /// Gets or sets the time to wait for the response to
    /// the WebSocket Ping or Close.
    /// </summary>
    /// <remarks>
    /// The set operation does nothing if the server has already
    /// started or it is shutting down.
    /// </remarks>
    /// <value>
    ///   <para>
    ///   A <see cref="TimeSpan"/> to wait for the response.
    ///   </para>
    ///   <para>
    ///   The default value is the same as 1 second.
    ///   </para>
    /// </value>
    /// <exception cref="ArgumentException">
    /// The value specified for a set operation is zero or less.
    /// </exception>
    public TimeSpan WaitTime {
      get {
        return _services.WaitTime;
      }

      set {
        if (value <= TimeSpan.Zero)
          throw new ArgumentException ("Zero or less.", "value");

        string msg;
        if (!canSet (out msg)) {
          _log.Warn (msg);
          return;
        }

        lock (_sync) {
          if (!canSet (out msg)) {
            _log.Warn (msg);
            return;
          }

          _services.WaitTime = value;
        }
      }
    }

    /// <summary>
    /// Gets the management function for the WebSocket services
    /// provided by the server.
    /// </summary>
    /// <value>
    /// A <see cref="WebSocketServiceManager"/> that manages
    /// the WebSocket services provided by the server.
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
        if (_state != ServerState.Start)
          return;

        _state = ServerState.ShuttingDown;
      }

      try {
        try {
          _services.Stop (1006, String.Empty);
        }
        finally {
          _listener.Abort ();
        }
      }
      catch {
      }

      _state = ServerState.Stop;
    }

    private bool canSet (out string message)
    {
      message = null;

      if (_state == ServerState.Start) {
        message = "The server has already started.";
        return false;
      }

      if (_state == ServerState.ShuttingDown) {
        message = "The server is shutting down.";
        return false;
      }

      return true;
    }

    private bool checkCertificate (out string message)
    {
      message = null;

      if (!_secure)
        return true;

      var user = _listener.SslConfiguration.ServerCertificate != null;

      var path = _listener.CertificateFolderPath;
      var port = EndPointListener.CertificateExists (_port, path);

      if (user && port) {
        _log.Warn ("The certificate associated with the port will be used.");
        return true;
      }

      if (!(user || port)) {
        message = "There is no certificate used to authenticate the server.";
        return false;
      }

      return true;
    }

    private static string convertToString (System.Net.IPAddress address)
    {
      return address.AddressFamily == AddressFamily.InterNetworkV6
             ? String.Format ("[{0}]", address.ToString ())
             : address.ToString ();
    }

    private string createFilePath (string path)
    {
      var parent = _rootPath;
      var child = path.TrimStart ('/', '\\');

      var buff = new StringBuilder (parent, 32);
      if (parent == "/" || parent == "\\")
        buff.Append (child);
      else
        buff.AppendFormat ("/{0}", child);

      return buff.ToString ().Replace ('\\', '/');
    }

    private static string getHost (Uri uri)
    {
      return uri.HostNameType == UriHostNameType.IPv6 ? uri.Host : uri.DnsSafeHost;
    }

    private void init (
      string hostname, System.Net.IPAddress address, int port, bool secure
    )
    {
      _hostname = hostname ?? convertToString (address);
      _address = address;
      _port = port;
      _secure = secure;

      var lsnr = new HttpListener ();
      var pref = String.Format (
                   "http{0}://{1}:{2}/", secure ? "s" : "", _hostname, port
                 );

      lsnr.Prefixes.Add (pref);
      _listener = lsnr;

      _log = _listener.Log;
      _rootPath = "./Public";
      _services = new WebSocketServiceManager (_log);
      _sync = new object ();
    }

    private void processRequest (HttpListenerContext context)
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
        evt (this, new HttpRequestEventArgs (context, _rootPath));
      else
        context.Response.StatusCode = (int) HttpStatusCode.NotImplemented;

      context.Response.Close ();
    }

    private void processRequest (HttpListenerWebSocketContext context)
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
        HttpListenerContext ctx = null;
        try {
          ctx = _listener.GetContext ();
          ThreadPool.QueueUserWorkItem (
            state => {
              try {
                if (ctx.Request.IsUpgradeTo ("websocket")) {
                  processRequest (ctx.AcceptWebSocket (null));
                  return;
                }

                processRequest (ctx);
              }
              catch (Exception ex) {
                _log.Fatal (ex.Message);
                _log.Debug (ex.ToString ());

                ctx.Connection.Close (true);
              }
            }
          );
        }
        catch (HttpListenerException ex) {
          if (_state == ServerState.ShuttingDown) {
            _log.Info ("The receiving is stopped.");
            break;
          }

          _log.Fatal (ex.Message);
          _log.Debug (ex.ToString ());

          break;
        }
        catch (Exception ex) {
          _log.Fatal (ex.Message);
          _log.Debug (ex.ToString ());

          if (ctx != null)
            ctx.Connection.Close (true);

          break;
        }
      }

      if (_state != ServerState.ShuttingDown)
        abort ();
    }

    private void start ()
    {
      if (_state == ServerState.Start) {
        _log.Info ("The server has already started.");
        return;
      }

      if (_state == ServerState.ShuttingDown) {
        _log.Warn ("The server is shutting down.");
        return;
      }

      lock (_sync) {
        if (_state == ServerState.Start) {
          _log.Info ("The server has already started.");
          return;
        }

        if (_state == ServerState.ShuttingDown) {
          _log.Warn ("The server is shutting down.");
          return;
        }

        _services.Start ();

        try {
          startReceiving ();
        }
        catch {
          _services.Stop (1011, String.Empty);
          throw;
        }

        _state = ServerState.Start;
      }
    }

    private void startReceiving ()
    {
      _listener.Start ();
      _receiveThread = new Thread (new ThreadStart (receiveRequest));
      _receiveThread.IsBackground = true;
      _receiveThread.Start ();
    }

    private void stop (ushort code, string reason)
    {
      if (_state == ServerState.Ready) {
        _log.Info ("The server is not started.");
        return;
      }

      if (_state == ServerState.ShuttingDown) {
        _log.Info ("The server is shutting down.");
        return;
      }

      if (_state == ServerState.Stop) {
        _log.Info ("The server has already stopped.");
        return;
      }

      lock (_sync) {
        if (_state == ServerState.ShuttingDown) {
          _log.Info ("The server is shutting down.");
          return;
        }

        if (_state == ServerState.Stop) {
          _log.Info ("The server has already stopped.");
          return;
        }

        _state = ServerState.ShuttingDown;
      }

      try {
        var threw = false;
        try {
          _services.Stop (code, reason);
        }
        catch {
          threw = true;
          throw;
        }
        finally {
          try {
            stopReceiving (5000);
          }
          catch {
            if (!threw)
              throw;
          }
        }
      }
      finally {
        _state = ServerState.Stop;
      }
    }

    private void stopReceiving (int millisecondsTimeout)
    {
      _listener.Close ();
      _receiveThread.Join (millisecondsTimeout);
    }

    private static bool tryCreateUri (
      string uriString, out Uri result, out string message
    )
    {
      result = null;
      message = null;

      var uri = uriString.ToUri ();
      if (uri == null) {
        message = "An invalid URI string.";
        return false;
      }

      if (!uri.IsAbsoluteUri) {
        message = "A relative URI.";
        return false;
      }

      var schm = uri.Scheme;
      if (!(schm == "http" || schm == "https")) {
        message = "The scheme part is not 'http' or 'https'.";
        return false;
      }

      if (uri.PathAndQuery != "/") {
        message = "It includes either or both path and query components.";
        return false;
      }

      if (uri.Fragment.Length > 0) {
        message = "It includes the fragment component.";
        return false;
      }

      if (uri.Port == 0) {
        message = "The port part is zero.";
        return false;
      }

      result = uri;
      return true;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Adds a WebSocket service with the specified behavior,
    /// <paramref name="path"/>, and <paramref name="creator"/>.
    /// </summary>
    /// <remarks>
    /// <paramref name="path"/> is converted to a URL-decoded string and
    /// '/' is trimmed from the end of the converted string if any.
    /// </remarks>
    /// <param name="path">
    /// A <see cref="string"/> that represents an absolute path to
    /// the service to add.
    /// </param>
    /// <param name="creator">
    ///   <para>
    ///   A <c>Func&lt;TBehavior&gt;</c> delegate.
    ///   </para>
    ///   <para>
    ///   It invokes the method called for creating
    ///   a new session instance for the service.
    ///   </para>
    ///   <para>
    ///   The method must create a new instance of
    ///   the specified behavior class and return it.
    ///   </para>
    /// </param>
    /// <typeparam name="TBehavior">
    ///   <para>
    ///   The type of the behavior for the service.
    ///   </para>
    ///   <para>
    ///   It must inherit the <see cref="WebSocketBehavior"/> class.
    ///   </para>
    /// </typeparam>
    /// <exception cref="ArgumentNullException">
    ///   <para>
    ///   <paramref name="path"/> is <see langword="null"/>.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="creator"/> is <see langword="null"/>.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="path"/> is an empty string.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="path"/> is not an absolute path.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="path"/> includes either or both
    ///   query and fragment components.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="path"/> is already in use.
    ///   </para>
    /// </exception>
    [Obsolete ("This method will be removed. Use added one instead.")]
    public void AddWebSocketService<TBehavior> (
      string path, Func<TBehavior> creator
    )
      where TBehavior : WebSocketBehavior
    {
      if (path == null)
        throw new ArgumentNullException ("path");

      if (creator == null)
        throw new ArgumentNullException ("creator");

      if (path.Length == 0)
        throw new ArgumentException ("An empty string.", "path");

      if (path[0] != '/')
        throw new ArgumentException ("Not an absolute path.", "path");

      if (path.IndexOfAny (new[] { '?', '#' }) > -1) {
        var msg = "It includes either or both query and fragment components.";
        throw new ArgumentException (msg, "path");
      }

      _services.Add<TBehavior> (path, creator);
    }

    /// <summary>
    /// Adds a WebSocket service with the specified behavior and
    /// <paramref name="path"/>.
    /// </summary>
    /// <remarks>
    /// <paramref name="path"/> is converted to a URL-decoded string and
    /// '/' is trimmed from the end of the converted string if any.
    /// </remarks>
    /// <param name="path">
    /// A <see cref="string"/> that represents an absolute path to
    /// the service to add.
    /// </param>
    /// <typeparam name="TBehaviorWithNew">
    ///   <para>
    ///   The type of the behavior for the service.
    ///   </para>
    ///   <para>
    ///   It must inherit the <see cref="WebSocketBehavior"/> class and
    ///   must have a public parameterless constructor.
    ///   </para>
    /// </typeparam>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="path"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="path"/> is an empty string.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="path"/> is not an absolute path.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="path"/> includes either or both
    ///   query and fragment components.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="path"/> is already in use.
    ///   </para>
    /// </exception>
    public void AddWebSocketService<TBehaviorWithNew> (string path)
      where TBehaviorWithNew : WebSocketBehavior, new ()
    {
      _services.AddService<TBehaviorWithNew> (path, null);
    }

    /// <summary>
    /// Adds a WebSocket service with the specified behavior,
    /// <paramref name="path"/>, and <paramref name="initializer"/>.
    /// </summary>
    /// <remarks>
    /// <paramref name="path"/> is converted to a URL-decoded string and
    /// '/' is trimmed from the end of the converted string if any.
    /// </remarks>
    /// <param name="path">
    /// A <see cref="string"/> that represents an absolute path to
    /// the service to add.
    /// </param>
    /// <param name="initializer">
    ///   <para>
    ///   An <c>Action&lt;TBehaviorWithNew&gt;</c> delegate or
    ///   <see langword="null"/> if not needed.
    ///   </para>
    ///   <para>
    ///   That delegate invokes the method called for initializing
    ///   a new session instance for the service.
    ///   </para>
    /// </param>
    /// <typeparam name="TBehaviorWithNew">
    ///   <para>
    ///   The type of the behavior for the service.
    ///   </para>
    ///   <para>
    ///   It must inherit the <see cref="WebSocketBehavior"/> class and
    ///   must have a public parameterless constructor.
    ///   </para>
    /// </typeparam>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="path"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="path"/> is an empty string.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="path"/> is not an absolute path.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="path"/> includes either or both
    ///   query and fragment components.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="path"/> is already in use.
    ///   </para>
    /// </exception>
    public void AddWebSocketService<TBehaviorWithNew> (
      string path, Action<TBehaviorWithNew> initializer
    )
      where TBehaviorWithNew : WebSocketBehavior, new ()
    {
      _services.AddService<TBehaviorWithNew> (path, initializer);
    }

    /// <summary>
    /// Gets the file with the specified <paramref name="path"/> from
    /// the document folder of the server.
    /// </summary>
    /// <returns>
    ///   <para>
    ///   An array of <see cref="byte"/> or <see langword="null"/>
    ///   if not found.
    ///   </para>
    ///   <para>
    ///   That array represents the contents of the file.
    ///   </para>
    /// </returns>
    /// <param name="path">
    /// A <see cref="string"/> that represents a virtual path to
    /// the file to find from the document folder.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="path"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="path"/> is an empty string.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="path"/> is an invalid path.
    ///   </para>
    /// </exception>
    [Obsolete ("This method will be removed.")]
    public byte[] GetFile (string path)
    {
      if (path == null)
        throw new ArgumentNullException ("path");

      if (path.Length == 0)
        throw new ArgumentException ("An empty string.", "path");

      if (path.IndexOf (':') > -1)
        throw new ArgumentException ("It contains ':'.", "path");

      if (path.IndexOf ("..") > -1)
        throw new ArgumentException ("It contains '..'.", "path");

      if (path.IndexOf ("//") > -1)
        throw new ArgumentException ("It contains '//'.", "path");

      if (path.IndexOf ("\\\\") > -1)
        throw new ArgumentException ("It contains '\\\\'.", "path");

      path = createFilePath (path);
      return File.Exists (path) ? File.ReadAllBytes (path) : null;
    }

    /// <summary>
    /// Removes a WebSocket service with the specified <paramref name="path"/>.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   <paramref name="path"/> is converted to a URL-decoded string and
    ///   '/' is trimmed from the end of the converted string if any.
    ///   </para>
    ///   <para>
    ///   The service is stopped with close status 1001 (going away)
    ///   if it has already started.
    ///   </para>
    /// </remarks>
    /// <returns>
    /// <c>true</c> if the service is successfully found and removed;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <param name="path">
    /// A <see cref="string"/> that represents an absolute path to
    /// the service to remove.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="path"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="path"/> is an empty string.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="path"/> is not an absolute path.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="path"/> includes either or both
    ///   query and fragment components.
    ///   </para>
    /// </exception>
    public bool RemoveWebSocketService (string path)
    {
      return _services.RemoveService (path);
    }

    /// <summary>
    /// Starts receiving incoming requests.
    /// </summary>
    /// <remarks>
    /// This method does nothing if the server has already
    /// started or it is shutting down.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// There is no certificate used to authenticate the server.
    /// </exception>
    public void Start ()
    {
      string msg;
      if (!checkCertificate (out msg))
        throw new InvalidOperationException (msg);

      start ();
    }

    /// <summary>
    /// Stops receiving incoming requests and closes each connection.
    /// </summary>
    /// <remarks>
    /// This method does nothing if the server is not started,
    /// it is shutting down, or it has already stopped.
    /// </remarks>
    public void Stop ()
    {
      stop (1005, String.Empty);
    }

    /// <summary>
    /// Stops receiving incoming requests and closes each connection.
    /// </summary>
    /// <remarks>
    /// This method does nothing if the server is not started,
    /// it is shutting down, or it has already stopped.
    /// </remarks>
    /// <param name="code">
    ///   <para>
    ///   A <see cref="ushort"/> that represents the status code
    ///   indicating the reason for the WebSocket connection close.
    ///   </para>
    ///   <para>
    ///   The status codes are defined in
    ///   <see href="http://tools.ietf.org/html/rfc6455#section-7.4">
    ///   Section 7.4</see> of RFC 6455.
    ///   </para>
    /// </param>
    /// <param name="reason">
    ///   <para>
    ///   A <see cref="string"/> that represents the reason for
    ///   the WebSocket connection close.
    ///   </para>
    ///   <para>
    ///   The size must be 123 bytes or less in UTF-8.
    ///   </para>
    /// </param>
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
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="code"/> is 1010 (mandatory extension).
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="code"/> is 1005 (no status) and
    ///   there is <paramref name="reason"/>.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="reason"/> could not be UTF-8-encoded.
    ///   </para>
    /// </exception>
    public void Stop (ushort code, string reason)
    {
      if (!code.IsCloseStatusCode ()) {
        var msg = "Less than 1000 or greater than 4999.";
        throw new ArgumentOutOfRangeException ("code", msg);
      }

      if (code == 1010) {
        var msg = "1010 cannot be used.";
        throw new ArgumentException (msg, "code");
      }

      if (!reason.IsNullOrEmpty ()) {
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
      }

      stop (code, reason);
    }

    /// <summary>
    /// Stops receiving incoming requests and closes each connection.
    /// </summary>
    /// <remarks>
    /// This method does nothing if the server is not started,
    /// it is shutting down, or it has already stopped.
    /// </remarks>
    /// <param name="code">
    ///   <para>
    ///   One of the <see cref="CloseStatusCode"/> enum values.
    ///   </para>
    ///   <para>
    ///   It represents the status code indicating the reason for
    ///   the WebSocket connection close.
    ///   </para>
    /// </param>
    /// <param name="reason">
    ///   <para>
    ///   A <see cref="string"/> that represents the reason for
    ///   the WebSocket connection close.
    ///   </para>
    ///   <para>
    ///   The size must be 123 bytes or less in UTF-8.
    ///   </para>
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The size of <paramref name="reason"/> is greater than 123 bytes.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="code"/> is
    ///   <see cref="CloseStatusCode.MandatoryExtension"/>.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="code"/> is
    ///   <see cref="CloseStatusCode.NoStatus"/> and
    ///   there is <paramref name="reason"/>.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="reason"/> could not be UTF-8-encoded.
    ///   </para>
    /// </exception>
    public void Stop (CloseStatusCode code, string reason)
    {
      if (code == CloseStatusCode.MandatoryExtension) {
        var msg = "MandatoryExtension cannot be used.";
        throw new ArgumentException (msg, "code");
      }

      if (!reason.IsNullOrEmpty ()) {
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
      }

      stop ((ushort) code, reason);
    }

    #endregion
  }
}
