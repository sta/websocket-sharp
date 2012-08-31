#region MIT License
/**
 * ResponseHandshake.cs
 *
 * The MIT License
 *
 * Copyright (c) 2012 sta.blockhead
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
using System.Net;
using System.Text;

namespace WebSocketSharp {

  public class ResponseHandshake : Handshake
  {
    public ResponseHandshake()
    {
      Version = "HTTP/1.1";
      Status  = "101";
      Reason  = "Switching Protocols";
      Headers = new NameValueCollection();

      AddHeader("Upgrade", "websocket");
      AddHeader("Connection", "Upgrade");
    }

    public bool IsWebSocketResponse {

      get {
        if (Version != "HTTP/1.1")
          return false;

        if (Status != "101")
          return false;

        if (!HeaderExists("Upgrade", "websocket"))
          return false;

        if (!HeaderExists("Connection", "Upgrade"))
          return false;

        if (!HeaderExists("Sec-WebSocket-Accept"))
          return false;

        return true;
      }
    }

    public string Reason  { get; private set; }
    public string Status  { get; private set; }

    public static ResponseHandshake Parse(string[] response)
    {
      var statusLine = response[0].Split(' ');
      if (statusLine.Length < 3)
        throw new ArgumentException("Invalid status line.");

      var reason = new StringBuilder(statusLine[2]);
      for (int i = 3; i < statusLine.Length; i++)
        reason.AppendFormat(" {0}", statusLine[i]);

      var headers = new WebHeaderCollection();
      for (int i = 1; i < response.Length; i++)
        headers.Add(response[i]);

      return new ResponseHandshake {
        Headers = headers,
        Reason  = reason.ToString(),
        Status  = statusLine[1],
        Version = statusLine[0]
      };
    }

    public override string ToString()
    {
      var buffer = new StringBuilder();

      buffer.AppendFormat("{0} {1} {2}{3}", Version, Status, Reason, _crlf);

      foreach (string key in Headers.AllKeys)
        buffer.AppendFormat("{0}: {1}{2}", key, Headers[key], _crlf);

      buffer.Append(_crlf);

      return buffer.ToString();
    }
  }
}
