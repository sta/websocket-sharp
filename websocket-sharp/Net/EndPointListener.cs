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
    #region Private Static Fields

    private static readonly string _defaultCertFolderPath =
      Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData);

    #endregion

    #region Private Fields

    private List<ListenerPrefix>                       _all; // host = '+'
    private X509Certificate2                           _cert;
    private IPEndPoint                                 _endpoint;
    private Dictionary<ListenerPrefix, HttpListener>   _prefixes;
    private bool                                       _secure;
    private Socket                                     _socket;
    private List<ListenerPrefix>                       _unhandled; // host = '*'
    private Dictionary<HttpConnection, HttpConnection> _unregistered;

    #endregion

    #region Public Constructors

    public EndPointListener (
      IPAddress address, int port, bool secure, string certFolderPath, X509Certificate2 defaultCert)
    {
      if (secure) {
        _secure = secure;
        _cert = getCertificate (port, certFolderPath, defaultCert);
        if (_cert == null)
          throw new ArgumentException ("No server certificate found.");
      }

      _endpoint = new IPEndPoint (address, port);
      _prefixes = new Dictionary<ListenerPrefix, HttpListener> ();
      _unregistered = new Dictionary<HttpConnection, HttpConnection> ();

      _socket = new Socket (address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
      _socket.Bind (_endpoint);
      _socket.Listen (500);

      var args = new SocketAsyncEventArgs ();
      args.UserToken = this;
      args.Completed += onAccept;
      _socket.AcceptAsync (args);
    }

    #endregion

    #region Public Properties

    public X509Certificate2 Certificate {
      get {
        return _cert;
      }
    }

    public bool IsSecure {
      get {
        return _secure;
      }
    }

    #endregion

    #region Private Methods

    private static void addSpecial (List<ListenerPrefix> prefixes, ListenerPrefix prefix)
    {
      if (prefixes == null)
        return;

      var path = prefix.Path;
      foreach (var pref in prefixes)
        if (pref.Path == path) // TODO: Code?
          throw new HttpListenerException (400, "Prefix already in use.");

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
      byte [] pvk = null;
      using (var fs = File.Open (filename, FileMode.Open, FileAccess.Read, FileShare.Read)) {
        pvk = new byte [fs.Length];
        fs.Read (pvk, 0, pvk.Length);
      }

      var rsa = new RSACryptoServiceProvider ();
      rsa.ImportCspBlob (pvk);

      return rsa;
    }

    private static X509Certificate2 getCertificate (
      int port, string certFolderPath, X509Certificate2 defaultCert)
    {
      if (certFolderPath == null || certFolderPath.Length == 0)
        certFolderPath = _defaultCertFolderPath;

      try {
        var cer = Path.Combine (certFolderPath, String.Format ("{0}.cer", port));
        var key = Path.Combine (certFolderPath, String.Format ("{0}.key", port));
        if (File.Exists (cer) && File.Exists (key)) {
          var cert = new X509Certificate2 (cer);
          cert.PrivateKey = createRSAFromFile (key);

          return cert;
        }
      }
      catch {
      }

      return defaultCert;
    }

    private static HttpListener matchFromList (
      string host, string path, List<ListenerPrefix> list, out ListenerPrefix prefix)
    {
      prefix = null;
      if (list == null)
        return null;

      HttpListener bestMatch = null;
      var bestLength = -1;
      foreach (var pref in list) {
        var ppath = pref.Path;
        if (ppath.Length < bestLength)
          continue;

        if (path.StartsWith (ppath)) {
          bestLength = ppath.Length;
          bestMatch = pref.Listener;
          prefix = pref;
        }
      }

      return bestMatch;
    }

    private static void onAccept (object sender, EventArgs e)
    {
      var args = (SocketAsyncEventArgs) e;
      var listener = (EndPointListener) args.UserToken;
      Socket accepted = null;
      if (args.SocketError == SocketError.Success) {
        accepted = args.AcceptSocket;
        args.AcceptSocket = null;
      }

      try {
        listener._socket.AcceptAsync (args);
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
        conn = new HttpConnection (accepted, listener);
        lock (((ICollection) listener._unregistered).SyncRoot)
          listener._unregistered [conn] = conn;

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

    private static bool removeSpecial (List<ListenerPrefix> prefixes, ListenerPrefix prefix)
    {
      if (prefixes == null)
        return false;

      var path = prefix.Path;
      var count = prefixes.Count;
      for (int i = 0; i < count; i++) {
        if (prefixes [i].Path == path) {
          prefixes.RemoveAt (i);
          return true;
        }
      }

      return false;
    }

    private HttpListener searchListener (Uri uri, out ListenerPrefix prefix)
    {
      prefix = null;
      if (uri == null)
        return null;

      var host = uri.Host;
      var port = uri.Port;
      var path = HttpUtility.UrlDecode (uri.AbsolutePath);
      var pathSlash = path [path.Length - 1] == '/' ? path : path + "/";

      HttpListener bestMatch = null;
      var bestLength = -1;
      if (host != null && host.Length > 0) {
        foreach (var pref in _prefixes.Keys) {
          var ppath = pref.Path;
          if (ppath.Length < bestLength)
            continue;

          if (pref.Host != host || pref.Port != port)
            continue;

          if (path.StartsWith (ppath) || pathSlash.StartsWith (ppath)) {
            bestLength = ppath.Length;
            bestMatch = _prefixes [pref];
            prefix = pref;
          }
        }

        if (bestLength != -1)
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

    internal static bool CertificateExists (int port, string certFolderPath)
    {
      if (certFolderPath == null || certFolderPath.Length == 0)
        certFolderPath = _defaultCertFolderPath;

      var cer = Path.Combine (certFolderPath, String.Format ("{0}.cer", port));
      var key = Path.Combine (certFolderPath, String.Format ("{0}.key", port));

      return File.Exists (cer) && File.Exists (key);
    }

    internal void RemoveConnection (HttpConnection connection)
    {
      lock (((ICollection) _unregistered).SyncRoot)
        _unregistered.Remove (connection);
    }

    #endregion

    #region Public Methods

    public void AddPrefix (ListenerPrefix prefix, HttpListener listener)
    {
      List<ListenerPrefix> current, future;
      if (prefix.Host == "*") {
        do {
          current = _unhandled;
          future = current != null
                   ? new List<ListenerPrefix> (current)
                   : new List<ListenerPrefix> ();

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
                   ? new List<ListenerPrefix> (current)
                   : new List<ListenerPrefix> ();

          prefix.Listener = listener;
          addSpecial (future, prefix);
        }
        while (Interlocked.CompareExchange (ref _all, future, current) != current);

        return;
      }

      Dictionary<ListenerPrefix, HttpListener> prefs, prefs2;
      do {
        prefs = _prefixes;
        if (prefs.ContainsKey (prefix)) {
          var other = prefs [prefix];
          if (other != listener) // TODO: Code?
            throw new HttpListenerException (400, "There's another listener for " + prefix);

          return;
        }

        prefs2 = new Dictionary<ListenerPrefix, HttpListener> (prefs);
        prefs2 [prefix] = listener;
      }
      while (Interlocked.CompareExchange (ref _prefixes, prefs2, prefs) != prefs);
    }

    public bool BindContext (HttpListenerContext context)
    {
      ListenerPrefix prefix;
      var listener = searchListener (context.Request.Url, out prefix);
      if (listener == null)
        return false;

      context.Listener = listener;
      context.Connection.Prefix = prefix;

      return true;
    }

    public void Close ()
    {
      _socket.Close ();

      lock (((ICollection) _unregistered).SyncRoot) {
        var conns = new List<HttpConnection> (_unregistered.Keys);
        _unregistered.Clear ();
        foreach (var conn in conns)
          conn.Close (true);

        conns.Clear ();
      }
    }

    public void RemovePrefix (ListenerPrefix prefix, HttpListener listener)
    {
      List<ListenerPrefix> current, future;
      if (prefix.Host == "*") {
        do {
          current = _unhandled;
          future = current != null
                   ? new List<ListenerPrefix> (current)
                   : new List<ListenerPrefix> ();

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
                   ? new List<ListenerPrefix> (current)
                   : new List<ListenerPrefix> ();

          if (!removeSpecial (future, prefix))
            break; // Prefix not found.
        }
        while (Interlocked.CompareExchange (ref _all, future, current) != current);

        checkIfRemove ();
        return;
      }

      Dictionary<ListenerPrefix, HttpListener> prefs, prefs2;
      do {
        prefs = _prefixes;
        if (!prefs.ContainsKey (prefix))
          break;

        prefs2 = new Dictionary<ListenerPrefix, HttpListener> (prefs);
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
