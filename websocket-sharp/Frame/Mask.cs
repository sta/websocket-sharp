#region MIT License
/*
 * Mask.cs
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

namespace WebSocketSharp.Frame {

  /// <summary>
  /// Contains the values of the MASK bit in the WebSocket data frame.
  /// </summary>
  /// <remarks>
  /// <para>
  /// The <b>Mask</b> enumeration contains the values of the <b>MASK</b> bit defined in
  /// <see href="http://tools.ietf.org/html/rfc6455#section-5.2">RFC 6455</see> for the WebSocket protocol.
  /// </para>
  /// <para>
  /// The <b>MASK</b> bit indicates whether the payload data in a WebSocket frame is masked.
  /// </para>
  /// </remarks>
  public enum Mask : byte
  {
    /// <summary>
    /// Equivalent to numeric value 0. Indicates that the payload data in a frame is not masked, no masking key in this frame.
    /// </summary>
    UNMASK = 0x0,
    /// <summary>
    /// Equivalent to numeric value 1. Indicates that the payload data in a frame is masked, a masking key is present in this frame.
    /// </summary>
    MASK   = 0x1
  }
}
