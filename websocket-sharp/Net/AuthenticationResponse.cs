#region License
/*
 * AuthenticationResponse.cs
 *
 * ParseBasicCredentials is derived from HttpListenerContext.cs (System.Net) of
 * Mono (http://www.mono-project.com).
 *
 * The MIT License
 *
 * Copyright (c) 2005 Novell, Inc. (http://www.novell.com)
 * Copyright (c) 2013-2023 sta.blockhead
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

namespace WebSocketSharp.Net
{
  internal class AuthenticationResponse
  {
    #region Private Fields

    private uint                  _nonceCount;
    private NameValueCollection   _parameters;
    private AuthenticationSchemes _scheme;

    #endregion

    #region Private Constructors

    private AuthenticationResponse (
      AuthenticationSchemes scheme, NameValueCollection parameters
    )
    {
      _scheme = scheme;
      _parameters = parameters;
    }

    #endregion

    #region Internal Constructors

    internal AuthenticationResponse (NetworkCredential credentials)
      : this (
          AuthenticationSchemes.Basic,
          new NameValueCollection (),
          credentials,
          0
        )
    {
    }

    internal AuthenticationResponse (
      AuthenticationChallenge challenge,
      NetworkCredential credentials,
      uint nonceCount
    )
      : this (challenge.Scheme, challenge.Parameters, credentials, nonceCount)
    {
    }

    internal AuthenticationResponse (
      AuthenticationSchemes scheme,
      NameValueCollection parameters,
      NetworkCredential credentials,
      uint nonceCount
    )
      : this (scheme, parameters)
    {
      _parameters["username"] = credentials.Username;
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

    private static string createA1 (
      string username, string password, string realm
    )
    {
      return String.Format ("{0}:{1}:{2}", username, realm, password);
    }

    private static string createA1 (
      string username,
      string password,
      string realm,
      string nonce,
      string cnonce
    )
    {
      var a1 = createA1 (username, password, realm);

      return String.Format ("{0}:{1}:{2}", hash (a1), nonce, cnonce);
    }

    private static string createA2 (string method, string uri)
    {
      return String.Format ("{0}:{1}", method, uri);
    }

    private static string createA2 (string method, string uri, string entity)
    {
      return String.Format ("{0}:{1}:{2}", method, uri, hash (entity));
    }

    private static string hash (string value)
    {
      var md5 = MD5.Create ();

      var bytes = Encoding.UTF8.GetBytes (value);
      var res = md5.ComputeHash (bytes);

      var buff = new StringBuilder (64);

      foreach (var b in res)
        buff.Append (b.ToString ("x2"));

      return buff.ToString ();
    }

    private void initAsDigest ()
    {
      var qops = _parameters["qop"];

      if (qops != null) {
        var auth = qops.Split (',').Contains (
                     qop => qop.Trim ().ToLower () == "auth"
                   );

        if (auth) {
          _parameters["qop"] = "auth";
          _parameters["cnonce"] = AuthenticationChallenge.CreateNonceValue ();
          _parameters["nc"] = String.Format ("{0:x8}", ++_nonceCount);
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

    internal static string CreateRequestDigest (NameValueCollection parameters)
    {
      var user = parameters["username"];
      var pass = parameters["password"];
      var realm = parameters["realm"];
      var nonce = parameters["nonce"];
      var uri = parameters["uri"];
      var algo = parameters["algorithm"];
      var qop = parameters["qop"];
      var cnonce = parameters["cnonce"];
      var nc = parameters["nc"];
      var method = parameters["method"];

      var a1 = algo != null && algo.ToLower () == "md5-sess"
               ? createA1 (user, pass, realm, nonce, cnonce)
               : createA1 (user, pass, realm);

      var a2 = qop != null && qop.ToLower () == "auth-int"
               ? createA2 (method, uri, parameters["entity"])
               : createA2 (method, uri);

      var secret = hash (a1);
      var data = qop != null
                 ? String.Format (
                     "{0}:{1}:{2}:{3}:{4}", nonce, nc, cnonce, qop, hash (a2)
                   )
                 : String.Format ("{0}:{1}", nonce, hash (a2));

      var keyed = String.Format ("{0}:{1}", secret, data);

      return hash (keyed);
    }

    internal static AuthenticationResponse Parse (string value)
    {
      try {
        var cred = value.Split (new[] { ' ' }, 2);

        if (cred.Length != 2)
          return null;

        var schm = cred[0].ToLower ();

        if (schm == "basic") {
          var parameters = ParseBasicCredentials (cred[1]);

          return new AuthenticationResponse (
                   AuthenticationSchemes.Basic, parameters
                 );
        }

        if (schm == "digest") {
          var parameters = AuthenticationChallenge.ParseParameters (cred[1]);

          return new AuthenticationResponse (
                   AuthenticationSchemes.Digest, parameters
                 );
        }

        return null;
      }
      catch {
        return null;
      }
    }

    internal static NameValueCollection ParseBasicCredentials (string value)
    {
      var ret = new NameValueCollection ();

      // Decode the basic-credentials (a Base64 encoded string).

      var bytes = Convert.FromBase64String (value);
      var userPass = Encoding.Default.GetString (bytes);

      // The format is [<domain>\]<username>:<password>.

      var i = userPass.IndexOf (':');
      var user = userPass.Substring (0, i);
      var pass = i < userPass.Length - 1
                 ? userPass.Substring (i + 1)
                 : String.Empty;

      // Check if <domain> exists.

      i = user.IndexOf ('\\');

      if (i > -1)
        user = user.Substring (i + 1);

      ret["username"] = user;
      ret["password"] = pass;

      return ret;
    }

    internal string ToBasicString ()
    {
      var user = _parameters["username"];
      var pass = _parameters["password"];
      var userPass = String.Format ("{0}:{1}", user, pass);

      var bytes = Encoding.UTF8.GetBytes (userPass);
      var cred = Convert.ToBase64String (bytes);

      return "Basic " + cred;
    }

    internal string ToDigestString ()
    {
      var buff = new StringBuilder (256);

      var user = _parameters["username"];
      var realm = _parameters["realm"];
      var nonce = _parameters["nonce"];
      var uri = _parameters["uri"];
      var res = _parameters["response"];

      buff.AppendFormat (
        "Digest username=\"{0}\", realm=\"{1}\", nonce=\"{2}\", uri=\"{3}\", response=\"{4}\"",
        user,
        realm,
        nonce,
        uri,
        res
      );

      var opaque = _parameters["opaque"];

      if (opaque != null)
        buff.AppendFormat (", opaque=\"{0}\"", opaque);

      var algo = _parameters["algorithm"];

      if (algo != null)
        buff.AppendFormat (", algorithm={0}", algo);

      var qop = _parameters["qop"];

      if (qop != null) {
        var cnonce = _parameters["cnonce"];
        var nc = _parameters["nc"];

        buff.AppendFormat (
          ", qop={0}, cnonce=\"{1}\", nc={2}", qop, cnonce, nc
        );
      }

      return buff.ToString ();
    }

    #endregion

    #region Public Methods

    public IIdentity ToIdentity ()
    {
      if (_scheme == AuthenticationSchemes.Basic) {
        var user = _parameters["username"];
        var pass = _parameters["password"];

        return new HttpBasicIdentity (user, pass);
      }

      if (_scheme == AuthenticationSchemes.Digest)
        return new HttpDigestIdentity (_parameters);

      return null;
    }

    public override string ToString ()
    {
      if (_scheme == AuthenticationSchemes.Basic)
        return ToBasicString ();

      if (_scheme == AuthenticationSchemes.Digest)
        return ToDigestString ();

      return String.Empty;
    }

    #endregion
  }
}
