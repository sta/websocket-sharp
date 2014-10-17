#region License
/*
 * WebSocketState.cs
 *
 * The MIT License
 *
 * Copyright (c) 2010-2014 sta.blockhead
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

namespace WebSocketSharp
{
  /// <summary>
  /// Contains the values of the state of the WebSocket connection.
  /// </summary>
  /// <remarks>
  /// The values of the state are defined in
  /// <see href="http://www.w3.org/TR/websockets/#dom-websocket-readystate">The WebSocket API</see>.
  /// </remarks>
  public enum WebSocketState : ushort
  {
    /// <summary>
    /// Equivalent to numeric value 0.
    /// Indicates that the connection hasn't yet been established.
    /// </summary>
    Connecting = 0,
    /// <summary>
    /// Equivalent to numeric value 1.
    /// Indicates that the connection is established and the communication is possible.
    /// </summary>
    Open = 1,
    /// <summary>
    /// Equivalent to numeric value 2.
    /// Indicates that the connection is going through the closing handshake or
    /// the <c>WebSocket.Close</c> method has been invoked.
    /// </summary>
    Closing = 2,
    /// <summary>
    /// Equivalent to numeric value 3.
    /// Indicates that the connection has been closed or couldn't be opened.
    /// </summary>
    Closed = 3
  }
}
