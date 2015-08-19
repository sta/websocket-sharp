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
using System.Text;

namespace WebSocketSharp
{
  /// <summary>
  /// Contains the event data associated with a <see cref="WebSocket.OnMessage"/> event.
  /// </summary>
  /// <remarks>
  ///   <para>
  ///   A <see cref="WebSocket.OnMessage"/> event occurs when the <see cref="WebSocket"/> receives
  ///   a text or binary message, or a Ping if the <see cref="WebSocket.EmitOnPing"/> property is
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
    private PayloadData _payloadData;
    private byte[] _rawData;

    #endregion

    #region Internal Constructors

    internal MessageEventArgs (WebSocketFrame frame)
    {
      _opcode = frame.Opcode;
      _payloadData = frame.PayloadData;
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
                  ? convertToString (_rawData)
                  : BitConverter.ToString (_rawData);

          _dataSet = true;
        }

        return _data;
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

    internal PayloadData PayloadData
    {
        get
        {
            return _payloadData ?? (_payloadData = new PayloadData(_rawData));
        }
    }

    /// <summary>
    /// Gets the type of the message.
    /// </summary>
    /// <value>
    /// <see cref="Opcode.Text"/>, <see cref="Opcode.Binary"/>, or <see cref="Opcode.Ping"/>.
    /// </value>
    public Opcode Type {
      get {
        return _opcode;
      }
    }

    #endregion

    #region Private Methods

    private static string convertToString (byte[] rawData)
    {
      try {
        return Encoding.UTF8.GetString (rawData);
      }
      catch {
        return null;
      }
    }

    #endregion
  }
}
