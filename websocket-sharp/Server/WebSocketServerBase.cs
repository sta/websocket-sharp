#region License
/*
 * WebSocketServerBase.cs
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

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using WebSocketSharp.Net.WebSockets;

namespace WebSocketSharp.Server
{
  /// <summary>
  /// Provides the basic functions of the server that receives the WebSocket connection requests.
  /// </summary>
  /// <remarks>
  /// The WebSocketServerBase class is an abstract class.
  /// </remarks>
  public abstract class WebSocketServerBase
  {
    #region Private Fields

    private IPAddress        _address;
    private X509Certificate2 _cert;
    private bool             _listening;
    private Logger           _logger;
    private int              _port;
    private Thread           _receiveRequestThread;
    private bool             _secure;
    private bool             _selfHost;
    private TcpListener      _listener;
    private Uri              _uri;

    #endregion

    #region Protected Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketServerBase"/> class.
    /// </summary>
    /// <remarks>
    /// This constructor initializes a new instance of this class as non self hosted server.
    /// </remarks>
    protected WebSocketServerBase ()
      : this (new Logger ())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketServerBase"/> class
    /// with the specified <paramref name="logger"/>.
    /// </summary>
    /// <remarks>
    /// This constructor initializes a new instance of this class as non self hosted server.
    /// </remarks>
    /// <param name="logger">
    /// A <see cref="Logger"/> that provides the logging functions.
    /// </param>
    protected WebSocketServerBase (Logger logger)
    {
      _logger = logger;
      _selfHost = false;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketServerBase"/> class
    /// that listens for incoming connection attempts on the specified WebSocket URL.
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
    protected WebSocketServerBase (string url)
    {
      if (url == null)
        throw new ArgumentNullException ("url");

      Uri uri;
      string msg;
      if (!tryCreateUri (url, out uri, out msg))
        throw new ArgumentException (msg, "url");

      init (uri);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketServerBase"/> class
    /// that listens for incoming connection attempts on the specified <paramref name="address"/>,
    /// <paramref name="port"/>, <paramref name="absPath"/> and <paramref name="secure"/>.
    /// </summary>
    /// <param name="address">
    /// A <see cref="IPAddress"/> that contains a local IP address.
    /// </param>
    /// <param name="port">
    /// An <see cref="int"/> that contains a port number. 
    /// </param>
    /// <param name="absPath">
    /// A <see cref="string"/> that contains an absolute path.
    /// </param>
    /// <param name="secure">
    /// A <see cref="bool"/> that indicates providing a secure connection or not.
    /// (<c>true</c> indicates providing a secure connection.)
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Either <paramref name="address"/> or <paramref name="absPath"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="port"/> is 0 or less, or 65536 or greater.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="absPath"/> is invalid.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   Pair of <paramref name="port"/> and <paramref name="secure"/> is invalid.
    ///   </para>
    /// </exception>
    protected WebSocketServerBase (IPAddress address, int port, string absPath, bool secure)
    {
      if (address == null)
        throw new ArgumentNullException ("address");

      if (absPath == null)
        throw new ArgumentNullException ("absPath");

      if (!port.IsPortNumber ())
        throw new ArgumentOutOfRangeException ("port", "Invalid port number: " + port);

      string msg;
      if (!absPath.IsValidAbsolutePath (out msg))
        throw new ArgumentException (msg, "absPath");

      if ((port == 80 && secure) || (port == 443 && !secure))
        throw new ArgumentException (String.Format (
          "Invalid pair of 'port' and 'secure': {0}, {1}", port, secure));

      _address = address;
      _port = port;
      _uri = absPath.ToUri ();
      _secure = secure;

      init ();
    }

    #endregion

    #region Protected Properties

    /// <summary>
    /// Gets or sets the WebSocket URL on which to listen for incoming connection attempts.
    /// </summary>
    /// <value>
    /// A <see cref="Uri"/> that contains a WebSocket URL.
    /// </value>
    protected Uri BaseUri {
      get {
        return _uri;
      }

      set {
        _uri = value;
      }
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the local IP address on which to listen for incoming connection attempts.
    /// </summary>
    /// <value>
    /// A <see cref="IPAddress"/> that contains a local IP address.
    /// </value>
    public IPAddress Address {
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
        if (_listening)
          return;

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
        return _listening;
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
    /// Gets a value indicating whether the server is self host.
    /// </summary>
    /// <value>
    /// <c>true</c> if the server is self host; otherwise, <c>false</c>.
    /// </value>
    public bool IsSelfHost {
      get {
        return _selfHost;
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

    #endregion

    #region Private Methods

    private void init ()
    {
      _listening = false;
      _logger = new Logger ();
      _selfHost = true;
      _listener = new TcpListener (_address, _port);
    }

    private void init (Uri uri)
    {
      var scheme = uri.Scheme;
      var host = uri.DnsSafeHost;
      var port = uri.Port;
      var addrs = Dns.GetHostAddresses (host);

      _uri = uri;
      _address = addrs [0];
      _port = port;
      _secure = scheme == "wss" ? true : false;

      init ();
    }

    private void processRequestAsync (TcpClient client)
    {
      WaitCallback callback = state =>
      {
        try {
          AcceptWebSocket (client.GetWebSocketContext (_secure, _cert));
        }
        catch (Exception ex)
        {
          client.Close ();
          _logger.Fatal (ex.Message);
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
        catch (SocketException) {
          _logger.Info ("TcpListener has been stopped.");
          break;
        }
        catch (Exception ex) {
          _logger.Fatal (ex.Message);
          break;
        }
      }
    }

    private void startReceiveRequestThread ()
    {
      _receiveRequestThread = new Thread (new ThreadStart (receiveRequest)); 
      _receiveRequestThread.IsBackground = true;
      _receiveRequestThread.Start ();
    }

    private static bool tryCreateUri (string uriString, out Uri result, out string message)
    {
      if (!uriString.TryCreateWebSocketUri (out result, out message))
        return false;

      if (!result.Query.IsNullOrEmpty ())
      {
        result = null;
        message = "Must not contain the query component: " + uriString;

        return false;
      }

      return true;
    }

    #endregion

    #region Protected Methods

    /// <summary>
    /// Accepts a WebSocket connection request.
    /// </summary>
    /// <param name="context">
    /// A <see cref="TcpListenerWebSocketContext"/> that contains the WebSocket connection request objects.
    /// </param>
    protected abstract void AcceptWebSocket (TcpListenerWebSocketContext context);

    #endregion

    #region Public Methods

    /// <summary>
    /// Starts to receive the WebSocket connection requests.
    /// </summary>
    public virtual void Start ()
    {
      if (!_selfHost || _listening)
        return;

      if (_secure && _cert == null)
      {
        _logger.Error ("Secure connection requires a server certificate.");
        return;
      }

      _listener.Start ();
      startReceiveRequestThread ();
      _listening = true;
    }

    /// <summary>
    /// Stops receiving the WebSocket connection requests.
    /// </summary>
    public virtual void Stop ()
    {
      if (!_selfHost || !_listening)
        return;

      _listener.Stop ();
      _receiveRequestThread.Join (5 * 1000);
      _listening = false;
    }

    #endregion
  }
}
