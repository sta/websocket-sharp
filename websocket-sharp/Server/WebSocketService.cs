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
using WebSocketSharp.Frame;

namespace WebSocketSharp.Server
{
  public abstract class WebSocketService
  {
    #region Private Fields

    private IWebSocketServer _server;
    private WebSocket        _socket;

    #endregion

    #region Property

    public bool IsBinded { get; private set; }

    #endregion

    #region Public Constructor

    public WebSocketService()
    {
      IsBinded = false;
    }

    #endregion

    #region Private Method

    private void defaultBind()
    {
      _socket.OnOpen += (sender, e) =>
      {
        _server.AddService(this);
      };

      _socket.OnClose += (sender, e) =>
      {
        _server.RemoveService(this);
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

      IsBinded = true;
    }

    public void BPing()
    {
      BPing(String.Empty);
    }

    public void BPing(string data)
    {
      if (IsBinded) _server.Ping(data);
    }

    public void Close()
    {
      if (IsBinded) _socket.Close();
    }

    public void Close(CloseStatusCode code, string reason)
    {
      if (IsBinded) _socket.Close(code, reason);
    }

    public void Open()
    {
      if (IsBinded) _socket.Connect();
    }

    public void Ping()
    {
      if (IsBinded) _socket.Ping();
    }

    public void Ping(string data)
    {
      if (IsBinded) _socket.Ping(data);
    }

    public void Publish(byte[] data)
    {
      if (IsBinded) _server.Publish(data);
    }

    public void Publish(string data)
    {
      if (IsBinded) _server.Publish(data);
    }

    public void Send(byte[] data)
    {
      if (IsBinded) _socket.Send(data);
    }

    public void Send(string data)
    {
      if (IsBinded) _socket.Send(data);
    }

    #endregion
  }
}
