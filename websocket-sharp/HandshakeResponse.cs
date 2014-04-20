#region License
/*
 * HandshakeResponse.cs
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
  internal class HandshakeResponse : HandshakeBase
  {
    #region Private Fields

    private string _code;
    private string _reason;

    #endregion

    #region Private Constructors

    private HandshakeResponse ()
    {
    }

    #endregion

    #region Public Constructors

    public HandshakeResponse (HttpStatusCode code)
    {
      _code = ((int) code).ToString ();
      _reason = code.GetDescription ();

      var headers = Headers;
      headers ["Server"] = "websocket-sharp/1.0";
      if (code == HttpStatusCode.SwitchingProtocols) {
        headers ["Upgrade"] = "websocket";
        headers ["Connection"] = "Upgrade";
      }
    }

    #endregion

    #region Public Properties

    public AuthenticationChallenge AuthChallenge {
      get {
        var challenge = Headers ["WWW-Authenticate"];
        return challenge != null && challenge.Length > 0
               ? AuthenticationChallenge.Parse (challenge)
               : null;
      }
    }

    public CookieCollection Cookies {
      get {
        return Headers.GetCookies (true);
      }
    }

    public bool IsUnauthorized {
      get {
        return _code == "401";
      }
    }

    public bool IsWebSocketResponse {
      get {
        var headers = Headers;
        return ProtocolVersion >= HttpVersion.Version11 &&
               _code == "101" &&
               headers.Contains ("Upgrade", "websocket") &&
               headers.Contains ("Connection", "Upgrade");
      }
    }

    public string Reason {
      get {
        return _reason;
      }

      private set {
        _reason = value;
      }
    }

    public string StatusCode {
      get {
        return _code;
      }

      private set {
        _code = value;
      }
    }

    #endregion

    #region Public Methods

    public static HandshakeResponse CreateCloseResponse (HttpStatusCode code)
    {
      var res = new HandshakeResponse (code);
      res.Headers ["Connection"] = "close";

      return res;
    }

    public static HandshakeResponse Parse (string [] headerParts)
    {
      var statusLine = headerParts [0].Split (new char [] { ' ' }, 3);
      if (statusLine.Length != 3)
        throw new ArgumentException ("Invalid status line: " + headerParts [0]);

      var headers = new WebHeaderCollection ();
      for (int i = 1; i < headerParts.Length; i++)
        headers.SetInternally (headerParts [i], true);

      return new HandshakeResponse {
        Headers = headers,
        ProtocolVersion = new Version (statusLine [0].Substring (5)),
        Reason = statusLine [2],
        StatusCode = statusLine [1]
      };
    }

    public void SetCookies (CookieCollection cookies)
    {
      if (cookies == null || cookies.Count == 0)
        return;

      var headers = Headers;
      foreach (var cookie in cookies.Sorted)
        headers.Add ("Set-Cookie", cookie.ToResponseString ());
    }

    public override string ToString ()
    {
      var buffer = new StringBuilder (64);
      buffer.AppendFormat (
        "HTTP/{0} {1} {2}{3}", ProtocolVersion, _code, _reason, CrLf);

      var headers = Headers;
      foreach (var key in headers.AllKeys)
        buffer.AppendFormat ("{0}: {1}{2}", key, headers [key], CrLf);

      buffer.Append (CrLf);

      var entity = EntityBody;
      if (entity.Length > 0)
        buffer.Append (entity);

      return buffer.ToString ();
    }

    #endregion
  }
}
