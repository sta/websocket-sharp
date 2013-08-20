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
  public class WebSocketServiceHost<T> : WebSocketServerBase, IServiceHost
    where T : WebSocketService, new ()
  {
    #region Private Fields

    private WebSocketServiceManager _sessions;

    #endregion

    #region Internal Constructors

    internal WebSocketServiceHost (Logger logger)
      : base (logger)
    {
      _sessions = new WebSocketServiceManager (logger);
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
      _sessions = new WebSocketServiceManager (Log);
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
    /// incoming connection attempts on the specified <paramref name="port"/> and <paramref name="absPath"/>.
    /// </summary>
    /// <param name="port">
    /// An <see cref="int"/> that contains a port number.
    /// </param>
    /// <param name="absPath">
    /// A <see cref="string"/> that contains an absolute path.
    /// </param>
    public WebSocketServiceHost (int port, string absPath)
      : this (System.Net.IPAddress.Any, port, absPath)
    {
    }

    /// <summary>
    /// Initializes a new instance of the WebSocketServiceHost&lt;T&gt; class that listens for
    /// incoming connection attempts on the specified <paramref name="port"/>, <paramref name="absPath"/>
    /// and <paramref name="secure"/>.
    /// </summary>
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
    public WebSocketServiceHost (int port, string absPath, bool secure)
      : this (System.Net.IPAddress.Any, port, absPath, secure)
    {
    }

    /// <summary>
    /// Initializes a new instance of the WebSocketServiceHost&lt;T&gt; class that listens for
    /// incoming connection attempts on the specified <paramref name="address"/>, <paramref name="port"/>
    /// and <paramref name="absPath"/>.
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
    public WebSocketServiceHost (System.Net.IPAddress address, int port, string absPath)
      : this (address, port, absPath, port == 443 ? true : false)
    {
    }

    /// <summary>
    /// Initializes a new instance of the WebSocketServiceHost&lt;T&gt; class that listens for
    /// incoming connection attempts on the specified <paramref name="address"/>, <paramref name="port"/>,
    /// <paramref name="absPath"/> and <paramref name="secure"/>.
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
    /// A <see cref="bool"/> that indicates providing a secure connection or not.
    /// (<c>true</c> indicates providing a secure connection.)
    /// </param>
    public WebSocketServiceHost (System.Net.IPAddress address, int port, string absPath, bool secure)
      : base (address, port, absPath, secure)
    {
      _sessions = new WebSocketServiceManager (Log);
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
    /// the inactive <see cref="WebSocketService"/> instances periodically.
    /// </summary>
    /// <value>
    /// <c>true</c> if the WebSocket service host cleans up the inactive WebSocket service instances every 60 seconds;
    /// otherwise, <c>false</c>. The default value is <c>true</c>.
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
      if (data.Length > 125)
      {
        var msg = "The payload length of a Close frame must be 125 bytes or less.";
        Log.Error (String.Format ("{0}\ncode: {1}\nreason: {2}", msg, code, reason));
        Error (msg);

        return;
      }

      base.Stop ();
      _sessions.Stop (code, reason);
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
      var ws = context.WebSocket;
      var path = context.Path.UrlDecode ();

      ws.Log = Log;
      if (path != Uri.GetAbsolutePath ().UrlDecode ())
      {
        ws.Close (HttpStatusCode.NotImplemented);
        return;
      }

      if (Uri.IsAbsoluteUri)
        ws.Url = Uri;

      ((IServiceHost) this).BindWebSocket (context);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Broadcasts the specified array of <see cref="byte"/> to all clients.
    /// </summary>
    /// <param name="data">
    /// An array of <see cref="byte"/> to broadcast.
    /// </param>
    public void Broadcast (byte [] data)
    {
      if (data == null)
      {
        var msg = "'data' must not be null.";
        Log.Error (msg);
        Error (msg);

        return;
      }

      _sessions.Broadcast (data);
    }

    /// <summary>
    /// Broadcasts the specified <see cref="string"/> to all clients.
    /// </summary>
    /// <param name="data">
    /// A <see cref="string"/> to broadcast.
    /// </param>
    public void Broadcast (string data)
    {
      if (data == null)
      {
        var msg = "'data' must not be null.";
        Log.Error (msg);
        Error (msg);

        return;
      }

      _sessions.Broadcast (data);
    }

    /// <summary>
    /// Sends Pings with the specified <see cref="string"/> to all clients.
    /// </summary>
    /// <returns>
    /// A Dictionary&lt;string, bool&gt; that contains the collection of session IDs and values
    /// indicating whether the service host received the Pongs from each clients in a time.
    /// </returns>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to send.
    /// </param>
    public Dictionary<string, bool> Broadping (string message)
    {
      if (message.IsNullOrEmpty ())
        return _sessions.Broadping (String.Empty);

      var len = Encoding.UTF8.GetBytes (message).Length;
      if (len > 125)
      {
        var msg = "The payload length of a Ping frame must be 125 bytes or less.";
        Log.Error (msg);
        Error (msg);

        return null;
      }

      return _sessions.Broadping (message);
    }

    /// <summary>
    /// Sends a Ping with the specified <see cref="string"/> to the client associated with
    /// the specified ID.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the service host receives a Pong from the client in a time; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="id">
    /// A <see cref="string"/> that contains an ID that represents the destination for the Ping.
    /// </param>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to send.
    /// </param>
    public bool PingTo (string id, string message)
    {
      if (message == null)
        message = String.Empty;

      var msg = id.IsNullOrEmpty ()
                ? "'id' must not be null or empty."
                : Encoding.UTF8.GetBytes (message).Length > 125
                  ? "The payload length of a Ping frame must be 125 bytes or less."
                  : String.Empty;

      if (msg.Length > 0)
      {
        Log.Error (msg);
        Error (msg);

        return false;
      }

      return _sessions.PingTo (id, message);
    }

    /// <summary>
    /// Sends a binary data to the client associated with the specified ID.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the client associated with <paramref name="id"/> is successfully found;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <param name="id">
    /// A <see cref="string"/> that contains an ID that represents the destination for the data.
    /// </param>
    /// <param name="data">
    /// An array of <see cref="byte"/> that contains a binary data to send.
    /// </param>
    public bool SendTo (string id, byte [] data)
    {
      var msg = id.IsNullOrEmpty ()
                ? "'id' must not be null or empty."
                : data == null
                  ? "'data' must not be null."
                  : String.Empty;

      if (msg.Length > 0)
      {
        Log.Error (msg);
        Error (msg);

        return false;
      }

      return _sessions.SendTo (id, data);
    }

    /// <summary>
    /// Sends a text data to the client associated with the specified ID.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the client associated with <paramref name="id"/> is successfully found;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <param name="id">
    /// A <see cref="string"/> that contains an ID that represents the destination for the data.
    /// </param>
    /// <param name="data">
    /// A <see cref="string"/> that contains a text data to send.
    /// </param>
    public bool SendTo (string id, string data)
    {
      var msg = id.IsNullOrEmpty ()
                ? "'id' must not be null or empty."
                : data == null
                  ? "'data' must not be null."
                  : String.Empty;

      if (msg.Length > 0)
      {
        Log.Error (msg);
        Error (msg);

        return false;
      }

      return _sessions.SendTo (id, data);
    }

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
      if (!code.IsCloseStatusCode ())
      {
        var msg = "Invalid status code for stop.";
        Log.Error (String.Format ("{0}\ncode: {1}", msg, code));
        Error (msg);

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
    void IServiceHost.BindWebSocket (WebSocketContext context)
    {
      T service = new T ();
      service.Bind (context, _sessions);
      service.Start ();
    }

    /// <summary>
    /// Broadcasts the specified array of <see cref="byte"/> to all clients.
    /// </summary>
    /// <param name="data">
    /// An array of <see cref="byte"/> to broadcast.
    /// </param>
    void IServiceHost.Broadcast (byte [] data)
    {
      _sessions.Broadcast (data);
    }

    /// <summary>
    /// Broadcasts the specified <see cref="string"/> to all clients.
    /// </summary>
    /// <param name="data">
    /// A <see cref="string"/> to broadcast.
    /// </param>
    void IServiceHost.Broadcast (string data)
    {
      _sessions.Broadcast (data);
    }

    /// <summary>
    /// Sends Pings with the specified <see cref="string"/> to all clients.
    /// </summary>
    /// <returns>
    /// A Dictionary&lt;string, bool&gt; that contains the collection of session IDs and values
    /// indicating whether the service host received the Pongs from each clients in a time.
    /// </returns>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to send.
    /// </param>
    Dictionary<string, bool> IServiceHost.Broadping (string message)
    {
      return _sessions.Broadping (message);
    }

    /// <summary>
    /// Sends a Ping with the specified <see cref="string"/> to the client associated with
    /// the specified ID.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the service host receives a Pong from the client in a time; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="id">
    /// A <see cref="string"/> that contains an ID that represents the destination for the Ping.
    /// </param>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to send.
    /// </param>
    bool IServiceHost.PingTo (string id, string message)
    {
      return _sessions.PingTo (id, message);
    }

    /// <summary>
    /// Sends a binary data to the client associated with the specified ID.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the client associated with <paramref name="id"/> is successfully found;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <param name="id">
    /// A <see cref="string"/> that contains an ID that represents the destination for the data.
    /// </param>
    /// <param name="data">
    /// An array of <see cref="byte"/> that contains a binary data to send.
    /// </param>
    bool IServiceHost.SendTo (string id, byte [] data)
    {
      return _sessions.SendTo (id, data);
    }

    /// <summary>
    /// Sends a text data to the client associated with the specified ID.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the client associated with <paramref name="id"/> is successfully found;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <param name="id">
    /// A <see cref="string"/> that contains an ID that represents the destination for the data.
    /// </param>
    /// <param name="data">
    /// A <see cref="string"/> that contains a text data to send.
    /// </param>
    bool IServiceHost.SendTo (string id, string data)
    {
      return _sessions.SendTo (id, data);
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
    void IServiceHost.Stop (ushort code, string reason)
    {
      base.Stop ();
      _sessions.Stop (code, reason);
    }

    #endregion
  }
}
