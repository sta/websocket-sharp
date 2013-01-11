#region MIT License
/*
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
using WebSocketSharp.Net;

namespace WebSocketSharp {

  internal abstract class Handshake {

    #region Field

    protected const string _crlf = "\r\n";

    #endregion

    #region Constructor

    protected Handshake()
    {
      ProtocolVersion = HttpVersion.Version11;
      Headers         = new NameValueCollection();
    }

    #endregion

    #region Properties

    public NameValueCollection Headers         { get; internal set; }
    public Version             ProtocolVersion { get; internal set; }

    #endregion

    #region Methods

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
      return Headers.Exists(name);
    }

    public bool HeaderExists(string name, string value)
    {
      return Headers.Exists(name, value);
    }

    public byte[] ToBytes()
    {
      return Encoding.UTF8.GetBytes(ToString());
    }
    
    #endregion
  }
}
