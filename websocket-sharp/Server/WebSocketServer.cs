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

    private WebSocketServiceHostManager _serviceHosts;

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
    }

    #endregion

    #region Public Properties

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
    /// Gets the collection of paths to the WebSocket services that the server provides.
    /// </summary>
    /// <value>
    /// An IEnumerable&lt;string&gt; that contains the collection of paths.
    /// </value>
    public IEnumerable<string> ServicePaths {
      get {
        var url = BaseUri.IsAbsoluteUri
                ? BaseUri.ToString ().TrimEnd ('/')
                : String.Empty;

        foreach (var path in _serviceHosts.ServicePaths)
          yield return url + path;
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

    #region Private Methods

    private void stop (ushort code, string reason)
    {
      var data = code.Append (reason);
      if (data.Length > 125)
      {
        Log.Error (String.Format (
          "The payload length of a Close frame must be 125 bytes or less.\ncode: {0}\nreason: {1}", code, reason));
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
      var websocket = context.WebSocket;
      var path = context.Path.UrlDecode ();

      websocket.Log = Log;
      IServiceHost host;
      if (!_serviceHosts.TryGetServiceHost (path, out host))
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
        Log.Error ("'servicePath' must not be null or empty.");
        return false;
      }

      return _serviceHosts.Remove (servicePath);
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
        Log.Error ("Invalid status code for stop.\ncode: " + code);
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
