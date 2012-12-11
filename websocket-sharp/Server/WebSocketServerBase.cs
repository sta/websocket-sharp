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

  public abstract class WebSocketServerBase {

    #region Fields

    private Thread      _acceptClientThread;
    private IPAddress   _address;
    private bool        _isSecure;
    private bool        _isSelfHost;
    private int         _port;
    private TcpListener _tcpListener;
    private Uri         _uri;

    #endregion

    #region Constructors

    protected WebSocketServerBase()
    {
      _isSelfHost = false;
    }

    protected WebSocketServerBase(string url)
    {
      if (url.IsNull())
        throw new ArgumentNullException("url");

      Uri    uri;
      string msg;
      if (!tryCreateUri(url, out uri, out msg))
        throw new ArgumentException(msg, "url");

      init(uri);
    }

    protected WebSocketServerBase(IPAddress address, int port, string absPath, bool secure)
    {
      if (address.IsNull())
        throw new ArgumentNullException("address");

      if (absPath.IsNull())
        throw new ArgumentNullException("absPath");

      string msg;
      if (!absPath.IsValidAbsolutePath(out msg))
        throw new ArgumentException(msg, "absPath");

      if ((port == 80  && secure) ||
          (port == 443 && !secure))
      {
        msg = String.Format(
          "Invalid pair of 'port' and 'secure': {0}, {1}", port, secure);
        throw new ArgumentException(msg);
      }

      _address  = address;
      _port     = port > 0
                ? port
                : secure ? 443 : 80;
      _uri      = absPath.ToUri();
      _isSecure = secure;

      init();
    }

    #endregion

    #region Protected Property

    protected Uri BaseUri
    {
      get {
        return _uri;
      }

      set {
        _uri = value;
      }
    }

    #endregion

    #region Public Properties

    public IPAddress Address {
      get {
        return _address;
      }
    }

    public bool IsSecure {
      get {
        return _isSecure;
      }
    }

    public bool IsSelfHost {
      get {
        return _isSelfHost;
      }
    }
    
    public int Port {
      get {
        return _port;
      }
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
          acceptSocketAsync(client);
        }
        catch (SocketException)
        {
          // TcpListener has been stopped.
          break;
        }
        catch (Exception ex)
        {
          onError(ex.Message);
          break;
        }
      }
    }

    private void acceptSocketAsync(TcpClient client)
    {
      WaitCallback acceptSocketCb = (state) =>
      {
        try
        {
          AcceptWebSocket(client);
        }
        catch (Exception ex)
        {
          onError(ex.Message);
        }
      };

      ThreadPool.QueueUserWorkItem(acceptSocketCb);
    }

    private void init()
    {
      _tcpListener = new TcpListener(_address, _port);
      _isSelfHost  = true;
    }

    private void init(Uri uri)
    {
      var scheme = uri.Scheme;
      var host   = uri.DnsSafeHost;
      var port   = uri.Port;
      var addrs  = Dns.GetHostAddresses(host);

      _uri      = uri;
      _address  = addrs[0];
      _isSecure = scheme == "wss" ? true : false;
      _port     = port > 0
                  ? port
                  : _isSecure ? 443 : 80;

      init();
    }

    private void onError(string message)
    {
      #if DEBUG
      var callerFrame = new StackFrame(1);
      var caller      = callerFrame.GetMethod();
      Console.WriteLine("WSSV: Error@{0}: {1}", caller.Name, message);
      #endif
      OnError.Emit(this, new ErrorEventArgs(message));
    }

    private void startAcceptClientThread()
    {
      _acceptClientThread = new Thread(new ThreadStart(acceptClient)); 
      _acceptClientThread.IsBackground = true;
      _acceptClientThread.Start();
    }

    private bool tryCreateUri(string uriString, out Uri result, out string message)
    {
      if (!uriString.TryCreateWebSocketUri(out result, out message))
        return false;

      if (!result.Query.IsNullOrEmpty())
      {
        result  = null;
        message = "Must not contain the query component: " + uriString;
        return false;
      }

      return true;
    }

    #endregion

    #region Protected Methods

    protected abstract void AcceptWebSocket(TcpClient client);

    protected virtual void Error(string message)
    {
      onError(message);
    }

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
