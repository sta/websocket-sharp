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

#region Contributors
/*
 * Contributors:
 * - Liryna <liryna.stark@gmail.com>
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
    #region Private Fields

    private static Dictionary<IPAddress, Dictionary<int, EndPointListener>> _ipToEndpoints;

    #endregion

    #region Static Constructor

    static EndPointManager ()
    {
      _ipToEndpoints = new Dictionary<IPAddress, Dictionary<int, EndPointListener>> ();
    }

    #endregion

    #region Private Constructors

    private EndPointManager ()
    {
    }

    #endregion

    #region Private Methods

    private static void addPrefix (string uriPrefix, HttpListener httpListener)
    {
      var pref = new HttpListenerPrefix (uriPrefix);
      if (pref.Path.IndexOf ('%') != -1)
        throw new HttpListenerException (400, "Invalid path."); // TODO: Code?

      if (pref.Path.IndexOf ("//", StringComparison.Ordinal) != -1)
        throw new HttpListenerException (400, "Invalid path."); // TODO: Code?

      // Always listens on all the interfaces, no matter the host name/ip used.
      var epl = getEndPointListener (IPAddress.Any, pref.Port, pref.IsSecure, httpListener);
      epl.AddPrefix (pref, httpListener);
    }

    private static EndPointListener getEndPointListener (
      IPAddress address, int port, bool secure, HttpListener httpListener)
    {
      Dictionary<int, EndPointListener> eps = null;
      if (_ipToEndpoints.ContainsKey (address)) {
        eps = _ipToEndpoints[address];
      }
      else {
        eps = new Dictionary<int, EndPointListener> ();
        _ipToEndpoints[address] = eps;
      }

      EndPointListener epl = null;
      if (eps.ContainsKey (port)) {
        epl = eps[port];
      }
      else {
        epl = new EndPointListener (
          address,
          port,
          secure,
          httpListener.CertificateFolderPath,
          httpListener.SslConfiguration,
          httpListener.ReuseAddress);

        eps[port] = epl;
      }

      return epl;
    }

    private static void removePrefix (string uriPrefix, HttpListener httpListener)
    {
      var pref = new HttpListenerPrefix (uriPrefix);
      if (pref.Path.IndexOf ('%') != -1)
        return;

      if (pref.Path.IndexOf ("//", StringComparison.Ordinal) != -1)
        return;

      var epl = getEndPointListener (IPAddress.Any, pref.Port, pref.IsSecure, httpListener);
      epl.RemovePrefix (pref, httpListener);
    }

    #endregion

    #region Public Methods

    public static void AddListener (HttpListener httpListener)
    {
      var added = new List<string> ();
      lock (((ICollection) _ipToEndpoints).SyncRoot) {
        try {
          foreach (var pref in httpListener.Prefixes) {
            addPrefix (pref, httpListener);
            added.Add (pref);
          }
        }
        catch {
          foreach (var pref in added)
            removePrefix (pref, httpListener);

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
        var eps = _ipToEndpoints[endpoint.Address];
        eps.Remove (endpoint.Port);
        if (eps.Count == 0)
          _ipToEndpoints.Remove (endpoint.Address);

        epListener.Close ();
      }
    }

    public static void RemoveListener (HttpListener httpListener)
    {
      lock (((ICollection) _ipToEndpoints).SyncRoot)
        foreach (var pref in httpListener.Prefixes)
          removePrefix (pref, httpListener);
    }

    public static void RemovePrefix (string uriPrefix, HttpListener httpListener)
    {
      lock (((ICollection) _ipToEndpoints).SyncRoot)
        removePrefix (uriPrefix, httpListener);
    }

    #endregion
  }
}
