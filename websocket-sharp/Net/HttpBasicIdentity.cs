#region License
/*
 * HttpBasicIdentity.cs
 *
 * This code is derived from System.Net.HttpListenerBasicIdentity.cs of Mono
 * (http://www.mono-project.com).
 *
 * The MIT License
 *
 * Copyright (c) 2005 Novell, Inc. (http://www.novell.com)
 * Copyright (c) 2014 sta.blockhead
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
 * - Gonzalo Paniagua Javier <gonzalo@novell.com>
 */
#endregion

using System;
using System.Security.Principal;

namespace WebSocketSharp.Net
{
  /// <summary>
  /// Holds the user name and password from the HTTP Basic authentication credentials.
  /// </summary>
  public class HttpBasicIdentity : GenericIdentity
  {
    #region Private Fields

    private string _password;

    #endregion

    #region internal Constructors

    internal HttpBasicIdentity (string username, string password)
      : base (username, "Basic")
    {
      _password = password;
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the password from the HTTP Basic authentication credentials.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the password.
    /// </value>
    public virtual string Password {
      get {
        return _password;
      }
    }

    #endregion
  }
}
