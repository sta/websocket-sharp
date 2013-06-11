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
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Threading;
using WebSocketSharp.Net;

namespace WebSocketSharp.Server {

  /// <summary>
  /// Provides a simple HTTP server that allows to accept the WebSocket connection requests.
  /// </summary>
  /// <remarks>
  /// <para>
  /// The HttpServer class provides the multi WebSocket service.
  /// </para>
  /// <para>
  /// <para>
  /// The HttpServer class needs the application configuration file to configure the server root path.
  /// </para>
  /// <code lang="xml">
  /// &lt;?xml version="1.0" encoding="utf-8"?&gt;
  /// &lt;configuration&gt;
  ///   &lt;appSettings&gt;
  ///     &lt;add key="RootPath" value="./Public" /&gt;
  ///   &lt;/appSettings&gt;
  /// &lt;/configuration&gt;
  /// </code>
  /// </para>
  /// </remarks>
  public class HttpServer {

    #region Private Fields

    private bool               _isWindows;
    private HttpListener       _listener;
    private int                _port;
    private Thread             _receiveRequestThread;
    private string             _rootPath;
    private ServiceHostManager _svcHosts;

    #endregion

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpServer"/> class that listens for incoming requests
    /// on port 80.
    /// </summary>
    public HttpServer()
      : this(80)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpServer"/> class that listens for incoming requests
    /// on the specified <paramref name="port"/>.
    /// </summary>
    /// <param name="port">
    /// An <see cref="int"/> that contains a port number. 
    /// </param>
    public HttpServer(int port)
    {
      _port = port;
      init();
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the port on which to listen for incoming requests.
    /// </summary>
    /// <value>
    /// An <see cref="int"/> that contains a port number.
    /// </value>
    public int Port {
      get { return _port; }
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

    private void configureFromConfigFile()
    {
      _rootPath = ConfigurationManager.AppSettings["RootPath"];
    }

    private void init()
    {
      _isWindows = false;
      _listener  = new HttpListener();
      _svcHosts  = new ServiceHostManager();

      var os = Environment.OSVersion;
      if (os.Platform != PlatformID.Unix && os.Platform != PlatformID.MacOSX)
        _isWindows = true;

      var prefix = String.Format(
        "http{0}://*:{1}/", _port == 443 ? "s" : String.Empty, _port);
      _listener.Prefixes.Add(prefix);

      configureFromConfigFile();
    }

    private void onError(string message)
    {
      #if DEBUG
      var callerFrame = new StackFrame(1);
      var caller      = callerFrame.GetMethod();
      Console.WriteLine("HTTPSV: Error@{0}: {1}", caller.Name, message);
      #endif
      OnError.Emit(this, new ErrorEventArgs(message));
    }

    private void onRequest(HttpListenerContext context)
    {
      var req = context.Request;
      var res = context.Response;
      var eventArgs = new HttpRequestEventArgs(context);

      if (req.HttpMethod == "GET" && !OnGet.IsNull())
      {
        OnGet(this, eventArgs);
        return;
      }

      if (req.HttpMethod == "HEAD" && !OnHead.IsNull())
      {
        OnHead(this, eventArgs);
        return;
      }

      if (req.HttpMethod == "POST" && !OnPost.IsNull())
      {
        OnPost(this, eventArgs);
        return;
      }

      if (req.HttpMethod == "PUT" && !OnPut.IsNull())
      {
        OnPut(this, eventArgs);
        return;
      }

      if (req.HttpMethod == "DELETE" && !OnDelete.IsNull())
      {
        OnDelete(this, eventArgs);
        return;
      }

      if (req.HttpMethod == "OPTIONS" && !OnOptions.IsNull())
      {
        OnOptions(this, eventArgs);
        return;
      }

      if (req.HttpMethod == "TRACE" && !OnTrace.IsNull())
      {
        OnTrace(this, eventArgs);
        return;
      }

      if (req.HttpMethod == "CONNECT" && !OnConnect.IsNull())
      {
        OnConnect(this, eventArgs);
        return;
      }

      if (req.HttpMethod == "PATCH" && !OnPatch.IsNull())
      {
        OnPatch(this, eventArgs);
        return;
      }

      res.StatusCode = (int)HttpStatusCode.NotImplemented;
    }

    private void processRequestAsync(HttpListenerContext context)
    {
      WaitCallback callback = (state) =>
      {
        var req = context.Request;
        var res = context.Response;

        try
        {
          if (req.IsUpgradeTo("websocket"))
          {
            if (upgradeToWebSocket(context))
              return;
          }
          else
          {
            onRequest(context);
          }

          res.Close();
        }
        catch (Exception ex)
        {
          onError(ex.Message);
        }
      };

      ThreadPool.QueueUserWorkItem(callback);
    }

    private void receiveRequest()
    {
      while (true)
      {
        try
        {
          var context = _listener.GetContext();
          processRequestAsync(context);
        }
        catch (HttpListenerException)
        {
          // HttpListener has been closed.
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

    private bool upgradeToWebSocket(HttpListenerContext context)
    {
      var res       = context.Response;
      var wsContext = context.AcceptWebSocket();
      var path      = wsContext.Path.UrlDecode();

      IServiceHost svcHost;
      if (!_svcHosts.TryGetServiceHost(path, out svcHost))
      {
        res.StatusCode = (int)HttpStatusCode.NotImplemented;
        return false;
      }

      svcHost.BindWebSocket(wsContext);
      return true;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Adds the specified type WebSocket service.
    /// </summary>
    /// <param name="absPath">
    /// A <see cref="string"/> that contains an absolute path associated with the WebSocket service.
    /// </param>
    /// <typeparam name="T">
    /// The type of the WebSocket service. The T must inherit the <see cref="WebSocketService"/> class.
    /// </typeparam>
    public void AddWebSocketService<T>(string absPath)
      where T : WebSocketService, new()
    {
      string msg;
      if (!absPath.IsValidAbsolutePath(out msg))
      {
        onError(msg);
        return;
      }

      var svcHost = new WebSocketServiceHost<T>();
      svcHost.Uri = absPath.ToUri();
      if (!Sweeping)
        svcHost.Sweeping = false;

      _svcHosts.Add(absPath, svcHost);
    }

    /// <summary>
    /// Gets the contents of the specified file.
    /// </summary>
    /// <returns>
    /// An array of <see cref="byte"/> that contains the contents of the file.
    /// </returns>
    /// <param name="path">
    /// A <see cref="string"/> that contains a virtual path to the file to get.
    /// </param>
    public byte[] GetFile(string path)
    {
      var filePath = _rootPath + path;
      if (_isWindows)
        filePath = filePath.Replace("/", "\\");

      return File.Exists(filePath)
             ? File.ReadAllBytes(filePath)
             : null;
    }

    /// <summary>
    /// Starts the <see cref="HttpServer"/>.
    /// </summary>
    public void Start()
    {
      _listener.Start();
      startReceiveRequestThread();
    }

    /// <summary>
    /// Shuts down the <see cref="HttpServer"/>.
    /// </summary>
    public void Stop()
    {
      _listener.Close();
      _receiveRequestThread.Join(5 * 1000);
      _svcHosts.Stop();
    }

    #endregion
  }
}
