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

    #region Public Properties

    /// <summary>
    /// Gets or sets a value indicating whether the WebSocket service cleans up the inactive
    /// sessions periodically.
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

    #endregion

    #region Internal Methods

    internal void StartSession (WebSocketContext context)
    {
      CreateSession ().Start (context, Sessions);
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
    private string                  _path;
    private WebSocketSessionManager _sessions;

    #endregion

    #region Internal Constructors

    internal WebSocketServiceHost (string path, Func<TBehavior> initializer, Logger logger)
    {
      _path = HttpUtility.UrlDecode (path).TrimEndSlash ();
      _initializer = initializer;
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

    #endregion

    #region Protected Methods

    protected override WebSocketBehavior CreateSession ()
    {
      return _initializer ();
    }

    #endregion
  }
}
