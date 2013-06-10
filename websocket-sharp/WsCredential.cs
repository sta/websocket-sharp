#region License
/*
 * WsCredential.cs
 *
 * The MIT License
 *
 * Copyright (c) 2013 sta.blockhead
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

namespace WebSocketSharp {

  /// <summary>
  /// Provides the credentials for HTTP authentication (Basic/Digest).
  /// </summary>
  public class WsCredential {

    #region Private Fields

    string _domain;
    string _password;
    string _userName;

    #endregion

    #region Internal Constructors

    internal WsCredential()
    {
    }

    internal WsCredential(string userName, string password)
      : this(userName, password, null)
    {
    }

    internal WsCredential(string userName, string password, string domain)
    {
      _userName = userName;
      _password = password;
      _domain = domain;
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the name of the user domain associated with the credentials.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that contains the name of the user domain associated with the credentials.
    /// Currently, returns the request uri of a WebSocket opening handshake.
    /// </value>
    public string Domain {
      get {
        return _domain ?? String.Empty;
      }

      internal set {
        _domain = value;
      }
    }

    /// <summary>
    /// Gets the password for the user name associated with the credentials.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that contains the password for the user name associated with the credentials.
    /// </value>
    public string Password {
      get {
        return _password ?? String.Empty;
      }

      internal set {
        _password = value;
      }
    }

    /// <summary>
    /// Gets the user name associated with the credentials.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that contains the user name associated with the credentials.
    /// </value>
    public string UserName {
      get {
        return _userName ?? String.Empty;
      }

      internal set {
        _userName = value;
      }
    }

    #endregion
  }
}
