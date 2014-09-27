#region License
/*
 * WebSocketServiceHost.cs
 *
 * The MIT License
 *
 * Copyright (c) 2012-2014 sta.blockhead
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

#region Contributors
/*
 * Contributors:
 * - Juan Manuel Lallana <juan.manuel.lallana@gmail.com>
 */
#endregion

using System;
using WebSocketSharp.Net;
using WebSocketSharp.Net.WebSockets;

namespace WebSocketSharp.Server
{
  /// <summary>
  /// Exposes the methods and properties used to access the information in a WebSocket service
  /// provided by the <see cref="HttpServer"/> or <see cref="WebSocketServer"/>.
  /// </summary>
  /// <remarks>
  /// The WebSocketServiceHost class is an abstract class.
  /// </remarks>
  public abstract class WebSocketServiceHost
  {
    #region Protected Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketServiceHost"/> class.
    /// </summary>
    protected WebSocketServiceHost ()
    {
    }

    #endregion

    #region Internal Properties

    internal ServerState State {
      get {
        return Sessions.State;
      }
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets or sets a value indicating whether the WebSocket service cleans up
    /// the inactive sessions periodically.
    /// </summary>
    /// <value>
    /// <c>true</c> if the service cleans up the inactive sessions periodically;
    /// otherwise, <c>false</c>.
    /// </value>
    public abstract bool KeepClean { get; set; }

    /// <summary>
    /// Gets the path to the WebSocket service.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the absolute path to the service.
    /// </value>
    public abstract string Path { get; }

    /// <summary>
    /// Gets the access to the sessions in the WebSocket service.
    /// </summary>
    /// <value>
    /// A <see cref="WebSocketSessionManager"/> that manages the sessions in the service.
    /// </value>
    public abstract WebSocketSessionManager Sessions { get; }

    /// <summary>
    /// Gets the <see cref="System.Type"/> of the behavior of the WebSocket service.
    /// </summary>
    /// <value>
    /// A <see cref="System.Type"/> that represents the type of the behavior of the service.
    /// </value>
    public abstract Type Type { get; }

    /// <summary>
    /// Gets or sets the wait time for the response to the WebSocket Ping or Close.
    /// </summary>
    /// <value>
    /// A <see cref="TimeSpan"/> that represents the wait time. The default value is
    /// the same as 1 second.
    /// </value>
    public abstract TimeSpan WaitTime { get; set; }

    #endregion

    #region Internal Methods

    internal void Start ()
    {
      Sessions.Start ();
    }

    internal void StartSession (WebSocketContext context)
    {
      CreateSession ().Start (context, Sessions);
    }

    internal void Stop (ushort code, string reason)
    {
      var e = new CloseEventArgs (code, reason);

      var send = !code.IsReserved ();
      var bytes =
        send ? WebSocketFrame.CreateCloseFrame (e.PayloadData, false).ToByteArray () : null;

      var timeout = send ? WaitTime : TimeSpan.Zero;
      Sessions.Stop (e, bytes, timeout);
    }

    #endregion

    #region Protected Methods

    /// <summary>
    /// Creates a new session in the WebSocket service.
    /// </summary>
    /// <returns>
    /// A <see cref="WebSocketBehavior"/> instance that represents a new session.
    /// </returns>
    protected abstract WebSocketBehavior CreateSession ();

    #endregion
  }

  internal class WebSocketServiceHost<TBehavior> : WebSocketServiceHost
    where TBehavior : WebSocketBehavior
  {
    #region Private Fields

    private Func<TBehavior>         _initializer;
    private Logger                  _logger;
    private string                  _path;
    private WebSocketSessionManager _sessions;

    #endregion

    #region Internal Constructors

    internal WebSocketServiceHost (string path, Func<TBehavior> initializer, Logger logger)
    {
      _path = path;
      _initializer = initializer;
      _logger = logger;
      _sessions = new WebSocketSessionManager (logger);
    }

    #endregion

    #region Public Properties

    public override bool KeepClean {
      get {
        return _sessions.KeepClean;
      }

      set {
        _sessions.KeepClean = value;
      }
    }

    public override string Path {
      get {
        return _path;
      }
    }

    public override WebSocketSessionManager Sessions {
      get {
        return _sessions;
      }
    }

    public override Type Type {
      get {
        return typeof (TBehavior);
      }
    }

    public override TimeSpan WaitTime {
      get {
        return _sessions.WaitTime;
      }

      set {
        var msg = _sessions.State.CheckIfStartable () ?? value.CheckIfValidWaitTime ();
        if (msg != null) {
          _logger.Error (msg);
          return;
        }

        _sessions.WaitTime = value;
      }
    }

    #endregion

    #region Protected Methods

    protected override WebSocketBehavior CreateSession ()
    {
      return _initializer ();
    }

    #endregion
  }
}
