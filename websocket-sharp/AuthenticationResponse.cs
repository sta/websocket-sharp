#region License
/*
 * AuthenticationResponse.cs
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
using System.Security.Principal;
using System.Text;
using WebSocketSharp.Net;

namespace WebSocketSharp
{
  internal class AuthenticationResponse
  {
    #region Private Fields

    private uint                _nonceCount;
    private NameValueCollection _parameters;
    private string              _scheme;

    #endregion

    #region Private Constructors

    private AuthenticationResponse (string scheme, NameValueCollection parameters)
    {
      _scheme = scheme;
      _parameters = parameters;
    }

    #endregion

    #region Internal Constructors

    internal AuthenticationResponse (NetworkCredential credentials)
      : this ("Basic", new NameValueCollection (), credentials, 0)
    {
    }

    internal AuthenticationResponse (
      AuthenticationChallenge challenge, NetworkCredential credentials, uint nonceCount)
      : this (challenge.Scheme, challenge.Parameters, credentials, nonceCount)
    {
    }

    internal AuthenticationResponse (
      string scheme, NameValueCollection parameters, NetworkCredential credentials, uint nonceCount)
    {
      _scheme = scheme.ToLower ();
      _parameters = parameters;
      _parameters ["username"] = credentials.UserName;
      _parameters ["password"] = credentials.Password;
      _parameters ["uri"] = credentials.Domain;
      _nonceCount = nonceCount;
      if (_scheme == "digest")
        initAsDigest ();
    }

    #endregion

    #region Internal Properties

    internal uint NonceCount {
      get {
        return _nonceCount < UInt32.MaxValue
               ? _nonceCount
               : 0;
      }
    }

    internal NameValueCollection Parameters {
      get {
        return _parameters;
      }
    }

    #endregion

    #region Public Properties

    public string Algorithm {
      get {
        return _parameters ["algorithm"];
      }
    }

    public string Cnonce {
      get {
        return _parameters ["cnonce"];
      }
    }

    public string Nc {
      get {
        return _parameters ["nc"];
      }
    }

    public string Nonce {
      get {
        return _parameters ["nonce"];
      }
    }

    public string Opaque {
      get {
        return _parameters ["opaque"];
      }
    }

    public string Password {
      get {
        return _parameters ["password"];
      }
    }

    public string Qop {
      get {
        return _parameters ["qop"];
      }
    }

    public string Realm {
      get {
        return _parameters ["realm"];
      }
    }

    public string Response {
      get {
        return _parameters ["response"];
      }
    }

    public string Scheme {
      get {
        return _scheme;
      }
    }

    public string Uri {
      get {
        return _parameters ["uri"];
      }
    }

    public string UserName {
      get {
        return _parameters ["username"];
      }
    }

    #endregion

    #region Private Methods

    private void initAsDigest ()
    {
      var qops = _parameters ["qop"];
      if (qops != null) {
        if (qops.Split (',').Contains (qop => qop.Trim ().ToLower () == "auth")) {
          _parameters ["qop"] = "auth";
          _parameters ["nc"] = String.Format ("{0:x8}", ++_nonceCount);
          _parameters ["cnonce"] = HttpUtility.CreateNonceValue ();
        }
        else {
          _parameters ["qop"] = null;
        }
      }

      _parameters ["method"] = "GET";
      _parameters ["response"] = HttpUtility.CreateRequestDigest (_parameters);
    }

    #endregion

    #region Public Methods

    public static AuthenticationResponse Parse (string value)
    {
      try {
        var credentials = value.Split (new [] { ' ' }, 2);
        if (credentials.Length != 2)
          return null;

        var scheme = credentials [0].ToLower ();
        return scheme == "basic"
               ? new AuthenticationResponse (scheme, credentials [1].ParseBasicCredentials ())
               : scheme == "digest"
                 ? new AuthenticationResponse (scheme, credentials [1].ParseAuthParameters ())
                 : null;
      }
      catch {
      }

      return null;
    }

    public IIdentity ToIdentity ()
    {
      return _scheme == "basic"
             ? new HttpBasicIdentity (
                 _parameters ["username"], _parameters ["password"]) as IIdentity
             : _scheme == "digest"
               ? new HttpDigestIdentity (_parameters)
               : null;
    }

    public override string ToString ()
    {
      return _scheme == "basic"
             ? HttpUtility.CreateBasicAuthCredentials (
                 _parameters ["username"], _parameters ["password"])
             : _scheme == "digest"
               ? HttpUtility.CreateDigestAuthCredentials (_parameters)
               : String.Empty;
    }

    #endregion
  }
}
