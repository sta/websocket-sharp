#region MIT License
/**
 * WebSocketServer.cs
 *
 * A C# implementation of a WebSocket protocol server.
 *
 * The MIT License
 *
 * Copyright (c) 2012 sta.blockhead
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
using WebSocketSharp.Net.Sockets;

namespace WebSocketSharp.Server {

  public class WebSocketServer : WebSocketServerBase
  {
    #region Field

    private Dictionary<string, IServiceHost> _services;

    #endregion

    #region Public Constructors

    public WebSocketServer()
      : this(80)
    {
    }

    public WebSocketServer(int port)
      : base(System.Net.IPAddress.Any, port)
    {
      _services = new Dictionary<string, IServiceHost>();
    }

    #endregion

    #region Protected Method

    protected override void bindSocket(TcpClient client)
    {
      var context = client.AcceptWebSocket();
      var socket  = context.WebSocket;
      var path    = context.RequestUri.ToString();
      if (!_services.ContainsKey(path))
      {
        socket.Close(HttpStatusCode.NotImplemented);
        return;
      }

      var service = _services[path];
      service.BindWebSocket(socket);
    }

    #endregion

    #region Public Methods

    public void AddService<T>(string path)
      where T : WebSocketService, new()
    {
      var service = new WebSocketServer<T>();
      _services.Add(path, service);
    }

    public override void Stop()
    {
      base.Stop();
      foreach (var service in _services.Values)
        service.Stop();
      _services.Clear();
    }

    #endregion
  }

  public class WebSocketServer<T> : WebSocketServerBase, IServiceHost
    where T : WebSocketService, new()
  {
    #region Fields

    private SessionManager _sessions;
    private Uri            _uri;

    #endregion

    #region Internal Constructor

    internal WebSocketServer()
    {
      init();
    }

    #endregion

    #region Public Constructors

    public WebSocketServer(string url)
      : base(url)
    {
      _uri = url.ToUri();
      init();
    }

    public WebSocketServer(int port)
      : this(port, "/")
    {
    }

    public WebSocketServer(int port, string path)
      : base(System.Net.IPAddress.Any, port)
    {
      var uri = path.ToUri();
      if (uri.IsAbsoluteUri)
      {
        var msg = "Not absolute path: " + path;
        throw new ArgumentException(msg, "path");
      }

      _uri = uri;
      init();
    }

    #endregion

    #region Property

    public Uri Uri
    {
      get { return _uri; }
    }

    #endregion

    #region Private Method

    private void init()
    {
      _sessions = new SessionManager();
    }

    #endregion

    #region Protected Method

    protected override void bindSocket(TcpClient client)
    {
      var socket = new WebSocket(_uri, client);
      BindWebSocket(socket);
    }

    #endregion

    #region Public Methods

    public void BindWebSocket(WebSocket socket)
    {
      T service = new T();
      service.Bind(socket, _sessions);
      service.Start();
    }

    public override void Stop()
    {
      base.Stop();
      _sessions.Stop();
    }

    #endregion
  }
}
