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
using System.Text;
using WebSocketSharp.Net;

namespace WebSocketSharp
{
  internal class HandshakeRequest : HandshakeBase
  {
    #region Private Fields

    private string _method;
    private string _uri;
    private bool   _websocketRequest;
    private bool   _websocketRequestWasSet;

    #endregion

    #region Private Constructors

    private HandshakeRequest ()
    {
    }

    #endregion

    #region Public Constructors

    public HandshakeRequest (string absPathAndQuery)
    {
      _uri = absPathAndQuery;
      _method = "GET";

      var headers = Headers;
      headers ["User-Agent"] = "websocket-sharp/1.0";
      headers ["Upgrade"] = "websocket";
      headers ["Connection"] = "Upgrade";
    }

    #endregion

    #region Public Properties

    public AuthenticationResponse AuthResponse {
      get {
        var auth = Headers ["Authorization"];
        return auth != null && auth.Length > 0
               ? AuthenticationResponse.Parse (auth)
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
        if (!_websocketRequestWasSet) {
          var headers = Headers;
          _websocketRequest = _method == "GET" &&
                              ProtocolVersion > HttpVersion.Version10 &&
                              headers.Contains ("Upgrade", "websocket") &&
                              headers.Contains ("Connection", "Upgrade");

          _websocketRequestWasSet = true;
        }

        return _websocketRequest;
      }
    }

    public string RequestUri {
      get {
        return _uri;
      }

      private set {
        _uri = value;
      }
    }

    #endregion

    #region Public Methods

    public static HandshakeRequest Parse (string [] headerParts)
    {
      var requestLine = headerParts [0].Split (new [] { ' ' }, 3);
      if (requestLine.Length != 3)
        throw new ArgumentException ("Invalid request line: " + headerParts [0]);

      var headers = new WebHeaderCollection ();
      for (int i = 1; i < headerParts.Length; i++)
        headers.SetInternally (headerParts [i], false);

      return new HandshakeRequest {
        Headers = headers,
        HttpMethod = requestLine [0],
        ProtocolVersion = new Version (requestLine [2].Substring (5)),
        RequestUri = requestLine [1]
      };
    }

    public void SetCookies (CookieCollection cookies)
    {
      if (cookies == null || cookies.Count == 0)
        return;

      var buff = new StringBuilder (64);
      foreach (var cookie in cookies.Sorted)
        if (!cookie.Expired)
          buff.AppendFormat ("{0}; ", cookie.ToString ());

      var len = buff.Length;
      if (len > 2) {
        buff.Length = len - 2;
        Headers ["Cookie"] = buff.ToString ();
      }
    }

    public override string ToString ()
    {
      var buff = new StringBuilder (64);
      buff.AppendFormat ("{0} {1} HTTP/{2}{3}", _method, _uri, ProtocolVersion, CrLf);

      var headers = Headers;
      foreach (var key in headers.AllKeys)
        buff.AppendFormat ("{0}: {1}{2}", key, headers [key], CrLf);

      buff.Append (CrLf);

      var entity = EntityBody;
      if (entity.Length > 0)
        buff.Append (entity);

      return buff.ToString ();
    }

    #endregion
  }
}
