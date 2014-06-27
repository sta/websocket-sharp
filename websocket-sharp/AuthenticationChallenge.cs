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
using WebSocketSharp.Net;

namespace WebSocketSharp
{
  internal class AuthenticationChallenge
  {
    #region Private Fields

    private NameValueCollection   _parameters;
    private AuthenticationSchemes _scheme;

    #endregion

    #region Private Constructors

    private AuthenticationChallenge (AuthenticationSchemes scheme, NameValueCollection parameters)
    {
      _scheme = scheme;
      _parameters = parameters;
    }

    #endregion

    #region Internal Constructors

    internal AuthenticationChallenge (AuthenticationSchemes scheme, string realm)
      : this (scheme, new NameValueCollection ())
    {
      _parameters["realm"] = realm;
      if (scheme == AuthenticationSchemes.Digest) {
        _parameters["nonce"] = AuthenticationResponse.CreateNonceValue ();
        _parameters["algorithm"] = "MD5";
        _parameters["qop"] = "auth";
      }
    }

    #endregion

    #region Internal Properties

    internal NameValueCollection Parameters {
      get {
        return _parameters;
      }
    }

    #endregion

    #region Public Properties

    public string Algorithm {
      get {
        return _parameters["algorithm"];
      }
    }

    public string Domain {
      get {
        return _parameters["domain"];
      }
    }

    public string Nonce {
      get {
        return _parameters["nonce"];
      }
    }

    public string Opaque {
      get {
        return _parameters["opaque"];
      }
    }

    public string Qop {
      get {
        return _parameters["qop"];
      }
    }

    public string Realm {
      get {
        return _parameters["realm"];
      }
    }

    public AuthenticationSchemes Scheme {
      get {
        return _scheme;
      }
    }

    public string Stale {
      get {
        return _parameters["stale"];
      }
    }

    #endregion

    #region Internal Methods

    internal static AuthenticationChallenge CreateBasicChallenge (string realm)
    {
      return new AuthenticationChallenge (AuthenticationSchemes.Basic, realm);
    }

    internal static AuthenticationChallenge CreateDigestChallenge (string realm)
    {
      return new AuthenticationChallenge (AuthenticationSchemes.Digest, realm);
    }

    internal static AuthenticationChallenge Parse (string value)
    {
      var chal = value.Split (new[] { ' ' }, 2);
      if (chal.Length != 2)
        return null;

      var scheme = chal[0].ToLower ();
      return scheme == "basic"
             ? new AuthenticationChallenge (
                 AuthenticationSchemes.Basic, AuthenticationResponse.ParseParameters (chal[1]))
             : scheme == "digest"
               ? new AuthenticationChallenge (
                   AuthenticationSchemes.Digest, AuthenticationResponse.ParseParameters (chal[1]))
               : null;
    }

    internal string ToBasicString ()
    {
      return String.Format ("Basic realm=\"{0}\"", _parameters["realm"]);
    }

    internal string ToDigestString ()
    {
      return String.Format (
        "Digest realm=\"{0}\", nonce=\"{1}\", algorithm={2}, qop=\"{3}\"",
        _parameters["realm"],
        _parameters["nonce"],
        _parameters["algorithm"],
        _parameters["qop"]);
    }

    #endregion

    #region Public Methods

    public override string ToString ()
    {
      return _scheme == AuthenticationSchemes.Basic
             ? ToBasicString ()
             : _scheme == AuthenticationSchemes.Digest
               ? ToDigestString ()
               : String.Empty;
    }

    #endregion
  }
}
