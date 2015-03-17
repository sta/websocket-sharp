#region License
/*
 * HttpListener.cs
 *
 * This code is derived from HttpListener.cs (System.Net) of Mono
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
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Threading;

// TODO: Logging.
namespace WebSocketSharp.Net
{
  /// <summary>
  /// Provides a simple, programmatically controlled HTTP listener.
  /// </summary>
  public sealed class HttpListener : IDisposable
  {
    #region Private Fields

    private AuthenticationSchemes                                _authSchemes;
    private Func<HttpListenerRequest, AuthenticationSchemes>     _authSchemeSelector;
    private string                                               _certFolderPath;
    private Dictionary<HttpConnection, HttpConnection>           _connections;
    private object                                               _connectionsSync;
    private List<HttpListenerContext>                            _ctxQueue;
    private object                                               _ctxQueueSync;
    private Dictionary<HttpListenerContext, HttpListenerContext> _ctxRegistry;
    private object                                               _ctxRegistrySync;
    private Func<IIdentity, NetworkCredential>                   _credFinder;
    private bool                                                 _disposed;
    private bool                                                 _ignoreWriteExceptions;
    private bool                                                 _listening;
    private Logger                                               _logger;
    private HttpListenerPrefixCollection                         _prefixes;
    private string                                               _realm;
    private bool                                                 _reuseAddress;
    private ServerSslConfiguration                               _sslConfig;
    private List<HttpListenerAsyncResult>                        _waitQueue;
    private object                                               _waitQueueSync;

    #endregion

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpListener"/> class.
    /// </summary>
    public HttpListener ()
    {
      _authSchemes = AuthenticationSchemes.Anonymous;

      _connections = new Dictionary<HttpConnection, HttpConnection> ();
      _connectionsSync = ((ICollection) _connections).SyncRoot;

      _ctxQueue = new List<HttpListenerContext> ();
      _ctxQueueSync = ((ICollection) _ctxQueue).SyncRoot;

      _ctxRegistry = new Dictionary<HttpListenerContext, HttpListenerContext> ();
      _ctxRegistrySync = ((ICollection) _ctxRegistry).SyncRoot;

      _logger = new Logger ();

      _prefixes = new HttpListenerPrefixCollection (this);

      _waitQueue = new List<HttpListenerAsyncResult> ();
      _waitQueueSync = ((ICollection) _waitQueue).SyncRoot;
    }

    #endregion

    #region Internal Properties

    internal bool IsDisposed {
      get {
        return _disposed;
      }
    }

    internal bool ReuseAddress {
      get {
        return _reuseAddress;
      }

      set {
        _reuseAddress = value;
      }
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets or sets the scheme used to authenticate the clients.
    /// </summary>
    /// <value>
    /// One of the <see cref="WebSocketSharp.Net.AuthenticationSchemes"/> enum values,
    /// represents the scheme used to authenticate the clients. The default value is
    /// <see cref="WebSocketSharp.Net.AuthenticationSchemes.Anonymous"/>.
    /// </value>
    /// <exception cref="ObjectDisposedException">
    /// This listener has been closed.
    /// </exception>
    public AuthenticationSchemes AuthenticationSchemes {
      get {
        CheckDisposed ();
        return _authSchemes;
      }

      set {
        CheckDisposed ();
        _authSchemes = value;
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
    public Func<HttpListenerRequest, AuthenticationSchemes> AuthenticationSchemeSelector {
      get {
        CheckDisposed ();
        return _authSchemeSelector;
      }

      set {
        CheckDisposed ();
        _authSchemeSelector = value;
      }
    }

    /// <summary>
    /// Gets or sets the path to the folder in which stores the certificate files used to
    /// authenticate the server on the secure connection.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   This property represents the path to the folder in which stores the certificate files
    ///   associated with each port number of added URI prefixes. A set of the certificate files
    ///   is a pair of the <c>'port number'.cer</c> (DER) and <c>'port number'.key</c>
    ///   (DER, RSA Private Key).
    ///   </para>
    ///   <para>
    ///   If this property is <see langword="null"/> or empty, the result of
    ///   <c>System.Environment.GetFolderPath
    ///   (<see cref="Environment.SpecialFolder.ApplicationData"/>)</c> is used as the default path.
    ///   </para>
    /// </remarks>
    /// <value>
    /// A <see cref="string"/> that represents the path to the folder in which stores
    /// the certificate files. The default value is <see langword="null"/>.
    /// </value>
    /// <exception cref="ObjectDisposedException">
    /// This listener has been closed.
    /// </exception>
    public string CertificateFolderPath {
      get {
        CheckDisposed ();
        return _certFolderPath;
      }

      set {
        CheckDisposed ();
        _certFolderPath = value;
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
    public bool IgnoreWriteExceptions {
      get {
        CheckDisposed ();
        return _ignoreWriteExceptions;
      }

      set {
        CheckDisposed ();
        _ignoreWriteExceptions = value;
      }
    }

    /// <summary>
    /// Gets a value indicating whether the listener has been started.
    /// </summary>
    /// <value>
    /// <c>true</c> if the listener has been started; otherwise, <c>false</c>.
    /// </value>
    public bool IsListening {
      get {
        return _listening;
      }
    }

    /// <summary>
    /// Gets a value indicating whether the listener can be used with the current operating system.
    /// </summary>
    /// <value>
    /// <c>true</c>.
    /// </value>
    public static bool IsSupported {
      get {
        return true;
      }
    }

    /// <summary>
    /// Gets the logging functions.
    /// </summary>
    /// <remarks>
    /// The default logging level is <see cref="LogLevel.Error"/>. If you would like to change it,
    /// you should set the <c>Log.Level</c> property to any of the <see cref="LogLevel"/> enum
    /// values.
    /// </remarks>
    /// <value>
    /// A <see cref="Logger"/> that provides the logging functions.
    /// </value>
    public Logger Log {
      get {
        return _logger;
      }
    }

    /// <summary>
    /// Gets the URI prefixes handled by the listener.
    /// </summary>
    /// <value>
    /// A <see cref="HttpListenerPrefixCollection"/> that contains the URI prefixes.
    /// </value>
    /// <exception cref="ObjectDisposedException">
    /// This listener has been closed.
    /// </exception>
    public HttpListenerPrefixCollection Prefixes {
      get {
        CheckDisposed ();
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
    public string Realm {
      get {
        CheckDisposed ();
        return _realm != null && _realm.Length > 0
               ? _realm
               : (_realm = "SECRET AREA");
      }

      set {
        CheckDisposed ();
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
    public ServerSslConfiguration SslConfiguration {
      get {
        CheckDisposed ();
        return _sslConfig ?? (_sslConfig = new ServerSslConfiguration (null));
      }

      set {
        CheckDisposed ();
        _sslConfig = value;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether, when NTLM authentication is used,
    /// the authentication information of first request is used to authenticate
    /// additional requests on the same connection.
    /// </summary>
    /// <remarks>
    /// This property isn't currently supported and always throws
    /// a <see cref="NotSupportedException"/>.
    /// </remarks>
    /// <value>
    /// <c>true</c> if the authentication information of first request is used;
    /// otherwise, <c>false</c>.
    /// </value>
    /// <exception cref="NotSupportedException">
    /// Any use of this property.
    /// </exception>
    public bool UnsafeConnectionNtlmAuthentication {
      get {
        throw new NotSupportedException ();
      }

      set {
        throw new NotSupportedException ();
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
    public Func<IIdentity, NetworkCredential> UserCredentialsFinder {
      get {
        CheckDisposed ();
        return _credFinder ?? (_credFinder = id => null);
      }

      set {
        CheckDisposed ();
        _credFinder = value;
      }
    }

    #endregion

    #region Private Methods

    private void cleanup (bool force)
    {
      lock (_ctxRegistrySync) {
        if (!force)
          sendServiceUnavailable ();

        cleanupContextRegistry ();
        cleanupConnections ();
        cleanupWaitQueue ();
      }
    }

    private void cleanupConnections ()
    {
      lock (_connectionsSync) {
        if (_connections.Count == 0)
          return;

        // Need to copy this since closing will call RemoveConnection.
        var keys = _connections.Keys;
        var conns = new HttpConnection[keys.Count];
        keys.CopyTo (conns, 0);
        _connections.Clear ();
        for (var i = conns.Length - 1; i >= 0; i--)
          conns[i].Close (true);
      }
    }

    private void cleanupContextRegistry ()
    {
      lock (_ctxRegistrySync) {
        if (_ctxRegistry.Count == 0)
          return;

        // Need to copy this since closing will call UnregisterContext.
        var keys = _ctxRegistry.Keys;
        var ctxs = new HttpListenerContext[keys.Count];
        keys.CopyTo (ctxs, 0);
        _ctxRegistry.Clear ();
        for (var i = ctxs.Length - 1; i >= 0; i--)
          ctxs[i].Connection.Close (true);
      }
    }

    private void cleanupWaitQueue ()
    {
      lock (_waitQueueSync) {
        if (_waitQueue.Count == 0)
          return;

        var ex = new ObjectDisposedException (GetType ().ToString ());
        foreach (var ares in _waitQueue)
          ares.Complete (ex);

        _waitQueue.Clear ();
      }
    }

    private void close (bool force)
    {
      EndPointManager.RemoveListener (this);
      cleanup (force);
    }

    // Must be called with a lock on _ctxQueue.
    private HttpListenerContext getContextFromQueue ()
    {
      if (_ctxQueue.Count == 0)
        return null;

      var ctx = _ctxQueue[0];
      _ctxQueue.RemoveAt (0);

      return ctx;
    }

    private void sendServiceUnavailable ()
    {
      lock (_ctxQueueSync) {
        if (_ctxQueue.Count == 0)
          return;

        var ctxs = _ctxQueue.ToArray ();
        _ctxQueue.Clear ();
        foreach (var ctx in ctxs) {
          var res = ctx.Response;
          res.StatusCode = (int) HttpStatusCode.ServiceUnavailable;
          res.Close ();
        }
      }
    }

    #endregion

    #region Internal Methods

    internal void AddConnection (HttpConnection connection)
    {
      lock (_connectionsSync)
        _connections[connection] = connection;
    }

    internal bool Authenticate (HttpListenerContext context)
    {
      var schm = SelectAuthenticationScheme (context);
      if (schm == AuthenticationSchemes.Anonymous)
        return true;

      if (schm != AuthenticationSchemes.Basic && schm != AuthenticationSchemes.Digest) {
        context.Response.Close (HttpStatusCode.Forbidden);
        return false;
      }

      var realm = Realm;
      var req = context.Request;
      var user = HttpUtility.CreateUser (
        req.Headers["Authorization"], schm, realm, req.HttpMethod, UserCredentialsFinder);

      if (user != null && user.Identity.IsAuthenticated) {
        context.User = user;
        return true;
      }

      if (schm == AuthenticationSchemes.Basic)
        context.Response.CloseWithAuthChallenge (
          AuthenticationChallenge.CreateBasicChallenge (realm).ToBasicString ());

      if (schm == AuthenticationSchemes.Digest)
        context.Response.CloseWithAuthChallenge (
          AuthenticationChallenge.CreateDigestChallenge (realm).ToDigestString ());

      return false;
    }

    internal HttpListenerAsyncResult BeginGetContext (HttpListenerAsyncResult asyncResult)
    {
      CheckDisposed ();
      if (_prefixes.Count == 0)
        throw new InvalidOperationException ("The listener has no URI prefix on which listens.");

      if (!_listening)
        throw new InvalidOperationException ("The listener hasn't been started.");

      // Lock _waitQueue early to avoid race conditions.
      lock (_waitQueueSync) {
        lock (_ctxQueueSync) {
          var ctx = getContextFromQueue ();
          if (ctx != null) {
            asyncResult.Complete (ctx, true);
            return asyncResult;
          }
        }

        _waitQueue.Add (asyncResult);
      }

      return asyncResult;
    }

    internal void CheckDisposed ()
    {
      if (_disposed)
        throw new ObjectDisposedException (GetType ().ToString ());
    }

    internal void RegisterContext (HttpListenerContext context)
    {
      lock (_ctxRegistrySync)
        _ctxRegistry[context] = context;

      HttpListenerAsyncResult ares = null;
      lock (_waitQueueSync) {
        if (_waitQueue.Count == 0) {
          lock (_ctxQueueSync)
            _ctxQueue.Add (context);
        }
        else {
          ares = _waitQueue[0];
          _waitQueue.RemoveAt (0);
        }
      }

      if (ares != null)
        ares.Complete (context);
    }

    internal void RemoveConnection (HttpConnection connection)
    {
      lock (_connectionsSync)
        _connections.Remove (connection);
    }

    internal AuthenticationSchemes SelectAuthenticationScheme (HttpListenerContext context)
    {
      return AuthenticationSchemeSelector != null
             ? AuthenticationSchemeSelector (context.Request)
             : _authSchemes;
    }

    internal void UnregisterContext (HttpListenerContext context)
    {
      lock (_ctxRegistrySync)
        _ctxRegistry.Remove (context);

      lock (_ctxQueueSync) {
        var i = _ctxQueue.IndexOf (context);
        if (i >= 0)
          _ctxQueue.RemoveAt (i);
      }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Shuts down the listener immediately.
    /// </summary>
    public void Abort ()
    {
      if (_disposed)
        return;

      close (true);
      _disposed = true;
    }

    /// <summary>
    /// Begins getting an incoming request asynchronously.
    /// </summary>
    /// <remarks>
    /// This asynchronous operation must be completed by calling the <c>EndGetContext</c> method.
    /// Typically, the method is invoked by the <paramref name="callback"/> delegate.
    /// </remarks>
    /// <returns>
    /// An <see cref="IAsyncResult"/> that represents the status of the asynchronous operation.
    /// </returns>
    /// <param name="callback">
    /// An <see cref="AsyncCallback"/> delegate that references the method to invoke
    /// when the asynchronous operation completes.
    /// </param>
    /// <param name="state">
    /// An <see cref="object"/> that represents a user defined object to pass to
    /// the <paramref name="callback"/> delegate.
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///   <para>
    ///   This listener has no URI prefix on which listens.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   This listener hasn't been started, or is currently stopped.
    ///   </para>
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This listener has been closed.
    /// </exception>
    public IAsyncResult BeginGetContext (AsyncCallback callback, Object state)
    {
      return BeginGetContext (new HttpListenerAsyncResult (callback, state));
    }

    /// <summary>
    /// Shuts down the listener.
    /// </summary>
    public void Close ()
    {
      if (_disposed)
        return;

      close (false);
      _disposed = true;
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
    public HttpListenerContext EndGetContext (IAsyncResult asyncResult)
    {
      CheckDisposed ();
      if (asyncResult == null)
        throw new ArgumentNullException ("asyncResult");

      var ares = asyncResult as HttpListenerAsyncResult;
      if (ares == null)
        throw new ArgumentException ("A wrong IAsyncResult.", "asyncResult");

      if (ares.EndCalled)
        throw new InvalidOperationException ("This IAsyncResult cannot be reused.");

      ares.EndCalled = true;
      if (!ares.IsCompleted)
        ares.AsyncWaitHandle.WaitOne ();

      return ares.GetContext (); // This will throw on error.
    }

    /// <summary>
    /// Gets an incoming request.
    /// </summary>
    /// <remarks>
    /// This method waits for an incoming request, and returns when a request is received.
    /// </remarks>
    /// <returns>
    /// A <see cref="HttpListenerContext"/> that represents a request.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///   <para>
    ///   This listener has no URI prefix on which listens.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   This listener hasn't been started, or is currently stopped.
    ///   </para>
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This listener has been closed.
    /// </exception>
    public HttpListenerContext GetContext ()
    {
      var ares = BeginGetContext (new HttpListenerAsyncResult (null, null));
      ares.InGet = true;

      return EndGetContext (ares);
    }

    /// <summary>
    /// Starts receiving incoming requests.
    /// </summary>
    /// <exception cref="ObjectDisposedException">
    /// This listener has been closed.
    /// </exception>
    public void Start ()
    {
      CheckDisposed ();
      if (_listening)
        return;

      EndPointManager.AddListener (this);
      _listening = true;
    }

    /// <summary>
    /// Stops receiving incoming requests.
    /// </summary>
    /// <exception cref="ObjectDisposedException">
    /// This listener has been closed.
    /// </exception>
    public void Stop ()
    {
      CheckDisposed ();
      if (!_listening)
        return;

      _listening = false;
      EndPointManager.RemoveListener (this);
      sendServiceUnavailable ();
    }

    #endregion

    #region Explicit Interface Implementations

    /// <summary>
    /// Releases all resources used by the listener.
    /// </summary>
    void IDisposable.Dispose ()
    {
      if (_disposed)
        return;

      close (true);
      _disposed = true;
    }

    #endregion
  }
}
