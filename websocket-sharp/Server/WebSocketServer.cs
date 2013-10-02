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
  /// The WebSocketServer class provides the multi WebSocket service.
  /// </remarks>
  public class WebSocketServer : WebSocketServerBase
  {
    #region Private Fields

    private WebSocketServiceHostManager _serviceHosts;
    private volatile ServerState        _state;
    private object                      _sync;

    #endregion

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketServer"/> class.
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
    public WebSocketServer (string url)
      : base (url)
    {
      if (BaseUri.AbsolutePath != "/")
        throw new ArgumentException ("Must not contain the path component: " + url, "url");

      _serviceHosts = new WebSocketServiceHostManager (Log);
      _state = ServerState.READY;
      _sync = new object ();
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
    public WebSocketServer (int port, bool secure)
      : this (System.Net.IPAddress.Any, port, secure)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketServer"/> class that listens for
    /// incoming connection attempts on the specified <paramref name="address"/> and <paramref name="port"/>.
    /// </summary>
    /// <param name="address">
    /// A <see cref="System.Net.IPAddress"/> that contains a local IP address.
    /// </param>
    /// <param name="port">
    /// An <see cref="int"/> that contains a port number.
    /// </param>
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
    /// A <see cref="System.Net.IPAddress"/> that contains a local IP address.
    /// </param>
    /// <param name="port">
    /// An <see cref="int"/> that contains a port number.
    /// </param>
    /// <param name="secure">
    /// A <see cref="bool"/> that indicates providing a secure connection or not.
    /// (<c>true</c> indicates providing a secure connection.)
    /// </param>
    public WebSocketServer (System.Net.IPAddress address, int port, bool secure)
      : base (address, port, "/", secure)
    {
      _serviceHosts = new WebSocketServiceHostManager (Log);
      _state = ServerState.READY;
      _sync = new object ();
    }

    #endregion

    #region Public Properties

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
    /// Gets the functions for the WebSocket services that the server provides.
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
      _serviceHosts.Stop (((ushort) CloseStatusCode.SERVER_ERROR).ToByteArray (ByteOrder.BIG), true);

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

      var path = context.Path;
      IWebSocketServiceHost host;
      if (!_serviceHosts.TryGetServiceHostInternally (path, out host))
      {
        websocket.Close (HttpStatusCode.NotImplemented);
        return;
      }

      if (BaseUri.IsAbsoluteUri)
        websocket.Url = new Uri (BaseUri, path);

      host.BindWebSocket (context);
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
        Log.Error (String.Format ("{0}\nservice path: {1}", msg, servicePath ?? ""));
        return;
      }

      var host = new WebSocketServiceHost<T> (serviceConstructor, Log);
      host.Uri = BaseUri.IsAbsoluteUri
               ? new Uri (BaseUri, servicePath)
               : servicePath.ToUri ();

      if (!KeepClean)
        host.KeepClean = false;

      _serviceHosts.Add (servicePath, host);
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
        Log.Error (String.Format ("{0}\nservice path: {1}", msg, servicePath ?? ""));
        return false;
      }

      return _serviceHosts.Remove (servicePath);
    }

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

        _serviceHosts.Start ();

        base.Start ();
        if (!IsListening)
        {
          _serviceHosts.Stop (new byte []{}, false);
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
          Log.Error (String.Format ("{0}\nstate: {1}\ncode: {2}\nreason: {3}", msg, _state, code, reason));
          return;
        }

        _state = ServerState.SHUTDOWN;
      }

      base.Stop ();
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
          Log.Error (String.Format ("{0}\nstate: {1}\nreason: {2}", msg, _state, reason));
          return;
        }

        _state = ServerState.SHUTDOWN;
      }

      base.Stop ();
      _serviceHosts.Stop (data, !code.IsReserved ());

      _state = ServerState.STOP;
    }

    #endregion
  }
}
