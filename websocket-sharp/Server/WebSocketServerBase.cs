#region MIT License
/**
 * WebSocketServerBase.cs
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
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace WebSocketSharp.Server {

  public abstract class WebSocketServerBase
  {
    #region Fields

    private Thread      _acceptClientThread;
    private IPAddress   _address;
    private bool        _isSelfHost;
    private int         _port;
    private TcpListener _tcpListener;

    #endregion

    #region Constructors

    protected WebSocketServerBase()
    {
      _isSelfHost = false;
    }

    protected WebSocketServerBase(string url)
    {
      string msg;
      if (!isValidUri(url, out msg))
        throw new ArgumentException(msg, "url");

      init();
    }

    protected WebSocketServerBase(IPAddress address, int port)
    {
      _port    = port <= 0 ? 80 : port;
      _address = address;
      init();
    }

    #endregion

    #region Property

    public IPAddress Address {
      get { return _address; }
    }

    public bool IsSelfHost {
      get { return _isSelfHost; }
    }
    
    public int Port {
      get { return _port; }
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
        try
        {
          var client = _tcpListener.AcceptTcpClient();
          acceptSocket(client);
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

    private void acceptSocket(TcpClient client)
    {
      WaitCallback acceptSocketCb = (state) =>
      {
        try
        {
          bindSocket(client);
        }
        catch (Exception ex)
        {
          error(ex.Message);
        }
      };
      ThreadPool.QueueUserWorkItem(acceptSocketCb);
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

    private void init()
    {
      _tcpListener = new TcpListener(_address, _port);
      _isSelfHost  = true;
    }

    private bool isValidUri(string url, out string message)
    {
      var uri = url.ToUri();
      if (!uri.IsAbsoluteUri)
      {
        message = "Not absolute uri: " + url;
        return false;
      }

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

    #endregion

    #region Protected Method

    protected abstract void bindSocket(TcpClient client);

    #endregion

    #region Public Methods

    public virtual void Start()
    {
      if (!_isSelfHost)
        return;

      _tcpListener.Start();
      startAcceptClientThread();
    }

    public virtual void Stop()
    {
      if (!_isSelfHost)
        return;

      _tcpListener.Stop();
      _acceptClientThread.Join(5 * 1000);
    }

    #endregion
  }
}
