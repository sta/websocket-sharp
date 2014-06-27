#region License
/*
 * AuthenticationResponse.cs
 *
 * ParseBasicCredentials is derived from System.Net.HttpListenerContext.cs of Mono
 * (http://www.mono-project.com).
 *
 * The MIT License
 *
 * Copyright (c) 2005 Novell, Inc. (http://www.novell.com)
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
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using WebSocketSharp.Net;

namespace WebSocketSharp
{
  internal class AuthenticationResponse
  {
    #region Private Fields

    private uint                  _nonceCount;
    private NameValueCollection   _parameters;
    private AuthenticationSchemes _scheme;

    #endregion

    #region Private Constructors

    private AuthenticationResponse (AuthenticationSchemes scheme, NameValueCollection parameters)
    {
      _scheme = scheme;
      _parameters = parameters;
    }

    #endregion

    #region Internal Constructors

    internal AuthenticationResponse (NetworkCredential credentials)
      : this (AuthenticationSchemes.Basic, new NameValueCollection (), credentials, 0)
    {
    }

    internal AuthenticationResponse (
      AuthenticationChallenge challenge, NetworkCredential credentials, uint nonceCount)
      : this (challenge.Scheme, challenge.Parameters, credentials, nonceCount)
    {
    }

    internal AuthenticationResponse (
      AuthenticationSchemes scheme,
      NameValueCollection parameters,
      NetworkCredential credentials,
      uint nonceCount)
      : this (scheme, parameters)
    {
      _parameters["username"] = credentials.UserName;
      _parameters["password"] = credentials.Password;
      _parameters["uri"] = credentials.Domain;
      _nonceCount = nonceCount;
      if (scheme == AuthenticationSchemes.Digest)
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
        return _parameters["algorithm"];
      }
    }

    public string Cnonce {
      get {
        return _parameters["cnonce"];
      }
    }

    public string Nc {
      get {
        return _parameters["nc"];
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

    public string Password {
      get {
        return _parameters["password"];
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

    public string Response {
      get {
        return _parameters["response"];
      }
    }

    public AuthenticationSchemes Scheme {
      get {
        return _scheme;
      }
    }

    public string Uri {
      get {
        return _parameters["uri"];
      }
    }

    public string UserName {
      get {
        return _parameters["username"];
      }
    }

    #endregion

    #region Private Methods

    private static string createA1 (string username, string password, string realm)
    {
      return String.Format ("{0}:{1}:{2}", username, realm, password);
    }

    private static string createA1 (
      string username, string password, string realm, string nonce, string cnonce)
    {
      return String.Format (
        "{0}:{1}:{2}", hash (createA1 (username, password, realm)), nonce, cnonce);
    }

    private static string createA2 (string method, string uri)
    {
      return String.Format ("{0}:{1}", method, uri);
    }

    private static string createA2 (string method, string uri, string entity)
    {
      return String.Format ("{0}:{1}:{2}", method, uri, entity);
    }

    private static string hash (string value)
    {
      var src = Encoding.UTF8.GetBytes (value);
      var md5 = MD5.Create ();
      var hashed = md5.ComputeHash (src);

      var res = new StringBuilder (64);
      foreach (var b in hashed)
        res.Append (b.ToString ("x2"));

      return res.ToString ();
    }

    private void initAsDigest ()
    {
      var qops = _parameters["qop"];
      if (qops != null) {
        if (qops.Split (',').Contains (qop => qop.Trim ().ToLower () == "auth")) {
          _parameters["qop"] = "auth";
          _parameters["nc"] = String.Format ("{0:x8}", ++_nonceCount);
          _parameters["cnonce"] = CreateNonceValue ();
        }
        else {
          _parameters["qop"] = null;
        }
      }

      _parameters["method"] = "GET";
      _parameters["response"] = CreateRequestDigest (_parameters);
    }

    #endregion

    #region Internal Methods

    internal static string CreateNonceValue ()
    {
      var src = new byte[16];
      var rand = new Random ();
      rand.NextBytes (src);

      var res = new StringBuilder (32);
      foreach (var b in src)
        res.Append (b.ToString ("x2"));

      return res.ToString ();
    }

    internal static string CreateRequestDigest (NameValueCollection parameters)
    {
      var username = parameters["username"];
      var password = parameters["password"];
      var realm = parameters["realm"];
      var nonce = parameters["nonce"];
      var uri = parameters["uri"];
      var algorithm = parameters["algorithm"];
      var qop = parameters["qop"];
      var nc = parameters["nc"];
      var cnonce = parameters["cnonce"];
      var method = parameters["method"];

      var a1 = algorithm != null && algorithm.ToLower () == "md5-sess"
               ? createA1 (username, password, realm, nonce, cnonce)
               : createA1 (username, password, realm);

      var a2 = qop != null && qop.ToLower () == "auth-int"
               ? createA2 (method, uri, parameters["entity"])
               : createA2 (method, uri);

      var secret = hash (a1);
      var data = qop != null
                 ? String.Format ("{0}:{1}:{2}:{3}:{4}", nonce, nc, cnonce, qop, hash (a2))
                 : String.Format ("{0}:{1}", nonce, hash (a2));

      return hash (String.Format ("{0}:{1}", secret, data));
    }

    internal static AuthenticationResponse Parse (string value)
    {
      try {
        var cred = value.Split (new[] { ' ' }, 2);
        if (cred.Length != 2)
          return null;

        var scheme = cred[0].ToLower ();
        return scheme == "basic"
               ? new AuthenticationResponse (
                   AuthenticationSchemes.Basic, ParseBasicCredentials (cred[1]))
               : scheme == "digest"
                 ? new AuthenticationResponse (
                     AuthenticationSchemes.Digest, ParseParameters (cred[1]))
                 : null;
      }
      catch {
      }

      return null;
    }

    internal static NameValueCollection ParseBasicCredentials (string value)
    {
      // Decode the basic-credentials (a Base64 encoded string).
      var userPass = Encoding.Default.GetString (Convert.FromBase64String (value));

      // The format is [<domain>\]<username>:<password>.
      var i = userPass.IndexOf (':');
      var user = userPass.Substring (0, i);
      var pass = i < userPass.Length - 1 ? userPass.Substring (i + 1) : String.Empty;

      // Check if 'domain' exists.
      i = user.IndexOf ('\\');
      if (i > -1)
        user = user.Substring (i + 1);

      var res = new NameValueCollection ();
      res["username"] = user;
      res["password"] = pass;

      return res;
    }

    internal static NameValueCollection ParseParameters (string value)
    {
      var res = new NameValueCollection ();
      foreach (var param in value.SplitHeaderValue (',')) {
        var i = param.IndexOf ('=');
        var name = i > 0 ? param.Substring (0, i).Trim () : null;
        var val = i < 0
                  ? param.Trim ().Trim ('"')
                  : i < param.Length - 1
                    ? param.Substring (i + 1).Trim ().Trim ('"')
                    : String.Empty;

        res.Add (name, val);
      }

      return res;
    }

    internal string ToBasicString ()
    {
      var userPass = String.Format ("{0}:{1}", _parameters["username"], _parameters["password"]);
      var cred = Convert.ToBase64String (Encoding.UTF8.GetBytes (userPass));

      return "Basic " + cred;
    }

    internal string ToDigestString ()
    {
      var res = new StringBuilder (64);
      res.AppendFormat ("username=\"{0}\"", _parameters["username"]);
      res.AppendFormat (", realm=\"{0}\"", _parameters["realm"]);
      res.AppendFormat (", nonce=\"{0}\"", _parameters["nonce"]);
      res.AppendFormat (", uri=\"{0}\"", _parameters["uri"]);

      var algorithm = _parameters["algorithm"];
      if (algorithm != null)
        res.AppendFormat (", algorithm={0}", algorithm);

      res.AppendFormat (", response=\"{0}\"", _parameters["response"]);

      var qop = _parameters["qop"];
      if (qop != null) {
        res.AppendFormat (", qop={0}", qop);
        res.AppendFormat (", nc={0}", _parameters["nc"]);
        res.AppendFormat (", cnonce=\"{0}\"", _parameters["cnonce"]);
      }

      var opaque = _parameters["opaque"];
      if (opaque != null)
        res.AppendFormat (", opaque=\"{0}\"", opaque);

      return "Digest " + res.ToString ();
    }

    #endregion

    #region Public Methods

    public IIdentity ToIdentity ()
    {
      return _scheme == AuthenticationSchemes.Basic
             ? new HttpBasicIdentity (_parameters["username"], _parameters["password"]) as IIdentity
             : _scheme == AuthenticationSchemes.Digest
               ? new HttpDigestIdentity (_parameters)
               : null;
    }

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
