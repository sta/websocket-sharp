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
 *   Juan Manuel Lallana <juan.manuel.lallana@gmail.com>
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
  /// The WebSocketServer class provides the multi WebSocket service.
  /// </remarks>
  public class WebSocketServer
  {
    #region Private Fields

    private System.Net.IPAddress               _address;
    private AuthenticationSchemes              _authSchemes;
    private X509Certificate2                   _cert;
    private Func<IIdentity, NetworkCredential> _credentialsFinder;
    private TcpListener                        _listener;
    private Logger                             _logger;
    private int                                _port;
    private string                             _realm;
    private Thread                             _receiveRequestThread;
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
    /// An instance initialized by this constructor listens for the incoming connection requests on
    /// port 80.
    /// </remarks>
    public WebSocketServer ()
      : this (80)
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
    ///   And if <paramref name="port"/> is 443, that instance provides a secure connection.
    ///   </para>
    /// </remarks>
    /// <param name="port">
    /// An <see cref="int"/> that represents the port number on which to listen.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="port"/> isn't between 1 and 65535.
    /// </exception>
    public WebSocketServer (int port)
      : this (System.Net.IPAddress.Any, port)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketServer"/> class with the specified
    /// WebSocket URL.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   An instance initialized by this constructor listens for the incoming connection requests
    ///   on the port (if any) in <paramref name="url"/>.
    ///   </para>
    ///   <para>
    ///   So if <paramref name="url"/> is without a port, either port 80 or 443 is used on which to
    ///   listen. It's determined by the scheme (ws or wss) in <paramref name="url"/>. (port 80 if
    ///   the scheme is ws.)
    ///   </para>
    /// </remarks>
    /// <param name="url">
    /// A <see cref="string"/> that represents the WebSocket URL of the server.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="url"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="url"/> is invalid.
    /// </exception>
    public WebSocketServer (string url)
    {
      if (url == null)
        throw new ArgumentNullException ("url");

      string msg;
      if (!tryCreateUri (url, out _uri, out msg))
        throw new ArgumentException (msg, "url");

      var host = _uri.DnsSafeHost;
      _address = host.ToIPAddress ();
      if (_address == null || !_address.IsLocal ())
        throw new ArgumentException ("The host part must be the local host name: " + host, "url");

      _port = _uri.Port;
      _secure = _uri.Scheme == "wss";

      init ();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketServer"/> class with the specified
    /// <paramref name="port"/> and <paramref name="secure"/>.
    /// </summary>
    /// <remarks>
    /// An instance initialized by this constructor listens for the incoming connection requests on
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
    ///   And if <paramref name="port"/> is 443, that instance provides a secure connection.
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
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="port"/> isn't between 1 and 65535.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="address"/> isn't a local IP address.
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
    /// An instance initialized by this constructor listens for the incoming connection requests on
    /// <paramref name="port"/>.
    /// </remarks>
    /// <param name="address">
    /// A <see cref="System.Net.IPAddress"/> that represents the local IP address of the server.
    /// </param>
    /// <param name="port">
    /// An <see cref="int"/> that represents the port number on which to listen.
    /// </param>
    /// <param name="secure">
    /// A <see cref="bool"/> that indicates providing a secure connection or not. (<c>true</c>
    /// indicates providing a secure connection.)
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="address"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="port"/> isn't between 1 and 65535.
    /// </exception>
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
    public WebSocketServer (System.Net.IPAddress address, int port, bool secure)
    {
      if (!address.IsLocal ())
        throw new ArgumentException ("Must be the local IP address: " + address, "address");

      if (!port.IsPortNumber ())
        throw new ArgumentOutOfRangeException ("port", "Must be between 1 and 65535: " + port);

      if ((port == 80 && secure) || (port == 443 && !secure))
        throw new ArgumentException (
          String.Format ("Invalid pair of 'port' and 'secure': {0}, {1}", port, secure));

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
    /// One of the <see cref="WebSocketSharp.Net.AuthenticationSchemes"/> enum values, indicates
    /// the scheme used to authenticate the clients.
    /// The default value is <see cref="WebSocketSharp.Net.AuthenticationSchemes.Anonymous"/>.
    /// </value>
    public AuthenticationSchemes AuthenticationSchemes {
      get {
        return _authSchemes;
      }

      set {
        if (!canSet ("AuthenticationSchemes"))
          return;

        _authSchemes = value;
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
        return _cert;
      }

      set {
        if (!canSet ("Certificate"))
          return;

        _cert = value;
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
    /// A <see cref="string"/> that represents the name of the realm. The default value is
    /// <c>SECRET AREA</c>.
    /// </value>
    public string Realm {
      get {
        return _realm ?? (_realm = "SECRET AREA");
      }

      set {
        if (!canSet ("Realm"))
          return;

        _realm = value;
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
        if (!canSet ("UserCredentialsFinder"))
          return;

        _credentialsFinder = value;
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
      _services.Stop (
        ((ushort) CloseStatusCode.ServerError).ToByteArrayInternally (ByteOrder.Big), true);

      _state = ServerState.Stop;
    }

    private void acceptRequestAsync (TcpClient client)
    {
      ThreadPool.QueueUserWorkItem (
        state => {
          try {
            var context = client.GetWebSocketContext (null, _secure, _cert, _logger);
            if (_authSchemes != AuthenticationSchemes.Anonymous &&
                !authenticateRequest (_authSchemes, context))
              return;

            acceptWebSocket (context);
          }
          catch (Exception ex) {
            _logger.Fatal (ex.ToString ());
            client.Close ();
          }
        });
    }

    private void acceptWebSocket (TcpListenerWebSocketContext context)
    {
      var path = context.Path;

      WebSocketServiceHost host;
      if (path == null || !_services.TryGetServiceHostInternally (path, out host)) {
        context.Close (HttpStatusCode.NotImplemented);
        return;
      }

      if (_uri.IsAbsoluteUri)
        context.WebSocket.Url = new Uri (_uri, path);

      host.StartSession (context);
    }

    private bool authenticateRequest (
      AuthenticationSchemes scheme, TcpListenerWebSocketContext context)
    {
      var challenge = scheme == AuthenticationSchemes.Basic
                    ? HttpUtility.CreateBasicAuthChallenge (Realm)
                    : scheme == AuthenticationSchemes.Digest
                      ? HttpUtility.CreateDigestAuthChallenge (Realm)
                      : null;

      if (challenge == null) {
        context.Close (HttpStatusCode.Forbidden);
        return false;
      }

      var retry = -1;
      var expected = scheme.ToString ();
      var realm = Realm;
      var credentialsFinder = UserCredentialsFinder;
      Func<bool> auth = null;
      auth = () => {
        retry++;
        if (retry > 99) {
          context.Close (HttpStatusCode.Forbidden);
          return false;
        }

        var header = context.Headers ["Authorization"];
        if (header == null || !header.StartsWith (expected, StringComparison.OrdinalIgnoreCase)) {
          context.SendAuthChallenge (challenge);
          return auth ();
        }

        context.SetUser (scheme, realm, credentialsFinder);
        if (context.IsAuthenticated)
          return true;

        context.SendAuthChallenge (challenge);
        return auth ();
      };

      return auth ();
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
      return _secure && _cert == null
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

    private void receiveRequest ()
    {
      while (true) {
        try {
          acceptRequestAsync (_listener.AcceptTcpClient ());
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
      _receiveRequestThread = new Thread (new ThreadStart (receiveRequest)); 
      _receiveRequestThread.IsBackground = true;
      _receiveRequestThread.Start ();
    }

    private void stopListener (int millisecondsTimeout)
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
        message = "Must not contain the path or query component: " + uriString;

        return false;
      }

      return true;
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
    /// Adds the specified typed WebSocket service with the specified <paramref name="path"/>
    /// and <paramref name="constructor"/>.
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
    /// Starts receiving the WebSocket connection requests.
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
    /// Stops receiving the WebSocket connection requests.
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

      stopListener (5000);
      _services.Stop (new byte [0], true);

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

      stopListener (5000);
      _services.Stop (data, !code.IsReserved ());

      _state = ServerState.Stop;
    }

    /// <summary>
    /// Stops receiving the WebSocket connection requests with the specified
    /// <see cref="CloseStatusCode"/> and <see cref="string"/>.
    /// </summary>
    /// <param name="code">
    /// One of the <see cref="CloseStatusCode"/> enum values, represents the status code indicating
    /// the reason for stop.
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

      stopListener (5000);
      _services.Stop (data, !code.IsReserved ());

      _state = ServerState.Stop;
    }

    #endregion
  }
}
