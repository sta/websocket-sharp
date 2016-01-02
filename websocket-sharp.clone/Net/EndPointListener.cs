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

/*
 * Authors:
 * - Gonzalo Paniagua Javier <gonzalo@novell.com>
 */

/*
 * Contributors:
 * - Liryna <liryna.stark@gmail.com>
 */

namespace WebSocketSharp.Net
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Sockets;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class EndPointListener
    {
        private List<HttpListenerPrefix> _all; // host == '+'

        private static readonly string DefaultCertFolderPath;

        private readonly IPEndPoint _endpoint;

        private readonly Socket _socket;

        private readonly ServerSslConfiguration _sslConfig;

        private readonly Dictionary<HttpConnection, HttpConnection> _unregistered;

        private readonly object _unregisteredSync;

        private Dictionary<HttpListenerPrefix, HttpListener> _prefixes;

        private List<HttpListenerPrefix> _unhandled; // host == '*'

        static EndPointListener()
        {
            DefaultCertFolderPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }

        public EndPointListener(IPAddress address, int port, ServerSslConfiguration sslConfig, bool reuseAddress)
        {
            _sslConfig = sslConfig;

            _prefixes = new Dictionary<HttpListenerPrefix, HttpListener>();

            _unregistered = new Dictionary<HttpConnection, HttpConnection>();
            _unregisteredSync = ((ICollection)_unregistered).SyncRoot;

            _socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            if (reuseAddress)
            {
                _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            }

            _endpoint = new IPEndPoint(address, port);
            _socket.Bind(_endpoint);
            _socket.Listen(500);

            var args = new SocketAsyncEventArgs();
            args.UserToken = this;
            args.Completed += OnAccept;
            _socket.AcceptAsync(args);
        }

        public bool IsSecure => _sslConfig != null;

        public ServerSslConfiguration SslConfiguration => _sslConfig;

        private static void AddSpecial(List<HttpListenerPrefix> prefixes, HttpListenerPrefix prefix)
        {
            if (prefixes == null) return;

            var path = prefix.Path;
            if (prefixes.Any(pref => pref.Path == path))
            {
                throw new HttpListenerException(400, "The prefix is already in use."); // TODO: Code?
            }

            prefixes.Add(prefix);
        }

        private async Task CheckIfRemove()
        {
            if (_prefixes.Count > 0)
            {
                return;
            }

            if (_unhandled != null && _unhandled.Count > 0)
            {
                return;
            }

            if (_all != null && _all.Count > 0)
            {
                return;
            }

            await EndPointManager.RemoveEndPoint(this, _endpoint).ConfigureAwait(false);
        }

        private static HttpListener MatchFromList(
            string path,
            IEnumerable<HttpListenerPrefix> list,
            out HttpListenerPrefix prefix)
        {
            prefix = null;
            if (list == null)
            {
                return null;
            }

            HttpListener bestMatch = null;
            var bestLen = -1;
            foreach (var pref in list)
            {
                var ppath = pref.Path;
                if (ppath.Length < bestLen)
                {
                    continue;
                }

                if (path.StartsWith(ppath))
                {
                    bestLen = ppath.Length;
                    bestMatch = pref.Listener;
                    prefix = pref;
                }
            }

            return bestMatch;
        }

        private static async void OnAccept(object sender, EventArgs e)
        {
            var args = (SocketAsyncEventArgs)e;
            var epl = (EndPointListener)args.UserToken;
            Socket accepted = null;
            if (args.SocketError == SocketError.Success)
            {
                accepted = args.AcceptSocket;
                args.AcceptSocket = null;
            }

            try
            {
                epl._socket.AcceptAsync(args);
            }
            catch
            {
                accepted?.Close();

                return;
            }

            if (accepted == null)
            {
                return;
            }

            HttpConnection conn = null;
            try
            {
                conn = new HttpConnection(accepted, epl);
                lock (epl._unregisteredSync) epl._unregistered[conn] = conn;

                await conn.ReadRequest().ConfigureAwait(false);
            }
            catch
            {
                if (conn != null)
                {
                    await conn.Close(true).ConfigureAwait(false);
                    return;
                }

                accepted.Close();
            }
        }

        private static bool RemoveSpecial(List<HttpListenerPrefix> prefixes, HttpListenerPrefix prefix)
        {
            if (prefixes == null) return false;

            var path = prefix.Path;
            var cnt = prefixes.Count;
            for (var i = 0; i < cnt; i++)
            {
                if (prefixes[i].Path == path)
                {
                    prefixes.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }

        private HttpListener SearchListener(Uri uri, out HttpListenerPrefix prefix)
        {
            prefix = null;
            if (uri == null) return null;

            var host = uri.Host;
            var port = uri.Port;
            var path = HttpUtility.UrlDecode(uri.AbsolutePath);
            var pathSlash = path[path.Length - 1] == '/' ? path : path + "/";

            HttpListener bestMatch = null;
            var bestLen = -1;
            if (host.Length > 0)
            {
                foreach (var pref in _prefixes.Keys)
                {
                    var ppath = pref.Path;
                    if (ppath.Length < bestLen) continue;

                    if (pref.Host != host || pref.Port != port) continue;

                    if (path.StartsWith(ppath) || pathSlash.StartsWith(ppath))
                    {
                        bestLen = ppath.Length;
                        bestMatch = _prefixes[pref];
                        prefix = pref;
                    }
                }

                if (bestLen != -1)
                {
                    return bestMatch;
                }
            }

            var list = _unhandled;
            bestMatch = MatchFromList(path, list, out prefix);
            if (path != pathSlash && bestMatch == null)
            {
                bestMatch = MatchFromList(pathSlash, list, out prefix);
            }

            if (bestMatch != null)
            {
                return bestMatch;
            }

            list = _all;
            bestMatch = MatchFromList(path, list, out prefix);
            if (path != pathSlash && bestMatch == null)
            {
                bestMatch = MatchFromList(pathSlash, list, out prefix);
            }

            if (bestMatch != null)
            {
                return bestMatch;
            }

            return null;
        }

        internal void RemoveConnection(HttpConnection connection)
        {
            lock (_unregisteredSync) _unregistered.Remove(connection);
        }

        public void AddPrefix(HttpListenerPrefix prefix, HttpListener httpListener)
        {
            List<HttpListenerPrefix> current, future;
            if (prefix.Host == "*")
            {
                do
                {
                    current = _unhandled;
                    future = current != null ? new List<HttpListenerPrefix>(current) : new List<HttpListenerPrefix>();

                    prefix.Listener = httpListener;
                    AddSpecial(future, prefix);
                }
                while (Interlocked.CompareExchange(ref _unhandled, future, current) != current);

                return;
            }

            if (prefix.Host == "+")
            {
                do
                {
                    current = _all;
                    future = current != null ? new List<HttpListenerPrefix>(current) : new List<HttpListenerPrefix>();

                    prefix.Listener = httpListener;
                    AddSpecial(future, prefix);
                }
                while (Interlocked.CompareExchange(ref _all, future, current) != current);

                return;
            }

            Dictionary<HttpListenerPrefix, HttpListener> prefs, prefs2;
            do
            {
                prefs = _prefixes;
                if (prefs.ContainsKey(prefix))
                {
                    var other = prefs[prefix];
                    if (other != httpListener) throw new HttpListenerException(400, String.Format("There's another listener for {0}.", prefix)); // TODO: Code?

                    return;
                }

                prefs2 = new Dictionary<HttpListenerPrefix, HttpListener>(prefs);
                prefs2[prefix] = httpListener;
            }
            while (Interlocked.CompareExchange(ref _prefixes, prefs2, prefs) != prefs);
        }

        public bool BindContext(HttpListenerContext context)
        {
            HttpListenerPrefix pref;
            var httpl = SearchListener(context.Request.Url, out pref);
            if (httpl == null) return false;

            context.Listener = httpl;
            context.Connection.Prefix = pref;

            return true;
        }

        public async Task Close()
        {
            _socket.Close();

            var conns = new List<HttpConnection>(_unregistered.Keys);
            _unregistered.Clear();
            var closeTasks = conns.Select(conn => conn.Close(true));
            await Task.WhenAll(closeTasks).ConfigureAwait(false);
        }

        public async Task RemovePrefix(HttpListenerPrefix prefix)
        {
            List<HttpListenerPrefix> current, future;
            if (prefix.Host == "*")
            {
                do
                {
                    current = _unhandled;
                    future = current != null ? new List<HttpListenerPrefix>(current) : new List<HttpListenerPrefix>();

                    if (!RemoveSpecial(future, prefix)) break; // Prefix not found.
                }
                while (Interlocked.CompareExchange(ref _unhandled, future, current) != current);

                await CheckIfRemove().ConfigureAwait(false);
                return;
            }

            if (prefix.Host == "+")
            {
                do
                {
                    current = _all;
                    future = current != null ? new List<HttpListenerPrefix>(current) : new List<HttpListenerPrefix>();

                    if (!RemoveSpecial(future, prefix)) break; // Prefix not found.
                }
                while (Interlocked.CompareExchange(ref _all, future, current) != current);

                await CheckIfRemove().ConfigureAwait(false);
                return;
            }

            Dictionary<HttpListenerPrefix, HttpListener> prefs, prefs2;
            do
            {
                prefs = _prefixes;
                if (!prefs.ContainsKey(prefix)) break;

                prefs2 = new Dictionary<HttpListenerPrefix, HttpListener>(prefs);
                prefs2.Remove(prefix);
            }
            while (Interlocked.CompareExchange(ref _prefixes, prefs2, prefs) != prefs);

            await CheckIfRemove().ConfigureAwait(false);
        }

        public void UnbindContext(HttpListenerContext context)
        {
            if (context == null || context.Listener == null) return;

            context.Listener.UnregisterContext(context);
        }
    }
}