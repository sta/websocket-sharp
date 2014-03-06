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

namespace WebSocketSharp
{
  /// <summary>
  /// Contains the event data associated with a <see cref="WebSocket.OnMessage"/> event.
  /// </summary>
  /// <remarks>
  /// A <see cref="WebSocket.OnMessage"/> event occurs when the <see cref="WebSocket"/> receives
  /// a text or binary data frame.
  /// If you want to get the received data, you access the <see cref="MessageEventArgs.Data"/> or
  /// <see cref="MessageEventArgs.RawData"/> property.
  /// </remarks>
  public class MessageEventArgs : EventArgs
  {
    #region Private Fields

    private string _data;
    private Opcode _opcode;
    private byte[] _rawData;

    #endregion

    #region Internal Constructors

    internal MessageEventArgs (Opcode opcode, byte[] data)
    {
      if ((ulong) data.LongLength > PayloadData.MaxLength)
        throw new WebSocketException (CloseStatusCode.TooBig);

      _opcode = opcode;
      _rawData = data;
      _data = convertToString (opcode, data);
    }

    internal MessageEventArgs (Opcode opcode, PayloadData payload)
    {
      _opcode = opcode;
      _rawData = payload.ApplicationData;
      _data = convertToString (opcode, _rawData);
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the received data as a <see cref="string"/>.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that contains the received data.
    /// </value>
    public string Data {
      get {
        return _data;
      }
    }

    /// <summary>
    /// Gets the received data as an array of <see cref="byte"/>.
    /// </summary>
    /// <value>
    /// An array of <see cref="byte"/> that contains the received data.
    /// </value>
    public byte [] RawData {
      get {
        return _rawData;
      }
    }

    /// <summary>
    /// Gets the type of the received data.
    /// </summary>
    /// <value>
    /// One of the <see cref="Opcode"/> values, indicates the type of the received data.
    /// </value>
    public Opcode Type {
      get {
        return _opcode;
      }
    }

    #endregion

    #region Private Methods

    private static string convertToString (Opcode opcode, byte [] data)
    {
      return data.LongLength == 0
             ? String.Empty
             : opcode == Opcode.Text
               ? Encoding.UTF8.GetString (data)
               : opcode.ToString ();
    }

    #endregion
  }
}
