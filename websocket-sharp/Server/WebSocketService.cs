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
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using WebSocketSharp.Frame;

namespace WebSocketSharp.Server
{
  public abstract class WebSocketService
  {
    #region Private Fields

    private Dictionary<string, WebSocketService> _services;
    private WebSocket                            _socket;

    #endregion

    #region Properties

    public string ID      { get; private set; }
    public bool   IsBound { get; private set; }
    public bool   IsStop  { get; private set; }

    #endregion

    #region Public Constructor

    public WebSocketService()
    {
      ID      = String.Empty;
      IsBound = false;
      IsStop  = false;
    }

    #endregion

    #region Private Method

    private void addService(string id, WebSocketService service)
    {
      lock (((ICollection)_services).SyncRoot)
      {
        _services.Add(id, service);
      }
    }

    private string getNewID()
    {
      return Guid.NewGuid().ToString("N");
    }

    private void defaultBind()
    {
      _socket.OnOpen += (sender, e) =>
      {
        ID = getNewID();
        addService(ID, this);
      };

      _socket.OnClose += (sender, e) =>
      {
        if (!IsStop)
        {
          removeService(ID);
        }
      };
    }

    private void removeService(string id)
    {
      lock (((ICollection)_services).SyncRoot)
      {
        _services.Remove(id);
      }
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

    public void Bind(WebSocket socket, Dictionary<string, WebSocketService> services)
    {
      _socket   = socket;
      _services = services;

      defaultBind();
      _socket.OnOpen    += onOpen;
      _socket.OnMessage += onMessage;
      _socket.OnError   += onError;
      _socket.OnClose   += onClose;

      IsBound = true;
    }

    public bool Ping()
    {
      return Ping(String.Empty);
    }

    public bool Ping(string data)
    {
      return IsBound
             ? _socket.Ping(data)
             : false;
    }

    public Dictionary<string, bool> PingAround()
    {
      return PingAround(String.Empty);
    }

    public Dictionary<string, bool> PingAround(string data)
    {
      if (!IsBound) return null;

      lock (((ICollection)_services).SyncRoot)
      {
        return PingTo(_services.Keys, data);
      }
    }

    public bool PingTo(string id)
    {
      return PingTo(id, String.Empty);
    }

    public Dictionary<string, bool> PingTo(IEnumerable<string> group)
    {
      return PingTo(group, String.Empty);
    }

    public bool PingTo(string id, string data)
    {
      if (!IsBound) return false;

      lock (((ICollection)_services).SyncRoot)
      {
        WebSocketService service;

        return _services.TryGetValue(id, out service)
               ? service.Ping(data)
               : false;
      }
    }

    public Dictionary<string, bool> PingTo(IEnumerable<string> group, string data)
    {
      if (!IsBound) return null;

      var result = new Dictionary<string, bool>();

      lock (((ICollection)_services).SyncRoot)
      {
        foreach (string id in group)
        {
          result.Add(id, PingTo(id, data));
        }
      }

      return result;
    }

    public void Publish<TData>(TData data)
    {
      if (!IsBound) return;

      WaitCallback broadcast = (state) =>
      {
        lock (((ICollection)_services).SyncRoot)
        {
          SendTo(_services.Keys, data);
        }
      };
      ThreadPool.QueueUserWorkItem(broadcast);
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
      if (!IsBound) return;

      if (typeof(TData) != typeof(string) &&
          typeof(TData) != typeof(byte[]))
      {
        var msg = "Type of data must be string or byte[].";
        throw new ArgumentException(msg);
      }

      lock (((ICollection)_services).SyncRoot)
      {
        WebSocketService service;

        if (_services.TryGetValue(id, out service))
        {
          if (typeof(TData) == typeof(string))
          {
            string data_ = (string)(object)data;
            service.Send(data_);
          }
          else if (typeof(TData) == typeof(byte[]))
          {
            byte[] data_ = (byte[])(object)data;
            service.Send(data_);
          }
        }
      }
    }

    public void SendTo<TData>(IEnumerable<string> group, TData data)
    {
      if (!IsBound) return;

      if (typeof(TData) != typeof(string) &&
          typeof(TData) != typeof(byte[]))
      {
        var msg = "Type of data must be string or byte[].";
        throw new ArgumentException(msg);
      }

      lock (((ICollection)_services).SyncRoot)
      {
        foreach (string id in group)
        {
          SendTo(id, data);
        }
      }
    }

    public void Start()
    {
      if (IsBound) _socket.Connect();
    }

    public void Stop()
    {
      Stop(CloseStatusCode.NORMAL, String.Empty);
    }

    public void Stop(CloseStatusCode code, string reason)
    {
      Stop((ushort)code, reason);
    }

    public void Stop(ushort code, string reason)
    {
      if (!IsBound || IsStop) return;

      IsStop = true;
      _socket.Close(code, reason);
    }

    #endregion
  }
}
