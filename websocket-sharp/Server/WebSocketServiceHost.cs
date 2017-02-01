#region License
/*
 * WebSocketServiceHost.cs
 *
 * The MIT License
 *
 * Copyright (c) 2012-2017 sta.blockhead
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
using WebSocketSharp.Net.WebSockets;

namespace WebSocketSharp.Server
{
  /// <summary>
  /// Exposes the methods and properties used to access the information in
  /// a WebSocket service provided by the <see cref="WebSocketServer"/> or
  /// <see cref="HttpServer"/>.
  /// </summary>
  /// <remarks>
  /// This class is an abstract class.
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
    /// Gets or sets a value indicating whether the service cleans up
    /// the inactive sessions periodically.
    /// </summary>
    /// <value>
    /// <c>true</c> if the service cleans up the inactive sessions periodically;
    /// otherwise, <c>false</c>.
    /// </value>
    public abstract bool KeepClean { get; set; }

    /// <summary>
    /// Gets the path to the service.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the absolute path to the service.
    /// </value>
    public abstract string Path { get; }

    /// <summary>
    /// Gets the access to the sessions in the service.
    /// </summary>
    /// <value>
    /// A <see cref="WebSocketSessionManager"/> that manages the sessions in
    /// the service.
    /// </value>
    public abstract WebSocketSessionManager Sessions { get; }

    /// <summary>
    /// Gets the <see cref="System.Type"/> of the behavior of the service.
    /// </summary>
    /// <value>
    /// A <see cref="System.Type"/> that represents the type of the behavior of
    /// the service.
    /// </value>
    public abstract Type Type { get; }

    /// <summary>
    /// Gets or sets the wait time for the response to the WebSocket Ping or Close.
    /// </summary>
    /// <value>
    /// A <see cref="TimeSpan"/> that represents the wait time for the response.
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
      Sessions.Stop (code, reason);
    }

    #endregion

    #region Protected Methods

    /// <summary>
    /// Creates a new session for the service.
    /// </summary>
    /// <returns>
    /// A <see cref="WebSocketBehavior"/> instance that represents a new session.
    /// </returns>
    protected abstract WebSocketBehavior CreateSession ();

    #endregion
  }
}
