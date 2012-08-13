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

    private SynchronizedCollection<WebSocketService> _services;
    private WsServerState                            _state;
    private TcpListener                              _tcpListener;
    private Uri                                      _uri;

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
        throw new ArgumentException("Unsupported WebSocket URI scheme: " + _uri.Scheme);
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
      _services    = new SynchronizedCollection<WebSocketService>();
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
        WebSocket socket = new WebSocket(_uri.ToString(), client);
        T service = new T();
        service.Bind(this, socket);
        service.Open();
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

    public void AddService(WebSocketService service)
    {
      _services.Add(service);
    }

    public void CloseServices()
    {
      CloseServices(CloseStatusCode.NORMAL, String.Empty);
    }

    public void CloseServices(CloseStatusCode code, string reason)
    {
      lock (_services.SyncRoot)
      {
        foreach (WebSocketService service in _services)
        {
          service.Close(code, reason);
        }
      }
    }

    public void Ping()
    {
      Ping(String.Empty);
    }

    public void Ping(string data)
    {
      WaitCallback broadcast = (state) =>
      {
        lock (_services.SyncRoot)
        {
          foreach (WebSocketService service in _services)
          {
            service.Ping(data);
          }
        }
      };
      ThreadPool.QueueUserWorkItem(broadcast);
    }

    public void Publish(byte[] data)
    {
      WaitCallback broadcast = (state) =>
      {
        lock (_services.SyncRoot)
        {
          foreach (WebSocketService service in _services)
          {
            service.Send(data);
          }
        }
      };
      ThreadPool.QueueUserWorkItem(broadcast);
    }

    public void Publish(string data)
    {
      WaitCallback broadcast = (state) =>
      {
        lock (_services.SyncRoot)
        {
          foreach (WebSocketService service in _services)
          {
            service.Send(data);
          }
        }
      };
      ThreadPool.QueueUserWorkItem(broadcast);
    }

    public void RemoveService(WebSocketService service)
    {
      _services.Remove(service);
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
      CloseServices();

      _state = WsServerState.STOP;
    }

    #endregion
  }
}
