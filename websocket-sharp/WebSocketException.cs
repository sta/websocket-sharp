#region License
/*
 * WebSocketException.cs
 *
 * The MIT License
 *
 * Copyright (c) 2012-2022 sta.blockhead
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
  /// The exception that is thrown when a fatal error occurs in
  /// the WebSocket communication.
  /// </summary>
  public class WebSocketException : Exception
  {
    #region Private Fields

    private ushort _code;

    #endregion

    #region Private Constructors

    private WebSocketException (
      ushort code, string message, Exception innerException
    )
      : base (message ?? code.GetErrorMessage (), innerException)
    {
      _code = code;
    }

    #endregion

    #region Internal Constructors

    internal WebSocketException ()
      : this (CloseStatusCode.Abnormal, null, null)
    {
    }

    internal WebSocketException (Exception innerException)
      : this (CloseStatusCode.Abnormal, null, innerException)
    {
    }

    internal WebSocketException (string message)
      : this (CloseStatusCode.Abnormal, message, null)
    {
    }

    internal WebSocketException (CloseStatusCode code)
      : this (code, null, null)
    {
    }

    internal WebSocketException (string message, Exception innerException)
      : this (CloseStatusCode.Abnormal, message, innerException)
    {
    }

    internal WebSocketException (CloseStatusCode code, Exception innerException)
      : this (code, null, innerException)
    {
    }

    internal WebSocketException (CloseStatusCode code, string message)
      : this (code, message, null)
    {
    }

    internal WebSocketException (
      CloseStatusCode code, string message, Exception innerException
    )
      : this ((ushort) code, message, innerException)
    {
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the status code indicating the cause of the exception.
    /// </summary>
    /// <value>
    ///   <para>
    ///   A <see cref="ushort"/> that represents the status code indicating
    ///   the cause of the exception.
    ///   </para>
    ///   <para>
    ///   It is one of the status codes for the WebSocket connection close.
    ///   </para>
    /// </value>
    public ushort Code {
      get {
        return _code;
      }
    }

    #endregion
  }
}
