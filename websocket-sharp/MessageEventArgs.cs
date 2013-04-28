#region License
/*
 * MessageEventArgs.cs
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

using System;
using System.Text;

namespace WebSocketSharp {

  /// <summary>
  /// Contains the event data associated with a <see cref="WebSocket.OnMessage"/> event.
  /// </summary>
  /// <remarks>
  /// The <see cref="WebSocket.OnMessage"/> event occurs when the WebSocket receives a text or binary data frame.
  /// If you want to get the received data, you should access the <see cref="MessageEventArgs.Data"/> or
  /// <see cref="MessageEventArgs.RawData"/> properties.
  /// </remarks>
  public class MessageEventArgs : EventArgs
  {
    #region Fields

    private PayloadData _data;
    private Opcode      _type;

    #endregion

    #region Constructors

    internal MessageEventArgs(Opcode type, byte[] data)
      : this(type, new PayloadData(data))
    {
    }

    internal MessageEventArgs(Opcode type, PayloadData data)
    {
      _type = type;
      _data = data;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the received data as a <see cref="string"/>.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that contains the received data.
    /// </value>
    public string Data {
      get {
        return _type == Opcode.TEXT || _type == Opcode.PING || _type == Opcode.PONG
               ? _data.Length > 0
                 ? Encoding.UTF8.GetString(_data.ToByteArray())
                 : String.Empty
               : _type.ToString();
      }
    }

    /// <summary>
    /// Gets the received data as an array of <see cref="byte"/>.
    /// </summary>
    /// <value>
    /// An array of <see cref="byte"/> that contains the received data.
    /// </value>
    public byte[] RawData {
      get {
        return _data.ToByteArray();
      }
    }

    /// <summary>
    /// Gets the type of the received data.
    /// </summary>
    /// <value>
    /// One of the <see cref="Opcode"/> values that indicates the type of the received data.
    /// </value>
    public Opcode Type {
      get {
        return _type;
      }
    }

    #endregion
  }
}
