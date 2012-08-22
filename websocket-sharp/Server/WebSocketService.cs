#region MIT License
/**
 * WebSocketService.cs
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
using WebSocketSharp.Frame;

namespace WebSocketSharp.Server
{
  public abstract class WebSocketService
  {
    #region Private Fields

    private IWebSocketServer _server;
    private WebSocket        _socket;

    #endregion

    #region Properties

    public string ID      { get; private set; }
    public bool   IsBound { get; private set; }

    #endregion

    #region Public Constructor

    public WebSocketService()
    {
      ID      = String.Empty;
      IsBound = false;
    }

    #endregion

    #region Private Method

    private string getNewID()
    {
      return Guid.NewGuid().ToString("N");
    }

    private void defaultBind()
    {
      _socket.OnOpen += (sender, e) =>
      {
        ID = getNewID();
        _server.AddService(ID, this);
      };

      _socket.OnClose += (sender, e) =>
      {
        if (_server.State == WsServerState.START)
        {
          _server.RemoveService(ID);
        }
      };
    }

    #endregion

    #region Protected Methods

    protected virtual void onOpen(object sender, EventArgs e)
    {
    }

    protected virtual void onMessage(object sender, MessageEventArgs e)
    {
    }

    protected virtual void onError(object sender, ErrorEventArgs e)
    {
    }

    protected virtual void onClose(object sender, CloseEventArgs e)
    {
    }

    #endregion

    #region Public Methods

    public void Bind(IWebSocketServer server, WebSocket socket)
    {
      _server = server;
      _socket = socket;

      defaultBind();
      _socket.OnOpen    += onOpen;
      _socket.OnMessage += onMessage;
      _socket.OnError   += onError;
      _socket.OnClose   += onClose;

      IsBound = true;
    }

    public Dictionary<string, bool> PingAround()
    {
      return PingAround(String.Empty);
    }

    public Dictionary<string, bool> PingAround(string data)
    {
      if (IsBound) return _server.PingAround(data);
      return null;
    }

    public bool Ping()
    {
      if (IsBound) return _socket.Ping();
      return false;
    }

    public bool Ping(string data)
    {
      if (IsBound) return _socket.Ping(data);
      return false;
    }

    public void Publish<TData>(TData data)
    {
      if (IsBound) _server.Publish(data);
    }

    public void Send(byte[] data)
    {
      if (IsBound) _socket.Send(data);
    }

    public void Send(string data)
    {
      if (IsBound) _socket.Send(data);
    }

    public void SendTo<TData>(string id, TData data)
    {
      if (IsBound) _server.SendTo(id, data);
    }

    public void SendTo<TData>(IEnumerable<string> group, TData data)
    {
      if (IsBound) _server.SendTo(group, data);
    }

    public void Start()
    {
      if (IsBound) _socket.Connect();
    }

    public void Stop()
    {
      if (IsBound) _socket.Close();
    }

    public void Stop(CloseStatusCode code, string reason)
    {
      if (IsBound) _socket.Close(code, reason);
    }

    #endregion
  }
}
