#region License
/*
 * AuthenticationChallenge.cs
 *
 * The MIT License
 *
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
using System.Text;

namespace WebSocketSharp.Net
{
  internal class AuthenticationChallenge
  {
    #region Private Fields

    private NameValueCollection   _parameters;
    private AuthenticationSchemes _scheme;

    #endregion

    #region Private Constructors

    private AuthenticationChallenge (
      AuthenticationSchemes scheme, NameValueCollection parameters
    )
    {
      _scheme = scheme;
      _parameters = parameters;
    }

    #endregion

    #region Internal Constructors

    internal AuthenticationChallenge (
      AuthenticationSchemes scheme, string realm
    )
      : this (scheme, new NameValueCollection ())
    {
      _parameters["realm"] = realm;

      if (scheme == AuthenticationSchemes.Digest) {
        _parameters["nonce"] = CreateNonceValue ();
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

    internal static string CreateNonceValue ()
    {
      var rand = new Random ();
      var bytes = new byte[16];

      rand.NextBytes (bytes);

      var buff = new StringBuilder (32);

      foreach (var b in bytes)
        buff.Append (b.ToString ("x2"));

      return buff.ToString ();
    }

    internal static AuthenticationChallenge Parse (string value)
    {
      var chal = value.Split (new[] { ' ' }, 2);

      if (chal.Length != 2)
        return null;

      var schm = chal[0].ToLower ();

      if (schm == "basic") {
        var parameters = ParseParameters (chal[1]);

        return new AuthenticationChallenge (
                 AuthenticationSchemes.Basic, parameters
               );
      }

      if (schm == "digest") {
        var parameters = ParseParameters (chal[1]);

        return new AuthenticationChallenge (
                 AuthenticationSchemes.Digest, parameters
               );
      }

      return null;
    }

    internal static NameValueCollection ParseParameters (string value)
    {
      var ret = new NameValueCollection ();

      foreach (var param in value.SplitHeaderValue (',')) {
        var i = param.IndexOf ('=');

        var name = i > 0 ? param.Substring (0, i).Trim () : null;
        var val = i < 0
                  ? param.Trim ().Trim ('"')
                  : i < param.Length - 1
                    ? param.Substring (i + 1).Trim ().Trim ('"')
                    : String.Empty;

        ret.Add (name, val);
      }

      return ret;
    }

    internal string ToBasicString ()
    {
      return String.Format ("Basic realm=\"{0}\"", _parameters["realm"]);
    }

    internal string ToDigestString ()
    {
      var buff = new StringBuilder (128);

      var domain = _parameters["domain"];
      var realm = _parameters["realm"];
      var nonce = _parameters["nonce"];

      if (domain != null) {
        buff.AppendFormat (
          "Digest realm=\"{0}\", domain=\"{1}\", nonce=\"{2}\"",
          realm,
          domain,
          nonce
        );
      }
      else {
        buff.AppendFormat ("Digest realm=\"{0}\", nonce=\"{1}\"", realm, nonce);
      }

      var opaque = _parameters["opaque"];

      if (opaque != null)
        buff.AppendFormat (", opaque=\"{0}\"", opaque);

      var stale = _parameters["stale"];

      if (stale != null)
        buff.AppendFormat (", stale={0}", stale);

      var algo = _parameters["algorithm"];

      if (algo != null)
        buff.AppendFormat (", algorithm={0}", algo);

      var qop = _parameters["qop"];

      if (qop != null)
        buff.AppendFormat (", qop=\"{0}\"", qop);

      return buff.ToString ();
    }

    #endregion

    #region Public Methods

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
