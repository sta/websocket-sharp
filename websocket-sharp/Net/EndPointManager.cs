#region License
/*
 * EndPointManager.cs
 *
 * This code is derived from EndPointManager.cs (System.Net) of Mono
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

      // Listens on all the interfaces if host name cannot be parsed by IPAddress.
      var lsnr = getEndPointListener (pref.Host, pref.Port, httpListener, pref.IsSecure);
      lsnr.AddPrefix (pref, httpListener);
    }

    private static IPAddress convertToAddress (string hostName)
    {
      if (hostName == "*" || hostName == "+")
        return IPAddress.Any;

      IPAddress addr;
      if (IPAddress.TryParse (hostName, out addr))
        return addr;

      try {
        var host = Dns.GetHostEntry (hostName);
        return host != null ? host.AddressList[0] : IPAddress.Any;
      }
      catch {
        return IPAddress.Any;
      }
    }

    private static EndPointListener getEndPointListener (
      string host, int port, HttpListener httpListener, bool secure)
    {
      var addr = convertToAddress (host);

      Dictionary<int, EndPointListener> eps = null;
      if (_ipToEndpoints.ContainsKey (addr)) {
        eps = _ipToEndpoints[addr];
      }
      else {
        eps = new Dictionary<int, EndPointListener> ();
        _ipToEndpoints[addr] = eps;
      }

      EndPointListener lsnr = null;
      if (eps.ContainsKey (port)) {
        lsnr = eps[port];
      }
      else {
        lsnr = new EndPointListener (
          addr,
          port,
          secure,
          httpListener.CertificateFolderPath,
          httpListener.SslConfiguration,
          httpListener.ReuseAddress);

        eps[port] = lsnr;
      }

      return lsnr;
    }

    private static void removePrefix (string uriPrefix, HttpListener httpListener)
    {
      var pref = new HttpListenerPrefix (uriPrefix);
      if (pref.Path.IndexOf ('%') != -1)
        return;

      if (pref.Path.IndexOf ("//", StringComparison.Ordinal) != -1)
        return;

      var lsnr = getEndPointListener (pref.Host, pref.Port, httpListener, pref.IsSecure);
      lsnr.RemovePrefix (pref, httpListener);
    }

    #endregion

    #region Internal Methods

    internal static void RemoveEndPoint (EndPointListener endpointListener)
    {
      lock (((ICollection) _ipToEndpoints).SyncRoot) {
        var addr = endpointListener.Address;
        var eps = _ipToEndpoints[addr];
        eps.Remove (endpointListener.Port);
        if (eps.Count == 0)
          _ipToEndpoints.Remove (addr);

        endpointListener.Close ();
      }
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
