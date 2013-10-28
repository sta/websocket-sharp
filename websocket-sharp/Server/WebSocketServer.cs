#region License
/*
 * WebSocketServer.cs
 *
 * A C# implementation of the WebSocket protocol server.
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
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using WebSocketSharp.Net;
using WebSocketSharp.Net.WebSockets;

namespace WebSocketSharp.Server
{
  /// <summary>
  /// Provides the functions of the server that receives the WebSocket connection requests.
  /// </summary>
  /// <remarks>
  /// The WebSocketServer class provides the multi WebSocket service.
  /// </remarks>
  public class WebSocketServer
  {
    #region Private Fields

    private System.Net.IPAddress        _address;
    private X509Certificate2            _cert;
    private TcpListener                 _listener;
    private Logger                      _logger;
    private int                         _port;
    private Thread                      _receiveRequestThread;
    private bool                        _secure;
    private WebSocketServiceHostManager _serviceHosts;
    private volatile ServerState        _state;
    private object                      _sync;
    private Uri                         _uri;

    #endregion

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketServer"/> class that listens for
    /// incoming requests on port 80.
    /// </summary>
    public WebSocketServer ()
      : this (80)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketServer"/> class that listens for
    /// incoming connection attempts on the specified <paramref name="port"/>.
    /// </summary>
    /// <param name="port">
    /// An <see cref="int"/> that contains a port number.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="port"/> is not between 1 and 65535.
    /// </exception>
    public WebSocketServer (int port)
      : this (System.Net.IPAddress.Any, port)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketServer"/> class that listens for
    /// incoming connection attempts on the specified WebSocket URL.
    /// </summary>
    /// <param name="url">
    /// A <see cref="string"/> that contains a WebSocket URL.
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
        throw new ArgumentException (String.Format (
          "The host part must be the local host name: {0}", host), "url");

      _port = _uri.Port;
      _secure = _uri.Scheme == "wss" ? true : false;

      init ();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketServer"/> class that listens for
    /// incoming connection attempts on the specified <paramref name="port"/> and <paramref name="secure"/>.
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
    public WebSocketServer (int port, bool secure)
      : this (System.Net.IPAddress.Any, port, secure)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketServer"/> class that listens for
    /// incoming connection attempts on the specified <paramref name="address"/> and <paramref name="port"/>.
    /// </summary>
    /// <param name="address">
    /// A <see cref="System.Net.IPAddress"/> that represents the local IP address.
    /// </param>
    /// <param name="port">
    /// An <see cref="int"/> that contains a port number.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="address"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="port"/> is not between 1 and 65535.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="address"/> is not the local IP address.
    /// </exception>
    public WebSocketServer (System.Net.IPAddress address, int port)
      : this (address, port, port == 443 ? true : false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketServer"/> class that listens for
    /// incoming connection attempts on the specified <paramref name="address"/>, <paramref name="port"/>
    /// and <paramref name="secure"/>.
    /// </summary>
    /// <param name="address">
    /// A <see cref="System.Net.IPAddress"/> that represents the local IP address.
    /// </param>
    /// <param name="port">
    /// An <see cref="int"/> that contains a port number.
    /// </param>
    /// <param name="secure">
    /// A <see cref="bool"/> that indicates providing a secure connection or not.
    /// (<c>true</c> indicates providing a secure connection.)
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="address"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="port"/> is not between 1 and 65535.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="address"/> is not the local IP address.
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
        throw new ArgumentException (String.Format (
          "Must be the local IP address: {0}", address), "address");

      if (!port.IsPortNumber ())
        throw new ArgumentOutOfRangeException ("port", "Must be between 1 and 65535: " + port);

      if ((port == 80 && secure) || (port == 443 && !secure))
        throw new ArgumentException (String.Format (
          "Invalid pair of 'port' and 'secure': {0}, {1}", port, secure));

      _address = address;
      _port = port;
      _secure = secure;
      _uri = "/".ToUri ();

      init ();
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the local IP address on which to listen for incoming connection attempts.
    /// </summary>
    /// <value>
    /// A <see cref="System.Net.IPAddress"/> that represents the local IP address.
    /// </value>
    public System.Net.IPAddress Address {
      get {
        return _address;
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
        if (_state == ServerState.START || _state == ServerState.SHUTDOWN)
        {
          _logger.Error (
            "The value of Certificate property cannot be changed because the server has already been started.");

          return;
        }

        _cert = value;
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
    /// <c>true</c> if the server provides secure connection; otherwise, <c>false</c>.
    /// </value>
    public bool IsSecure {
      get {
        return _secure;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the server cleans up the inactive sessions periodically.
    /// </summary>
    /// <value>
    /// <c>true</c> if the server cleans up the inactive sessions every 60 seconds;
    /// otherwise, <c>false</c>. The default value is <c>true</c>.
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
    }

    /// <summary>
    /// Gets the port on which to listen for incoming connection attempts.
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
    /// Gets the functions for the WebSocket services provided by the server.
    /// </summary>
    /// <value>
    /// A <see cref="WebSocketServiceHostManager"/> that manages the WebSocket services.
    /// </value>
    public WebSocketServiceHostManager WebSocketServices {
      get {
        return _serviceHosts;
      }
    }

    #endregion

    #region Private Methods

    private void abort ()
    {
      lock (_sync)
      {
        if (!IsListening)
          return;

        _state = ServerState.SHUTDOWN;
      }

      _listener.Stop ();
      _serviceHosts.Stop (
        ((ushort) CloseStatusCode.SERVER_ERROR).ToByteArrayInternally (ByteOrder.BIG), true);

      _state = ServerState.STOP;
    }

    private void acceptWebSocket (TcpListenerWebSocketContext context)
    {
      var websocket = context.WebSocket;
      websocket.Log = _logger;

      var path = context.Path;
      WebSocketServiceHost host;
      if (path == null || !_serviceHosts.TryGetServiceHostInternally (path, out host))
      {
        websocket.Close (HttpStatusCode.NotImplemented);
        return;
      }

      if (_uri.IsAbsoluteUri)
        websocket.Url = new Uri (_uri, path);

      host.StartSession (context);
    }

    private string checkIfCertExists ()
    {
      return _secure && _cert == null
             ? "The secure connection requires a server certificate."
             : null;
    }

    private void init ()
    {
      _listener = new TcpListener (_address, _port);
      _logger = new Logger ();
      _serviceHosts = new WebSocketServiceHostManager (_logger);
      _state = ServerState.READY;
      _sync = new object ();
    }

    private void processRequestAsync (TcpClient client)
    {
      WaitCallback callback = state =>
      {
        try {
          acceptWebSocket (client.GetWebSocketContext (_secure, _cert));
        }
        catch (Exception ex)
        {
          _logger.Fatal (ex.ToString ());
          client.Close ();
        }
      };

      ThreadPool.QueueUserWorkItem (callback);
    }

    private void receiveRequest ()
    {
      while (true)
      {
        try {
          processRequestAsync (_listener.AcceptTcpClient ());
        }
        catch (SocketException ex) {
          _logger.Warn (String.Format ("Receiving has been stopped.\nreason: {0}.", ex.Message));
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

    private void startReceiveRequestThread ()
    {
      _receiveRequestThread = new Thread (new ThreadStart (receiveRequest)); 
      _receiveRequestThread.IsBackground = true;
      _receiveRequestThread.Start ();
    }

    private void stopListener (int timeOut)
    {
      _listener.Stop ();
      _receiveRequestThread.Join (timeOut);
    }

    private static bool tryCreateUri (string uriString, out Uri result, out string message)
    {
      if (!uriString.TryCreateWebSocketUri (out result, out message))
        return false;

      if (result.PathAndQuery != "/")
      {
        result = null;
        message = "Must not contain the path or query component: " + uriString;

        return false;
      }

      return true;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Adds the specified typed WebSocket service with the specified <paramref name="servicePath"/>.
    /// </summary>
    /// <remarks>
    /// This method converts <paramref name="servicePath"/> to URL-decoded string and
    /// removes <c>'/'</c> from tail end of <paramref name="servicePath"/>.
    /// </remarks>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service.
    /// </param>
    /// <typeparam name="TWithNew">
    /// The type of the WebSocket service. The TWithNew must inherit the <see cref="WebSocketService"/>
    /// class and must have a public parameterless constructor.
    /// </typeparam>
    public void AddWebSocketService<TWithNew> (string servicePath)
      where TWithNew : WebSocketService, new ()
    {
      AddWebSocketService<TWithNew> (servicePath, () => new TWithNew ());
    }

    /// <summary>
    /// Adds the specified typed WebSocket service with the specified <paramref name="servicePath"/> and
    /// <paramref name="serviceConstructor"/>.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   This method converts <paramref name="servicePath"/> to URL-decoded string and
    ///   removes <c>'/'</c> from tail end of <paramref name="servicePath"/>.
    ///   </para>
    ///   <para>
    ///   <paramref name="serviceConstructor"/> returns a initialized specified typed WebSocket service
    ///   instance.
    ///   </para>
    /// </remarks>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service.
    /// </param>
    /// <param name="serviceConstructor">
    /// A Func&lt;T&gt; delegate that references the method used to initialize a new WebSocket service
    /// instance (a new WebSocket session).
    /// </param>
    /// <typeparam name="T">
    /// The type of the WebSocket service. The T must inherit the <see cref="WebSocketService"/> class.
    /// </typeparam>
    public void AddWebSocketService<T> (string servicePath, Func<T> serviceConstructor)
      where T : WebSocketService
    {
      var msg = servicePath.CheckIfValidServicePath () ??
                (serviceConstructor == null ? "'serviceConstructor' must not be null." : null);

      if (msg != null)
      {
        _logger.Error (String.Format ("{0}\nservice path: {1}", msg, servicePath ?? ""));
        return;
      }

      var host = new WebSocketServiceHost<T> (servicePath, serviceConstructor, _logger);
      if (!KeepClean)
        host.KeepClean = false;

      _serviceHosts.Add (host.ServicePath, host);
    }

    /// <summary>
    /// Removes the WebSocket service with the specified <paramref name="servicePath"/>.
    /// </summary>
    /// <remarks>
    /// This method converts <paramref name="servicePath"/> to URL-decoded string and
    /// removes <c>'/'</c> from tail end of <paramref name="servicePath"/>.
    /// </remarks>
    /// <returns>
    /// <c>true</c> if the WebSocket service is successfully found and removed; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    public bool RemoveWebSocketService (string servicePath)
    {
      var msg = servicePath.CheckIfValidServicePath ();
      if (msg != null)
      {
        _logger.Error (String.Format ("{0}\nservice path: {1}", msg, servicePath ?? ""));
        return false;
      }

      return _serviceHosts.Remove (servicePath);
    }

    /// <summary>
    /// Starts to receive the WebSocket connection requests.
    /// </summary>
    public void Start ()
    {
      lock (_sync)
      {
        var msg = _state.CheckIfStopped () ?? checkIfCertExists ();
        if (msg != null)
        {
          _logger.Error (String.Format ("{0}\nstate: {1}\nsecure: {2}", msg, _state, _secure));
          return;
        }

        _serviceHosts.Start ();
        _listener.Start ();
        startReceiveRequestThread ();

        _state = ServerState.START;
      }
    }

    /// <summary>
    /// Stops receiving the WebSocket connection requests.
    /// </summary>
    public void Stop ()
    {
      lock (_sync)
      {
        var msg = _state.CheckIfStarted ();
        if (msg != null)
        {
          _logger.Error (String.Format ("{0}\nstate: {1}", msg, _state));
          return;
        }

        _state = ServerState.SHUTDOWN;
      }

      stopListener (5000);
      _serviceHosts.Stop (new byte []{}, true);

      _state = ServerState.STOP;
    }

    /// <summary>
    /// Stops receiving the WebSocket connection requests with the specified <see cref="ushort"/> and
    /// <see cref="string"/>.
    /// </summary>
    /// <param name="code">
    /// A <see cref="ushort"/> that contains a status code indicating the reason for stop.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that contains the reason for stop.
    /// </param>
    public void Stop (ushort code, string reason)
    {
      byte [] data = null;
      lock (_sync)
      {
        var msg = _state.CheckIfStarted () ??
                  code.CheckIfValidCloseStatusCode () ??
                  (data = code.Append (reason)).CheckIfValidCloseData ();

        if (msg != null)
        {
          _logger.Error (String.Format (
            "{0}\nstate: {1}\ncode: {2}\nreason: {3}", msg, _state, code, reason));

          return;
        }

        _state = ServerState.SHUTDOWN;
      }

      stopListener (5000);
      _serviceHosts.Stop (data, !code.IsReserved ());

      _state = ServerState.STOP;
    }

    /// <summary>
    /// Stops receiving the WebSocket connection requests with the specified <see cref="CloseStatusCode"/>
    /// and <see cref="string"/>.
    /// </summary>
    /// <param name="code">
    /// One of the <see cref="CloseStatusCode"/> values that represent the status codes indicating
    /// the reasons for stop.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that contains the reason for stop.
    /// </param>
    public void Stop (CloseStatusCode code, string reason)
    {
      byte [] data = null;
      lock (_sync)
      {
        var msg = _state.CheckIfStarted () ??
                  (data = ((ushort) code).Append (reason)).CheckIfValidCloseData ();

        if (msg != null)
        {
          _logger.Error (String.Format ("{0}\nstate: {1}\nreason: {2}", msg, _state, reason));
          return;
        }

        _state = ServerState.SHUTDOWN;
      }

      stopListener (5000);
      _serviceHosts.Stop (data, !code.IsReserved ());

      _state = ServerState.STOP;
    }

    #endregion
  }
}
