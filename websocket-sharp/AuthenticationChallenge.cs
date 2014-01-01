#region License
/*
 * AuthenticationChallenge.cs
 *
 * The MIT License
 *
 * Copyright (c) 2013-2014 sta.blockhead
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
using System.Text;

namespace WebSocketSharp
{
  internal class AuthenticationChallenge
  {
    #region Private Fields

    private NameValueCollection _params;
    private string              _scheme;

    #endregion

    #region Internal Constructors

    internal AuthenticationChallenge (string authScheme, string authParams)
    {
      _scheme = authScheme;
      _params = authParams.ParseAuthParams ();
    }

    #endregion

    #region Internal Properties

    internal NameValueCollection Params {
      get {
        return _params;
      }
    }

    #endregion

    #region Public Properties

    public string Algorithm {
      get {
        return _params ["algorithm"];
      }
    }

    public string Domain {
      get {
        return _params ["domain"];
      }
    }

    public string Nonce {
      get {
        return _params ["nonce"];
      }
    }

    public string Opaque {
      get {
        return _params ["opaque"];
      }
    }

    public string Qop {
      get {
        return _params ["qop"];
      }
    }

    public string Realm {
      get {
        return _params ["realm"];
      }
    }

    public string Scheme {
      get {
        return _scheme;
      }
    }

    public string Stale {
      get {
        return _params ["stale"];
      }
    }

    #endregion

    #region Public Methods

    public static AuthenticationChallenge Parse (string value)
    {
      var challenge = value.Split (new char [] {' '}, 2);
      var scheme = challenge [0].ToLower ();
      return scheme == "basic" || scheme == "digest"
             ? new AuthenticationChallenge (scheme, challenge [1])
             : null;
    }

    #endregion
  }
}
