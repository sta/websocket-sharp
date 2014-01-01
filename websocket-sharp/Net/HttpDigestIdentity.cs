#region License
/*
 * HttpDigestIdentity.cs
 *
 * The MIT License
 *
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

using System;
using System.Collections.Specialized;
using System.Security.Principal;

namespace WebSocketSharp.Net
{
  /// <summary>
  /// Holds the user name and other authentication parameters from an HTTP Digest
  /// authentication request.
  /// </summary>
  public class HttpDigestIdentity : GenericIdentity
  {
    #region Private Fields

    private NameValueCollection _params;

    #endregion

    #region Internal Constructors

    internal HttpDigestIdentity (NameValueCollection authParams)
      : base (authParams ["username"], "Digest")
    {
      _params = authParams;
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the algorithm parameter from an HTTP Digest authentication request.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the algorithm parameter.
    /// </value>
    public string Algorithm {
      get {
        return _params ["algorithm"];
      }
    }

    /// <summary>
    /// Gets the cnonce parameter from an HTTP Digest authentication request.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the cnonce parameter.
    /// </value>
    public string Cnonce {
      get {
        return _params ["cnonce"];
      }
    }

    /// <summary>
    /// Gets the nc parameter from an HTTP Digest authentication request.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the nc parameter.
    /// </value>
    public string Nc {
      get {
        return _params ["nc"];
      }
    }

    /// <summary>
    /// Gets the nonce parameter from an HTTP Digest authentication request.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the nonce parameter.
    /// </value>
    public string Nonce {
      get {
        return _params ["nonce"];
      }
    }

    /// <summary>
    /// Gets the opaque parameter from an HTTP Digest authentication request.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the opaque parameter.
    /// </value>
    public string Opaque {
      get {
        return _params ["opaque"];
      }
    }

    /// <summary>
    /// Gets the qop parameter from an HTTP Digest authentication request.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the qop parameter.
    /// </value>
    public string Qop {
      get {
        return _params ["qop"];
      }
    }

    /// <summary>
    /// Gets the realm parameter from an HTTP Digest authentication request.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the realm parameter.
    /// </value>
    public string Realm {
      get {
        return _params ["realm"];
      }
    }

    /// <summary>
    /// Gets the response parameter from an HTTP Digest authentication request.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the response parameter.
    /// </value>
    public string Response {
      get {
        return _params ["response"];
      }
    }

    /// <summary>
    /// Gets the uri parameter from an HTTP Digest authentication request.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the uri parameter.
    /// </value>
    public string Uri {
      get {
        return _params ["uri"];
      }
    }

    #endregion

    #region Internal Methods

    internal bool IsValid (
      string password, string realm, string method, string entity)
    {
      var parameters = new NameValueCollection (_params);
      parameters ["password"] = password;
      parameters ["realm"] = realm;
      parameters ["method"] = method;
      parameters ["entity"] = entity;

      return _params ["response"] == HttpUtility.CreateRequestDigest (parameters);
    }

    #endregion
  }
}
