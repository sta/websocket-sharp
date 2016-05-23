#region License
/*
 * HttpListenerPrefix.cs
 *
 * This code is derived from ListenerPrefix.cs (System.Net) of Mono
 * (http://www.mono-project.com).
 *
 * The MIT License
 *
 * Copyright (c) 2005 Novell, Inc. (http://www.novell.com)
 * Copyright (c) 2012-2015 sta.blockhead
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

#region Authors
/*
 * Authors:
 * - Gonzalo Paniagua Javier <gonzalo@novell.com>
 * - Oleg Mihailik <mihailik@gmail.com>
 */
#endregion

using System;
using System.Net;

namespace WebSocketSharp.Net
{
  internal sealed class HttpListenerPrefix
  {
    #region Private Fields

    private IPAddress[]  _addresses;
    private string       _host;
    private HttpListener _listener;
    private string       _original;
    private string       _path;
    private ushort       _port;
    private bool         _secure;

    #endregion

    #region Internal Constructors

    // Must be called after calling the CheckPrefix method.
    internal HttpListenerPrefix (string uriPrefix)
    {
      _original = uriPrefix;
      parse (uriPrefix);
    }

    #endregion

    #region Public Properties

    public IPAddress[] Addresses {
      get {
        return _addresses;
      }

      set {
        _addresses = value;
      }
    }

    public string Host {
      get {
        return _host;
      }
    }

    public bool IsSecure {
      get {
        return _secure;
      }
    }

    public HttpListener Listener {
      get {
        return _listener;
      }

      set {
        _listener = value;
      }
    }

    public string Path {
      get {
        return _path;
      }
    }

    public int Port {
      get {
        return (int) _port;
      }
    }

    #endregion

    #region Private Methods

    private void parse (string uriPrefix)
    {
      var uri = new System.Uri(uriPrefix);

      _port = (ushort)uri.Port;
      _host = uri.Host;
      _path = uri.PathAndQuery;
      _secure = uri.Scheme.Equals("https");

      var pathLen = _path.Length;
      if (pathLen > 1)
        _path = _path.Substring (0, pathLen - 1);
    }

    #endregion

    #region Public Methods

    public static void CheckPrefix (string uriPrefix)
    {
        var uri = new System.Uri(uriPrefix);

        if (!uri.Scheme.Equals("http") && !uri.Scheme.Equals("https"))
          throw new ArgumentException ("The scheme isn't 'http' or 'https'.");

        if (!uri.PathAndQuery.EndsWith("/"))
          throw new ArgumentException ("Ends without '/'.");
    }

    // The Equals and GetHashCode methods are required to detect duplicates in any collection.
    public override bool Equals (Object obj)
    {
      var pref = obj as HttpListenerPrefix;
      return pref != null && pref._original == _original;
    }

    public override int GetHashCode ()
    {
      return _original.GetHashCode ();
    }

    public override string ToString ()
    {
      return _original;
    }

    #endregion
  }
}
