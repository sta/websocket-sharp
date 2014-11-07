#region License
/*
 * EndPointListener.cs
 *
 * This code is derived from System.Net.EndPointListener.cs of Mono
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
 * - Gonzalo Paniagua Javier <gonzalo@novell.com>
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
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace WebSocketSharp.Net
{
  internal sealed class EndPointListener
  {
    #region Private Fields

    private List<HttpListenerPrefix>                     _all; // host == '+'
    private static readonly string                       _defaultCertFolderPath;
    private IPEndPoint                                   _endpoint;
    private Dictionary<HttpListenerPrefix, HttpListener> _prefixes;
    private bool                                         _secure;
    private Socket                                       _socket;
    private ServerSslConfiguration                       _sslConfig;
    private List<HttpListenerPrefix>                     _unhandled; // host == '*'
    private Dictionary<HttpConnection, HttpConnection>   _unregistered;
    private object                                       _unregisteredSync;

    #endregion

    #region Static Constructor

    static EndPointListener ()
    {
      _defaultCertFolderPath =
        Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData);
    }

    #endregion

    #region Public Constructors

    public EndPointListener (
      IPAddress address,
      int port,
      bool secure,
      string certificateFolderPath,
      ServerSslConfiguration sslConfig,
      bool reuseAddress)
    {
      if (secure) {
        var cert = getCertificate (port, certificateFolderPath, sslConfig.ServerCertificate);
        if (cert == null)
          throw new ArgumentException ("No server certificate could be found.");

        _secure = secure;
        _sslConfig = sslConfig;
        _sslConfig.ServerCertificate = cert;
      }

      _prefixes = new Dictionary<HttpListenerPrefix, HttpListener> ();

      _unregistered = new Dictionary<HttpConnection, HttpConnection> ();
      _unregisteredSync = ((ICollection) _unregistered).SyncRoot;

      _socket = new Socket (address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
      if (reuseAddress)
        _socket.SetSocketOption (SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

      _endpoint = new IPEndPoint (address, port);
      _socket.Bind (_endpoint);
      _socket.Listen (500);

      var args = new SocketAsyncEventArgs ();
      args.UserToken = this;
      args.Completed += onAccept;
      _socket.AcceptAsync (args);
    }

    #endregion

    #region Public Properties

    public bool IsSecure {
      get {
        return _secure;
      }
    }

    public ServerSslConfiguration SslConfiguration {
      get {
        return _sslConfig;
      }
    }

    #endregion

    #region Private Methods

    private static void addSpecial (List<HttpListenerPrefix> prefixes, HttpListenerPrefix prefix)
    {
      if (prefixes == null)
        return;

      var path = prefix.Path;
      foreach (var pref in prefixes)
        if (pref.Path == path)
          throw new HttpListenerException (400, "The prefix is already in use."); // TODO: Code?

      prefixes.Add (prefix);
    }

    private void checkIfRemove ()
    {
      if (_prefixes.Count > 0)
        return;

      if (_unhandled != null && _unhandled.Count > 0)
        return;

      if (_all != null && _all.Count > 0)
        return;

      EndPointManager.RemoveEndPoint (this, _endpoint);
    }

    private static RSACryptoServiceProvider createRSAFromFile (string filename)
    {
      byte[] pvk = null;
      using (var fs = File.Open (filename, FileMode.Open, FileAccess.Read, FileShare.Read)) {
        pvk = new byte[fs.Length];
        fs.Read (pvk, 0, pvk.Length);
      }

      var rsa = new RSACryptoServiceProvider ();
      rsa.ImportCspBlob (pvk);

      return rsa;
    }

    private static X509Certificate2 getCertificate (
      int port, string certificateFolderPath, X509Certificate2 defaultCertificate)
    {
      if (certificateFolderPath == null || certificateFolderPath.Length == 0)
        certificateFolderPath = _defaultCertFolderPath;

      try {
        var cer = Path.Combine (certificateFolderPath, String.Format ("{0}.cer", port));
        var key = Path.Combine (certificateFolderPath, String.Format ("{0}.key", port));
        if (File.Exists (cer) && File.Exists (key)) {
          var cert = new X509Certificate2 (cer);
          cert.PrivateKey = createRSAFromFile (key);

          return cert;
        }
      }
      catch {
      }

      return defaultCertificate;
    }

    private static HttpListener matchFromList (
      string host, string path, List<HttpListenerPrefix> list, out HttpListenerPrefix prefix)
    {
      prefix = null;
      if (list == null)
        return null;

      HttpListener bestMatch = null;
      var bestLen = -1;
      foreach (var pref in list) {
        var ppath = pref.Path;
        if (ppath.Length < bestLen)
          continue;

        if (path.StartsWith (ppath)) {
          bestLen = ppath.Length;
          bestMatch = pref.Listener;
          prefix = pref;
        }
      }

      return bestMatch;
    }

    private static void onAccept (object sender, EventArgs e)
    {
      var args = (SocketAsyncEventArgs) e;
      var epl = (EndPointListener) args.UserToken;
      Socket accepted = null;
      if (args.SocketError == SocketError.Success) {
        accepted = args.AcceptSocket;
        args.AcceptSocket = null;
      }

      try {
        epl._socket.AcceptAsync (args);
      }
      catch {
        if (accepted != null)
          accepted.Close ();

        return;
      }

      if (accepted == null)
        return;

      HttpConnection conn = null;
      try {
        conn = new HttpConnection (accepted, epl);
        lock (epl._unregisteredSync)
          epl._unregistered[conn] = conn;

        conn.BeginReadRequest ();
      }
      catch {
        if (conn != null) {
          conn.Close (true);
          return;
        }

        accepted.Close ();
      }
    }

    private static bool removeSpecial (List<HttpListenerPrefix> prefixes, HttpListenerPrefix prefix)
    {
      if (prefixes == null)
        return false;

      var path = prefix.Path;
      var cnt = prefixes.Count;
      for (var i = 0; i < cnt; i++) {
        if (prefixes[i].Path == path) {
          prefixes.RemoveAt (i);
          return true;
        }
      }

      return false;
    }

    private HttpListener searchListener (Uri uri, out HttpListenerPrefix prefix)
    {
      prefix = null;
      if (uri == null)
        return null;

      var host = uri.Host;
      var port = uri.Port;
      var path = HttpUtility.UrlDecode (uri.AbsolutePath);
      var pathSlash = path[path.Length - 1] == '/' ? path : path + "/";

      HttpListener bestMatch = null;
      var bestLen = -1;
      if (host != null && host.Length > 0) {
        foreach (var pref in _prefixes.Keys) {
          var ppath = pref.Path;
          if (ppath.Length < bestLen)
            continue;

          if (pref.Host != host || pref.Port != port)
            continue;

          if (path.StartsWith (ppath) || pathSlash.StartsWith (ppath)) {
            bestLen = ppath.Length;
            bestMatch = _prefixes[pref];
            prefix = pref;
          }
        }

        if (bestLen != -1)
          return bestMatch;
      }

      var list = _unhandled;
      bestMatch = matchFromList (host, path, list, out prefix);
      if (path != pathSlash && bestMatch == null)
        bestMatch = matchFromList (host, pathSlash, list, out prefix);

      if (bestMatch != null)
        return bestMatch;

      list = _all;
      bestMatch = matchFromList (host, path, list, out prefix);
      if (path != pathSlash && bestMatch == null)
        bestMatch = matchFromList (host, pathSlash, list, out prefix);

      if (bestMatch != null)
        return bestMatch;

      return null;
    }

    #endregion

    #region Internal Methods

    internal static bool CertificateExists (int port, string certificateFolderPath)
    {
      if (certificateFolderPath == null || certificateFolderPath.Length == 0)
        certificateFolderPath = _defaultCertFolderPath;

      var cer = Path.Combine (certificateFolderPath, String.Format ("{0}.cer", port));
      var key = Path.Combine (certificateFolderPath, String.Format ("{0}.key", port));

      return File.Exists (cer) && File.Exists (key);
    }

    internal void RemoveConnection (HttpConnection connection)
    {
      lock (_unregisteredSync)
        _unregistered.Remove (connection);
    }

    #endregion

    #region Public Methods

    public void AddPrefix (HttpListenerPrefix prefix, HttpListener httpListener)
    {
      List<HttpListenerPrefix> current, future;
      if (prefix.Host == "*") {
        do {
          current = _unhandled;
          future = current != null
                   ? new List<HttpListenerPrefix> (current)
                   : new List<HttpListenerPrefix> ();

          prefix.Listener = httpListener;
          addSpecial (future, prefix);
        }
        while (Interlocked.CompareExchange (ref _unhandled, future, current) != current);

        return;
      }

      if (prefix.Host == "+") {
        do {
          current = _all;
          future = current != null
                   ? new List<HttpListenerPrefix> (current)
                   : new List<HttpListenerPrefix> ();

          prefix.Listener = httpListener;
          addSpecial (future, prefix);
        }
        while (Interlocked.CompareExchange (ref _all, future, current) != current);

        return;
      }

      Dictionary<HttpListenerPrefix, HttpListener> prefs, prefs2;
      do {
        prefs = _prefixes;
        if (prefs.ContainsKey (prefix)) {
          var other = prefs[prefix];
          if (other != httpListener)
            throw new HttpListenerException (
              400, String.Format ("There's another listener for {0}.", prefix)); // TODO: Code?

          return;
        }

        prefs2 = new Dictionary<HttpListenerPrefix, HttpListener> (prefs);
        prefs2[prefix] = httpListener;
      }
      while (Interlocked.CompareExchange (ref _prefixes, prefs2, prefs) != prefs);
    }

    public bool BindContext (HttpListenerContext context)
    {
      HttpListenerPrefix pref;
      var httpl = searchListener (context.Request.Url, out pref);
      if (httpl == null)
        return false;

      context.Listener = httpl;
      context.Connection.Prefix = pref;

      return true;
    }

    public void Close ()
    {
      _socket.Close ();

      lock (_unregisteredSync) {
        var conns = new List<HttpConnection> (_unregistered.Keys);
        _unregistered.Clear ();
        foreach (var conn in conns)
          conn.Close (true);

        conns.Clear ();
      }
    }

    public void RemovePrefix (HttpListenerPrefix prefix, HttpListener httpListener)
    {
      List<HttpListenerPrefix> current, future;
      if (prefix.Host == "*") {
        do {
          current = _unhandled;
          future = current != null
                   ? new List<HttpListenerPrefix> (current)
                   : new List<HttpListenerPrefix> ();

          if (!removeSpecial (future, prefix))
            break; // Prefix not found.
        }
        while (Interlocked.CompareExchange (ref _unhandled, future, current) != current);

        checkIfRemove ();
        return;
      }

      if (prefix.Host == "+") {
        do {
          current = _all;
          future = current != null
                   ? new List<HttpListenerPrefix> (current)
                   : new List<HttpListenerPrefix> ();

          if (!removeSpecial (future, prefix))
            break; // Prefix not found.
        }
        while (Interlocked.CompareExchange (ref _all, future, current) != current);

        checkIfRemove ();
        return;
      }

      Dictionary<HttpListenerPrefix, HttpListener> prefs, prefs2;
      do {
        prefs = _prefixes;
        if (!prefs.ContainsKey (prefix))
          break;

        prefs2 = new Dictionary<HttpListenerPrefix, HttpListener> (prefs);
        prefs2.Remove (prefix);
      }
      while (Interlocked.CompareExchange (ref _prefixes, prefs2, prefs) != prefs);

      checkIfRemove ();
    }

    public void UnbindContext (HttpListenerContext context)
    {
      if (context == null || context.Listener == null)
        return;

      context.Listener.UnregisterContext (context);
    }

    #endregion
  }
}
