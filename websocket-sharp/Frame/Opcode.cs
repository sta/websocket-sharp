#region MIT License
/*
 * Opcode.cs
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
  /// Contains the values of the opcodes that denotes the frame type of the WebSocket frame.
  /// </summary>
  /// <remarks>
  /// The <b>Opcode</b> enumeration contains the values of the opcodes defined in
  /// <see href="http://tools.ietf.org/html/rfc6455#section-5.2">RFC 6455</see> for the WebSocket protocol.
  /// </remarks>
  [Flags]
  public enum Opcode : byte
  {
    /// <summary>
    /// Equivalent to numeric value 0. Indicates a continuation frame.
    /// </summary>
    CONT   = 0x0,
    /// <summary>
    /// Equivalent to numeric value 1. Indicates a text frame.
    /// </summary>
    TEXT   = 0x1,
    /// <summary>
    /// Equivalent to numeric value 2. Indicates a binary frame.
    /// </summary>
    BINARY = 0x2,
    /// <summary>
    /// Equivalent to numeric value 8. Indicates a connection close frame.
    /// </summary>
    CLOSE  = 0x8,
    /// <summary>
    /// Equivalent to numeric value 9. Indicates a ping frame.
    /// </summary>
    PING   = 0x9,
    /// <summary>
    /// Equivalent to numeric value 10. Indicates a pong frame.
    /// </summary>
    PONG   = 0xa
  }
}
