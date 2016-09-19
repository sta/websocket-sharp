#region License
/*
 * CookieException.cs
 *
 * This code is derived from System.Net.CookieException.cs of Mono
 * (http://www.mono-project.com).
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

#region Authors
/*
 * Authors:
 * - Lawrence Pit <loz@cable.a2000.nl>
 */
#endregion

using System;
using System.Runtime.Serialization;
//using System.Security.Permissions;

namespace WebSocketSharp.Net
{
  /// <summary>
  /// The exception that is thrown when a <see cref="Cookie"/> gets an error.
  /// </summary>
  [Serializable]
  public class CookieException : FormatException
  {
    #region Internal Constructors

    internal CookieException (string message)
      : base (message)
    {
    }

    internal CookieException (string message, Exception innerException)
      : base (message, innerException)
    {
    }

    #endregion

    #region Protected Constructors

    

    #endregion

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="CookieException"/> class.
    /// </summary>
    public CookieException ()
      : base ()
    {
    }

    #endregion

    #region Public Methods

    

    #endregion

    #region Explicit Interface Implementation

    

    #endregion
  }
}
