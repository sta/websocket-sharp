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
using System.Threading;
using WebSocketSharp.Frame;

namespace WebSocketSharp.Server {

  public class WebSocketServer<T> : IWebSocketServer
    where T : WebSocketService, new()
  {
    #region Fields

    private Thread                               _acceptClientThread;
    private IPAddress                            _address;
    private bool                                 _isSelfHost;
    private int                                  _port;
    private Dictionary<string, WebSocketService> _services;
    private TcpListener                          _tcpListener;
    private Uri                                  _uri;

    #endregion

    #region Internal Constructor

    internal WebSocketServer()
    {
      _services   = new Dictionary<string, WebSocketService>();
      _isSelfHost = false;
    }

    #endregion

    #region Public Constructors

    public WebSocketServer(string url)
      : this()
    {
      var uri = new Uri(url);

      string msg;
      if (!isValidUri(uri, out msg))
        throw new ArgumentException(msg, "url");

      _tcpListener = new TcpListener(_address, _port);
      _isSelfHost  = true;
    }

    public WebSocketServer(int port)
      : this(port, "/")
    {
    }

    public WebSocketServer(int port, string path)
      : this()
    {
      var uri = path.ToUri();
      if (uri.IsAbsoluteUri)
      {
        var msg = "Not absolute path: " + path;
        throw new ArgumentException(msg, "path");
      }

      _uri     = uri;
      _address = IPAddress.Any;
      _port    = port <= 0 ? 80 : port;

      _tcpListener = new TcpListener(_address, _port);
      _isSelfHost  = true;
    }

    #endregion

    #region Properties

    public IPAddress Address
    {
      get { return _address; }
    }

    public bool IsSelfHost {
      get { return _isSelfHost; }
    }

    public int Port
    {
      get { return _port; }
    }

    public Uri Uri
    {
      get { return _uri; }
    }

    #endregion

    #region Events

    public event EventHandler<ErrorEventArgs> OnError;

    #endregion

    #region Private Methods

    private void acceptClient()
    {
      while (true)
      {
        try {
          var client = _tcpListener.AcceptTcpClient();
          startService(client);
        }
        catch (SocketException)
        {
          // TcpListener has been stopped.
          break;
        }
        catch (Exception ex)
        {
          error(ex.Message);
          break;
        }
      }
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

    private bool isValidUri(Uri uri, out string message)
    {
      var scheme = uri.Scheme;
      var port   = uri.Port;
      var host   = uri.DnsSafeHost;
      var ips    = Dns.GetHostAddresses(host);

      if (scheme != "ws" && scheme != "wss")
      {
        message = "Unsupported WebSocket URI scheme: " + scheme;
        return false;
      }

      if ((scheme == "wss" && port != 443) ||
          (scheme != "wss" && port == 443))
      {
        message = String.Format(
          "Invalid pair of WebSocket URI scheme and port: {0}, {1}", scheme, port);
        return false;
      }

      if (ips.Length == 0)
      {
        message = "Invalid WebSocket URI host: " + host;
        return false;
      }

      if (port <= 0)
        port = scheme == "ws" ? 80 : 443;

      _uri     = uri;
      _address = ips[0];
      _port    = port;

      message = String.Empty;
      return true;
    }

    private void startAcceptClientThread()
    {
      _acceptClientThread = new Thread(new ThreadStart(acceptClient)); 
      _acceptClientThread.IsBackground = true;
      _acceptClientThread.Start();
    }

    private void startService(TcpClient client)
    {
      WaitCallback startServiceCb = (state) =>
      {
        try {
          var socket = new WebSocket(_uri, client);
          BindWebSocket(socket);
        }
        catch (Exception ex)
        {
          error(ex.Message);
        }
      };
      ThreadPool.QueueUserWorkItem(startServiceCb);
    }

    #endregion

    #region Public Methods

    public void BindWebSocket(WebSocket socket)
    {
      T service = new T();
      service.Bind(socket, _services);
      service.Start();
    }

    public void Start()
    {
      if (!_isSelfHost)
        return;

      _tcpListener.Start();
      startAcceptClientThread();
    }

    public void Stop()
    {
      if (_isSelfHost)
      {
        _tcpListener.Stop();
        _acceptClientThread.Join(5 * 1000);
      }

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
