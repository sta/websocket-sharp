/*
 * HttpListener.cs
 *
 * This code is derived from System.Net.HttpListener.cs of Mono
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
    using System.Security.Principal;

    /// <summary>
	/// Provides a simple, programmatically controlled HTTP listener.
	/// </summary>
	internal sealed class HttpListener : IDisposable
    {
        private readonly Func<HttpListenerRequest, AuthenticationSchemes> _authSchemeSelector;
        private readonly Dictionary<HttpConnection, HttpConnection> _connections;
        private readonly object _connectionsSync;
        private readonly List<HttpListenerContext> _ctxQueue;
        private readonly object _ctxQueueSync;
        private readonly Dictionary<HttpListenerContext, HttpListenerContext> _ctxRegistry;
        private readonly object _ctxRegistrySync;
        private readonly List<ListenerAsyncResult> _waitQueue;
        private readonly object _waitQueueSync;
        private readonly HttpListenerPrefixCollection _prefixes;
        private Func<IIdentity, NetworkCredential> _credFinder;
        private bool _disposed;
        private AuthenticationSchemes _authSchemes;
        private bool _ignoreWriteExceptions;
        private bool _listening;
        private string _realm;
        private bool _reuseAddress;

        private ServerSslConfiguration _sslConfig;

        /// <summary>
        /// Initializes a new instance of the <see cref="HttpListener"/> class.
        /// </summary>
        public HttpListener()
        {
            _authSchemes = AuthenticationSchemes.Anonymous;

            _connections = new Dictionary<HttpConnection, HttpConnection>();
            _connectionsSync = ((ICollection)_connections).SyncRoot;

            _ctxQueue = new List<HttpListenerContext>();
            _ctxQueueSync = ((ICollection)_ctxQueue).SyncRoot;

            _ctxRegistry = new Dictionary<HttpListenerContext, HttpListenerContext>();
            _ctxRegistrySync = ((ICollection)_ctxRegistry).SyncRoot;

            _prefixes = new HttpListenerPrefixCollection(this);

            _waitQueue = new List<ListenerAsyncResult>();
            _waitQueueSync = ((ICollection)_waitQueue).SyncRoot;
        }
        
        internal bool ReuseAddress
        {
            get
            {
                return _reuseAddress;
            }

            set
            {
                _reuseAddress = value;
            }
        }
        
        /// <summary>
        /// Gets or sets the delegate called to select the scheme used to authenticate the clients.
        /// </summary>
        /// <remarks>
        /// If you set this property, the listener uses the authentication scheme selected by
        /// the delegate for each request. Or if you don't set, the listener uses the value of
        /// the <see cref="HttpListener.AuthenticationSchemes"/> property as the authentication
        /// scheme for all requests.
        /// </remarks>
        /// <value>
        /// A <c>Func&lt;<see cref="HttpListenerRequest"/>, <see cref="AuthenticationSchemes"/>&gt;</c>
        /// delegate that references the method used to select an authentication scheme. The default
        /// value is <see langword="null"/>.
        /// </value>
        /// <exception cref="ObjectDisposedException">
        /// This listener has been closed.
        /// </exception>
        public Func<HttpListenerRequest, AuthenticationSchemes> AuthenticationSchemeSelector
        {
            get
            {
                CheckDisposed();
                return _authSchemeSelector;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the listener returns exceptions that occur when
        /// sending the response to the client.
        /// </summary>
        /// <value>
        /// <c>true</c> if the listener shouldn't return those exceptions; otherwise, <c>false</c>.
        /// The default value is <c>false</c>.
        /// </value>
        /// <exception cref="ObjectDisposedException">
        /// This listener has been closed.
        /// </exception>
        public bool IgnoreWriteExceptions
        {
            get
            {
                CheckDisposed();
                return _ignoreWriteExceptions;
            }

            set
            {
                CheckDisposed();
                _ignoreWriteExceptions = value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the listener has been started.
        /// </summary>
        /// <value>
        /// <c>true</c> if the listener has been started; otherwise, <c>false</c>.
        /// </value>
        public bool IsListening => _listening;
        
        /// <summary>
		/// Gets the URI prefixes handled by the listener.
		/// </summary>
		/// <value>
		/// A <see cref="HttpListenerPrefixCollection"/> that contains the URI prefixes.
		/// </value>
		/// <exception cref="ObjectDisposedException">
		/// This listener has been closed.
		/// </exception>
		public HttpListenerPrefixCollection Prefixes
        {
            get
            {
                CheckDisposed();
                return _prefixes;
            }
        }

        /// <summary>
        /// Gets or sets the name of the realm associated with the listener.
        /// </summary>
        /// <value>
        /// A <see cref="string"/> that represents the name of the realm. The default value is
        /// <c>"SECRET AREA"</c>.
        /// </value>
        /// <exception cref="ObjectDisposedException">
        /// This listener has been closed.
        /// </exception>
        public string Realm
        {
            get
            {
                CheckDisposed();
                return _realm != null && _realm.Length > 0
                       ? _realm
                       : (_realm = "SECRET AREA");
            }

            set
            {
                CheckDisposed();
                _realm = value;
            }
        }

        /// <summary>
        /// Gets or sets the SSL configuration used to authenticate the server and
        /// optionally the client for secure connection.
        /// </summary>
        /// <value>
        /// A <see cref="ServerSslConfiguration"/> that represents the configuration used
        /// to authenticate the server and optionally the client for secure connection.
        /// </value>
        /// <exception cref="ObjectDisposedException">
        /// This listener has been closed.
        /// </exception>
        public ServerSslConfiguration SslConfiguration
        {
            get
            {
                CheckDisposed();
                return _sslConfig ?? (_sslConfig = new ServerSslConfiguration(null));
            }

            set
            {
                CheckDisposed();
                _sslConfig = value;
            }
        }
        
        /// <summary>
        /// Gets or sets the delegate called to find the credentials for an identity used to
        /// authenticate a client.
        /// </summary>
        /// <value>
        /// A <c>Func&lt;<see cref="IIdentity"/>, <see cref="NetworkCredential"/>&gt;</c> delegate
        /// that references the method used to find the credentials. The default value is a function
        /// that only returns <see langword="null"/>.
        /// </value>
        /// <exception cref="ObjectDisposedException">
        /// This listener has been closed.
        /// </exception>
        public Func<IIdentity, NetworkCredential> UserCredentialsFinder
        {
            get
            {
                CheckDisposed();
                return _credFinder ?? (_credFinder = id => null);
            }

            set
            {
                CheckDisposed();
                _credFinder = value;
            }
        }

        private void cleanup(bool force)
        {
            lock (_ctxRegistrySync)
            {
                if (!force)
                    sendServiceUnavailable();

                cleanupContextRegistry();
                cleanupConnections();
                cleanupWaitQueue();
            }
        }

        private void cleanupConnections()
        {
            lock (_connectionsSync)
            {
                if (_connections.Count == 0)
                    return;

                // Need to copy this since closing will call RemoveConnection.
                var keys = _connections.Keys;
                var conns = new HttpConnection[keys.Count];
                keys.CopyTo(conns, 0);
                _connections.Clear();
                for (var i = conns.Length - 1; i >= 0; i--)
                    conns[i].Close(true);
            }
        }

        private void cleanupContextRegistry()
        {
            lock (_ctxRegistrySync)
            {
                if (_ctxRegistry.Count == 0)
                    return;

                // Need to copy this since closing will call UnregisterContext.
                var keys = _ctxRegistry.Keys;
                var ctxs = new HttpListenerContext[keys.Count];
                keys.CopyTo(ctxs, 0);
                _ctxRegistry.Clear();
                for (var i = ctxs.Length - 1; i >= 0; i--)
                    ctxs[i].Connection.Close(true);
            }
        }

        private void cleanupWaitQueue()
        {
            lock (_waitQueueSync)
            {
                if (_waitQueue.Count == 0)
                    return;

                var ex = new ObjectDisposedException(GetType().ToString());
                foreach (var ares in _waitQueue)
                    ares.Complete(ex);

                _waitQueue.Clear();
            }
        }

        private void close(bool force)
        {
            EndPointManager.RemoveListener(this);
            cleanup(force);
        }

        // Must be called with a lock on _ctxQueue.
        private HttpListenerContext getContextFromQueue()
        {
            if (_ctxQueue.Count == 0)
                return null;

            var ctx = _ctxQueue[0];
            _ctxQueue.RemoveAt(0);

            return ctx;
        }

        private void sendServiceUnavailable()
        {
            lock (_ctxQueueSync)
            {
                if (_ctxQueue.Count == 0)
                    return;

                var ctxs = _ctxQueue.ToArray();
                _ctxQueue.Clear();
                foreach (var ctx in ctxs)
                {
                    var res = ctx.Response;
                    res.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
                    res.Close();
                }
            }
        }

        internal void AddConnection(HttpConnection connection)
        {
            lock (_connectionsSync)
                _connections[connection] = connection;
        }

        internal ListenerAsyncResult BeginGetContext(ListenerAsyncResult asyncResult)
        {
            CheckDisposed();
            if (_prefixes.Count == 0)
                throw new InvalidOperationException("The listener has no URI prefix on which listens.");

            if (!_listening)
                throw new InvalidOperationException("The listener hasn't been started.");

            // Lock _waitQueue early to avoid race conditions.
            lock (_waitQueueSync)
            {
                lock (_ctxQueueSync)
                {
                    var ctx = getContextFromQueue();
                    if (ctx != null)
                    {
                        asyncResult.Complete(ctx, true);
                        return asyncResult;
                    }
                }

                _waitQueue.Add(asyncResult);
            }

            return asyncResult;
        }

        internal void CheckDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().ToString());
        }

        internal void RegisterContext(HttpListenerContext context)
        {
            lock (_ctxRegistrySync)
                _ctxRegistry[context] = context;

            ListenerAsyncResult ares = null;
            lock (_waitQueueSync)
            {
                if (_waitQueue.Count == 0)
                {
                    lock (_ctxQueueSync)
                        _ctxQueue.Add(context);
                }
                else
                {
                    ares = _waitQueue[0];
                    _waitQueue.RemoveAt(0);
                }
            }

            if (ares != null)
                ares.Complete(context);
        }

        internal void RemoveConnection(HttpConnection connection)
        {
            lock (_connectionsSync)
                _connections.Remove(connection);
        }

        internal AuthenticationSchemes SelectAuthenticationScheme(HttpListenerContext context)
        {
            return AuthenticationSchemeSelector != null
                   ? AuthenticationSchemeSelector(context.Request)
                   : _authSchemes;
        }

        internal void UnregisterContext(HttpListenerContext context)
        {
            lock (_ctxRegistrySync)
                _ctxRegistry.Remove(context);

            lock (_ctxQueueSync)
            {
                var i = _ctxQueue.IndexOf(context);
                if (i >= 0)
                    _ctxQueue.RemoveAt(i);
            }
        }
        
        /// <summary>
        /// Ends an asynchronous operation to get an incoming request.
        /// </summary>
        /// <remarks>
        /// This method completes an asynchronous operation started by calling
        /// the <c>BeginGetContext</c> method.
        /// </remarks>
        /// <returns>
        /// A <see cref="HttpListenerContext"/> that represents a request.
        /// </returns>
        /// <param name="asyncResult">
        /// An <see cref="IAsyncResult"/> obtained by calling the <c>BeginGetContext</c> method.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="asyncResult"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="asyncResult"/> wasn't obtained by calling the <c>BeginGetContext</c> method.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// This method was already called for the specified <paramref name="asyncResult"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// This listener has been closed.
        /// </exception>
        public HttpListenerContext EndGetContext(IAsyncResult asyncResult)
        {
            CheckDisposed();
            if (asyncResult == null)
                throw new ArgumentNullException("asyncResult");

            var ares = asyncResult as ListenerAsyncResult;
            if (ares == null)
                throw new ArgumentException("A wrong IAsyncResult.", "asyncResult");

            if (ares.EndCalled)
                throw new InvalidOperationException("This IAsyncResult cannot be reused.");

            ares.EndCalled = true;
            if (!ares.IsCompleted)
                ares.AsyncWaitHandle.WaitOne();

            lock (_waitQueueSync)
            {
                var i = _waitQueue.IndexOf(ares);
                if (i >= 0)
                    _waitQueue.RemoveAt(i);
            }

            var ctx = ares.GetContext();
            var schm = SelectAuthenticationScheme(ctx);
            if (schm != AuthenticationSchemes.Anonymous)
                ctx.SetUser(schm, Realm, UserCredentialsFinder);

            return ctx; // This will throw on error.
        }
        
        /// <summary>
        /// Releases all resources used by the listener.
        /// </summary>
        void IDisposable.Dispose()
        {
            if (_disposed)
                return;

            close(true);
            _disposed = true;
        }
    }
}
