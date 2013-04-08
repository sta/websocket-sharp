#region License
/*
 * CloseEventArgs.cs
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
  /// Contains the event data associated with a <see cref="WebSocket.OnClose"/> event.
  /// </summary>
  /// <remarks>
  /// The <see cref="WebSocket.OnClose"/> event occurs when the WebSocket receives a close control frame or
  /// the <c>WebSocket.Close</c> method is called. If you want to get the reason for closure, you should access
  /// the <see cref="CloseEventArgs.Code"/> or <see cref="CloseEventArgs.Reason"/> properties.
  /// </remarks>
  public class CloseEventArgs : MessageEventArgs
  {
    #region Fields

    private bool   _clean;
    private ushort _code;
    private string _reason;

    #endregion

    #region Constructors

    internal CloseEventArgs(PayloadData data)
      : base(Opcode.CLOSE, data)
    {
      _code   = getCodeFrom(data);
      _reason = getReasonFrom(data);
      _clean  = false;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the status code for closure.
    /// </summary>
    /// <value>
    /// A <see cref="ushort"/> that contains a status code for closure.
    /// </value>
    public ushort Code {
      get {
        return _code;
      }
    }

    /// <summary>
    /// Gets the reason for closure.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that contains a reason for closure.
    /// </value>
    public string Reason {
      get {
        return _reason;
      }
    }

    /// <summary>
    /// Indicates whether the WebSocket connection closed cleanly.
    /// </summary>
    /// <value>
    /// <c>true</c> if the WebSocket connection closed cleanly; otherwise, <c>false</c>.
    /// </value>
    public bool WasClean {
      get {
        return _clean;
      }

      internal set {
        _clean = value;
      }
    }

    #endregion

    #region Private Methods

    private ushort getCodeFrom(PayloadData data)
    {
      return data.Length >= 2
             ? data.ToByteArray().SubArray(0, 2).To<ushort>(ByteOrder.BIG)
             : (ushort)CloseStatusCode.NO_STATUS_CODE;
    }

    private string getReasonFrom(PayloadData data)
    {
      if (data.Length <= 2)
        return String.Empty;

      var buffer = data.ToByteArray().SubArray(2, (int)(data.Length - 2));
      return Encoding.UTF8.GetString(buffer);
    }

    #endregion
  }
}
