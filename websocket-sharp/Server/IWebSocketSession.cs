#region License
/*
 * IWebSocketSession.cs
 *
 * The MIT License
 *
 * Copyright (c) 2013 sta.blockhead
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
using WebSocketSharp.Net.WebSockets;

namespace WebSocketSharp.Server
{
  /// <summary>
  /// Exposes the properties for the session to the WebSocket service.
  /// </summary>
  public interface IWebSocketSession
  {
    /// <summary>
    /// Gets the WebSocket connection request information.
    /// </summary>
    /// <value>
    /// A <see cref="WebSocketContext"/> that contains the WebSocket connection request information.
    /// </value>
    WebSocketContext Context { get; }

    /// <summary>
    /// Gets the unique ID of the session to the WebSocket service.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that contains the unique ID of the session.
    /// </value>
    string ID { get; }

    /// <summary>
    /// Gets the time that the session has been started.
    /// </summary>
    /// <value>
    /// A <see cref="DateTime"/> that represents the time that the session has been started.
    /// </value>
    DateTime StartTime { get; }

    /// <summary>
    /// Gets the state of the WebSocket connection.
    /// </summary>
    /// <value>
    /// One of the <see cref="WebSocketState"/> values.
    /// </value>
    WebSocketState State { get; }
  }
}
