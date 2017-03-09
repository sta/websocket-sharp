#region License
/*
 * WebSocketServer.cs
 *
 * A C# implementation of the WebSocket protocol server.
 *
 * The MIT License
 *
 * Copyright (c) 2012-2015 sta.blockhead
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
 * - Jonas Hovgaard <j@jhovgaard.dk>
 * - Liryna <liryna.stark@gmail.com>
 * - Rohan Singh <rohan-singh@hotmail.com>
 */
#endregion

using System;
using System.Collections.Generic;
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
  /// Provides a WebSocket protocol server.
  /// </summary>
  /// <remarks>
  /// This class can provide multiple WebSocket services.
  /// </remarks>
  public class WebSocketServer
  {
    #region Private Fields

    private System.Net.IPAddress               _address;
    private bool                               _allowForwardedRequest;
    private AuthenticationSchemes              _authSchemes;
    private static readonly string             _defaultRealm;
    private bool                               _dnsStyle;
    private string                             _hostname;
    private TcpListener                        _listener;
    private Logger                             _log;
    private int                                _port;
    private string                             _realm;
    private string                             _realmInUse;
    private Thread                             _receiveThread;
    private bool                               _reuseAddress;
    private bool                               _secure;
    private WebSocketServiceManager            _services;
    private ServerSslConfiguration             _sslConfig;
    private ServerSslConfiguration             _sslConfigInUse;
    private volatile ServerState               _state;
    private object                             _sync;
    private Func<IIdentity, NetworkCredential> _userCredFinder;

    #endregion

    #region Static Constructor

    static WebSocketServer ()
    {
      _defaultRealm = "SECRET AREA";
    }

    #endregion

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketServer"/> class.
    /// </summary>
    /// <remarks>
    /// The new instance listens for the incoming handshake requests on port 80.
    /// </remarks>
    public WebSocketServer ()
    {
      var addr = System.Net.IPAddress.Any;
      init (addr.ToString (), addr, 80, false);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketServer"/> class
    /// with the specified <paramref name="port"/>.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   The new instance listens for the incoming handshake requests on
    ///   <paramref name="port"/>.
    ///   </para>
    ///   <para>
    ///   It provides secure connections if <paramref name="port"/> is 443.
    ///   </para>
    /// </remarks>
    /// <param name="port">
    /// An <see cref="int"/> that represents the port number on which to listen.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="port"/> is less than 1 or greater than 65535.
    /// </exception>
    public WebSocketServer (int port)
      : this (port, port == 443)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketServer"/> class
    /// with the specified WebSocket URL.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   The new instance listens for the incoming handshake requests on
    ///   the host name and port of <paramref name="url"/>.
    ///   </para>
    ///   <para>
    ///   It provides secure connections if the scheme of <paramref name="url"/>
    ///   is wss.
    ///   </para>
    ///   <para>
    ///   If <paramref name="url"/> includes no port, either port 80 or 443 is
    ///   used on which to listen. It is determined by the scheme (ws or wss)
    ///   of <paramref name="url"/>.
    ///   </para>
    /// </remarks>
    /// <param name="url">
    /// A <see cref="string"/> that represents the WebSocket URL for the server.
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
    public WebSocketServer (string url)
    {
      if (url == null)
        throw new ArgumentNullException ("url");

      if (url.Length == 0)
        throw new ArgumentException ("An empty string.", "url");

      Uri uri;
      string msg;
      if (!tryCreateUri (url, out uri, out msg))
        throw new ArgumentException (msg, "url");

      var host = uri.DnsSafeHost;

      var addr = host.ToIPAddress ();
      if (addr == null) {
        msg = "The host part could not be converted to an IP address.";
        throw new ArgumentException (msg, "url");
      }

      if (!addr.IsLocal ()) {
        msg = "The IP address is not a local IP address.";
        throw new ArgumentException (msg, "url");
      }

      init (host, addr, uri.Port, uri.Scheme == "wss");
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketServer"/> class with
    /// the specified <paramref name="port"/> and <paramref name="secure"/>.
    /// </summary>
    /// <remarks>
    /// The new instance listens for the incoming handshake requests on
    /// <paramref name="port"/>.
    /// </remarks>
    /// <param name="port">
    /// An <see cref="int"/> that represents the port number on which to listen.
    /// </param>
    /// <param name="secure">
    /// A <see cref="bool"/> that specifies providing secure connections or not.
    /// <c>true</c> specifies providing secure connections.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="port"/> is less than 1 or greater than 65535.
    /// </exception>
    public WebSocketServer (int port, bool secure)
    {
      if (!port.IsPortNumber ()) {
        var msg = "It is less than 1 or greater than 65535.";
        throw new ArgumentOutOfRangeException ("port", msg);
      }

      var addr = System.Net.IPAddress.Any;
      init (addr.ToString (), addr, port, secure);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketServer"/> class with
    /// the specified <paramref name="address"/> and <paramref name="port"/>.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   The new instance listens for the incoming handshake requests on
    ///   <paramref name="address"/> and <paramref name="port"/>.
    ///   </para>
    ///   <para>
    ///   It provides secure connections if <paramref name="port"/> is 443.
    ///   </para>
    /// </remarks>
    /// <param name="address">
    /// A <see cref="System.Net.IPAddress"/> that represents the local IP address
    /// for the server.
    /// </param>
    /// <param name="port">
    /// An <see cref="int"/> that represents the port number on which to listen.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="address"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="address"/> is not a local IP address.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="port"/> is less than 1 or greater than 65535.
    /// </exception>
    public WebSocketServer (System.Net.IPAddress address, int port)
      : this (address, port, port == 443)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketServer"/> class
    /// with the specified <paramref name="address"/>, <paramref name="port"/>,
    /// and <paramref name="secure"/>.
    /// </summary>
    /// <remarks>
    /// The new instance listens for the incoming handshake requests on
    /// <paramref name="address"/> and <paramref name="port"/>.
    /// </remarks>
    /// <param name="address">
    /// A <see cref="System.Net.IPAddress"/> that represents the local IP address
    /// for the server.
    /// </param>
    /// <param name="port">
    /// An <see cref="int"/> that represents the port number on which to listen.
    /// </param>
    /// <param name="secure">
    /// A <see cref="bool"/> that specifies providing secure connections or not.
    /// <c>true</c> specifies providing secure connections.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="address"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="address"/> is not a local IP address.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="port"/> is less than 1 or greater than 65535.
    /// </exception>
    public WebSocketServer (System.Net.IPAddress address, int port, bool secure)
    {
      if (address == null)
        throw new ArgumentNullException ("address");

      if (!address.IsLocal ())
        throw new ArgumentException ("Not a local IP address.", "address");

      if (!port.IsPortNumber ()) {
        var msg = "It is less than 1 or greater than 65535.";
        throw new ArgumentOutOfRangeException ("port", msg);
      }

      init (address.ToString (), address, port, secure);
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the IP address of the server.
    /// </summary>
    /// <value>
    /// A <see cref="System.Net.IPAddress"/> that represents
    /// the IP address of the server.
    /// </value>
    public System.Net.IPAddress Address {
      get {
        return _address;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the server accepts
    /// a handshake request without checking the request URI.
    /// </summary>
    /// <remarks>
    /// The set operation does nothing if the server has already started or
    /// it is shutting down.
    /// </remarks>
    /// <value>
    /// <c>true</c> if the server accepts a handshake request without
    /// checking the request URI; otherwise, <c>false</c>. The default
    /// value is <c>false</c>.
    /// </value>
    public bool AllowForwardedRequest {
      get {
        return _allowForwardedRequest;
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

          _allowForwardedRequest = value;
        }
      }
    }

    /// <summary>
    /// Gets or sets the scheme used to authenticate the clients.
    /// </summary>
    /// <remarks>
    /// The set operation does nothing if the server has already started or
    /// it is shutting down.
    /// </remarks>
    /// <value>
    /// One of the <see cref="WebSocketSharp.Net.AuthenticationSchemes"/> enum
    /// values. It specifies the scheme used to authenticate the clients.
    /// The default value is
    /// <see cref="WebSocketSharp.Net.AuthenticationSchemes.Anonymous"/>.
    /// </value>
    public AuthenticationSchemes AuthenticationSchemes {
      get {
        return _authSchemes;
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

          _authSchemes = value;
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
    /// Gets a value indicating whether the server provides secure connections.
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
    /// The set operation does nothing if the server has already started or
    /// it is shutting down.
    /// </remarks>
    /// <value>
    /// <c>true</c> if the server cleans up the inactive sessions every 60
    /// seconds; otherwise, <c>false</c>. The default value is <c>true</c>.
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
    ///   <para>
    ///   The default logging level is <see cref="LogLevel.Error"/>.
    ///   </para>
    ///   <para>
    ///   If you would like to change the logging level,
    ///   you should set the <c>Log.Level</c> property to
    ///   any of the <see cref="LogLevel"/> enum values.
    ///   </para>
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
    /// Gets the port number of the server.
    /// </summary>
    /// <value>
    /// An <see cref="int"/> that represents the port number on which to
    /// listen for the incoming handshake requests.
    /// </value>
    public int Port {
      get {
        return _port;
      }
    }

    /// <summary>
    /// Gets or sets the name of the realm for the server.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   The set operation does nothing if the server has
    ///   already started or it is shutting down.
    ///   </para>
    ///   <para>
    ///   If this property is <see langword="null"/> or empty,
    ///   SECRET AREA will be used as the name.
    ///   </para>
    /// </remarks>
    /// <value>
    /// A <see cref="string"/> that represents the name of
    /// the realm or <see langword="null"/> by default.
    /// </value>
    public string Realm {
      get {
        return _realm;
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

          _realm = value;
        }
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the server is allowed to
    /// be bound to an address that is already in use.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   The set operation does nothing if the server has already started or
    ///   it is shutting down.
    ///   </para>
    ///   <para>
    ///   If you would like to resolve to wait for socket in TIME_WAIT state,
    ///   you should set this property to <c>true</c>.
    ///   </para>
    /// </remarks>
    /// <value>
    /// <c>true</c> if the server is allowed to be bound to an address that
    /// is already in use; otherwise, <c>false</c>. The default value is
    /// <c>false</c>.
    /// </value>
    public bool ReuseAddress {
      get {
        return _reuseAddress;
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

          _reuseAddress = value;
        }
      }
    }

    /// <summary>
    /// Gets the configuration used to provide secure connections.
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
        if (_sslConfig == null)
          _sslConfig = new ServerSslConfiguration (null);

        return _sslConfig;
      }
    }

    /// <summary>
    /// Gets or sets the delegate called to find the credentials for
    /// an identity used to authenticate a client.
    /// </summary>
    /// <remarks>
    /// The set operation does nothing if the server has already started or
    /// it is shutting down.
    /// </remarks>
    /// <value>
    /// A <c>Func&lt;IIdentity, NetworkCredential&gt;</c> delegate
    /// that invokes the method used to find the credentials or
    /// <see langword="null"/> by default.
    /// </value>
    public Func<IIdentity, NetworkCredential> UserCredentialsFinder {
      get {
        return _userCredFinder;
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

          _userCredFinder = value;
        }
      }
    }

    /// <summary>
    /// Gets or sets the wait time for the response to
    /// the WebSocket Ping or Close.
    /// </summary>
    /// <remarks>
    /// The set operation does nothing if the server has already
    /// started or it is shutting down.
    /// </remarks>
    /// <value>
    /// A <see cref="TimeSpan"/> that represents the wait time for
    /// the response. The default value is the same as 1 second.
    /// </value>
    /// <exception cref="ArgumentException">
    /// The value specified for a set operation is zero or less.
    /// </exception>
    public TimeSpan WaitTime {
      get {
        return _services.WaitTime;
      }

      set {
        string msg;
        if (!value.CheckWaitTime (out msg))
          throw new ArgumentException (msg, "value");

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
          _listener.Stop ();
        }
        finally {
          _services.Stop (1006, String.Empty);
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

    private bool checkHostNameForRequest (string name)
    {
      return !_dnsStyle
             || Uri.CheckHostName (name) != UriHostNameType.Dns
             || name == _hostname;
    }

    private bool checkServicePath (string path, out string message)
    {
      message = null;

      if (path.IsNullOrEmpty ()) {
        message = "'path' is null or empty.";
        return false;
      }

      if (path[0] != '/') {
        message = "'path' is not an absolute path.";
        return false;
      }

      if (path.IndexOfAny (new[] { '?', '#' }) > -1) {
        message = "'path' includes either or both query and fragment components.";
        return false;
      }

      return true;
    }

    private bool checkSslConfiguration (
      ServerSslConfiguration configuration, out string message
    )
    {
      message = null;

      if (!_secure)
        return true;

      if (configuration == null) {
        message = "There is no configuration.";
        return false;
      }

      if (configuration.ServerCertificate == null) {
        message = "The configuration has no server certificate.";
        return false;
      }

      return true;
    }

    private string getRealm ()
    {
      var realm = _realm;
      return realm != null && realm.Length > 0 ? realm : _defaultRealm;
    }

    private ServerSslConfiguration getSslConfiguration ()
    {
      var sslConfig = _sslConfig;
      if (sslConfig == null)
        return null;

      var ret =
        new ServerSslConfiguration (
          sslConfig.ServerCertificate,
          sslConfig.ClientCertificateRequired,
          sslConfig.EnabledSslProtocols,
          sslConfig.CheckCertificateRevocation
        );

      ret.ClientCertificateValidationCallback =
        sslConfig.ClientCertificateValidationCallback;

      return ret;
    }

    private void init (
      string hostname, System.Net.IPAddress address, int port, bool secure
    )
    {
      _hostname = hostname;
      _address = address;
      _port = port;
      _secure = secure;

      _authSchemes = AuthenticationSchemes.Anonymous;
      _dnsStyle = Uri.CheckHostName (hostname) == UriHostNameType.Dns;
      _listener = new TcpListener (address, port);
      _log = new Logger ();
      _services = new WebSocketServiceManager (_log);
      _sync = new object ();
    }

    private void processRequest (TcpListenerWebSocketContext context)
    {
      var uri = context.RequestUri;
      if (uri == null) {
        context.Close (HttpStatusCode.BadRequest);
        return;
      }

      if (!_allowForwardedRequest) {
        if (uri.Port != _port) {
          context.Close (HttpStatusCode.BadRequest);
          return;
        }

        if (!checkHostNameForRequest (uri.DnsSafeHost)) {
          context.Close (HttpStatusCode.NotFound);
          return;
        }
      }

      WebSocketServiceHost host;
      if (!_services.InternalTryGetServiceHost (uri.AbsolutePath, out host)) {
        context.Close (HttpStatusCode.NotImplemented);
        return;
      }

      host.StartSession (context);
    }

    private void receiveRequest ()
    {
      while (true) {
        TcpClient cl = null;
        try {
          cl = _listener.AcceptTcpClient ();
          ThreadPool.QueueUserWorkItem (
            state => {
              try {
                var ctx =
                  cl.GetWebSocketContext (null, _secure, _sslConfigInUse, _log);

                if (!ctx.Authenticate (_authSchemes, _realmInUse, _userCredFinder))
                  return;

                processRequest (ctx);
              }
              catch (Exception ex) {
                _log.Fatal (ex.ToString ());
                cl.Close ();
              }
            }
          );
        }
        catch (SocketException ex) {
          if (_state == ServerState.ShuttingDown) {
            _log.Info ("The receiving is stopped.");
            break;
          }

          _log.Fatal (ex.ToString ());
          break;
        }
        catch (Exception ex) {
          _log.Fatal (ex.ToString ());
          if (cl != null)
            cl.Close ();

          break;
        }
      }

      if (_state != ServerState.ShuttingDown)
        abort ();
    }

    private void start (ServerSslConfiguration sslConfig)
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

        _sslConfigInUse = sslConfig;
        _realmInUse = getRealm ();

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
      if (_reuseAddress) {
        _listener.Server.SetSocketOption (
          SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true
        );
      }

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
          stopReceiving (5000);
        }
        catch {
          threw = true;
          throw;
        }
        finally {
          try {
            _services.Stop (code, reason);
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
      _listener.Stop ();
      _receiveThread.Join (millisecondsTimeout);
    }

    private static bool tryCreateUri (
      string uriString, out Uri result, out string message
    )
    {
      if (!uriString.TryCreateWebSocketUri (out result, out message))
        return false;

      if (result.PathAndQuery != "/") {
        result = null;
        message = "It includes the path or query component.";

        return false;
      }

      return true;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Adds a WebSocket service with the specified behavior,
    /// <paramref name="path"/>, and <paramref name="initializer"/>.
    /// </summary>
    /// <param name="path">
    /// A <see cref="string"/> that represents an absolute path to
    /// the service. It will be converted to a URL-decoded string,
    /// and will be removed <c>'/'</c> from tail end if any.
    /// </param>
    /// <param name="initializer">
    /// A <c>Func&lt;TBehavior&gt;</c> delegate that invokes
    /// the method used to create a new session instance for
    /// the service. The method must create a new instance of
    /// the specified behavior class and return it.
    /// </param>
    /// <typeparam name="TBehavior">
    /// The type of the behavior for the service. It must inherit
    /// the <see cref="WebSocketBehavior"/> class.
    /// </typeparam>
    public void AddWebSocketService<TBehavior> (
      string path, Func<TBehavior> initializer
    )
      where TBehavior : WebSocketBehavior
    {
      if (path == null)
        throw new ArgumentNullException ("path");

      if (initializer == null)
        throw new ArgumentNullException ("initializer");

      if (path.Length == 0)
        throw new ArgumentException ("An empty string.", "path");

      if (path[0] != '/')
        throw new ArgumentException ("Not an absolute path.", "path");

      if (path.IndexOfAny (new[] { '?', '#' }) > -1) {
        var msg = "It includes either or both query and fragment components.";
        throw new ArgumentException (msg, "path");
      }

      _services.Add<TBehavior> (path, initializer);
    }

    /// <summary>
    /// Adds a WebSocket service with the specified behavior and
    /// <paramref name="path"/>.
    /// </summary>
    /// <remarks>
    /// <paramref name="path"/> is converted to a URL-decoded string and
    /// / is trimmed from the end of the converted string if any.
    /// </remarks>
    /// <param name="path">
    /// A <see cref="string"/> that represents an absolute path to
    /// the service to add.
    /// </param>
    /// <typeparam name="TBehaviorWithNew">
    /// The type of the behavior for the service. It must inherit
    /// the <see cref="WebSocketBehavior"/> class and it must have
    /// a public parameterless constructor.
    /// </typeparam>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="path"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="path"/> is empty.
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
    /// / is trimmed from the end of the converted string if any.
    /// </remarks>
    /// <param name="path">
    /// A <see cref="string"/> that represents an absolute path to
    /// the service to add.
    /// </param>
    /// <param name="initializer">
    /// An <c>Action&lt;TBehaviorWithNew&gt;</c> delegate that invokes
    /// the method used to initialize a new session instance for
    /// the service or <see langword="null"/> if not needed.
    /// </param>
    /// <typeparam name="TBehaviorWithNew">
    /// The type of the behavior for the service. It must inherit
    /// the <see cref="WebSocketBehavior"/> class and it must have
    /// a public parameterless constructor.
    /// </typeparam>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="path"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="path"/> is empty.
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
    /// Removes a WebSocket service with the specified <paramref name="path"/>.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   <paramref name="path"/> is converted to a URL-decoded string and
    ///   / is trimmed from the end of the converted string if any.
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
    ///   <paramref name="path"/> is empty.
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
    /// Starts receiving the WebSocket handshake requests.
    /// </summary>
    /// <remarks>
    /// This method does nothing if the server has already started or
    /// it is shutting down.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// There is no configuration used to provide secure connections or
    /// the configuration has no server certificate.
    /// </exception>
    /// <exception cref="SocketException">
    /// The underlying <see cref="TcpListener"/> has failed to start.
    /// </exception>
    public void Start ()
    {
      var sslConfig = getSslConfiguration ();

      string msg;
      if (!checkSslConfiguration (sslConfig, out msg))
        throw new InvalidOperationException (msg);

      start (sslConfig);
    }

    /// <summary>
    /// Stops receiving the WebSocket handshake requests,
    /// and closes the WebSocket connections.
    /// </summary>
    /// <remarks>
    /// This method does nothing if the server is not started,
    /// it is shutting down, or it has already stopped.
    /// </remarks>
    /// <exception cref="SocketException">
    /// The underlying <see cref="TcpListener"/> has failed to stop.
    /// </exception>
    public void Stop ()
    {
      stop (1005, String.Empty);
    }

    /// <summary>
    /// Stops receiving the WebSocket handshake requests,
    /// and closes the WebSocket connections with the specified
    /// <paramref name="code"/> and <paramref name="reason"/>.
    /// </summary>
    /// <remarks>
    /// This method does nothing if the server is not started,
    /// it is shutting down, or it has already stopped.
    /// </remarks>
    /// <param name="code">
    ///   <para>
    ///   A <see cref="ushort"/> that represents the status code
    ///   indicating the reason for the close.
    ///   </para>
    ///   <para>
    ///   The status codes are defined in
    ///   <see href="http://tools.ietf.org/html/rfc6455#section-7.4">
    ///   Section 7.4</see> of RFC 6455.
    ///   </para>
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that represents the reason for
    /// the close. The size must be 123 bytes or less in UTF-8.
    /// </param>
    /// <exception cref="SocketException">
    /// The underlying <see cref="TcpListener"/> has failed to stop.
    /// </exception>
    public void Stop (ushort code, string reason)
    {
      string msg;
      if (!WebSocket.CheckParametersForClose (code, reason, false, out msg)) {
        _log.Error (msg);
        return;
      }

      stop (code, reason);
    }

    /// <summary>
    /// Stops receiving the WebSocket handshake requests,
    /// and closes the WebSocket connections with the specified
    /// <paramref name="code"/> and <paramref name="reason"/>.
    /// </summary>
    /// <remarks>
    /// This method does nothing if the server is not started,
    /// it is shutting down, or it has already stopped.
    /// </remarks>
    /// <param name="code">
    /// One of the <see cref="CloseStatusCode"/> enum values.
    /// It represents the status code indicating the reason for
    /// the close.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that represents the reason for
    /// the close. The size must be 123 bytes or less in UTF-8.
    /// </param>
    /// <exception cref="SocketException">
    /// The underlying <see cref="TcpListener"/> has failed to stop.
    /// </exception>
    public void Stop (CloseStatusCode code, string reason)
    {
      string msg;
      if (!WebSocket.CheckParametersForClose (code, reason, false, out msg)) {
        _log.Error (msg);
        return;
      }

      stop ((ushort) code, reason);
    }

    #endregion
  }
}
