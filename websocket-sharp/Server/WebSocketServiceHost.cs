#region MIT License
/**
 * WebSocketServiceHost.cs
 *
 * A C# implementation of the WebSocket protocol server.
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

namespace WebSocketSharp.Server {

  public class WebSocketServiceHost<T> : WebSocketServerBase, IServiceHost
    where T : WebSocketService, new()
  {
    #region Field

    private SessionManager _sessions;

    #endregion

    #region Internal Constructor

    internal WebSocketServiceHost()
    {
      init();
    }

    #endregion

    #region Public Constructors

    public WebSocketServiceHost(int port)
      : this(port, "/")
    {
    }

    public WebSocketServiceHost(string url)
      : base(url)
    {
      init();
    }

    public WebSocketServiceHost(int port, string absPath)
      : this(System.Net.IPAddress.Any, port, absPath)
    {
    }

    public WebSocketServiceHost(System.Net.IPAddress address, int port, string absPath)
      : base(address, port, absPath)
    {
      init();
    }

    #endregion

    #region Properties

    public bool Sweeped {
      get {
        return _sessions.Sweeped;
      }

      set {
        _sessions.Sweeped = value;
      }
    }

    public Uri Uri {
      get {
        return BaseUri;
      }

      internal set {
        BaseUri = value;
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
        socket.Url = Uri;

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

    public void Broadcast(string data)
    {
      _sessions.Broadcast(data);
    }

    public Dictionary<string, bool> Broadping(string message)
    {
      return _sessions.Broadping(message);
    }

    public override void Stop()
    {
      base.Stop();
      _sessions.Stop();
    }

    #endregion
  }
}
