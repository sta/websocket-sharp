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
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using WebSocketSharp.Frame;
using WebSocketSharp.Net.Sockets;

namespace WebSocketSharp.Server {

  public class WebSocketServer : WebSocketServerBase
  {
    #region Field

    private Dictionary<string, IWebSocketServer> _servers;

    #endregion

    #region Public Constructors

    public WebSocketServer()
      : this(80)
    {
    }

    public WebSocketServer(int port)
      : base(IPAddress.Any, port)
    {
      _servers = new Dictionary<string, IWebSocketServer>();
    }

    #endregion

    #region Protected Method

    protected override void bindSocket(TcpClient client)
    {
      var context = client.AcceptWebSocket();
      var path    = context.RequestUri.ToString();
      if (!_servers.ContainsKey(path))
      {
        var stream = context.Stream;
        var res    = ResponseHandshake.NotImplemented;
        stream.WriteHandshake(res);
        stream.Close();
        client.Close();
        return;
      }

      var socket = context.WebSocket;
      var server = _servers[path];
      server.BindWebSocket(socket);
    }

    #endregion

    #region Public Methods

    public void AddService<T>(string path)
      where T : WebSocketService, new()
    {
      var server = new WebSocketServer<T>();
      _servers.Add(path, server);
    }

    public override void Stop()
    {
      base.Stop();
      foreach (var server in _servers.Values)
        server.Stop();
    }

    #endregion
  }

  public class WebSocketServer<T> : WebSocketServerBase, IWebSocketServer
    where T : WebSocketService, new()
  {
    #region Fields

    private Dictionary<string, WebSocketService> _services;
    private Uri                                  _uri;

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
      : base(IPAddress.Any, port)
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
      _services = new Dictionary<string, WebSocketService>();
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
      service.Bind(socket, _services);
      service.Start();
    }

    public override void Stop()
    {
      base.Stop();
      StopServices();
    }

    public void StopServices()
    {
      StopServices(CloseStatusCode.NORMAL, String.Empty);
    }

    public void StopServices(CloseStatusCode code, string reason)
    {
      lock (((ICollection)_services).SyncRoot)
      {
        foreach (WebSocketService service in _services.Values)
          service.Stop(code, reason);
      }
    }

    #endregion
  }
}
