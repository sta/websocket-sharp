#region MIT License
/**
 * RequestHandshake.cs
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
using System.Net;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;

namespace WebSocketSharp
{
  public class RequestHandshake
  {
    private const string _crlf = "\r\n";

    private RequestHandshake()
    {
    }

    public RequestHandshake(string uri)
    {
      Method  = "GET";
      Uri     = uri;
      Version = "HTTP/1.1";
      Headers = new NameValueCollection();

      AddHeader("Upgrade", "websocket");
      AddHeader("Connection", "Upgrade");
    }

    public NameValueCollection Headers { get; private set; }

    public bool IsWebSocketRequest {

      get {
        if (Method != "GET")
          return false;

        if (Version != "HTTP/1.1")
          return false;

        if (!HeaderExists("Upgrade", "websocket"))
          return false;

        if (!HeaderExists("Connection", "Upgrade"))
          return false;

        if (!HeaderExists("Host"))
          return false;

        if (!HeaderExists("Sec-WebSocket-Key"))
          return false;

        if (!HeaderExists("Sec-WebSocket-Version"))
          return false;

        return true;
      }
    }

    public string Method  { get; private set; }
    public string Uri     { get; private set; }
    public string Version { get; private set; }

    public static RequestHandshake Parse(byte[] data)
    {
      var request = Encoding.UTF8.GetString(data)
        .Replace("\r\n", "\n").Replace("\n\n", "\n").TrimEnd('\n')
        .Split('\n');

      return Parse(request);
    }

    public static RequestHandshake Parse(string[] request)
    {
      var requestLine = request[0].Split(' ');

      if (requestLine.Length != 3)
        throw new ArgumentException("Invalid request line.");

      var headers = new WebHeaderCollection();

      for (int i = 1; i < request.Length; i++)
        headers.Add(request[i]);

      return new RequestHandshake {
        Headers = headers,
        Method  = requestLine[0],
        Uri     = requestLine[1],
        Version = requestLine[2]        
      };
    }

    public void AddHeader(string name, string value)
    {
      Headers.Add(name, value);
    }

    public string[] GetHeaderValues(string name)
    {
      return Headers.GetValues(name);
    }

    public bool HeaderExists(string name)
    {
      return Headers[name] != null
             ? true
             : false;
    }

    public bool HeaderExists(string name, string value)
    {
      var values = GetHeaderValues(name);

      if (values == null)
        return false;

      foreach (string v in values)
        if (String.Compare(value, v, true) == 0)
          return true;

      return false;
    }

    public byte[] ToBytes()
    {
      return Encoding.UTF8.GetBytes(ToString());
    }

    public override string ToString()
    {
      var buffer = new StringBuilder();

      buffer.AppendFormat("{0} {1} {2}{3}", Method, Uri, Version, _crlf);

      foreach (string key in Headers.AllKeys)
        buffer.AppendFormat("{0}: {1}{2}", key, Headers[key], _crlf);

      buffer.Append(_crlf);

      return buffer.ToString();
    }
  }
}
