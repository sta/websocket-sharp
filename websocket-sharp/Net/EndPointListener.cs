#region License
/*
 * EndPointListener.cs
 *
 * This code is derived from EndPointListener.cs (System.Net) of Mono
 * (http://www.mono-project.com).
 *
 * The MIT License
 *
 * Copyright (c) 2005 Novell, Inc. (http://www.novell.com)
 * Copyright (c) 2012-2016 sta.blockhead
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
 * - Nicholas Devenish
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

    #region Internal Constructors

    internal EndPointListener (
      IPAddress address,
      int port,
      bool reuseAddress,
      bool secure,
      string certificateFolderPath,
      ServerSslConfiguration sslConfig)
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
      _socket.BeginAccept (onAccept, this);
    }

    #endregion

    #region Public Properties

    public IPAddress Address {
      get {
        return _endpoint.Address;
      }
    }

    public bool IsSecure {
      get {
        return _secure;
      }
    }

    public int Port {
      get {
        return _endpoint.Port;
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

      var list = _unhandled;
      if (list != null && list.Count > 0)
        return;

      list = _all;
      if (list != null && list.Count > 0)
        return;

      EndPointManager.RemoveEndPoint (this);
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

    private static void onAccept (IAsyncResult asyncResult)
    {
      var lsnr = (EndPointListener) asyncResult.AsyncState;

      Socket sock = null;
      try {
        sock = lsnr._socket.EndAccept (asyncResult);
      }
      catch (SocketException) {
        // TODO: Should log the error code when this class has a logging.
      }
      catch (ObjectDisposedException) {
        return;
      }

      try {
        lsnr._socket.BeginAccept (onAccept, lsnr);
      }
      catch {
        if (sock != null)
          sock.Close ();

        return;
      }

      if (sock == null)
        return;

      processAccepted (sock, lsnr);
    }

    private static void processAccepted (Socket socket, EndPointListener listener)
    {
      HttpConnection conn = null;
      try {
        conn = new HttpConnection (socket, listener);
        lock (listener._unregisteredSync)
          listener._unregistered[conn] = conn;

        conn.BeginReadRequest ();
      }
      catch {
        if (conn != null) {
          conn.Close (true);
          return;
        }

        socket.Close ();
      }
    }

    private static bool removeSpecial (List<HttpListenerPrefix> prefixes, HttpListenerPrefix prefix)
    {
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
      var dns = Uri.CheckHostName (host) == UriHostNameType.Dns;
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

          if (pref.Port != port)
            continue;

          if (dns) {
            var phost = pref.Host;
            if (Uri.CheckHostName (phost) == UriHostNameType.Dns && phost != host)
              continue;
          }

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

    public void AddPrefix (HttpListenerPrefix prefix, HttpListener listener)
    {
      List<HttpListenerPrefix> current, future;
      if (prefix.Host == "*") {
        do {
          current = _unhandled;
          future = current != null
                   ? new List<HttpListenerPrefix> (current)
                   : new List<HttpListenerPrefix> ();

          prefix.Listener = listener;
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

          prefix.Listener = listener;
          addSpecial (future, prefix);
        }
        while (Interlocked.CompareExchange (ref _all, future, current) != current);

        return;
      }

      Dictionary<HttpListenerPrefix, HttpListener> prefs, prefs2;
      do {
        prefs = _prefixes;
        if (prefs.ContainsKey (prefix)) {
          if (prefs[prefix] != listener)
            throw new HttpListenerException (
              400, String.Format ("There's another listener for {0}.", prefix)); // TODO: Code?

          return;
        }

        prefs2 = new Dictionary<HttpListenerPrefix, HttpListener> (prefs);
        prefs2[prefix] = listener;
      }
      while (Interlocked.CompareExchange (ref _prefixes, prefs2, prefs) != prefs);
    }

    public bool BindContext (HttpListenerContext context)
    {
      HttpListenerPrefix pref;
      var lsnr = searchListener (context.Request.Url, out pref);
      if (lsnr == null)
        return false;

      context.Listener = lsnr;
      context.Connection.Prefix = pref;

      return true;
    }

    public void Close ()
    {
      _socket.Close ();

      HttpConnection[] conns = null;
      lock (_unregisteredSync) {
        if (_unregistered.Count == 0)
          return;

        var keys = _unregistered.Keys;
        conns = new HttpConnection[keys.Count];
        keys.CopyTo (conns, 0);
        _unregistered.Clear ();
      }

      for (var i = conns.Length - 1; i >= 0; i--)
        conns[i].Close (true);
    }

    public void RemovePrefix (HttpListenerPrefix prefix, HttpListener listener)
    {
      List<HttpListenerPrefix> current, future;
      if (prefix.Host == "*") {
        do {
          current = _unhandled;
          if (current == null)
            break;

          future = new List<HttpListenerPrefix> (current);
          if (!removeSpecial (future, prefix))
            break; // The prefix wasn't found.
        }
        while (Interlocked.CompareExchange (ref _unhandled, future, current) != current);

        checkIfRemove ();
        return;
      }

      if (prefix.Host == "+") {
        do {
          current = _all;
          if (current == null)
            break;

          future = new List<HttpListenerPrefix> (current);
          if (!removeSpecial (future, prefix))
            break; // The prefix wasn't found.
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

    #endregion
  }
}
