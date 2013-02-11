#region MIT License
/*
 * WebSocketServerBase.cs
 *
 * The MIT License
 *
 * Copyright (c) 2012-2013 sta.blockhead
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
using WebSocketSharp.Net.WebSockets;

namespace WebSocketSharp.Server {

  /// <summary>
  /// Provides the basic functions of the server that receives the WebSocket connection requests.
  /// </summary>
  /// <remarks>
  /// The WebSocketServerBase class is an abstract class.
  /// </remarks>
  public abstract class WebSocketServerBase {

    #region Fields

    private Thread      _receiveRequestThread;
    private IPAddress   _address;
    private bool        _isSecure;
    private bool        _isSelfHost;
    private int         _port;
    private TcpListener _tcpListener;
    private Uri         _uri;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketServerBase"/> class.
    /// </summary>
    protected WebSocketServerBase()
    {
      _isSelfHost = false;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketServerBase"/> class that listens for incoming connection attempts
    /// on the specified WebSocket URL.
    /// </summary>
    /// <param name="url">
    /// A <see cref="string"/> that contains a WebSocket URL.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="url"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="url"/> is invalid.
    /// </exception>
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

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketServerBase"/> class that listens for incoming connection attempts
    /// on the specified <paramref name="address"/>, <paramref name="port"/>, <paramref name="absPath"/> and <paramref name="secure"/>.
    /// </summary>
    /// <param name="address">
    /// A <see cref="IPAddress"/> that contains a local IP address.
    /// </param>
    /// <param name="port">
    /// An <see cref="int"/> that contains a port number. 
    /// </param>
    /// <param name="absPath">
    /// A <see cref="string"/> that contains an absolute path.
    /// </param>
    /// <param name="secure">
    /// A <see cref="bool"/> that indicates providing a secure connection or not. (<c>true</c> indicates providing a secure connection.)
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Either <paramref name="address"/> or <paramref name="absPath"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <para>
    /// <paramref name="absPath"/> is invalid.
    /// </para>
    /// <para>
    /// -or-
    /// </para>
    /// <para>
    /// Pair of <paramref name="port"/> and <paramref name="secure"/> is invalid.
    /// </para>
    /// </exception>
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

    /// <summary>
    /// Gets or sets the WebSocket URL on which to listen for incoming connection attempts.
    /// </summary>
    /// <value>
    /// A <see cref="Uri"/> that contains a WebSocket URL.
    /// </value>
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

    /// <summary>
    /// Gets the local IP address on which to listen for incoming connection attempts.
    /// </summary>
    /// <value>
    /// A <see cref="IPAddress"/> that contains a local IP address.
    /// </value>
    public IPAddress Address {
      get {
        return _address;
      }
    }

    /// <summary>
    /// Gets a value indicating whether the server provides secure connection.
    /// </summary>
    /// <value>
    /// <c>true</c> if the server provides secure connection; otherwise, <c>false</c>.
    /// </value>
    public bool IsSecure {
      get {
        return _isSecure;
      }
    }

    /// <summary>
    /// Gets a value indicating whether the server is self host.
    /// </summary>
    /// <value>
    /// <c>true</c> if the server is self host; otherwise, <c>false</c>.
    /// </value>
    public bool IsSelfHost {
      get {
        return _isSelfHost;
      }
    }

    /// <summary>
    /// Gets the port on which to listen for incoming connection attempts.
    /// </summary>
    /// <value>
    /// An <see cref="int"/> that contains a port number.
    /// </value>
    public int Port {
      get {
        return _port;
      }
    }

    #endregion

    #region Event

    /// <summary>
    /// Occurs when the server gets an error.
    /// </summary>
    public event EventHandler<ErrorEventArgs> OnError;

    #endregion

    #region Private Methods

    private void acceptWebSocketAsync(TcpListenerWebSocketContext context)
    {
      WaitCallback callback = (state) =>
      {
        try
        {
          AcceptWebSocket(context);
        }
        catch (Exception ex)
        {
          onError(ex.Message);
        }
      };

      ThreadPool.QueueUserWorkItem(callback);
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

    private void receiveRequest()
    {
      while (true)
      {
        try
        {
          var context = _tcpListener.AcceptWebSocket(_isSecure);
          acceptWebSocketAsync(context);
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

    private void startReceiveRequestThread()
    {
      _receiveRequestThread = new Thread(new ThreadStart(receiveRequest)); 
      _receiveRequestThread.IsBackground = true;
      _receiveRequestThread.Start();
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

    /// <summary>
    /// Accepts a WebSocket connection.
    /// </summary>
    /// <param name="context">
    /// A <see cref="TcpListenerWebSocketContext"/> that contains the WebSocket connection request objects.
    /// </param>
    protected abstract void AcceptWebSocket(TcpListenerWebSocketContext context);

    /// <summary>
    /// Occurs the <see cref="WebSocketServerBase.OnError"/> event with the specified <see cref="string"/>.
    /// </summary>
    /// <param name="message">
    /// A <see cref="string"/> that contains an error message.
    /// </param>
    protected virtual void Error(string message)
    {
      onError(message);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Starts to receive the WebSocket connection requests.
    /// </summary>
    public virtual void Start()
    {
      if (!_isSelfHost)
        return;

      _tcpListener.Start();
      startReceiveRequestThread();
    }

    /// <summary>
    /// Stops receiving the WebSocket connection requests.
    /// </summary>
    public virtual void Stop()
    {
      if (!_isSelfHost)
        return;

      _tcpListener.Stop();
      _receiveRequestThread.Join(5 * 1000);
    }

    #endregion
  }
}
