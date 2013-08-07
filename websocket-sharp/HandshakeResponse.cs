#region License
/*
 * HandshakeResponse.cs
 *
 * The MIT License
 *
 * Copyright (c) 2012-2013 sta.blockhead
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
    #region Public Constructors

    public HandshakeResponse ()
      : this (HttpStatusCode.SwitchingProtocols)
    {
      AddHeader ("Upgrade", "websocket");
      AddHeader ("Connection", "Upgrade");
    }

    public HandshakeResponse (HttpStatusCode code)
    {
      StatusCode = ((int) code).ToString ();
      Reason = code.GetDescription ();
      AddHeader ("Server", "websocket-sharp/1.0");
    }

    #endregion

    #region Public Properties

    public AuthenticationChallenge AuthChallenge {
      get {
        var challenge = Headers ["WWW-Authenticate"];
        return !challenge.IsNullOrEmpty ()
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
        return StatusCode == "401";
      }
    }

    public bool IsWebSocketResponse {
      get {
        return ProtocolVersion >= HttpVersion.Version11 &&
               StatusCode == "101" &&
               Headers.Contains ("Upgrade", "websocket") &&
               Headers.Contains ("Connection", "Upgrade");
      }
    }

    public string Reason {
      get; private set;
    }

    public string StatusCode {
      get; private set;
    }

    #endregion

    #region Public Methods

    public static HandshakeResponse CreateCloseResponse (HttpStatusCode code)
    {
      var res = new HandshakeResponse (code);
      res.AddHeader ("Connection", "close");

      return res;
    }

    public static HandshakeResponse Parse (string [] response)
    {
      var statusLine = response [0].Split (' ');
      if (statusLine.Length < 3)
        throw new ArgumentException ("Invalid status line.");

      var reason = new StringBuilder (statusLine [2], 64);
      for (int i = 3; i < statusLine.Length; i++)
        reason.AppendFormat (" {0}", statusLine [i]);

      var headers = new WebHeaderCollection ();
      for (int i = 1; i < response.Length; i++)
        headers.SetInternal (response [i], true);

      return new HandshakeResponse {
        Headers = headers,
        Reason = reason.ToString (),
        StatusCode = statusLine [1],
        ProtocolVersion = new Version (statusLine [0].Substring (5))
      };
    }

    public void SetCookies (CookieCollection cookies)
    {
      if (cookies == null || cookies.Count == 0)
        return;

      foreach (var cookie in cookies.Sorted)
        AddHeader ("Set-Cookie", cookie.ToResponseString ());
    }

    public override string ToString ()
    {
      var buffer = new StringBuilder (64);
      buffer.AppendFormat ("HTTP/{0} {1} {2}{3}", ProtocolVersion, StatusCode, Reason, CrLf);
      foreach (string key in Headers.AllKeys)
        buffer.AppendFormat ("{0}: {1}{2}", key, Headers [key], CrLf);

      buffer.Append (CrLf);
      return buffer.ToString ();
    }

    #endregion
  }
}
