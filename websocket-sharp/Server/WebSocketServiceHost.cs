#region License
/*
 * WebSocketServiceHost.cs
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

#region Contributors
/*
 * Contributors:
 *   Juan Manuel Lallana <juan.manuel.lallana@gmail.com>
 */
#endregion

using System;
using System.Collections.Generic;
using WebSocketSharp.Net;
using WebSocketSharp.Net.WebSockets;

namespace WebSocketSharp.Server
{
  /// <summary>
  /// Provides the methods and properties for the WebSocket service host.
  /// </summary>
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
    /// Gets or sets a value indicating whether the WebSocket service host cleans up
    /// the inactive sessions periodically.
    /// </summary>
    /// <value>
    /// <c>true</c> if the WebSocket service host cleans up the inactive sessions periodically;
    /// otherwise, <c>false</c>.
    /// </value>
    public abstract bool KeepClean { get; set; }

    /// <summary>
    /// Gets the path to the WebSocket service managed by the WebSocket service host.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service.
    /// </value>
    public abstract string ServicePath { get; }

    /// <summary>
    /// Gets the number of the sessions to the WebSocket service.
    /// </summary>
    /// <value>
    /// An <see cref="int"/> that contains the session count.
    /// </value>
    public abstract int SessionCount { get; }

    /// <summary>
    /// Gets the manager of the sessions to the WebSocket service.
    /// </summary>
    /// <value>
    /// A <see cref="WebSocketSessionManager"/> that manages the sessions.
    /// </value>
    public abstract WebSocketSessionManager Sessions { get; }

    #endregion

    #region Internal Methods

    /// <summary>
    /// Starts a new session to the WebSocket service using the specified <see cref="WebSocketContext"/>.
    /// </summary>
    /// <param name="context">
    /// A <see cref="WebSocketContext"/> that contains a WebSocket connection request objects.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="context"/> is <see langword="null"/>.
    /// </exception>
    internal void StartSession (WebSocketContext context)
    {
      if (context == null)
        throw new ArgumentNullException ("context");

      var session = CreateSession ();
      session.Start (context, Sessions);
    }

    #endregion

    #region Protected Methods

    /// <summary>
    /// Creates a new session to the WebSocket service.
    /// </summary>
    /// <returns>
    /// A <see cref="WebSocketService"/> instance that represents a new session.
    /// </returns>
    protected abstract WebSocketService CreateSession ();

    #endregion
  }

  /// <summary>
  /// Provides the methods and properties for the WebSocket service host.
  /// </summary>
  /// <typeparam name="T">
  /// The type of the WebSocket service provided by the server.
  /// The T must inherit the <see cref="WebSocketService"/> class.
  /// </typeparam>
  internal class WebSocketServiceHost<T> : WebSocketServiceHost
    where T : WebSocketService
  {
    #region Private Fields

    private Func<T>                 _constructor;
    private string                  _path;
    private WebSocketSessionManager _sessions;

    #endregion

    #region Internal Constructors

    internal WebSocketServiceHost (string path, Func<T> constructor, Logger logger)
    {
      _path = HttpUtility.UrlDecode (path).TrimEndSlash ();
      _constructor = constructor;
      _sessions = new WebSocketSessionManager (logger);
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets or sets a value indicating whether the WebSocket service host cleans up
    /// the inactive sessions periodically.
    /// </summary>
    /// <value>
    /// <c>true</c> if the WebSocket service host cleans up the inactive sessions
    /// every 60 seconds; otherwise, <c>false</c>. The default value is <c>true</c>.
    /// </value>
    public override bool KeepClean {
      get {
        return _sessions.KeepClean;
      }

      set {
        _sessions.KeepClean = value;
      }
    }

    /// <summary>
    /// Gets the path to the WebSocket service managed by the WebSocket service host.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service.
    /// </value>
    public override string ServicePath {
      get {
        return _path;
      }
    }

    /// <summary>
    /// Gets the number of the sessions to the WebSocket service.
    /// </summary>
    /// <value>
    /// An <see cref="int"/> that contains the session count.
    /// </value>
    public override int SessionCount {
      get {
        return _sessions.Count;
      }
    }

    /// <summary>
    /// Gets the manager of the sessions to the WebSocket service.
    /// </summary>
    /// <value>
    /// A <see cref="WebSocketSessionManager"/> that manages the sessions.
    /// </value>
    public override WebSocketSessionManager Sessions {
      get {
        return _sessions;
      }
    }

    #endregion

    #region Protected Methods

    /// <summary>
    /// Creates a new session to the WebSocket service.
    /// </summary>
    /// <returns>
    /// A <see cref="WebSocketService"/> instance that represents a new session.
    /// </returns>
    protected override WebSocketService CreateSession ()
    {
      return _constructor ();
    }

    #endregion
  }
}
