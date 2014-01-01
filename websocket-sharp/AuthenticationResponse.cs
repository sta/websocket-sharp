#region License
/*
 * AuthenticationResponse.cs
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
    private NameValueCollection _params;
    private string              _scheme;

    #endregion

    #region Private Constructors

    private AuthenticationResponse (
      string authScheme, NameValueCollection authParams)
    {
      _scheme = authScheme;
      _params = authParams;
    }

    #endregion

    #region Internal Constructors

    internal AuthenticationResponse (NetworkCredential credentials)
      : this ("Basic", new NameValueCollection (), credentials, 0)
    {
    }

    internal AuthenticationResponse (
      AuthenticationChallenge challenge,
      NetworkCredential credentials,
      uint nonceCount)
      : this (challenge.Scheme, challenge.Params, credentials, nonceCount)
    {
    }

    internal AuthenticationResponse (
      string authScheme,
      NameValueCollection authParams,
      NetworkCredential credentials,
      uint nonceCount)
    {
      _scheme = authScheme.ToLower ();
      _params = authParams;
      _params ["username"] = credentials.UserName;
      _params ["password"] = credentials.Password;
      _params ["uri"] = credentials.Domain;
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

    public string Cnonce {
      get {
        return _params ["cnonce"];
      }
    }

    public string Nc {
      get {
        return _params ["nc"];
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

    public string Password {
      get {
        return _params ["password"];
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

    public string Response {
      get {
        return _params ["response"];
      }
    }

    public string Scheme {
      get {
        return _scheme;
      }
    }

    public string Uri {
      get {
        return _params ["uri"];
      }
    }

    public string UserName {
      get {
        return _params ["username"];
      }
    }

    #endregion

    #region Private Methods

    private static bool contains (string [] array, string item)
    {
      foreach (var i in array)
        if (i.Trim ().ToLower () == item)
          return true;

      return false;
    }

    private void initAsDigest ()
    {
      var qops = _params ["qop"];
      if (qops != null) {
        var qop = "auth";
        if (contains (qops.Split (','), qop)) {
          _params ["qop"] = qop;
          _params ["nc"] = String.Format ("{0:x8}", ++_nonceCount);
          _params ["cnonce"] = HttpUtility.CreateNonceValue ();
        }
        else
          _params ["qop"] = null;
      }

      _params ["method"] = "GET";
      _params ["response"] = HttpUtility.CreateRequestDigest (_params);
    }

    #endregion

    #region Public Methods

    public static AuthenticationResponse Parse (string value)
    {
      try {
        var credentials = value.Split (new char [] { ' ' }, 2);
        if (credentials.Length != 2)
          return null;

        var scheme = credentials [0].ToLower ();
        return scheme == "basic"
               ? new AuthenticationResponse (
                   scheme, credentials [1].ParseBasicAuthResponseParams ())
               : scheme == "digest"
                 ? new AuthenticationResponse (
                     scheme, credentials [1].ParseAuthParams ())
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
                 _params ["username"], _params ["password"]) as IIdentity
             : _scheme == "digest"
               ? new HttpDigestIdentity (_params)
               : null;
    }

    public override string ToString ()
    {
      return _scheme == "basic"
             ? HttpUtility.CreateBasicAuthCredentials (
                 _params ["username"], _params ["password"])
             : _scheme == "digest"
               ? HttpUtility.CreateDigestAuthCredentials (_params)
               : String.Empty;
    }

    #endregion
  }
}
