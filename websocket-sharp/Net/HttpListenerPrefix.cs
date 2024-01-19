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
 * Copyright (c) 2012-2024 sta.blockhead
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

namespace WebSocketSharp.Net
{
  internal sealed class HttpListenerPrefix
  {
    #region Private Fields

    private string       _host;
    private bool         _isSecure;
    private HttpListener _listener;
    private string       _original;
    private string       _path;
    private string       _port;
    private string       _prefix;
    private string       _scheme;

    #endregion

    #region Internal Constructors

    internal HttpListenerPrefix (string uriPrefix, HttpListener listener)
    {
      _original = uriPrefix;
      _listener = listener;

      parse (uriPrefix);
    }

    #endregion

    #region Public Properties

    public string Host {
      get {
        return _host;
      }
    }

    public bool IsSecure {
      get {
        return _isSecure;
      }
    }

    public HttpListener Listener {
      get {
        return _listener;
      }
    }

    public string Original {
      get {
        return _original;
      }
    }

    public string Path {
      get {
        return _path;
      }
    }

    public string Port {
      get {
        return _port;
      }
    }

    public string Scheme {
      get {
        return _scheme;
      }
    }

    #endregion

    #region Private Methods

    private void parse (string uriPrefix)
    {
      var compType = StringComparison.Ordinal;

      _isSecure = uriPrefix.StartsWith ("https", compType);
      _scheme = _isSecure ? "https" : "http";

      var hostStartIdx = uriPrefix.IndexOf (':') + 3;

      var len = uriPrefix.Length;
      var rootIdx = uriPrefix
                    .IndexOf ('/', hostStartIdx + 1, len - hostStartIdx - 1);

      var colonIdx = uriPrefix
                     .LastIndexOf (':', rootIdx - 1, rootIdx - hostStartIdx - 1);

      var hasPort = uriPrefix[rootIdx - 1] != ']' && colonIdx > hostStartIdx;

      if (hasPort) {
        _host = uriPrefix.Substring (hostStartIdx, colonIdx - hostStartIdx);
        _port = uriPrefix.Substring (colonIdx + 1, rootIdx - colonIdx - 1);
      }
      else {
        _host = uriPrefix.Substring (hostStartIdx, rootIdx - hostStartIdx);
        _port = _isSecure ? "443" : "80";
      }

      _path = uriPrefix.Substring (rootIdx);

      var fmt = "{0}://{1}:{2}{3}";

      _prefix = String.Format (fmt, _scheme, _host, _port, _path);
    }

    #endregion

    #region Public Methods

    public static void CheckPrefix (string uriPrefix)
    {
      if (uriPrefix == null)
        throw new ArgumentNullException ("uriPrefix");

      var len = uriPrefix.Length;

      if (len == 0) {
        var msg = "An empty string.";

        throw new ArgumentException (msg, "uriPrefix");
      }

      var compType = StringComparison.Ordinal;
      var isHttpSchm = uriPrefix.StartsWith ("http://", compType)
                       || uriPrefix.StartsWith ("https://", compType);

      if (!isHttpSchm) {
        var msg = "The scheme is not http or https.";

        throw new ArgumentException (msg, "uriPrefix");
      }

      var endIdx = len - 1;

      if (uriPrefix[endIdx] != '/') {
        var msg = "It ends without a forward slash.";

        throw new ArgumentException (msg, "uriPrefix");
      }

      var hostStartIdx = uriPrefix.IndexOf (':') + 3;

      if (hostStartIdx >= endIdx) {
        var msg = "No host is specified.";

        throw new ArgumentException (msg, "uriPrefix");
      }

      if (uriPrefix[hostStartIdx] == ':') {
        var msg = "No host is specified.";

        throw new ArgumentException (msg, "uriPrefix");
      }

      var rootIdx = uriPrefix.IndexOf ('/', hostStartIdx, len - hostStartIdx);

      if (rootIdx == hostStartIdx) {
        var msg = "No host is specified.";

        throw new ArgumentException (msg, "uriPrefix");
      }

      if (uriPrefix[rootIdx - 1] == ':') {
        var msg = "No port is specified.";

        throw new ArgumentException (msg, "uriPrefix");
      }

      if (rootIdx == endIdx - 1) {
        var msg = "No path is specified.";

        throw new ArgumentException (msg, "uriPrefix");
      }
    }

    public override bool Equals (object obj)
    {
      var pref = obj as HttpListenerPrefix;

      return pref != null && _prefix.Equals (pref._prefix);
    }

    public override int GetHashCode ()
    {
      return _prefix.GetHashCode ();
    }

    public override string ToString ()
    {
      return _prefix;
    }

    #endregion
  }
}
