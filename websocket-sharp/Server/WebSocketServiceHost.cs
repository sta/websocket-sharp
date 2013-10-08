#region License
/*
 * WebSocketServiceHost.cs
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

#region Thanks
/*
 * Thanks:
 *   Juan Manuel Lallana <juan.manuel.lallana@gmail.com>
 */
#endregion

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using WebSocketSharp.Net;
using WebSocketSharp.Net.WebSockets;

namespace WebSocketSharp.Server
{
  /// <summary>
  /// Provides the functions of the server that receives the WebSocket connection requests.
  /// </summary>
  /// <remarks>
  /// The WebSocketServiceHost&lt;T&gt; class provides the single WebSocket service.
  /// </remarks>
  /// <typeparam name="T">
  /// The type of the WebSocket service that the server provides.
  /// The T must inherit the <see cref="WebSocketService"/> class.
  /// </typeparam>
  public class WebSocketServiceHost<T> : WebSocketServerBase, IWebSocketServiceHost
    where T : WebSocketService
  {
    #region Private Fields

    private Func<T>                 _serviceConstructor;
    private string                  _servicePath;
    private WebSocketSessionManager _sessions;
    private volatile ServerState    _state;
    private object                  _sync;

    #endregion

    #region Internal Constructors

    internal WebSocketServiceHost (Func<T> serviceConstructor, Logger logger)
      : base (logger)
    {
      _serviceConstructor = serviceConstructor;
      _sessions = new WebSocketSessionManager (logger);
      _state = ServerState.READY;
      _sync = new object ();
    }

    #endregion

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the WebSocketServiceHost&lt;T&gt; class that listens for
    /// incoming connection attempts on the specified <paramref name="port"/>.
    /// </summary>
    /// <param name="port">
    /// An <see cref="int"/> that contains a port number.
    /// </param>
    /// <param name="serviceConstructor">
    /// A Func&lt;T&gt; delegate that references the method used to initialize a new WebSocket service
    /// instance (a new WebSocket session).
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="serviceConstructor"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="port"/> is 0 or less, or 65536 or greater.
    /// </exception>
    public WebSocketServiceHost (int port, Func<T> serviceConstructor)
      : this (port, "/", serviceConstructor)
    {
    }

    /// <summary>
    /// Initializes a new instance of the WebSocketServiceHost&lt;T&gt; class that listens for
    /// incoming connection attempts on the specified WebSocket URL.
    /// </summary>
    /// <param name="url">
    /// A <see cref="string"/> that contains a WebSocket URL.
    /// </param>
    /// <param name="serviceConstructor">
    /// A Func&lt;T&gt; delegate that references the method used to initialize a new WebSocket service
    /// instance (a new WebSocket session).
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <para>
    ///   <paramref name="url"/> is <see langword="null"/>.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="serviceConstructor"/> is <see langword="null"/>.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="url"/> is invalid.
    /// </exception>
    public WebSocketServiceHost (string url, Func<T> serviceConstructor)
      : base (url)
    {
      if (serviceConstructor == null)
        throw new ArgumentNullException ("serviceConstructor");

      _serviceConstructor = serviceConstructor;
      _sessions = new WebSocketSessionManager (Log);
      _state = ServerState.READY;
      _sync = new object ();
    }

    /// <summary>
    /// Initializes a new instance of the WebSocketServiceHost&lt;T&gt; class that listens for
    /// incoming connection attempts on the specified <paramref name="port"/> and <paramref name="secure"/>.
    /// </summary>
    /// <param name="port">
    /// An <see cref="int"/> that contains a port number.
    /// </param>
    /// <param name="secure">
    /// A <see cref="bool"/> that indicates providing a secure connection or not.
    /// (<c>true</c> indicates providing a secure connection.)
    /// </param>
    /// <param name="serviceConstructor">
    /// A Func&lt;T&gt; delegate that references the method used to initialize a new WebSocket service
    /// instance (a new WebSocket session).
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="serviceConstructor"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="port"/> is 0 or less, or 65536 or greater.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Pair of <paramref name="port"/> and <paramref name="secure"/> is invalid.
    /// </exception>
    public WebSocketServiceHost (int port, bool secure, Func<T> serviceConstructor)
      : this (port, "/", secure, serviceConstructor)
    {
    }

    /// <summary>
    /// Initializes a new instance of the WebSocketServiceHost&lt;T&gt; class that listens for
    /// incoming connection attempts on the specified <paramref name="port"/> and
    /// <paramref name="servicePath"/>.
    /// </summary>
    /// <param name="port">
    /// An <see cref="int"/> that contains a port number.
    /// </param>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path.
    /// </param>
    /// <param name="serviceConstructor">
    /// A Func&lt;T&gt; delegate that references the method used to initialize a new WebSocket service
    /// instance (a new WebSocket session).
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <para>
    ///   <paramref name="servicePath"/> is <see langword="null"/>.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="serviceConstructor"/> is <see langword="null"/>.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="port"/> is 0 or less, or 65536 or greater.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="servicePath"/> is invalid.
    /// </exception>
    public WebSocketServiceHost (int port, string servicePath, Func<T> serviceConstructor)
      : this (System.Net.IPAddress.Any, port, servicePath, serviceConstructor)
    {
    }

    /// <summary>
    /// Initializes a new instance of the WebSocketServiceHost&lt;T&gt; class that listens for
    /// incoming connection attempts on the specified <paramref name="port"/>, <paramref name="servicePath"/>
    /// and <paramref name="secure"/>.
    /// </summary>
    /// <param name="port">
    /// An <see cref="int"/> that contains a port number.
    /// </param>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path.
    /// </param>
    /// <param name="secure">
    /// A <see cref="bool"/> that indicates providing a secure connection or not.
    /// (<c>true</c> indicates providing a secure connection.)
    /// </param>
    /// <param name="serviceConstructor">
    /// A Func&lt;T&gt; delegate that references the method used to initialize a new WebSocket service
    /// instance (a new WebSocket session).
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <para>
    ///   <paramref name="servicePath"/> is <see langword="null"/>.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="serviceConstructor"/> is <see langword="null"/>.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="port"/> is 0 or less, or 65536 or greater.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="servicePath"/> is invalid.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   Pair of <paramref name="port"/> and <paramref name="secure"/> is invalid.
    ///   </para>
    /// </exception>
    public WebSocketServiceHost (int port, string servicePath, bool secure, Func<T> serviceConstructor)
      : this (System.Net.IPAddress.Any, port, servicePath, secure, serviceConstructor)
    {
    }

    /// <summary>
    /// Initializes a new instance of the WebSocketServiceHost&lt;T&gt; class that listens for
    /// incoming connection attempts on the specified <paramref name="address"/>, <paramref name="port"/>
    /// and <paramref name="servicePath"/>.
    /// </summary>
    /// <param name="address">
    /// A <see cref="System.Net.IPAddress"/> that contains a local IP address.
    /// </param>
    /// <param name="port">
    /// An <see cref="int"/> that contains a port number.
    /// </param>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path.
    /// </param>
    /// <param name="serviceConstructor">
    /// A Func&lt;T&gt; delegate that references the method used to initialize a new WebSocket service
    /// instance (a new WebSocket session).
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <para>
    ///   <paramref name="address"/> is <see langword="null"/>.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="servicePath"/> is <see langword="null"/>.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="serviceConstructor"/> is <see langword="null"/>.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="port"/> is 0 or less, or 65536 or greater.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="servicePath"/> is invalid.
    /// </exception>
    public WebSocketServiceHost (
      System.Net.IPAddress address, int port, string servicePath, Func<T> serviceConstructor)
      : this (address, port, servicePath, port == 443 ? true : false, serviceConstructor)
    {
    }

    /// <summary>
    /// Initializes a new instance of the WebSocketServiceHost&lt;T&gt; class that listens for
    /// incoming connection attempts on the specified <paramref name="address"/>, <paramref name="port"/>,
    /// <paramref name="servicePath"/> and <paramref name="secure"/>.
    /// </summary>
    /// <param name="address">
    /// A <see cref="System.Net.IPAddress"/> that contains a local IP address.
    /// </param>
    /// <param name="port">
    /// An <see cref="int"/> that contains a port number.
    /// </param>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path.
    /// </param>
    /// <param name="secure">
    /// A <see cref="bool"/> that indicates providing a secure connection or not.
    /// (<c>true</c> indicates providing a secure connection.)
    /// </param>
    /// <param name="serviceConstructor">
    /// A Func&lt;T&gt; delegate that references the method used to initialize a new WebSocket service
    /// instance (a new WebSocket session).
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <para>
    ///   <paramref name="address"/> is <see langword="null"/>.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="servicePath"/> is <see langword="null"/>.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="serviceConstructor"/> is <see langword="null"/>.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="port"/> is 0 or less, or 65536 or greater.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="servicePath"/> is invalid.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   Pair of <paramref name="port"/> and <paramref name="secure"/> is invalid.
    ///   </para>
    /// </exception>
    public WebSocketServiceHost (
      System.Net.IPAddress address, int port, string servicePath, bool secure, Func<T> serviceConstructor)
      : base (address, port, servicePath, secure)
    {
      if (serviceConstructor == null)
        throw new ArgumentNullException ("serviceConstructor");

      _serviceConstructor = serviceConstructor;
      _sessions = new WebSocketSessionManager (Log);
      _state = ServerState.READY;
      _sync = new object ();
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the connection count to the WebSocket service host.
    /// </summary>
    /// <value>
    /// An <see cref="int"/> that contains the connection count.
    /// </value>
    public int ConnectionCount {
      get {
        return _sessions.Count;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the WebSocket service host cleans up
    /// the inactive sessions periodically.
    /// </summary>
    /// <value>
    /// <c>true</c> if the WebSocket service host cleans up the inactive sessions
    /// every 60 seconds; otherwise, <c>false</c>. The default value is <c>true</c>.
    /// </value>
    public bool KeepClean {
      get {
        return _sessions.KeepClean;
      }

      set {
        _sessions.KeepClean = value;
      }
    }

    /// <summary>
    /// Gets the path to the WebSocket service provided by the WebSocket service host.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service.
    /// </value>
    public string ServicePath {
      get {
        if (_servicePath == null)
          _servicePath = HttpUtility.UrlDecode (BaseUri.GetAbsolutePath ()).TrimEndSlash ();

        return _servicePath;
      }
    }

    /// <summary>
    /// Gets the manager of the sessions to the WebSocket service host.
    /// </summary>
    /// <value>
    /// A <see cref="WebSocketSessionManager"/> that manages the sessions.
    /// </value>
    public WebSocketSessionManager Sessions {
      get {
        return _sessions;
      }
    }

    /// <summary>
    /// Gets the WebSocket URL on which to listen for incoming connection attempts.
    /// </summary>
    /// <value>
    /// A <see cref="Uri"/> that contains a WebSocket URL.
    /// </value>
    public Uri Uri {
      get {
        return BaseUri;
      }

      internal set {
        BaseUri = value;
      }
    }

    #endregion

    #region Protected Methods

    /// <summary>
    /// Aborts receiving the WebSocket connection requests.
    /// </summary>
    /// <remarks>
    /// This method is called when an exception occurs while receiving the WebSocket connection requests.
    /// </remarks>
    protected override void Abort ()
    {
      lock (_sync)
      {
        if (_state != ServerState.START)
          return;

        _state = ServerState.SHUTDOWN;
      }

      StopListener ();
      _sessions.Stop (
        ((ushort) CloseStatusCode.SERVER_ERROR).ToByteArrayInternally (ByteOrder.BIG), true);

      _state = ServerState.STOP;
    }

    /// <summary>
    /// Accepts a WebSocket connection request.
    /// </summary>
    /// <param name="context">
    /// A <see cref="TcpListenerWebSocketContext"/> that contains the WebSocket connection request objects.
    /// </param>
    protected override void AcceptWebSocket (TcpListenerWebSocketContext context)
    {
      var websocket = context.WebSocket;
      websocket.Log = Log;

      var path = HttpUtility.UrlDecode (context.Path).TrimEndSlash ();
      if (path != ServicePath)
      {
        websocket.Close (HttpStatusCode.NotImplemented);
        return;
      }

      if (BaseUri.IsAbsoluteUri)
        websocket.Url = BaseUri;

      ((IWebSocketServiceHost) this).BindWebSocket (context);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Starts to receive the WebSocket connection requests.
    /// </summary>
    public override void Start ()
    {
      lock (_sync)
      {
        var msg = _state.CheckIfStopped ();
        if (msg != null)
        {
          Log.Error (String.Format ("{0}\nstate: {1}", msg, _state));
          return;
        }

        _sessions.Start ();

        base.Start ();
        if (!IsListening)
        {
          _sessions.Stop (new byte []{}, false);
          return;
        }

        _state = ServerState.START;
      }
    }

    /// <summary>
    /// Stops receiving the WebSocket connection requests.
    /// </summary>
    public override void Stop ()
    {
      lock (_sync)
      {
        var msg = _state.CheckIfStarted ();
        if (msg != null)
        {
          Log.Error (String.Format ("{0}\nstate: {1}", msg, _state));
          return;
        }

        _state = ServerState.SHUTDOWN;
      }

      base.Stop ();
      _sessions.Stop (new byte []{}, true);

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
          Log.Error (String.Format ("{0}\nstate: {1}\ncode: {2}\nreason: {3}", msg, _state, code, reason));
          return;
        }

        _state = ServerState.SHUTDOWN;
      }

      base.Stop ();
      _sessions.Stop (data, !code.IsReserved ());

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
          Log.Error (String.Format ("{0}\nstate: {1}\nreason: {2}", msg, _state, reason));
          return;
        }

        _state = ServerState.SHUTDOWN;
      }

      base.Stop ();
      _sessions.Stop (data, !code.IsReserved ());

      _state = ServerState.STOP;
    }

    #endregion

    #region Explicit Interface Implementation

    /// <summary>
    /// Binds the specified <see cref="WebSocketContext"/> to a WebSocket service instance.
    /// </summary>
    /// <param name="context">
    /// A <see cref="WebSocketContext"/> that contains the WebSocket connection request objects to bind.
    /// </param>
    void IWebSocketServiceHost.BindWebSocket (WebSocketContext context)
    {
      T service = _serviceConstructor ();
      service.Bind (context, _sessions);
      service.Start ();
    }

    #endregion
  }
}
