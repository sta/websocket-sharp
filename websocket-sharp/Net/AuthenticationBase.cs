#region License
/*
 * AuthenticationBase.cs
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
using System.Text;

namespace WebSocketSharp.Net
{
  internal abstract class AuthenticationBase
  {
    #region Private Fields

    private AuthenticationSchemes _scheme;

    #endregion

    #region Internal Fields

    internal NameValueCollection Parameters;

    #endregion

    #region Protected Constructors

    protected AuthenticationBase (
      AuthenticationSchemes scheme, NameValueCollection parameters
    )
    {
      _scheme = scheme;
      Parameters = parameters;
    }

    #endregion

    #region Public Properties

    public string Algorithm {
      get {
        return Parameters["algorithm"];
      }
    }

    public string Nonce {
      get {
        return Parameters["nonce"];
      }
    }

    public string Opaque {
      get {
        return Parameters["opaque"];
      }
    }

    public string Qop {
      get {
        return Parameters["qop"];
      }
    }

    public string Realm {
      get {
        return Parameters["realm"];
      }
    }

    public AuthenticationSchemes Scheme {
      get {
        return _scheme;
      }
    }

    #endregion

    #region Internal Methods

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

    internal abstract string ToBasicString ();

    internal abstract string ToDigestString ();

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
