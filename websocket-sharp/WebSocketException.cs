#region License
/*
 * WebSocketException.cs
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

namespace WebSocketSharp
{
  /// <summary>
  /// Represents the exception that occurred when attempting to perform an operation
  /// on the WebSocket connection.
  /// </summary>
  public class WebSocketException : Exception
  {
    #region Internal Constructors

    internal WebSocketException ()
      : this (CloseStatusCode.ABNORMAL)
    {
    }

    internal WebSocketException (CloseStatusCode code)
      : this (code, null)
    {
    }

    internal WebSocketException (string reason)
      : this (CloseStatusCode.ABNORMAL, reason)
    {
    }

    internal WebSocketException (CloseStatusCode code, string reason)
      : base (reason ?? code.GetMessage ())
    {
      Code = code;
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the <see cref="CloseStatusCode"/> associated with the <see cref="WebSocketException"/>.
    /// </summary>
    /// <value>
    /// One of the <see cref="CloseStatusCode"/> values, indicates the causes of the <see cref="WebSocketException"/>.
    /// </value>
    public CloseStatusCode Code {
      get; private set;
    }

    #endregion
  }
}
