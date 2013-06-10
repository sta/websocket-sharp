#region License
/*
 * AuthenticationChallenge.cs
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
using System.Text;

namespace WebSocketSharp {

  internal class AuthenticationChallenge {

    #region Private Fields

    private string _algorithm;
    private string _domain;
    private string _nonce;
    private string _opaque;
    private string _qop;
    private string _realm;
    private string _scheme;
    private string _stale;

    #endregion

    #region Private Constructors

    private AuthenticationChallenge()
    {
    }

    #endregion

    #region Public Properties

    public string Algorithm {
      get {
        return _algorithm ?? String.Empty;
      }

      private set {
        _algorithm = value;
      }
    }

    public string Domain {
      get {
        return _domain ?? String.Empty;
      }

      private set {
        _domain = value;
      }
    }

    public string Nonce {
      get {
        return _nonce ?? String.Empty;
      }

      private set {
        _nonce = value;
      }
    }

    public string Opaque {
      get {
        return _opaque ?? String.Empty;
      }

      private set {
        _opaque = value;
      }
    }

    public string Qop {
      get {
        return _qop ?? String.Empty;
      }

      private set {
        _qop = value;
      }
    }

    public string Realm {
      get {
        return _realm ?? String.Empty;
      }

      private set {
        _realm = value;
      }
    }

    public string Scheme {
      get {
        return _scheme ?? String.Empty;
      }

      private set {
        _scheme = value;
      }
    }

    public string Stale {
      get {
        return _stale ?? String.Empty;
      }

      private set {
        _stale = value;
      }
    }

    #endregion

    #region Public Methods

    public static AuthenticationChallenge Parse(string challenge)
    {
      var authChallenge = new AuthenticationChallenge();
      if (challenge.StartsWith("basic", StringComparison.OrdinalIgnoreCase))
      {
        authChallenge.Scheme = "Basic";
        authChallenge.Realm = challenge.Substring(6).GetValueInternal("=").Trim('"');

        return authChallenge;
      }

      foreach (var p in challenge.SplitHeaderValue(','))
      {
        var param = p.Trim();
        if (param.StartsWith("digest", StringComparison.OrdinalIgnoreCase))
        {
          authChallenge.Scheme = "Digest";
          authChallenge.Realm = param.Substring(7).GetValueInternal("=").Trim('"');

          continue;
        }

        var value = param.GetValueInternal("=").Trim('"');
        if (param.StartsWith("domain", StringComparison.OrdinalIgnoreCase))
        {
          authChallenge.Domain = value;
          continue;
        }

        if (param.StartsWith("nonce", StringComparison.OrdinalIgnoreCase))
        {
          authChallenge.Nonce = value;
          continue;
        }

        if (param.StartsWith("opaque", StringComparison.OrdinalIgnoreCase))
        {
          authChallenge.Opaque = value;
          continue;
        }

        if (param.StartsWith("stale", StringComparison.OrdinalIgnoreCase))
        {
          authChallenge.Stale = value;
          continue;
        }

        if (param.StartsWith("algorithm", StringComparison.OrdinalIgnoreCase))
        {
          authChallenge.Algorithm = value;
          continue;
        }

        if (param.StartsWith("qop", StringComparison.OrdinalIgnoreCase))
          authChallenge.Qop = value;
      }

      return authChallenge;
    }

    #endregion
  }
}
