#region License
/*
 * HttpServer.cs
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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using WebSocketSharp.Net;

namespace WebSocketSharp.Server
{
  /// <summary>
  /// Provides a simple HTTP server that allows to accept the WebSocket connection requests.
  /// </summary>
  /// <remarks>
  /// The HttpServer instance can provide the multi WebSocket services.
  /// </remarks>
  public class HttpServer
  {
    #region Private Fields

    private HttpListener       _listener;
    private bool               _listening;
    private Logger             _logger;
    private int                _port;
    private Thread             _receiveRequestThread;
    private string             _rootPath;
    private ServiceHostManager _svcHosts;
    private bool               _windows;

    #endregion

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpServer"/> class that listens for incoming requests
    /// on port 80.
    /// </summary>
    public HttpServer ()
      : this (80)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpServer"/> class that listens for incoming requests
    /// on the specified <paramref name="port"/>.
    /// </summary>
    /// <param name="port">
    /// An <see cref="int"/> that contains a port number. 
    /// </param>
    public HttpServer (int port)
    {
      _port = port;
      init ();
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets a value indicating whether the server has been started.
    /// </summary>
    /// <value>
    /// <c>true</c> if the server has been started; otherwise, <c>false</c>.
    /// </value>
    public bool IsListening {
      get {
        return _listening;
      }
    }

    /// <summary>
    /// Gets the logging functions.
    /// </summary>
    /// <remarks>
    /// The default logging level is the <see cref="LogLevel.ERROR"/>.
    /// If you want to change the current logging level, you set the <c>Log.Level</c> property
    /// to one of the <see cref="LogLevel"/> values which you want.
    /// </remarks>
    /// <value>
    /// A <see cref="Logger"/> that provides the logging functions.
    /// </value>
    public Logger Log {
      get {
        return _logger;
      }
    }

    /// <summary>
    /// Gets the port on which to listen for incoming requests.
    /// </summary>
    /// <value>
    /// An <see cref="int"/> that contains a port number.
    /// </value>
    public int Port {
      get {
        return _port;
      }
    }

    /// <summary>
    /// Gets or sets the document root path of server.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that contains the document root path of server.
    /// The default value is <c>./Public</c>.
    /// </value>
    public string RootPath {
      get {
        return _rootPath;
      }

      set {
        if (!_listening)
          _rootPath = value;
      }
    }

    /// <summary>
    /// Gets the collection of paths associated with the every WebSocket services that the server provides.
    /// </summary>
    /// <value>
    /// An IEnumerable&lt;string&gt; that contains the collection of paths.
    /// </value>
    public IEnumerable<string> ServicePaths {
      get {
        return _svcHosts.Paths;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the server cleans up the inactive WebSocket service
    /// instances periodically.
    /// </summary>
    /// <value>
    /// <c>true</c> if the server cleans up the inactive WebSocket service instances every 60 seconds;
    /// otherwise, <c>false</c>. The default value is <c>true</c>.
    /// </value>
    public bool Sweeping {
      get {
        return _svcHosts.Sweeping;
      }

      set {
        _svcHosts.Sweeping = value;
      }
    }

    #endregion

    #region Public Events

    /// <summary>
    /// Occurs when the server receives an HTTP CONNECT request.
    /// </summary>
    public event EventHandler<HttpRequestEventArgs> OnConnect;

    /// <summary>
    /// Occurs when the server receives an HTTP DELETE request.
    /// </summary>
    public event EventHandler<HttpRequestEventArgs> OnDelete;

    /// <summary>
    /// Occurs when the server gets an error.
    /// </summary>
    public event EventHandler<ErrorEventArgs> OnError;

    /// <summary>
    /// Occurs when the server receives an HTTP GET request.
    /// </summary>
    public event EventHandler<HttpRequestEventArgs> OnGet;

    /// <summary>
    /// Occurs when the server receives an HTTP HEAD request.
    /// </summary>
    public event EventHandler<HttpRequestEventArgs> OnHead;

    /// <summary>
    /// Occurs when the server receives an HTTP OPTIONS request.
    /// </summary>
    public event EventHandler<HttpRequestEventArgs> OnOptions;

    /// <summary>
    /// Occurs when the server receives an HTTP PATCH request.
    /// </summary>
    public event EventHandler<HttpRequestEventArgs> OnPatch;

    /// <summary>
    /// Occurs when the server receives an HTTP POST request.
    /// </summary>
    public event EventHandler<HttpRequestEventArgs> OnPost;

    /// <summary>
    /// Occurs when the server receives an HTTP PUT request.
    /// </summary>
    public event EventHandler<HttpRequestEventArgs> OnPut;

    /// <summary>
    /// Occurs when the server receives an HTTP TRACE request.
    /// </summary>
    public event EventHandler<HttpRequestEventArgs> OnTrace;

    #endregion

    #region Private Methods

    private void error (string message)
    {
      OnError.Emit (this, new ErrorEventArgs (message));
    }

    private void init ()
    {
      _listener = new HttpListener ();
      _listening = false;
      _logger = new Logger ();
      _rootPath = "./Public";
      _svcHosts = new ServiceHostManager ();

      _windows = false;
      var os = Environment.OSVersion;
      if (os.Platform != PlatformID.Unix && os.Platform != PlatformID.MacOSX)
        _windows = true;

      var prefix = String.Format ("http{0}://*:{1}/", _port == 443 ? "s" : "", _port);
      _listener.Prefixes.Add (prefix);
    }

    private void processHttpRequest (HttpListenerContext context)
    {
      var eventArgs = new HttpRequestEventArgs (context);
      var method = context.Request.HttpMethod;
      if (method == "GET" && OnGet != null)
      {
        OnGet (this, eventArgs);
        return;
      }

      if (method == "HEAD" && OnHead != null)
      {
        OnHead (this, eventArgs);
        return;
      }

      if (method == "POST" && OnPost != null)
      {
        OnPost (this, eventArgs);
        return;
      }

      if (method == "PUT" && OnPut != null)
      {
        OnPut (this, eventArgs);
        return;
      }

      if (method == "DELETE" && OnDelete != null)
      {
        OnDelete (this, eventArgs);
        return;
      }

      if (method == "OPTIONS" && OnOptions != null)
      {
        OnOptions (this, eventArgs);
        return;
      }

      if (method == "TRACE" && OnTrace != null)
      {
        OnTrace (this, eventArgs);
        return;
      }

      if (method == "CONNECT" && OnConnect != null)
      {
        OnConnect (this, eventArgs);
        return;
      }

      if (method == "PATCH" && OnPatch != null)
      {
        OnPatch (this, eventArgs);
        return;
      }

      context.Response.StatusCode = (int) HttpStatusCode.NotImplemented;
    }

    private bool processWebSocketRequest (HttpListenerContext context)
    {
      var wsContext = context.AcceptWebSocket ();
      var path = wsContext.Path.UrlDecode ();

      IServiceHost svcHost;
      if (!_svcHosts.TryGetServiceHost (path, out svcHost))
      {
        context.Response.StatusCode = (int) HttpStatusCode.NotImplemented;
        return false;
      }

      wsContext.WebSocket.Log = _logger;
      svcHost.BindWebSocket (wsContext);

      return true;
    }

    private void processRequestAsync (HttpListenerContext context)
    {
      WaitCallback callback = state =>
      {
        try {
          if (context.Request.IsUpgradeTo ("websocket"))
          {
            if (processWebSocketRequest (context))
              return;
          }
          else
          {
            processHttpRequest (context);
          }

          context.Response.Close ();
        }
        catch (Exception ex) {
          _logger.Fatal (ex.Message);
          error ("An exception has occured.");
        }
      };

      ThreadPool.QueueUserWorkItem (callback);
    }

    private void receiveRequest ()
    {
      while (true)
      {
        try {
          processRequestAsync (_listener.GetContext ());
        }
        catch (HttpListenerException) {
          _logger.Info ("HttpListener has been stopped.");
          break;
        }
        catch (Exception ex) {
          _logger.Fatal (ex.Message);
          error ("An exception has occured.");

          break;
        }
      }
    }

    private void startReceiveRequestThread ()
    {
      _receiveRequestThread = new Thread (new ThreadStart (receiveRequest)); 
      _receiveRequestThread.IsBackground = true;
      _receiveRequestThread.Start ();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Adds the specified typed WebSocket service.
    /// </summary>
    /// <param name="absPath">
    /// A <see cref="string"/> that contains an absolute path associated with the WebSocket service.
    /// </param>
    /// <typeparam name="T">
    /// The type of the WebSocket service. The T must inherit the <see cref="WebSocketService"/> class.
    /// </typeparam>
    public void AddWebSocketService<T> (string absPath)
      where T : WebSocketService, new ()
    {
      string msg;
      if (!absPath.IsValidAbsolutePath (out msg))
      {
        _logger.Error (msg);
        error (msg);

        return;
      }

      var svcHost = new WebSocketServiceHost<T> (_logger);
      svcHost.Uri = absPath.ToUri ();
      if (!Sweeping)
        svcHost.Sweeping = false;

      _svcHosts.Add (absPath, svcHost);
    }

    /// <summary>
    /// Gets the contents of the file with the specified <paramref name="path"/>.
    /// </summary>
    /// <returns>
    /// An array of <see cref="byte"/> that contains the contents of the file if exists;
    /// otherwise, <see langword="null"/>.
    /// </returns>
    /// <param name="path">
    /// A <see cref="string"/> that contains a virtual path to the file to get.
    /// </param>
    public byte[] GetFile (string path)
    {
      var filePath = _rootPath + path;
      if (_windows)
        filePath = filePath.Replace ("/", "\\");

      return File.Exists (filePath)
             ? File.ReadAllBytes (filePath)
             : null;
    }

    /// <summary>
    /// Starts to receive the HTTP requests.
    /// </summary>
    public void Start ()
    {
      if (_listening)
        return;

      _listener.Start ();
      startReceiveRequestThread ();
      _listening = true;
    }

    /// <summary>
    /// Stops receiving the HTTP requests.
    /// </summary>
    public void Stop ()
    {
      if (!_listening)
        return;

      _listener.Close ();
      _receiveRequestThread.Join (5 * 1000);
      _svcHosts.Stop ();
      _listening = false;
    }

    #endregion
  }
}
