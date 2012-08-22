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
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using WebSocketSharp.Frame;

namespace WebSocketSharp.Server
{
  public class WebSocketServer<T> : IWebSocketServer
    where T : WebSocketService, new()
  {
    #region Private Fields

    private object                               _forServices;
    private Dictionary<string, WebSocketService> _services;
    private WsServerState                        _state;
    private TcpListener                          _tcpListener;
    private Uri                                  _uri;

    #endregion

    #region Properties

    public IPAddress Address
    {
      get { return Endpoint.Address; }
    }

    public IPEndPoint Endpoint
    {
      get { return (IPEndPoint)_tcpListener.LocalEndpoint; }
    }

    public int Port
    {
      get { return Endpoint.Port; }
    }

    public WsServerState State
    {
      get { return _state; }
    }

    public string Url
    {
      get { return _uri.ToString(); }
    }

    #endregion

    #region Events

    public event EventHandler<ErrorEventArgs> OnError;

    #endregion

    #region Public Constructor

    public WebSocketServer(string url)
    {
      _uri = new Uri(url);

      if (!isValidScheme(_uri))
      {
        var msg = "Unsupported WebSocket URI scheme: " + _uri.Scheme;
        throw new ArgumentException(msg);
      }

      string scheme = _uri.Scheme;
      int    port   = _uri.Port;

      if (port <= 0)
      {
        if (scheme == "wss")
        {
          port = 443;
        }
        else
        {
          port = 80;
        }
      }

      _tcpListener = new TcpListener(IPAddress.Any, port);
      _forServices = new object();
      _services    = new Dictionary<string, WebSocketService>();
      _state       = WsServerState.READY;
    }

    #endregion

    #region Private Methods

    private void acceptClient(IAsyncResult ar)
    {
      TcpListener listener = (TcpListener)ar.AsyncState;

      if (listener.Server == null || !listener.Server.IsBound)
      {
        return;
      }

      try
      {
        TcpClient client = listener.EndAcceptTcpClient(ar);
        WebSocket socket = new WebSocket(_uri, client);
        T service = new T();
        service.Bind(this, socket);
        service.Start();
      }
      catch (ObjectDisposedException)
      {
        // TcpListener has been stopped.
        return;
      }
      catch (Exception ex)
      {
        error(ex.Message);
      }

      listener.BeginAcceptTcpClient(acceptClient, listener);
    }

    private void error(string message)
    {
      #if DEBUG
      var callerFrame = new StackFrame(1);
      var caller      = callerFrame.GetMethod();
      Console.WriteLine("WSSV: Error@{0}: {1}", caller.Name, message);
      #endif
      OnError.Emit(this, new ErrorEventArgs(message));
    }

    private bool isValidScheme(Uri uri)
    {
      string scheme = uri.Scheme;
      if (scheme == "ws" || scheme == "wss")
      {
        return true;
      }

      return false;
    }

    #endregion

    #region Public Methods

    public void AddService(string id, WebSocketService service)
    {
      lock (_forServices)
      {
        _services.Add(id, service);
      }
    }

    public Dictionary<string, bool> PingAround()
    {
      return PingAround(String.Empty);
    }

    public Dictionary<string, bool> PingAround(string data)
    {
      var result = new Dictionary<string, bool>();

      lock (_forServices)
      {
        foreach (WebSocketService service in _services.Values)
        {
          result.Add(service.ID, service.Ping(data));
        }
      }

      return result;
    }

    public void Publish<TData>(TData data)
    {
      WaitCallback broadcast = (state) =>
      {
        lock (_forServices)
        {
          SendTo(_services.Keys, data);
        }
      };
      ThreadPool.QueueUserWorkItem(broadcast);
    }

    public void RemoveService(string id)
    {
      lock (_forServices)
      {
        _services.Remove(id);
      }
    }

    public void SendTo<TData>(string id, TData data)
    {
      if (typeof(TData) != typeof(string) &&
          typeof(TData) != typeof(byte[]))
      {
        var msg = "Type of data must be string or byte[].";
        throw new ArgumentException(msg);
      }

      lock (_forServices)
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
      if (typeof(TData) != typeof(string) &&
          typeof(TData) != typeof(byte[]))
      {
        var msg = "Type of data must be string or byte[].";
        throw new ArgumentException(msg);
      }

      lock (_forServices)
      {
        foreach (string id in group)
        {
          SendTo(id, data);
        }
      }
    }

    public void Start()
    {
      _tcpListener.Start();
      _tcpListener.BeginAcceptTcpClient(acceptClient, _tcpListener);
      _state = WsServerState.START;
    }

    public void Stop()
    {
      _state = WsServerState.SHUTDOWN;

      _tcpListener.Stop();
      StopServices();

      _state = WsServerState.STOP;
    }

    public void StopServices()
    {
      StopServices(CloseStatusCode.NORMAL, String.Empty);
    }

    public void StopServices(CloseStatusCode code, string reason)
    {
      lock (_forServices)
      {
        foreach (WebSocketService service in _services.Values)
        {
          service.Stop(code, reason);
        }
      }
    }

    #endregion
  }
}
