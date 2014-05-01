#region License
/*
 * EndPointManager.cs
 *
 * This code is derived from System.Net.EndPointManager.cs of Mono
 * (http://www.mono-project.com).
 *
 * The MIT License
 *
 * Copyright (c) 2005 Novell, Inc. (http://www.novell.com)
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

#region Authors
/*
 * Authors:
 * - Gonzalo Paniagua Javier <gonzalo@ximian.com>
 */
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;

namespace WebSocketSharp.Net
{
  internal sealed class EndPointManager
  {
    #region Private Static Fields

    private static Dictionary<IPAddress, Dictionary<int, EndPointListener>> _ipToEndpoints =
      new Dictionary<IPAddress, Dictionary<int, EndPointListener>> ();

    #endregion

    #region Private Constructors

    private EndPointManager ()
    {
    }

    #endregion

    #region Private Methods

    private static void addPrefix (string uriPrefix, HttpListener httpListener)
    {
      var prefix = new ListenerPrefix (uriPrefix);
      if (prefix.Path.IndexOf ('%') != -1)
        throw new HttpListenerException (400, "Invalid path."); // TODO: Code?

      if (prefix.Path.IndexOf ("//", StringComparison.Ordinal) != -1)
        throw new HttpListenerException (400, "Invalid path."); // TODO: Code?

      // Always listens on all the interfaces, no matter the host name/ip used.
      var epListener = getEndPointListener (
        IPAddress.Any, prefix.Port, httpListener, prefix.Secure);

      epListener.AddPrefix (prefix, httpListener);
    }

    private static EndPointListener getEndPointListener (
      IPAddress address, int port, HttpListener httpListener, bool secure)
    {
      Dictionary<int, EndPointListener> endpoints = null;
      if (_ipToEndpoints.ContainsKey (address)) {
        endpoints = _ipToEndpoints [address];
      }
      else {
        endpoints = new Dictionary<int, EndPointListener> ();
        _ipToEndpoints [address] = endpoints;
      }

      EndPointListener epListener = null;
      if (endpoints.ContainsKey (port)) {
        epListener = endpoints [port];
      }
      else {
        epListener = new EndPointListener (
          address,
          port,
          secure,
          httpListener.CertificateFolderPath,
          httpListener.DefaultCertificate);

        endpoints [port] = epListener;
      }

      return epListener;
    }

    private static void removePrefix (string uriPrefix, HttpListener httpListener)
    {
      var prefix = new ListenerPrefix (uriPrefix);
      if (prefix.Path.IndexOf ('%') != -1)
        return;

      if (prefix.Path.IndexOf ("//", StringComparison.Ordinal) != -1)
        return;

      var epListener = getEndPointListener (
        IPAddress.Any, prefix.Port, httpListener, prefix.Secure);

      epListener.RemovePrefix (prefix, httpListener);
    }

    #endregion

    #region Public Methods

    public static void AddListener (HttpListener httpListener)
    {
      var added = new List<string> ();
      lock (((ICollection) _ipToEndpoints).SyncRoot) {
        try {
          foreach (var prefix in httpListener.Prefixes) {
            addPrefix (prefix, httpListener);
            added.Add (prefix);
          }
        }
        catch {
          foreach (var prefix in added)
            removePrefix (prefix, httpListener);

          throw;
        }
      }
    }

    public static void AddPrefix (string uriPrefix, HttpListener httpListener)
    {
      lock (((ICollection) _ipToEndpoints).SyncRoot)
        addPrefix (uriPrefix, httpListener);
    }

    public static void RemoveEndPoint (EndPointListener epListener, IPEndPoint endpoint)
    {
      lock (((ICollection) _ipToEndpoints).SyncRoot) {
        var endpoints = _ipToEndpoints [endpoint.Address];
        endpoints.Remove (endpoint.Port);
        if (endpoints.Count == 0)
          _ipToEndpoints.Remove (endpoint.Address);

        epListener.Close ();
      }
    }

    public static void RemoveListener (HttpListener httpListener)
    {
      lock (((ICollection) _ipToEndpoints).SyncRoot)
        foreach (var prefix in httpListener.Prefixes)
          removePrefix (prefix, httpListener);
    }

    public static void RemovePrefix (string uriPrefix, HttpListener httpListener)
    {
      lock (((ICollection) _ipToEndpoints).SyncRoot)
        removePrefix (uriPrefix, httpListener);
    }

    #endregion
  }
}
