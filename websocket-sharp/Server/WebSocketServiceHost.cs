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
using WebSocketSharp.Net;
using WebSocketSharp.Net.WebSockets;

namespace WebSocketSharp.Server {

  /// <summary>
  /// Provides the functions of the server that receives the WebSocket connection requests.
  /// </summary>
  /// <remarks>
  /// The WebSocketServiceHost&lt;T&gt; class provides the single WebSocket service.
  /// </remarks>
  /// <typeparam name="T">
  /// The type of the WebSocket service that the server provides. The T must inherit the <see cref="WebSocketService"/> class.
  /// </typeparam>
  public class WebSocketServiceHost<T> : WebSocketServerBase, IServiceHost
    where T : WebSocketService, new()
  {
    #region Private Fields

    private WebSocketServiceManager _sessions;

    #endregion

    #region Internal Constructors

    internal WebSocketServiceHost()
    {
      init();
    }

    #endregion

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the WebSocketServiceHost&lt;T&gt; class that listens for incoming connection attempts
    /// on the specified <paramref name="port"/>.
    /// </summary>
    /// <param name='port'>
    /// An <see cref="int"/> that contains a port number.
    /// </param>
    public WebSocketServiceHost(int port)
      : this(port, "/")
    {
    }

    /// <summary>
    /// Initializes a new instance of the WebSocketServiceHost&lt;T&gt; class that listens for incoming connection attempts
    /// on the specified WebSocket URL.
    /// </summary>
    /// <param name="url">
    /// A <see cref="string"/> that contains a WebSocket URL.
    /// </param>
    public WebSocketServiceHost(string url)
      : base(url)
    {
      init();
    }

    /// <summary>
    /// Initializes a new instance of the WebSocketServiceHost&lt;T&gt; class that listens for incoming connection attempts
    /// on the specified <paramref name="port"/> and <paramref name="secure"/>.
    /// </summary>
    /// <param name="port">
    /// An <see cref="int"/> that contains a port number. 
    /// </param>
    /// <param name="secure">
    /// A <see cref="bool"/> that indicates providing a secure connection or not. (<c>true</c> indicates providing a secure connection.)
    /// </param>
    public WebSocketServiceHost(int port, bool secure)
      : this(port, "/", secure)
    {
    }

    /// <summary>
    /// Initializes a new instance of the WebSocketServiceHost&lt;T&gt; class that listens for incoming connection attempts
    /// on the specified <paramref name="port"/> and <paramref name="absPath"/>.
    /// </summary>
    /// <param name="port">
    /// An <see cref="int"/> that contains a port number. 
    /// </param>
    /// <param name="absPath">
    /// A <see cref="string"/> that contains an absolute path.
    /// </param>
    public WebSocketServiceHost(int port, string absPath)
      : this(System.Net.IPAddress.Any, port, absPath)
    {
    }

    /// <summary>
    /// Initializes a new instance of the WebSocketServiceHost&lt;T&gt; class that listens for incoming connection attempts
    /// on the specified <paramref name="port"/>, <paramref name="absPath"/> and <paramref name="secure"/>.
    /// </summary>
    /// <param name="port">
    /// An <see cref="int"/> that contains a port number. 
    /// </param>
    /// <param name="absPath">
    /// A <see cref="string"/> that contains an absolute path.
    /// </param>
    /// <param name="secure">
    /// A <see cref="bool"/> that indicates providing a secure connection or not. (<c>true</c> indicates providing a secure connection.)
    /// </param>
    public WebSocketServiceHost(int port, string absPath, bool secure)
      : this(System.Net.IPAddress.Any, port, absPath, secure)
    {
    }

    /// <summary>
    /// Initializes a new instance of the WebSocketServiceHost&lt;T&gt; class that listens for incoming connection attempts
    /// on the specified <paramref name="address"/>, <paramref name="port"/> and <paramref name="absPath"/>.
    /// </summary>
    /// <param name="address">
    /// A <see cref="System.Net.IPAddress"/> that contains a local IP address.
    /// </param>
    /// <param name="port">
    /// An <see cref="int"/> that contains a port number. 
    /// </param>
    /// <param name="absPath">
    /// A <see cref="string"/> that contains an absolute path.
    /// </param>
    public WebSocketServiceHost(System.Net.IPAddress address, int port, string absPath)
      : this(address, port, absPath, port == 443 ? true : false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the WebSocketServiceHost&lt;T&gt; class that listens for incoming connection attempts
    /// on the specified <paramref name="address"/>, <paramref name="port"/>, <paramref name="absPath"/> and <paramref name="secure"/>.
    /// </summary>
    /// <param name="address">
    /// A <see cref="System.Net.IPAddress"/> that contains a local IP address.
    /// </param>
    /// <param name="port">
    /// An <see cref="int"/> that contains a port number. 
    /// </param>
    /// <param name="absPath">
    /// A <see cref="string"/> that contains an absolute path.
    /// </param>
    /// <param name="secure">
    /// A <see cref="bool"/> that indicates providing a secure connection or not. (<c>true</c> indicates providing a secure connection.)
    /// </param>
    public WebSocketServiceHost(System.Net.IPAddress address, int port, string absPath, bool secure)
      : base(address, port, absPath, secure)
    {
      init();
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets or sets a value indicating whether the server cleans up the inactive clients periodically.
    /// </summary>
    /// <value>
    /// <c>true</c> if the server cleans up the inactive clients every 60 seconds; otherwise, <c>false</c>.
    /// The default value is <c>true</c>.
    /// </value>
    public bool Sweeped {
      get {
        return _sessions.Sweeped;
      }

      set {
        _sessions.Sweeped = value;
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

    private void init()
    {
      _sessions = new WebSocketServiceManager();
    }

    #endregion

    #region Protected Methods

    /// <summary>
    /// Accepts a WebSocket connection request.
    /// </summary>
    /// <param name="context">
    /// A <see cref="TcpListenerWebSocketContext"/> that contains the WebSocket connection request objects.
    /// </param>
    protected override void AcceptWebSocket(TcpListenerWebSocketContext context)
    {
      var websocket = context.WebSocket;
      var path      = context.Path.UrlDecode();
      if (path != Uri.GetAbsolutePath().UrlDecode())
      {
        websocket.Close(HttpStatusCode.NotImplemented);
        return;
      }

      if (Uri.IsAbsoluteUri)
        websocket.Url = Uri;

      ((IServiceHost)this).BindWebSocket(context);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Broadcasts the specified <see cref="string"/> to all clients.
    /// </summary>
    /// <param name="data">
    /// A <see cref="string"/> to broadcast.
    /// </param>
    public void Broadcast(string data)
    {
      _sessions.Broadcast(data);
    }

    /// <summary>
    /// Pings with the specified <see cref="string"/> to all clients.
    /// </summary>
    /// <returns>
    /// A Dictionary&lt;string, bool&gt; that contains the collection of session IDs and values
    /// indicating whether the server received the Pongs from each clients in a time.
    /// </returns>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message.
    /// </param>
    public Dictionary<string, bool> Broadping(string message)
    {
      return _sessions.Broadping(message);
    }

    /// <summary>
    /// Stops receiving the WebSocket connection requests.
    /// </summary>
    public override void Stop()
    {
      base.Stop();
      _sessions.Stop();
    }

    #endregion

    #region Explicit Interface Implementation

    /// <summary>
    /// Binds the specified <see cref="WebSocketContext"/> to a <see cref="WebSocketService"/> instance.
    /// </summary>
    /// <param name="context">
    /// A <see cref="WebSocketContext"/> that contains the WebSocket connection request objects to bind.
    /// </param>
    void IServiceHost.BindWebSocket(WebSocketContext context)
    {
      T service = new T();
      service.Bind(context, _sessions);
      service.Start();
    }

    #endregion
  }
}
