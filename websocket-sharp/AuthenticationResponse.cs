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
using System.Security.Cryptography;
using System.Text;

namespace WebSocketSharp {

  internal class AuthenticationResponse {

    #region Private Fields

    private string _algorithm;
    private string _cnonce;
    private string _method;
    private string _nc;
    private string _nonce;
    private string _opaque;
    private string _password;
    private string _qop;
    private string _realm;
    private string _response;
    private string _scheme;
    private string _uri;
    private string _userName;

    #endregion

    #region Private Constructors

    private AuthenticationResponse()
    {
    }

    #endregion

    #region Public Constructors

    public AuthenticationResponse(WsCredential credential)
    {
      _userName = credential.UserName;
      _password = credential.Password;
      _scheme = "Basic";
    }

    public AuthenticationResponse(WsCredential credential, AuthenticationChallenge challenge)
    {
      _userName = credential.UserName;
      _password = credential.Password;
      _scheme = challenge.Scheme;
      _realm = challenge.Realm;
      if (_scheme == "Digest")
        initForDigest(credential, challenge);
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

    public string Cnonce {
      get {
        return _cnonce ?? String.Empty;
      }

      private set {
        _cnonce = value;
      }
    }

    public string Nc {
      get {
        return _nc ?? String.Empty;
      }

      private set {
        _nc = value;
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

    public string Response {
      get {
        return _response ?? String.Empty;
      }

      private set {
        _response = value;
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

    public string Uri {
      get {
        return _uri ?? String.Empty;
      }

      private set {
        _uri = value;
      }
    }

    public string UserName {
      get {
        return _userName ?? String.Empty;
      }

      private set {
        _userName = value;
      }
    }

    #endregion

    #region Private Methods

    private string a1()
    {
      var result = String.Format("{0}:{1}:{2}", _userName, _realm, _password);
      return _algorithm != null && _algorithm.ToLower() == "md5-sess"
             ? String.Format("{0}:{1}:{2}", hash(result), _nonce, _cnonce)
             : result;
    }

    private string a2()
    {
      return String.Format("{0}:{1}", _method, _uri);
    }

    private static string createNonceValue()
    {
      var src = new byte[16];
      var rand = new Random();
      rand.NextBytes(src);
      var nonce = new StringBuilder(32);
      foreach (var b in src)
        nonce.Append(b.ToString("x2"));

      return nonce.ToString();
    }

    private string createRequestDigest()
    {
      if (Qop == "auth")
      {
        var data = String.Format("{0}:{1}:{2}:{3}:{4}",
          _nonce, _nc, _cnonce, _qop, hash(a2()));
        return kd(hash(a1()), data);
      }

      return kd(hash(a1()), String.Format("{0}:{1}", _nonce, hash(a2())));
    }

    private static string hash(string value)
    {
      var md5 = MD5.Create();
      var src = Encoding.UTF8.GetBytes(value);
      var hashed = md5.ComputeHash(src);
      var result = new StringBuilder(64);
      foreach (var b in hashed)
        result.Append(b.ToString("x2"));

      return result.ToString();
    }

    private void initForDigest(WsCredential credential, AuthenticationChallenge challenge)
    {
      _nonce = challenge.Nonce;
      _method = "GET";
      _uri = credential.Domain;
      _algorithm = challenge.Algorithm;
      _opaque = challenge.Opaque;
      foreach (var qop in challenge.Qop.Split(','))
      {
        if (qop.Trim().ToLower() == "auth")
        {
          _qop = "auth";
          _nc = "00000001";
          break;
        }
      }

      _cnonce = createNonceValue();
      _response = createRequestDigest();
    }

    private static string kd(string secret, string data)
    {
      var concatenated = String.Format("{0}:{1}", secret, data);
      return hash(concatenated);
    }

    private string toBasicCredentials()
    {
      var userPass = String.Format("{0}:{1}", _userName, _password);
      var base64UserPass = Convert.ToBase64String(Encoding.UTF8.GetBytes(userPass));

      return "Basic " + base64UserPass;
    }

    private string toDigestCredentials()
    {
      var digestResponse = new StringBuilder(64);
      digestResponse.AppendFormat("username={0}", _userName.Quote());
      digestResponse.AppendFormat(", realm={0}", _realm.Quote());
      digestResponse.AppendFormat(", nonce={0}", _nonce.Quote());
      digestResponse.AppendFormat(", uri={0}", _uri.Quote());
      digestResponse.AppendFormat(", response={0}", _response.Quote());
      if (!_algorithm.IsNullOrEmpty())
        digestResponse.AppendFormat(", algorithm={0}", _algorithm);

      if (!_opaque.IsNullOrEmpty())
        digestResponse.AppendFormat(", opaque={0}", _opaque.Quote());

      if (!_qop.IsNullOrEmpty())
        digestResponse.AppendFormat(", qop={0}", _qop);

      if (!_nc.IsNullOrEmpty())
        digestResponse.AppendFormat(", nc={0}", _nc);

      if (!_qop.IsNullOrEmpty())
        digestResponse.AppendFormat(", cnonce={0}", _cnonce.Quote());

      return "Digest " + digestResponse.ToString();
    }

    #endregion

    #region Public Methods

    public static AuthenticationResponse Parse(string response)
    {
      throw new NotImplementedException();
    }

    public override string ToString()
    {
      return _scheme == "Basic"
             ? toBasicCredentials()
             : toDigestCredentials();
    }

    #endregion
  }
}
