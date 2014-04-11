#region License
/*
 * AuthenticationSchemes.cs
 *
 * This code is derived from System.Net.AuthenticationSchemes.cs of Mono
 * (http://www.mono-project.com).
 *
 * The MIT License
 *
 * Copyright (c) 2005 Novell, Inc. (http://www.novell.com)
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
 * - Atsushi Enomoto <atsushi@ximian.com>
 */
#endregion

using System;

namespace WebSocketSharp.Net
{
  /// <summary>
  /// Contains the values of the schemes for authentication.
  /// </summary>
  [Flags]
  public enum AuthenticationSchemes
  {
    /// <summary>
    /// Indicates that no authentication is allowed.
    /// </summary>
    None,
    /// <summary>
    /// Indicates digest authentication.
    /// </summary>
    Digest = 1,
    /// <summary>
    /// Indicates basic authentication.
    /// </summary>
    Basic = 8,
    /// <summary>
    /// Indicates anonymous authentication.
    /// </summary>
    Anonymous = 0x8000
  }
}
