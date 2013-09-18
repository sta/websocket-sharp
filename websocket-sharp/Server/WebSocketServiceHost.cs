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
    where T : WebSocketService, new ()
  {
    #region Private Fields

    private string                  _servicePath;
    private WebSocketSessionManager _sessions;

    #endregion

    #region Internal Constructors

    internal WebSocketServiceHost (Logger logger)
      : base (logger)
    {
      _sessions = new WebSocketSessionManager (logger);
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
    public WebSocketServiceHost (int port)
      : this (port, "/")
    {
    }

    /// <summary>
    /// Initializes a new instance of the WebSocketServiceHost&lt;T&gt; class that listens for
    /// incoming connection attempts on the specified WebSocket URL.
    /// </summary>
    /// <param name="url">
    /// A <see cref="string"/> that contains a WebSocket URL.
    /// </param>
    public WebSocketServiceHost (string url)
      : base (url)
    {
      _sessions = new WebSocketSessionManager (Log);
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
    public WebSocketServiceHost (int port, bool secure)
      : this (port, "/", secure)
    {
    }

    /// <summary>
    /// Initializes a new instance of the WebSocketServiceHost&lt;T&gt; class that listens for
    /// incoming connection attempts on the specified <paramref name="port"/> and <paramref name="servicePath"/>.
    /// </summary>
    /// <param name="port">
    /// An <see cref="int"/> that contains a port number.
    /// </param>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path.
    /// </param>
    public WebSocketServiceHost (int port, string servicePath)
      : this (System.Net.IPAddress.Any, port, servicePath)
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
    public WebSocketServiceHost (int port, string servicePath, bool secure)
      : this (System.Net.IPAddress.Any, port, servicePath, secure)
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
    public WebSocketServiceHost (System.Net.IPAddress address, int port, string servicePath)
      : this (address, port, servicePath, port == 443 ? true : false)
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
    public WebSocketServiceHost (System.Net.IPAddress address, int port, string servicePath, bool secure)
      : base (address, port, servicePath, secure)
    {
      _sessions = new WebSocketSessionManager (Log);
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

    #region Private Methods

    private void stop (ushort code, string reason)
    {
      var data = code.Append (reason);
      var msg = data.CheckIfValidCloseData ();
      if (msg != null)
      {
        Log.Error (String.Format ("{0}\ncode: {1}\nreason: {2}", msg, code, reason));
        return;
      }

      base.Stop ();
      _sessions.Stop (data);
    }

    #endregion

    #region Protected Methods

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
    /// Stops receiving the WebSocket connection requests.
    /// </summary>
    public override void Stop ()
    {
      base.Stop ();
      _sessions.Stop ();
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
      var msg = code.CheckIfValidCloseStatusCode ();
      if (msg != null)
      {
        Log.Error (String.Format ("{0}\ncode: {1}", msg, code));
        return;
      }

      stop (code, reason);
    }

    /// <summary>
    /// Stops receiving the WebSocket connection requests with the specified <see cref="CloseStatusCode"/>
    /// and <see cref="string"/>.
    /// </summary>
    /// <param name="code">
    /// A <see cref="CloseStatusCode"/> that contains a status code indicating the reason for stop.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that contains the reason for stop.
    /// </param>
    public void Stop (CloseStatusCode code, string reason)
    {
      stop ((ushort) code, reason);
    }

    #endregion

    #region Explicit Interface Implementation

    /// <summary>
    /// Binds the specified <see cref="WebSocketContext"/> to a <see cref="WebSocketService"/> instance.
    /// </summary>
    /// <param name="context">
    /// A <see cref="WebSocketContext"/> that contains the WebSocket connection request objects to bind.
    /// </param>
    void IWebSocketServiceHost.BindWebSocket (WebSocketContext context)
    {
      T service = new T ();
      service.Bind (context, _sessions);
      service.Start ();
    }

    #endregion
  }
}
