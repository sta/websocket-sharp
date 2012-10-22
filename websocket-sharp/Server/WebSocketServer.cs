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
      : this(System.Net.IPAddress.Any, port)
    {
    }

    public WebSocketServer(string url)
      : base(url)
    {
      if (BaseUri.AbsolutePath != "/")
      {
        var msg = "Must not contain the path component: " + url;
        throw new ArgumentException(msg, "url");
      }

      init();
    }

    public WebSocketServer(System.Net.IPAddress address, int port)
      : base(address, port)
    {
      init();
    }

    #endregion

    #region Private Method

    private void init()
    {
      _services = new Dictionary<string, IServiceHost>();
    }

    #endregion

    #region Protected Method

    protected override void AcceptWebSocket(TcpClient client)
    {
      var context = client.AcceptWebSocket();
      var socket  = context.WebSocket;
      var path    = context.Path.UrlDecode();
      if (!_services.ContainsKey(path))
      {
        socket.Close(HttpStatusCode.NotImplemented);
        return;
      }

      if (BaseUri.IsAbsoluteUri)
        socket.Url = new Uri(BaseUri, path);

      var service = _services[path];
      service.BindWebSocket(socket);
    }

    #endregion

    #region Public Methods

    public void AddService<T>(string absPath)
      where T : WebSocketService, new()
    {
      string msg;
      if (!absPath.IsValidAbsolutePath(out msg))
      {
        Error(msg);
        return;
      }

      var service = new WebSocketServer<T>();
      _services.Add(absPath, service);
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

    #endregion

    #region Internal Constructor

    internal WebSocketServer()
    {
      init();
    }

    #endregion

    #region Public Constructors

    public WebSocketServer(int port)
      : this(port, "/")
    {
    }

    public WebSocketServer(string url)
      : base(url)
    {
      init();
    }

    public WebSocketServer(int port, string absPath)
      : this(System.Net.IPAddress.Any, port, absPath)
    {
    }

    public WebSocketServer(System.Net.IPAddress address, int port, string absPath)
      : base(address, port, absPath)
    {
      init();
    }

    #endregion

    #region Property

    public Uri Uri {
      get {
        return BaseUri;
      }
    }

    #endregion

    #region Private Method

    private void init()
    {
      _sessions = new SessionManager();
    }

    #endregion

    #region Protected Method

    protected override void AcceptWebSocket(TcpClient client)
    {
      var context = client.AcceptWebSocket();
      var socket  = context.WebSocket;
      var path    = context.Path.UrlDecode();
      if (path != Uri.GetAbsolutePath().UrlDecode())
      {
        socket.Close(HttpStatusCode.NotImplemented);
        return;
      }

      if (Uri.IsAbsoluteUri)
        socket.Url = new Uri(Uri, path);

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
