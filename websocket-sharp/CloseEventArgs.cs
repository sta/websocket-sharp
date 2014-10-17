#region License
/*
 * CloseEventArgs.cs
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
  /// Contains the event data associated with a <see cref="WebSocket.OnClose"/> event.
  /// </summary>
  /// <remarks>
  ///   <para>
  ///   A <see cref="WebSocket.OnClose"/> event occurs when the WebSocket connection has been
  ///   closed.
  ///   </para>
  ///   <para>
  ///   If you would like to get the reason for the close, you should access
  ///   the <see cref="CloseEventArgs.Code"/> or <see cref="CloseEventArgs.Reason"/> property.
  ///   </para>
  /// </remarks>
  public class CloseEventArgs : EventArgs
  {
    #region Private Fields

    private bool        _clean;
    private ushort      _code;
    private PayloadData _payloadData;
    private byte[]      _rawData;
    private string      _reason;

    #endregion

    #region Internal Constructors

    internal CloseEventArgs ()
    {
      _payloadData = new PayloadData ();
      _rawData = _payloadData.ApplicationData;

      _code = (ushort) CloseStatusCode.NoStatusCode;
      _reason = String.Empty;
    }

    internal CloseEventArgs (ushort code)
    {
      _code = code;
      _reason = String.Empty;
      _rawData = code.InternalToByteArray (ByteOrder.Big);
    }

    internal CloseEventArgs (CloseStatusCode code)
      : this ((ushort) code)
    {
    }

    internal CloseEventArgs (PayloadData payloadData)
    {
      _payloadData = payloadData;
      _rawData = payloadData.ApplicationData;

      var len = _rawData.Length;
      _code = len > 1
              ? _rawData.SubArray (0, 2).ToUInt16 (ByteOrder.Big)
              : (ushort) CloseStatusCode.NoStatusCode;

      _reason = len > 2
                ? Encoding.UTF8.GetString (_rawData.SubArray (2, len - 2))
                : String.Empty;
    }

    internal CloseEventArgs (ushort code, string reason)
    {
      _code = code;
      _reason = reason ?? String.Empty;
      _rawData = code.Append (reason);
    }

    internal CloseEventArgs (CloseStatusCode code, string reason)
      : this ((ushort) code, reason)
    {
    }

    #endregion

    #region Internal Properties

    internal PayloadData PayloadData {
      get {
        return _payloadData ?? (_payloadData = new PayloadData (_rawData));
      }
    }

    internal byte[] RawData {
      get {
        return _rawData;
      }
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the status code for the close.
    /// </summary>
    /// <value>
    /// A <see cref="ushort"/> that represents the status code for the close if any.
    /// </value>
    public ushort Code {
      get {
        return _code;
      }
    }

    /// <summary>
    /// Gets the reason for the close.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the reason for the close if any.
    /// </value>
    public string Reason {
      get {
        return _reason;
      }
    }

    /// <summary>
    /// Gets a value indicating whether the WebSocket connection has been closed cleanly.
    /// </summary>
    /// <value>
    /// <c>true</c> if the WebSocket connection has been closed cleanly; otherwise, <c>false</c>.
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
  }
}
