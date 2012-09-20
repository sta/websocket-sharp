#region MIT License
/**
 * HttpServer.cs
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
using System.Configuration;
using System.IO;
using System.Threading;
using WebSocketSharp.Net;

namespace WebSocketSharp.Server {

  public class HttpServer {

    #region Fields

    private Thread                               _acceptRequestThread;
    private bool                                 _isWindows;
    private HttpListener                         _listener;
    private int                                  _port;
    private string                               _rootPath;
    private Dictionary<string, IWebSocketServer> _wsServers;

    #endregion

    #region Constructors

    public HttpServer()
      : this(80)
    {
    }

    public HttpServer(int port)
    {
      _port = port;
      init();
    }

    #endregion

    #region Property

    public int Port {
      get { return _port; }
    }

    #endregion

    #region Events

    public event EventHandler<ResponseEventArgs> OnConnect;
    public event EventHandler<ResponseEventArgs> OnDelete;
    public event EventHandler<ErrorEventArgs>    OnError;
    public event EventHandler<ResponseEventArgs> OnGet;
    public event EventHandler<ResponseEventArgs> OnHead;
    public event EventHandler<ResponseEventArgs> OnOptions;
    public event EventHandler<ResponseEventArgs> OnPatch;
    public event EventHandler<ResponseEventArgs> OnPost;
    public event EventHandler<ResponseEventArgs> OnPut;
    public event EventHandler<ResponseEventArgs> OnTrace;

    #endregion

    #region Private Methods

    private void acceptRequest()
    {
      while (true)
      {
        try
        {
          var context = _listener.GetContext();
          respond(context);
        }
        catch (HttpListenerException)
        {
          // HttpListener has been closed.
          break;
        }
        catch (Exception ex)
        {
          OnError.Emit(this, new ErrorEventArgs(ex.Message));
          break;
        }
      }
    }

    private void configureFromConfigFile()
    {
      _rootPath = ConfigurationManager.AppSettings["RootPath"];
    }

    private void init()
    {
      _isWindows = false;
      _listener  = new HttpListener();
      _wsServers = new Dictionary<string, IWebSocketServer>();

      var os = Environment.OSVersion;
      if (os.Platform != PlatformID.Unix && os.Platform != PlatformID.MacOSX)
        _isWindows = true;

      var prefix = String.Format(
        "http{0}://*:{1}/", _port == 443 ? "s" : String.Empty, _port);
      _listener.Prefixes.Add(prefix);

      configureFromConfigFile();
    }

    private bool isUpgrade(HttpListenerRequest request, string value)
    {
      if (!request.Headers.Exists("Upgrade", value))
        return false;

      if (!request.Headers.Exists("Connection", "Upgrade"))
        return false;

      return true;
    }

    private void respond(HttpListenerContext context)
    {
      WaitCallback respondCb = (state) =>
      {
        var req = context.Request;
        var res = context.Response;

        try
        {
          if (isUpgrade(req, "websocket"))
          {
            if (upgradeToWebSocket(context))
              return;
          }
          else
          {
            respondToClient(context);
          }
        }
        catch (Exception ex)
        {
          OnError.Emit(this, new ErrorEventArgs(ex.Message));
        }

        res.Close();
      };
      ThreadPool.QueueUserWorkItem(respondCb);
    }

    private void respondToClient(HttpListenerContext context)
    {
      var req = context.Request;
      var res = context.Response;
      var eventArgs = new ResponseEventArgs(context);

      if (req.HttpMethod == "GET" && OnGet != null)
      {
        OnGet(this, eventArgs);
        return;
      }

      if (req.HttpMethod == "HEAD" && OnHead != null)
      {
        OnHead(this, eventArgs);
        return;
      }

      if (req.HttpMethod == "POST" && OnPost != null)
      {
        OnPost(this, eventArgs);
        return;
      }

      if (req.HttpMethod == "PUT" && OnPut != null)
      {
        OnPut(this, eventArgs);
        return;
      }

      if (req.HttpMethod == "DELETE" && OnDelete != null)
      {
        OnDelete(this, eventArgs);
        return;
      }

      if (req.HttpMethod == "OPTIONS" && OnOptions != null)
      {
        OnOptions(this, eventArgs);
        return;
      }

      if (req.HttpMethod == "TRACE" && OnTrace != null)
      {
        OnTrace(this, eventArgs);
        return;
      }

      if (req.HttpMethod == "CONNECT" && OnConnect != null)
      {
        OnConnect(this, eventArgs);
        return;
      }

      if (req.HttpMethod == "PATCH" && OnPatch != null)
      {
        OnPatch(this, eventArgs);
        return;
      }

      res.StatusCode = (int)HttpStatusCode.NotImplemented;
    }

    private void startAcceptRequestThread()
    {
      _acceptRequestThread = new Thread(new ThreadStart(acceptRequest)); 
      _acceptRequestThread.IsBackground = true;
      _acceptRequestThread.Start();
    }

    private bool upgradeToWebSocket(HttpListenerContext context)
    {
      var req = context.Request;
      var res = context.Response;

      if (!req.IsWebSocketRequest)
      {
        res.StatusCode = (int)HttpStatusCode.BadRequest;
        return false;
      }

      var path = req.RawUrl;
      if (!_wsServers.ContainsKey(path))
      {
        res.StatusCode = (int)HttpStatusCode.NotImplemented;
        return false;
      }

      var wsContext = context.AcceptWebSocket(path);
      var socket    = wsContext.WebSocket;
      var wsServer  = _wsServers[path];
      wsServer.BindWebSocket(socket);

      return true;
    }

    #endregion

    #region Public Methods

    public void AddService<T>(string path)
      where T : WebSocketService, new()
    {
      var server = new WebSocketServer<T>();
      _wsServers.Add(path, server);
    }

    public byte[] GetFile(string path)
    {
      var filePath = _rootPath + path;
      if (_isWindows)
        filePath = filePath.Replace("/", "\\");

      if (File.Exists(filePath))
        return File.ReadAllBytes(filePath);

      return null;
    }

    public void Start()
    {
      _listener.Start();
      startAcceptRequestThread();
    }

    public void Stop()
    {
      _listener.Close();
      _acceptRequestThread.Join(5 * 1000);
      foreach (var server in _wsServers.Values)
        server.Stop();
    }

    #endregion
  }
}
