#region License
/*
 * MessageEventArgs.cs
 *
 * The MIT License
 *
 * Copyright (c) 2012-2015 sta.blockhead
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
  /// Represents the event data for the <see cref="WebSocket.OnMessage"/> event.
  /// </summary>
  /// <remarks>
  ///   <para>
  ///   A <see cref="WebSocket.OnMessage"/> event occurs when the <see cref="WebSocket"/> receives
  ///   a text or binary message, or a ping if the <see cref="WebSocket.EmitOnPing"/> property is
  ///   set to <c>true</c>.
  ///   </para>
  ///   <para>
  ///   If you would like to get the message data, you should access the <see cref="Data"/> or
  ///   <see cref="RawData"/> property.
  ///   </para>
  /// </remarks>
  public class MessageEventArgs : EventArgs
  {
    #region Private Fields

    private string _data;
    private bool   _dataSet;
    private Opcode _opcode;
    private byte[] _rawData;

    #endregion

    #region Internal Constructors

    internal MessageEventArgs (WebSocketFrame frame)
    {
      _opcode = frame.Opcode;
      _rawData = frame.PayloadData.ApplicationData;
    }

    internal MessageEventArgs (Opcode opcode, byte[] rawData)
    {
      if ((ulong) rawData.LongLength > PayloadData.MaxLength)
        throw new WebSocketException (CloseStatusCode.TooBig);

      _opcode = opcode;
      _rawData = rawData;
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the message data as a <see cref="string"/>.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the message data,
    /// or <see langword="null"/> if the message data cannot be decoded to a string.
    /// </value>
    public string Data {
      get {
        if (!_dataSet) {
          _data = _opcode != Opcode.Binary
                  ? _rawData.UTF8Decode ()
                  : BitConverter.ToString (_rawData);

          _dataSet = true;
        }

        return _data;
      }
    }

    /// <summary>
    /// Gets a value indicating whether the message type is binary.
    /// </summary>
    /// <value>
    /// <c>true</c> if the message type is binary; otherwise, <c>false</c>.
    /// </value>
    public bool IsBinary {
      get {
        return _opcode == Opcode.Binary;
      }
    }

    /// <summary>
    /// Gets a value indicating whether the message type is ping.
    /// </summary>
    /// <value>
    /// <c>true</c> if the message type is ping; otherwise, <c>false</c>.
    /// </value>
    public bool IsPing {
      get {
        return _opcode == Opcode.Ping;
      }
    }

    /// <summary>
    /// Gets a value indicating whether the message type is text.
    /// </summary>
    /// <value>
    /// <c>true</c> if the message type is text; otherwise, <c>false</c>.
    /// </value>
    public bool IsText {
      get {
        return _opcode == Opcode.Text;
      }
    }

    /// <summary>
    /// Gets the message data as an array of <see cref="byte"/>.
    /// </summary>
    /// <value>
    /// An array of <see cref="byte"/> that represents the message data.
    /// </value>
    public byte[] RawData {
      get {
        return _rawData;
      }
    }

    /// <summary>
    /// Gets the message type.
    /// </summary>
    /// <value>
    /// <see cref="Opcode.Text"/>, <see cref="Opcode.Binary"/>, or <see cref="Opcode.Ping"/>.
    /// </value>
    [Obsolete ("This property will be removed. Use any of the Is properties instead.")]
    public Opcode Type {
      get {
        return _opcode;
      }
    }

    #endregion
  }
}
