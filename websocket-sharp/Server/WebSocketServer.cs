#region License
/*
 * WebSocketServer.cs
 *
 * A C# implementation of the WebSocket protocol server.
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
 * - Jonas Hovgaard <j@jhovgaard.dk>
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
  /// The WebSocketServer class can provide multiple WebSocket services.
  /// </remarks>
  public class WebSocketServer
  {
    #region Private Fields

    private System.Net.IPAddress               _address;
    private AuthenticationSchemes              _authSchemes;
    private X509Certificate2                   _certificate;
    private Func<IIdentity, NetworkCredential> _credentialsFinder;
    private TcpListener                        _listener;
    private Logger                             _logger;
    private int                                _port;
    private string                             _realm;
    private Thread                             _receiveRequestThread;
    private bool                               _reuseAddress;
    private bool                               _secure;
    private WebSocketServiceManager            _services;
    private volatile ServerState               _state;
    private object                             _sync;
    private Uri                                _uri;

    #endregion

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketServer"/> class.
    /// </summary>
    /// <remarks>
    /// An instance initialized by this constructor listens for the incoming connection requests
    /// on port 80.
    /// </remarks>
    public WebSocketServer ()
      : this (System.Net.IPAddress.Any, 80, false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketServer"/> class with the specified
    /// <paramref name="port"/>.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   An instance initialized by this constructor listens for the incoming connection requests
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
    public WebSocketServer (int port)
      : this (System.Net.IPAddress.Any, port, port == 443)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketServer"/> class with the specified
    /// WebSocket URL.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   An instance initialized by this constructor listens for the incoming connection requests
    ///   on the port in <paramref name="url"/>.
    ///   </para>
    ///   <para>
    ///   If <paramref name="url"/> doesn't include a port, either port 80 or 443 is used on which
    ///   to listen. It's determined by the scheme (ws or wss) in <paramref name="url"/>.
    ///   (Port 80 if the scheme is ws.)
    ///   </para>
    /// </remarks>
    /// <param name="url">
    /// A <see cref="string"/> that represents the WebSocket URL of the server.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="url"/> is invalid.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="url"/> is <see langword="null"/>.
    /// </exception>
    public WebSocketServer (string url)
    {
      if (url == null)
        throw new ArgumentNullException ("url");

      string msg;
      if (!tryCreateUri (url, out _uri, out msg))
        throw new ArgumentException (msg, "url");

      _address = _uri.DnsSafeHost.ToIPAddress ();
      if (_address == null || !_address.IsLocal ())
        throw new ArgumentException ("The host part isn't a local host name: " + url, "url");

      _port = _uri.Port;
      _secure = _uri.Scheme == "wss";

      init ();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketServer"/> class with the specified
    /// <paramref name="port"/> and <paramref name="secure"/>.
    /// </summary>
    /// <remarks>
    /// An instance initialized by this constructor listens for the incoming connection requests
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
    public WebSocketServer (int port, bool secure)
      : this (System.Net.IPAddress.Any, port, secure)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketServer"/> class with the specified
    /// <paramref name="address"/> and <paramref name="port"/>.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   An instance initialized by this constructor listens for the incoming connection requests
    ///   on <paramref name="port"/>.
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
    /// <exception cref="ArgumentException">
    /// <paramref name="address"/> isn't a local IP address.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="address"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="port"/> isn't between 1 and 65535.
    /// </exception>
    public WebSocketServer (System.Net.IPAddress address, int port)
      : this (address, port, port == 443)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketServer"/> class with the specified
    /// <paramref name="address"/>, <paramref name="port"/>, and <paramref name="secure"/>.
    /// </summary>
    /// <remarks>
    /// An instance initialized by this constructor listens for the incoming connection requests
    /// on <paramref name="port"/>.
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
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="address"/> isn't a local IP address.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   Pair of <paramref name="port"/> and <paramref name="secure"/> is invalid.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="address"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="port"/> isn't between 1 and 65535.
    /// </exception>
    public WebSocketServer (System.Net.IPAddress address, int port, bool secure)
    {
      if (!address.IsLocal ())
        throw new ArgumentException ("Not a local IP address: " + address, "address");

      if (!port.IsPortNumber ())
        throw new ArgumentOutOfRangeException ("port", "Not between 1 and 65535: " + port);

      if ((port == 80 && secure) || (port == 443 && !secure))
        throw new ArgumentException (
          String.Format ("An invalid pair of 'port' and 'secure': {0}, {1}", port, secure));

      _address = address;
      _port = port;
      _secure = secure;
      _uri = "/".ToUri ();

      init ();
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the local IP address of the server.
    /// </summary>
    /// <value>
    /// A <see cref="System.Net.IPAddress"/> that represents the local IP address of the server.
    /// </value>
    public System.Net.IPAddress Address {
      get {
        return _address;
      }
    }

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
        return _authSchemes;
      }

      set {
        var msg = _state.CheckIfStartable ();
        if (msg != null) {
          _logger.Error (msg);
          return;
        }

        _authSchemes = value;
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
        return _certificate;
      }

      set {
        var msg = _state.CheckIfStartable ();
        if (msg != null) {
          _logger.Error (msg);
          return;
        }

        _certificate = value;
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
    /// periodically.
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
    /// Gets the port on which to listen for incoming connection requests.
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
        return _realm ?? (_realm = "SECRET AREA");
      }

      set {
        var msg = _state.CheckIfStartable ();
        if (msg != null) {
          _logger.Error (msg);
          return;
        }

        _realm = value;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the server is allowed to be bound to an address
    /// that is already in use.
    /// </summary>
    /// <remarks>
    /// If you would like to resolve to wait for socket in <c>TIME_WAIT</c> state, you should set
    /// this property to <c>true</c>.
    /// </remarks>
    /// <value>
    /// <c>true</c> if the server is allowed to be bound to an address that is already in use;
    /// otherwise, <c>false</c>. The default value is <c>false</c>.
    /// </value>
    public bool ReuseAddress {
      get {
        return _reuseAddress;
      }

      set {
        var msg = _state.CheckIfStartable ();
        if (msg != null) {
          _logger.Error (msg);
          return;
        }

        _reuseAddress = value;
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
        return _credentialsFinder ?? (_credentialsFinder = identity => null);
      }

      set {
        var msg = _state.CheckIfStartable ();
        if (msg != null) {
          _logger.Error (msg);
          return;
        }

        _credentialsFinder = value;
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

    #region Private Methods

    private void abort ()
    {
      lock (_sync) {
        if (!IsListening)
          return;

        _state = ServerState.ShuttingDown;
      }

      _listener.Stop ();
      _services.Stop (new CloseEventArgs (CloseStatusCode.ServerError), true, false);

      _state = ServerState.Stop;
    }

    private bool authenticateRequest (
      AuthenticationSchemes scheme, TcpListenerWebSocketContext context)
    {
      var chal = scheme == AuthenticationSchemes.Basic
                 ? AuthenticationChallenge.CreateBasicChallenge (Realm).ToBasicString ()
                 : scheme == AuthenticationSchemes.Digest
                   ? AuthenticationChallenge.CreateDigestChallenge (Realm).ToDigestString ()
                   : null;

      if (chal == null) {
        context.Close (HttpStatusCode.Forbidden);
        return false;
      }

      var retry = -1;
      var schm = scheme.ToString ();
      var realm = Realm;
      var credFinder = UserCredentialsFinder;
      Func<bool> auth = null;
      auth = () => {
        retry++;
        if (retry > 99) {
          context.Close (HttpStatusCode.Forbidden);
          return false;
        }

        var res = context.Headers["Authorization"];
        if (res == null || !res.StartsWith (schm, StringComparison.OrdinalIgnoreCase)) {
          context.SendAuthenticationChallenge (chal);
          return auth ();
        }

        context.SetUser (scheme, realm, credFinder);
        if (!context.IsAuthenticated) {
          context.SendAuthenticationChallenge (chal);
          return auth ();
        }

        return true;
      };

      return auth ();
    }

    private string checkIfCertificateExists ()
    {
      return _secure && _certificate == null
             ? "The secure connection requires a server certificate."
             : null;
    }

    private void init ()
    {
      _authSchemes = AuthenticationSchemes.Anonymous;
      _listener = new TcpListener (_address, _port);
      _logger = new Logger ();
      _services = new WebSocketServiceManager (_logger);
      _state = ServerState.Ready;
      _sync = new object ();
    }

    private void processWebSocketRequest (TcpListenerWebSocketContext context)
    {
      var uri = context.RequestUri;
      if (uri == null) {
        context.Close (HttpStatusCode.BadRequest);
        return;
      }

      if (_uri.IsAbsoluteUri) {
        var actual = uri.DnsSafeHost;
        var expected = _uri.DnsSafeHost;
        if (Uri.CheckHostName (actual) == UriHostNameType.Dns &&
            Uri.CheckHostName (expected) == UriHostNameType.Dns &&
            actual != expected) {
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
        try {
          var cl = _listener.AcceptTcpClient ();
          ThreadPool.QueueUserWorkItem (
            state => {
              try {
                var ctx = cl.GetWebSocketContext (null, _secure, _certificate, _logger);
                if (_authSchemes != AuthenticationSchemes.Anonymous &&
                    !authenticateRequest (_authSchemes, ctx))
                  return;

                processWebSocketRequest (ctx);
              }
              catch (Exception ex) {
                _logger.Fatal (ex.ToString ());
                cl.Close ();
              }
            });
        }
        catch (SocketException ex) {
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
      if (_reuseAddress)
        _listener.Server.SetSocketOption (
          SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

      _listener.Start ();
      _receiveRequestThread = new Thread (new ThreadStart (receiveRequest));
      _receiveRequestThread.IsBackground = true;
      _receiveRequestThread.Start ();
    }

    private void stopReceiving (int millisecondsTimeout)
    {
      _listener.Stop ();
      _receiveRequestThread.Join (millisecondsTimeout);
    }

    private static bool tryCreateUri (string uriString, out Uri result, out string message)
    {
      if (!uriString.TryCreateWebSocketUri (out result, out message))
        return false;

      if (result.PathAndQuery != "/") {
        result = null;
        message = "Includes the path or query component: " + uriString;

        return false;
      }

      return true;
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
    /// Adds a WebSocket service with the specified behavior, <paramref name="path"/>,
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
    /// Starts receiving the WebSocket connection requests.
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
    /// Stops receiving the WebSocket connection requests.
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

      stopReceiving (5000);
      _services.Stop (new CloseEventArgs (), true, true);

      _state = ServerState.Stop;
    }

    /// <summary>
    /// Stops receiving the WebSocket connection requests with the specified <see cref="ushort"/>
    /// and <see cref="string"/>.
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

      stopReceiving (5000);

      var send = !code.IsReserved ();
      _services.Stop (e, send, send);

      _state = ServerState.Stop;
    }

    /// <summary>
    /// Stops receiving the WebSocket connection requests with the specified
    /// <see cref="CloseStatusCode"/> and <see cref="string"/>.
    /// </summary>
    /// <param name="code">
    /// One of the <see cref="CloseStatusCode"/> enum values, represents the status code
    /// indicating the reason for stop.
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

      stopReceiving (5000);

      var send = !code.IsReserved ();
      _services.Stop (e, send, send);

      _state = ServerState.Stop;
    }

    #endregion
  }
}
