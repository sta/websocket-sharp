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

    private ServiceHostManager _serviceHosts;

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

      _serviceHosts = new ServiceHostManager (Log);
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
      _serviceHosts = new ServiceHostManager (Log);
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the connection count to the <see cref="WebSocketServer"/>.
    /// </summary>
    /// <value>
    /// An <see cref="int"/> that contains the connection count.
    /// </value>
    public int ConnectionCount {
      get {
        return _serviceHosts.ConnectionCount;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the server cleans up the inactive WebSocket service
    /// instances periodically.
    /// </summary>
    /// <value>
    /// <c>true</c> if the server cleans up the inactive WebSocket service instances every 60 seconds;
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
    /// Gets the collection of paths associated with the every WebSocket services that the server provides.
    /// </summary>
    /// <value>
    /// An IEnumerable&lt;string&gt; that contains the collection of paths.
    /// </value>
    public IEnumerable<string> ServicePaths {
      get {
        var url = BaseUri.IsAbsoluteUri
                ? BaseUri.ToString ().TrimEnd ('/')
                : String.Empty;

        foreach (var path in _serviceHosts.Paths)
          yield return url + path;
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
      _serviceHosts.Stop (code, reason);
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
      IServiceHost host;
      if (!_serviceHosts.TryGetServiceHost (path, out host))
      {
        ws.Close (HttpStatusCode.NotImplemented);
        return;
      }

      if (BaseUri.IsAbsoluteUri)
        ws.Url = new Uri (BaseUri, path);

      host.BindWebSocket (context);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Adds the specified typed WebSocket service with the specified <paramref name="servicePath"/>.
    /// </summary>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service.
    /// </param>
    /// <typeparam name="T">
    /// The type of the WebSocket service. The T must inherit the <see cref="WebSocketService"/> class.
    /// </typeparam>
    public void AddWebSocketService<T> (string servicePath)
      where T : WebSocketService, new ()
    {
      string msg;
      if (!servicePath.IsValidAbsolutePath (out msg))
      {
        Log.Error (msg);
        Error (msg);

        return;
      }

      var host = new WebSocketServiceHost<T> (Log);
      host.Uri = BaseUri.IsAbsoluteUri
               ? new Uri (BaseUri, servicePath)
               : servicePath.ToUri ();

      if (!KeepClean)
        host.KeepClean = false;

      _serviceHosts.Add (servicePath, host);
    }

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

      _serviceHosts.Broadcast (data);
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

      _serviceHosts.Broadcast (data);
    }

    /// <summary>
    /// Broadcasts the specified array of <see cref="byte"/> to all clients of the WebSocket service
    /// with the specified <paramref name="servicePath"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the WebSocket service is found; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    /// <param name="data">
    /// An array of <see cref="byte"/> to broadcast.
    /// </param>
    public bool BroadcastTo (string servicePath, byte [] data)
    {
      var msg = servicePath.IsNullOrEmpty ()
              ? "'servicePath' must not be null or empty."
              : data == null
                ? "'data' must not be null."
                : String.Empty;

      if (msg.Length > 0)
      {
        Log.Error (msg);
        Error (msg);

        return false;
      }

      return _serviceHosts.BroadcastTo (servicePath, data);
    }

    /// <summary>
    /// Broadcasts the specified <see cref="string"/> to all clients of the WebSocket service
    /// with the specified <paramref name="servicePath"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the WebSocket service is found; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    /// <param name="data">
    /// A <see cref="string"/> to broadcast.
    /// </param>
    public bool BroadcastTo (string servicePath, string data)
    {
      var msg = servicePath.IsNullOrEmpty ()
              ? "'servicePath' must not be null or empty."
              : data == null
                ? "'data' must not be null."
                : String.Empty;

      if (msg.Length > 0)
      {
        Log.Error (msg);
        Error (msg);

        return false;
      }

      return _serviceHosts.BroadcastTo (servicePath, data);
    }

    /// <summary>
    /// Sends Pings with the specified <see cref="string"/> to all clients.
    /// </summary>
    /// <returns>
    /// A Dictionary&lt;string, Dictionary&lt;string, bool&gt;&gt; that contains the collection of
    /// service paths and pairs of ID and value indicating whether the <see cref="WebSocketServer"/>
    /// received the Pongs from each clients in a time.
    /// </returns>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to send.
    /// </param>
    public Dictionary<string, Dictionary<string, bool>> Broadping (string message)
    {
      if (message.IsNullOrEmpty ())
        return _serviceHosts.Broadping (String.Empty);

      var len = Encoding.UTF8.GetBytes (message).Length;
      if (len > 125)
      {
        var msg = "The payload length of a Ping frame must be 125 bytes or less.";
        Log.Error (msg);
        Error (msg);

        return null;
      }

      return _serviceHosts.Broadping (message);
    }

    /// <summary>
    /// Sends Pings with the specified <see cref="string"/> to all clients of the WebSocket service
    /// with the specified <paramref name="servicePath"/>.
    /// </summary>
    /// <returns>
    /// A Dictionary&lt;string, bool&gt; that contains the collection of session IDs and values
    /// indicating whether the <see cref="WebSocketServer"/> received the Pongs from each clients
    /// in a time. If the WebSocket service is not found, returns <see langword="null"/>.
    /// </returns>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to send.
    /// </param>
    public Dictionary<string, bool> BroadpingTo (string servicePath, string message)
    {
      if (message == null)
        message = String.Empty;

      var msg = servicePath.IsNullOrEmpty ()
              ? "'servicePath' must not be null or empty."
              : Encoding.UTF8.GetBytes (message).Length > 125
                ? "The payload length of a Ping frame must be 125 bytes or less."
                : String.Empty;

      if (msg.Length > 0)
      {
        Log.Error (msg);
        Error (msg);

        return null;
      }

      return _serviceHosts.BroadpingTo (servicePath, message);
    }

    /// <summary>
    /// Gets the connection count to the WebSocket service with the specified <paramref name="servicePath"/>.
    /// </summary>
    /// <returns>
    /// An <see cref="int"/> that contains the connection count if the WebSocket service is successfully found;
    /// otherwise, <c>-1</c>.
    /// </returns>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    public int GetConnectionCount (string servicePath)
    {
      if (servicePath.IsNullOrEmpty ())
      {
        var msg = "'servicePath' must not be null or empty.";
        Log.Error (msg);
        Error (msg);

        return -1;
      }

      return _serviceHosts.GetConnectionCount (servicePath);
    }

    /// <summary>
    /// Sends a Ping with the specified <see cref="string"/> to the client associated with
    /// the specified <paramref name="servicePath"/> and <paramref name="id"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the <see cref="WebSocketServer"/> receives a Pong from the client in a time;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    /// <param name="id">
    /// A <see cref="string"/> that contains an ID that represents the destination for the Ping.
    /// </param>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to send.
    /// </param>
    public bool PingTo (string servicePath, string id, string message)
    {
      if (message == null)
        message = String.Empty;

      var msg = servicePath.IsNullOrEmpty ()
              ? "'servicePath' must not be null or empty."
              : id.IsNullOrEmpty ()
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

      return _serviceHosts.PingTo (servicePath, id, message);
    }

    /// <summary>
    /// Removes the WebSocket service with the specified <paramref name="servicePath"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the WebSocket service is successfully found and removed; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    public bool RemoveWebSocketService (string servicePath)
    {
      if (servicePath.IsNullOrEmpty ())
      {
        var msg = "'servicePath' must not be null or empty.";
        Log.Error (msg);
        Error (msg);

        return false;
      }

      return _serviceHosts.Remove (servicePath);
    }

    /// <summary>
    /// Sends a binary data to the client associated with the specified <paramref name="servicePath"/> and
    /// <paramref name="id"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the client is successfully found; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    /// <param name="id">
    /// A <see cref="string"/> that contains an ID that represents the destination for the data.
    /// </param>
    /// <param name="data">
    /// An array of <see cref="byte"/> that contains a binary data to send.
    /// </param>
    public bool SendTo (string servicePath, string id, byte [] data)
    {
      var msg = servicePath.IsNullOrEmpty ()
              ? "'servicePath' must not be null or empty."
              : id.IsNullOrEmpty ()
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

      return _serviceHosts.SendTo (servicePath, id, data);
    }

    /// <summary>
    /// Sends a text data to the client associated with the specified <paramref name="servicePath"/> and
    /// <paramref name="id"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the client is successfully found; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    /// <param name="id">
    /// A <see cref="string"/> that contains an ID that represents the destination for the data.
    /// </param>
    /// <param name="data">
    /// A <see cref="string"/> that contains a text data to send.
    /// </param>
    public bool SendTo (string servicePath, string id, string data)
    {
      var msg = servicePath.IsNullOrEmpty ()
              ? "'servicePath' must not be null or empty."
              : id.IsNullOrEmpty ()
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

      return _serviceHosts.SendTo (servicePath, id, data);
    }

    /// <summary>
    /// Stops receiving the WebSocket connection requests.
    /// </summary>
    public override void Stop ()
    {
      base.Stop ();
      _serviceHosts.Stop ();
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
  }
}
