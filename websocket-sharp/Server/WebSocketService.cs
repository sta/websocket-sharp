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
using System.Collections.Specialized;
using System.Threading;
using WebSocketSharp.Frame;

namespace WebSocketSharp.Server {

  public abstract class WebSocketService
  {
    #region Private Fields

    private SessionManager _sessions;
    private WebSocket      _socket;

    #endregion

    #region Public Constructor

    public WebSocketService()
    {
      ID      = String.Empty;
      IsBound = false;
    }

    #endregion

    #region Protected Properties

    protected NameValueCollection QueryString {
      get {
        return IsBound ? _socket.QueryString : null;
      }
    }

    protected SessionManager Sessions {
      get {
        return IsBound ? _sessions : null;
      }
    }

    #endregion

    #region Public Properties

    public string ID      { get; private set; }
    public bool   IsBound { get; private set; }

    #endregion

    #region Private Method

    private void defaultBind()
    {
      _socket.OnOpen += (sender, e) =>
      {
        ID = _sessions.Add(this);
      };

      _socket.OnClose += (sender, e) =>
      {
        _sessions.Remove(ID);
      };
    }

    #endregion

    #region Protected Methods

    protected virtual void OnClose(object sender, CloseEventArgs e)
    {
    }

    protected virtual void OnError(object sender, ErrorEventArgs e)
    {
    }

    protected virtual void OnMessage(object sender, MessageEventArgs e)
    {
    }

    protected virtual void OnOpen(object sender, EventArgs e)
    {
    }

    #endregion

    #region Internal Methods

    internal void SendAsync(byte[] data, Action completed)
    {
      _socket.SendAsync(data, completed);
    }

    internal void SendAsync(string data, Action completed)
    {
      _socket.SendAsync(data, completed);
    }

    #endregion

    #region Public Methods

    public void Bind(WebSocket socket, SessionManager sessions)
    {
      _socket   = socket;
      _sessions = sessions;

      defaultBind();
      _socket.OnOpen    += OnOpen;
      _socket.OnMessage += OnMessage;
      _socket.OnError   += OnError;
      _socket.OnClose   += OnClose;

      IsBound = true;
    }

    public bool Ping()
    {
      return Ping(String.Empty);
    }

    public bool Ping(string message)
    {
      return IsBound
             ? _socket.Ping(message)
             : false;
    }

    public Dictionary<string, bool> PingAround()
    {
      return PingAround(String.Empty);
    }

    public Dictionary<string, bool> PingAround(string message)
    {
      return IsBound
             ? _sessions.Broadping(message)
             : null;
    }

    public bool PingTo(string id)
    {
      return PingTo(id, String.Empty);
    }

    public bool PingTo(string id, string message)
    {
      if (!IsBound)
        return false;

      WebSocketService service;
      return _sessions.TryGetByID(id, out service)
             ? service.Ping(message)
             : false;
    }

    public void Publish(byte[] data)
    {
      if (IsBound)
        _sessions.Broadcast(data);
    }

    public void Publish(string data)
    {
      if (IsBound)
        _sessions.Broadcast(data);
    }

    public void Send(byte[] data)
    {
      if (IsBound)
        _socket.Send(data);
    }

    public void Send(string data)
    {
      if (IsBound)
        _socket.Send(data);
    }

    public void SendTo(string id, byte[] data)
    {
      if (!IsBound)
        return;

      WebSocketService service;
      if (_sessions.TryGetByID(id, out service))
        service.Send(data);
    }

    public void SendTo(string id, string data)
    {
      if (!IsBound)
        return;

      WebSocketService service;
      if (_sessions.TryGetByID(id, out service))
        service.Send(data);
    }

    public void Start()
    {
      if (IsBound)
        _socket.Connect();
    }

    public void Stop()
    {
      if (!IsBound)
        return;

      _socket.Close();
    }

    public void Stop(CloseStatusCode code, string reason)
    {
      Stop((ushort)code, reason);
    }

    public void Stop(ushort code, string reason)
    {
      if (!IsBound)
        return;

      _socket.Close(code, reason);
    }

    #endregion
  }
}
