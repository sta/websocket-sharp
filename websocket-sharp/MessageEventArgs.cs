#region License
/*
 * MessageEventArgs.cs
 *
 * The MIT License
 *
 * Copyright (c) 2012-2014 sta.blockhead
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
  ///   a text or binary message.
  ///   </para>
  ///   <para>
  ///   If you would like to get the message data, you should access
  ///   the <see cref="MessageEventArgs.Data"/> or <see cref="MessageEventArgs.RawData"/> property.
  ///   </para>
  /// </remarks>
  public class MessageEventArgs : EventArgs
  {
    #region Private Fields

    private string _data;
    private Opcode _opcode;
    private byte[] _rawData;

    #endregion

    #region Internal Constructors

    internal MessageEventArgs (WebSocketFrame frame)
    {
      _opcode = frame.Opcode;
      _rawData = frame.PayloadData.ApplicationData;
      _data = convertToString (_opcode, _rawData);
    }

    internal MessageEventArgs (Opcode opcode, byte[] rawData)
    {
      if ((ulong) rawData.LongLength > PayloadData.MaxLength)
        throw new WebSocketException (CloseStatusCode.TooBig);

      _opcode = opcode;
      _rawData = rawData;
      _data = convertToString (opcode, rawData);
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the message data as a <see cref="string"/>.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   If the message data is empty, this property returns <see cref="String.Empty"/>.
    ///   </para>
    ///   <para>
    ///   Or if the message is a binary message, this property returns <c>"Binary"</c>.
    ///   </para>
    /// </remarks>
    /// <value>
    /// A <see cref="string"/> that represents the message data.
    /// </value>
    public string Data {
      get {
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

    /// <summary>
    /// Gets the type of the message.
    /// </summary>
    /// <value>
    /// <see cref="Opcode.Text"/> or <see cref="Opcode.Binary"/>.
    /// </value>
    public Opcode Type {
      get {
        return _opcode;
      }
    }

    #endregion

    #region Private Methods

    private static string convertToString (Opcode opcode, byte[] rawData)
    {
      return rawData.LongLength == 0
             ? String.Empty
             : opcode == Opcode.Text
               ? Encoding.UTF8.GetString (rawData)
               : opcode.ToString ();
    }

    #endregion
  }
}
