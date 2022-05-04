#region License
/*
 * HttpResponse.cs
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
using System.IO;
using System.Text;
using WebSocketSharp.Net;

namespace WebSocketSharp
{
  internal class HttpResponse : HttpBase
  {
    #region Private Fields

    private int    _code;
    private string _reason;

    #endregion

    #region Private Constructors

    private HttpResponse (
      int code, string reason, Version version, NameValueCollection headers
    )
      : base (version, headers)
    {
      _code = code;
      _reason = reason;
    }

    #endregion

    #region Internal Constructors

    internal HttpResponse (HttpStatusCode code)
      : this (code, code.GetDescription ())
    {
    }

    internal HttpResponse (HttpStatusCode code, string reason)
      : this (
          (int) code,
          reason,
          HttpVersion.Version11,
          new NameValueCollection ()
        )
    {
      Headers["Server"] = "websocket-sharp/1.0";
    }

    #endregion

    #region Public Properties

    public CookieCollection Cookies {
      get {
        return Headers.GetCookies (true);
      }
    }

    public bool CloseConnection {
      get {
        var compType = StringComparison.OrdinalIgnoreCase;

        return Headers.Contains ("Connection", "close", compType);
      }
    }

    public bool IsProxyAuthenticationRequired {
      get {
        return _code == 407;
      }
    }

    public bool IsRedirect {
      get {
        return _code == 301 || _code == 302;
      }
    }

    public bool IsSuccess {
      get {
        return _code >= 200 && _code <= 299;
      }
    }

    public bool IsUnauthorized {
      get {
        return _code == 401;
      }
    }

    public bool IsWebSocketResponse {
      get {
        return ProtocolVersion > HttpVersion.Version10
               && _code == 101
               && Headers.Upgrades ("websocket");
      }
    }

    public string Reason {
      get {
        return _reason;
      }
    }

    public int StatusCode {
      get {
        return _code;
      }
    }

    #endregion

    #region Internal Methods

    internal static HttpResponse CreateCloseResponse (HttpStatusCode code)
    {
      var ret = new HttpResponse (code);

      ret.Headers["Connection"] = "close";

      return ret;
    }

    internal static HttpResponse CreateUnauthorizedResponse (string challenge)
    {
      var ret = new HttpResponse (HttpStatusCode.Unauthorized);

      ret.Headers["WWW-Authenticate"] = challenge;

      return ret;
    }

    internal static HttpResponse CreateWebSocketResponse ()
    {
      var ret = new HttpResponse (HttpStatusCode.SwitchingProtocols);

      var headers = ret.Headers;

      headers["Upgrade"] = "websocket";
      headers["Connection"] = "Upgrade";

      return ret;
    }

    internal static HttpResponse Parse (string[] headerParts)
    {
      var len = headerParts.Length;

      if (len == 0) {
        var msg = "An empty response has been received.";

        throw new ArgumentException (msg);
      }

      var statusLineParts = headerParts[0].Split (new[] { ' ' }, 3);

      if (statusLineParts.Length != 3) {
        var msg = "It includes an invalid status line.";

        throw new ArgumentException (msg);
      }

      var s = statusLineParts[1];
      var code = Int32.Parse (s);

      var reason = statusLineParts[2];

      s = statusLineParts[0].Substring (5);
      var ver = new Version (s);

      var headers = new WebHeaderCollection ();

      for (var i = 1; i < len; i++)
        headers.InternalSet (headerParts[i], true);

      return new HttpResponse (code, reason, ver, headers);
    }

    internal static HttpResponse ReadResponse (
      Stream stream, int millisecondsTimeout
    )
    {
      return Read<HttpResponse> (stream, Parse, millisecondsTimeout);
    }

    #endregion

    #region Public Methods

    public void SetCookies (CookieCollection cookies)
    {
      if (cookies == null || cookies.Count == 0)
        return;

      var headers = Headers;

      foreach (var cookie in cookies.Sorted) {
        var val = cookie.ToResponseString ();

        headers.Add ("Set-Cookie", val);
      }
    }

    public override string ToString ()
    {
      var buff = new StringBuilder (64);

      var fmt = "HTTP/{0} {1} {2}{3}";
      buff.AppendFormat (fmt, ProtocolVersion, _code, _reason, CrLf);

      var headers = Headers;

      foreach (var key in headers.AllKeys)
        buff.AppendFormat ("{0}: {1}{2}", key, headers[key], CrLf);

      buff.Append (CrLf);

      var entity = EntityBody;

      if (entity.Length > 0)
        buff.Append (entity);

      return buff.ToString ();
    }

    #endregion
  }
}
