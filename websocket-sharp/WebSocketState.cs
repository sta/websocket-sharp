#region License
/*
 * WebSocketState.cs
 *
 * The MIT License
 *
 * Copyright (c) 2010-2022 sta.blockhead
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
  /// Indicates the state of the WebSocket interface.
  /// </summary>
  public enum WebSocketState : ushort
  {
    /// <summary>
    /// Equivalent to numeric value 0. Indicates that a new interface has
    /// been created.
    /// </summary>
    New = 0,
    /// <summary>
    /// Equivalent to numeric value 1. Indicates that the connect process is
    /// in progress.
    /// </summary>
    Connecting = 1,
    /// <summary>
    /// Equivalent to numeric value 2. Indicates that the connection has
    /// been established and the communication is possible.
    /// </summary>
    Open = 2,
    /// <summary>
    /// Equivalent to numeric value 3. Indicates that the close process is
    /// in progress.
    /// </summary>
    Closing = 3,
    /// <summary>
    /// Equivalent to numeric value 4. Indicates that the connection has
    /// been closed or could not be established.
    /// </summary>
    Closed = 4
  }
}
