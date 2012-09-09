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
using System.Configuration;
using System.IO;
using System.Threading;
using WebSocketSharp.Net;

namespace WebSocketSharp.Server {

  public class HttpServer<T>
    where T : WebSocketService, new()
  {
    #region Fields

    private Thread             _acceptRequestThread;
    private HttpListener       _listener;
    private int                _port;
    private string             _rootPath;
    private Uri                _wsPath;
    private WebSocketServer<T> _wsServer;

    #endregion

    #region Constructors

    public HttpServer()
      : this(80)
    {
    }

    public HttpServer(int port)
      : this(port, "/")
    {
    }

    public HttpServer(int port, string wsPath)
    {
      _listener  = new HttpListener();
      _port      = port;
      var prefix = String.Format(
        "http{0}://*:{1}/", _port == 443 ? "s" : String.Empty, _port);
      _listener.Prefixes.Add(prefix);
      _rootPath  = ConfigurationManager.AppSettings["RootPath"];
      _wsPath    = wsPath.ToUri();
      _wsServer  = new WebSocketServer<T>();
    }

    #endregion

    #region Property

    public int Port {
      get { return _port; }
    }

    #endregion

    #region Events

    public event EventHandler<ErrorEventArgs>    OnError;
    public event EventHandler<ResponseEventArgs> OnResponse;

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

    private void respond(HttpListenerContext context)
    {
      WaitCallback respondCb = (state) =>
      {
        try
        {
          var req = context.Request;
          var res = context.Response;

          if (req.IsWebSocketRequest)
          {
            upgradeToWebSocket(context);
          }
          else
          {
            OnResponse.Emit(this, new ResponseEventArgs(context));
            res.Close();
          }
        }
        catch (Exception ex)
        {
          OnError.Emit(this, new ErrorEventArgs(ex.Message));
        }
      };
      ThreadPool.QueueUserWorkItem(respondCb);
    }

    private void startAcceptRequestThread()
    {
      _acceptRequestThread = new Thread(new ThreadStart(acceptRequest)); 
      _acceptRequestThread.IsBackground = true;
      _acceptRequestThread.Start();
    }

    private void upgradeToWebSocket(HttpListenerContext context)
    {
      var wsContext = context.AcceptWebSocket();
      var socket    = wsContext.WebSocket;
      if (_wsPath.ToString() != "/")
        socket.Url = _wsPath;
      _wsServer.BindWebSocket(socket);
    }

    #endregion

    #region Public Methods

    public byte[] GetFile(string path)
    {
      var filePath = _rootPath + path;
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
      _wsServer.StopServices();
    }

    #endregion
  }
}
