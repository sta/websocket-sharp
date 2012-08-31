#region MIT License
/**
 * Handshake.cs
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
using System.Text;

namespace WebSocketSharp {

  public abstract class Handshake {

    protected const string _crlf = "\r\n";

    protected Handshake()
    {
    }

    public NameValueCollection Headers { get; protected set; }
    public string              Version { get; protected set; }

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
  }
}
