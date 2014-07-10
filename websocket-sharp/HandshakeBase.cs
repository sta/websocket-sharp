#region License
/*
 * HandshakeBase.cs
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
  internal abstract class HandshakeBase
  {
    #region Private Fields

    private NameValueCollection _headers;
    private Version             _version;

    #endregion

    #region Internal Fields

    internal byte[] EntityBodyData;

    #endregion

    #region Protected Const Fields

    protected const string CrLf = "\r\n";

    #endregion

    #region Protected Constructors

    protected HandshakeBase (Version version, NameValueCollection headers)
    {
      _version = version;
      _headers = headers;
    }

    #endregion

    #region Public Properties

    public string EntityBody {
      get {
        return EntityBodyData != null && EntityBodyData.LongLength > 0
               ? getEncoding (_headers["Content-Type"]).GetString (EntityBodyData)
               : String.Empty;
      }
    }

    public NameValueCollection Headers {
      get {
        return _headers;
      }
    }

    public Version ProtocolVersion {
      get {
        return _version;
      }
    }

    #endregion

    #region Private Methods

    private static Encoding getEncoding (string contentType)
    {
      if (contentType == null || contentType.Length == 0)
        return Encoding.UTF8;

      var i = contentType.IndexOf ("charset=", StringComparison.Ordinal);
      if (i == -1)
        return Encoding.UTF8;

      var charset = contentType.Substring (i + 8);
      i = charset.IndexOf (';');
      if (i != -1)
        charset = charset.Substring (0, i);

      return Encoding.GetEncoding (charset);
    }

    #endregion

    #region Public Methods

    public byte[] ToByteArray ()
    {
      return Encoding.UTF8.GetBytes (ToString ());
    }
    
    #endregion
  }
}
