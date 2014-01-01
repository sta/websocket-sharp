#region License
/*
 * HandshakeRequest.cs
 *
 * The MIT License
 *
 * Copyright (c) 2012-2014 sta.blockhead
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
using System.Linq;
using System.Text;
using WebSocketSharp.Net;

namespace WebSocketSharp
{
  internal class HandshakeRequest : HandshakeBase
  {
    #region Private Fields

    private string              _method;
    private NameValueCollection _queryString;
    private string              _rawUrl;
    private Uri                 _uri;

    #endregion

    #region Private Constructors

    private HandshakeRequest ()
    {
    }

    #endregion

    #region Public Constructors

    public HandshakeRequest (string uriString)
    {
      _method = "GET";
      _uri = uriString.ToUri ();
      _rawUrl = _uri.IsAbsoluteUri
              ? _uri.PathAndQuery
              : uriString;

      var headers = Headers;
      headers ["User-Agent"] = "websocket-sharp/1.0";
      headers ["Upgrade"] = "websocket";
      headers ["Connection"] = "Upgrade";
    }

    #endregion

    #region Public Properties

    public AuthenticationResponse AuthResponse {
      get {
        var response = Headers ["Authorization"];
        return !response.IsNullOrEmpty ()
               ? AuthenticationResponse.Parse (response)
               : null;
      }
    }

    public CookieCollection Cookies {
      get {
        return Headers.GetCookies (false);
      }
    }

    public string HttpMethod {
      get {
        return _method;
      }

      private set {
        _method = value;
      }
    }

    public bool IsWebSocketRequest {
      get {
        var headers = Headers;
        return _method == "GET" &&
               ProtocolVersion >= HttpVersion.Version11 &&
               headers.Contains ("Upgrade", "websocket") &&
               headers.Contains ("Connection", "Upgrade");
      }
    }

    public NameValueCollection QueryString {
      get {
        if (_queryString == null) {
          _queryString = new NameValueCollection ();
          var i = RawUrl.IndexOf ('?');
          if (i > 0) {
            var query = RawUrl.Substring (i + 1);
            var components = query.Split ('&');
            foreach (var c in components) {
              var nv = c.GetNameAndValue ("=");
              if (nv.Key != null) {
                var name = nv.Key.UrlDecode ();
                var val = nv.Value.UrlDecode ();
                _queryString.Add (name, val);
              }
            }
          }
        }

        return _queryString;
      }
    }

    public string RawUrl {
      get {
        return _rawUrl;
      }

      private set {
        _rawUrl = value;
      }
    }

    public Uri RequestUri {
      get {
        return _uri;
      }

      private set {
        _uri = value;
      }
    }

    #endregion

    #region Public Methods

    public static HandshakeRequest Parse (string [] request)
    {
      var requestLine = request [0].Split (' ');
      if (requestLine.Length != 3)
        throw new ArgumentException (
          "Invalid HTTP Request-Line: " + request [0], "request");

      var headers = new WebHeaderCollection ();
      for (int i = 1; i < request.Length; i++)
        headers.SetInternal (request [i], false);

      return new HandshakeRequest {
        Headers = headers,
        HttpMethod = requestLine [0],
        ProtocolVersion = new Version (requestLine [2].Substring (5)),
        RawUrl = requestLine [1],
        RequestUri = requestLine [1].ToUri ()
      };
    }

    public void SetCookies (CookieCollection cookies)
    {
      if (cookies == null || cookies.Count == 0)
        return;

      var sorted = cookies.Sorted.ToArray ();
      var header = new StringBuilder (sorted [0].ToString (), 64);
      for (int i = 1; i < sorted.Length; i++)
        if (!sorted [i].Expired)
          header.AppendFormat ("; {0}", sorted [i].ToString ());

      Headers ["Cookie"] = header.ToString ();
    }

    public override string ToString ()
    {
      var buffer = new StringBuilder (64);
      buffer.AppendFormat ("{0} {1} HTTP/{2}{3}", _method, _rawUrl, ProtocolVersion, CrLf);

      var headers = Headers;
      foreach (var key in headers.AllKeys)
        buffer.AppendFormat ("{0}: {1}{2}", key, headers [key], CrLf);

      buffer.Append (CrLf);
      return buffer.ToString ();
    }

    #endregion
  }
}
