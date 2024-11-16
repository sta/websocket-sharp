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
  /// <remarks>
  ///   <para>
  ///   The listener supports HTTP/1.1 version request and response.
  ///   </para>
  ///   <para>
  ///   And the listener allows to accept WebSocket handshake requests.
  ///   </para>
  ///   <para>
  ///   This class cannot be inherited.
  ///   </para>
  /// </remarks>
  public sealed class HttpListener : IDisposable
  {
    #region Private Fields

    private AuthenticationSchemes                            _authSchemes;
    private Func<HttpListenerRequest, AuthenticationSchemes> _authSchemeSelector;
    private string                                           _certFolderPath;
    private Queue<HttpListenerContext>                       _contextQueue;
    private LinkedList<HttpListenerContext>                  _contextRegistry;
    private object                                           _contextRegistrySync;
    private static readonly string                           _defaultRealm;
    private bool                                             _disposed;
    private bool                                             _ignoreWriteExceptions;
    private volatile bool                                    _isListening;
    private Logger                                           _log;
    private HttpListenerPrefixCollection                     _prefixes;
    private string                                           _realm;
    private bool                                             _reuseAddress;
    private ServerSslConfiguration                           _sslConfig;
    private object                                           _sync;
    private Func<IIdentity, NetworkCredential>               _userCredFinder;
    private Queue<HttpListenerAsyncResult>                   _waitQueue;

    #endregion

    #region Static Constructor

    static HttpListener ()
    {
      _defaultRealm = "SECRET AREA";
    }

    #endregion

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpListener"/> class.
    /// </summary>
    public HttpListener ()
    {
      _authSchemes = AuthenticationSchemes.Anonymous;
      _contextQueue = new Queue<HttpListenerContext> ();
      _contextRegistry = new LinkedList<HttpListenerContext> ();
      _contextRegistrySync = ((ICollection) _contextRegistry).SyncRoot;
      _log = new Logger ();
      _prefixes = new HttpListenerPrefixCollection (this);
      _sync = new object ();
      _waitQueue = new Queue<HttpListenerAsyncResult> ();
    }

    #endregion

    #region Internal Properties

    internal string ObjectName {
      get {
        return GetType ().ToString ();
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
    ///   <para>
    ///   One of the <see cref="WebSocketSharp.Net.AuthenticationSchemes"/>
    ///   enum values.
    ///   </para>
    ///   <para>
    ///   It represents the scheme used to authenticate the clients.
    ///   </para>
    ///   <para>
    ///   The default value is
    ///   <see cref="WebSocketSharp.Net.AuthenticationSchemes.Anonymous"/>.
    ///   </para>
    /// </value>
    /// <exception cref="ObjectDisposedException">
    /// This listener has been closed.
    /// </exception>
    public AuthenticationSchemes AuthenticationSchemes {
      get {
        if (_disposed)
          throw new ObjectDisposedException (ObjectName);

        return _authSchemes;
      }

      set {
        if (_disposed)
          throw new ObjectDisposedException (ObjectName);

        _authSchemes = value;
      }
    }

    /// <summary>
    /// Gets or sets the delegate called to determine the scheme used to
    /// authenticate the clients.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   If this property is set, the listener uses the authentication
    ///   scheme selected by the delegate for each request.
    ///   </para>
    ///   <para>
    ///   Or if this property is not set, the listener uses the value of
    ///   the <see cref="HttpListener.AuthenticationSchemes"/> property
    ///   as the authentication scheme for all requests.
    ///   </para>
    /// </remarks>
    /// <value>
    ///   <para>
    ///   A <see cref="T:System.Func{HttpListenerRequest, AuthenticationSchemes}"/>
    ///   delegate.
    ///   </para>
    ///   <para>
    ///   It represents the delegate called when the listener selects
    ///   an authentication scheme.
    ///   </para>
    ///   <para>
    ///   <see langword="null"/> if not necessary.
    ///   </para>
    ///   <para>
    ///   The default value is <see langword="null"/>.
    ///   </para>
    /// </value>
    /// <exception cref="ObjectDisposedException">
    /// This listener has been closed.
    /// </exception>
    public Func<HttpListenerRequest, AuthenticationSchemes> AuthenticationSchemeSelector {
      get {
        if (_disposed)
          throw new ObjectDisposedException (ObjectName);

        return _authSchemeSelector;
      }

      set {
        if (_disposed)
          throw new ObjectDisposedException (ObjectName);

        _authSchemeSelector = value;
      }
    }

    /// <summary>
    /// Gets or sets the path to the folder in which stores the certificate
    /// files used to authenticate the server on the secure connection.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   This property represents the path to the folder in which stores
    ///   the certificate files associated with each port number of added
    ///   URI prefixes.
    ///   </para>
    ///   <para>
    ///   A set of the certificate files is a pair of &lt;port number&gt;.cer
    ///   (DER) and &lt;port number&gt;.key (DER, RSA Private Key).
    ///   </para>
    ///   <para>
    ///   If this property is <see langword="null"/> or an empty string,
    ///   the result of the <see cref="Environment.SpecialFolder.ApplicationData"/>
    ///   with the <see cref="Environment.GetFolderPath"/> method is used as
    ///   the default path.
    ///   </para>
    /// </remarks>
    /// <value>
    ///   <para>
    ///   A <see cref="string"/> that represents the path to the folder
    ///   in which stores the certificate files.
    ///   </para>
    ///   <para>
    ///   The default value is <see langword="null"/>.
    ///   </para>
    /// </value>
    /// <exception cref="ObjectDisposedException">
    /// This listener has been closed.
    /// </exception>
    public string CertificateFolderPath {
      get {
        if (_disposed)
          throw new ObjectDisposedException (ObjectName);

        return _certFolderPath;
      }

      set {
        if (_disposed)
          throw new ObjectDisposedException (ObjectName);

        _certFolderPath = value;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the listener returns
    /// exceptions that occur when sending the response to the client.
    /// </summary>
    /// <value>
    ///   <para>
    ///   <c>true</c> if the listener should not return those exceptions;
    ///   otherwise, <c>false</c>.
    ///   </para>
    ///   <para>
    ///   The default value is <c>false</c>.
    ///   </para>
    /// </value>
    /// <exception cref="ObjectDisposedException">
    /// This listener has been closed.
    /// </exception>
    public bool IgnoreWriteExceptions {
      get {
        if (_disposed)
          throw new ObjectDisposedException (ObjectName);

        return _ignoreWriteExceptions;
      }

      set {
        if (_disposed)
          throw new ObjectDisposedException (ObjectName);

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
        return _isListening;
      }
    }

    /// <summary>
    /// Gets a value indicating whether the listener can be used with
    /// the current operating system.
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
    ///   <para>
    ///   The default logging level is <see cref="LogLevel.Error"/>.
    ///   </para>
    ///   <para>
    ///   If you would like to change it, you should set the <c>Log.Level</c>
    ///   property to any of the <see cref="LogLevel"/> enum values.
    ///   </para>
    /// </remarks>
    /// <value>
    /// A <see cref="Logger"/> that provides the logging functions.
    /// </value>
    /// <exception cref="ObjectDisposedException">
    /// This listener has been closed.
    /// </exception>
    public Logger Log {
      get {
        if (_disposed)
          throw new ObjectDisposedException (ObjectName);

        return _log;
      }
    }

    /// <summary>
    /// Gets the URI prefixes handled by the listener.
    /// </summary>
    /// <value>
    /// A <see cref="HttpListenerPrefixCollection"/> that contains the URI
    /// prefixes.
    /// </value>
    /// <exception cref="ObjectDisposedException">
    /// This listener has been closed.
    /// </exception>
    public HttpListenerPrefixCollection Prefixes {
      get {
        if (_disposed)
          throw new ObjectDisposedException (ObjectName);

        return _prefixes;
      }
    }

    /// <summary>
    /// Gets or sets the name of the realm associated with the listener.
    /// </summary>
    /// <remarks>
    /// If this property is <see langword="null"/> or an empty string,
    /// "SECRET AREA" is used as the name of the realm.
    /// </remarks>
    /// <value>
    ///   <para>
    ///   A <see cref="string"/> that represents the name of the realm.
    ///   </para>
    ///   <para>
    ///   The default value is <see langword="null"/>.
    ///   </para>
    /// </value>
    /// <exception cref="ObjectDisposedException">
    /// This listener has been closed.
    /// </exception>
    public string Realm {
      get {
        if (_disposed)
          throw new ObjectDisposedException (ObjectName);

        return _realm;
      }

      set {
        if (_disposed)
          throw new ObjectDisposedException (ObjectName);

        _realm = value;
      }
    }

    /// <summary>
    /// Gets the configuration for secure connection.
    /// </summary>
    /// <value>
    /// A <see cref="ServerSslConfiguration"/> that represents the
    /// configuration used to provide secure connections.
    /// </value>
    /// <exception cref="ObjectDisposedException">
    /// This listener has been closed.
    /// </exception>
    public ServerSslConfiguration SslConfiguration {
      get {
        if (_disposed)
          throw new ObjectDisposedException (ObjectName);

        if (_sslConfig == null)
          _sslConfig = new ServerSslConfiguration ();

        return _sslConfig;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether, when NTLM authentication is used,
    /// the authentication information of first request is used to authenticate
    /// additional requests on the same connection.
    /// </summary>
    /// <remarks>
    /// This property is not currently supported and always throws
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
    /// Gets or sets the delegate called to find the credentials for
    /// an identity used to authenticate a client.
    /// </summary>
    /// <value>
    ///   <para>
    ///   A <see cref="T:System.Func{IIdentity, NetworkCredential}"/>
    ///   delegate.
    ///   </para>
    ///   <para>
    ///   It represents the delegate called when the listener finds
    ///   the credentials used to authenticate a client.
    ///   </para>
    ///   <para>
    ///   It must return <see langword="null"/> if the credentials
    ///   are not found.
    ///   </para>
    ///   <para>
    ///   <see langword="null"/> if not necessary.
    ///   </para>
    ///   <para>
    ///   The default value is <see langword="null"/>.
    ///   </para>
    /// </value>
    /// <exception cref="ObjectDisposedException">
    /// This listener has been closed.
    /// </exception>
    public Func<IIdentity, NetworkCredential> UserCredentialsFinder {
      get {
        if (_disposed)
          throw new ObjectDisposedException (ObjectName);

        return _userCredFinder;
      }

      set {
        if (_disposed)
          throw new ObjectDisposedException (ObjectName);

        _userCredFinder = value;
      }
    }

    #endregion

    #region Private Methods

    private bool authenticateClient (HttpListenerContext context)
    {
      var schm = selectAuthenticationScheme (context.Request);

      if (schm == AuthenticationSchemes.Anonymous)
        return true;

      if (schm == AuthenticationSchemes.None) {
        var msg = "Authentication not allowed";

        context.SendError (403, msg);

        return false;
      }

      var realm = getRealm ();

      if (!context.SetUser (schm, realm, _userCredFinder)) {
        context.SendAuthenticationChallenge (schm, realm);

        return false;
      }

      return true;
    }

    private HttpListenerAsyncResult beginGetContext (
      AsyncCallback callback,
      object state
    )
    {
      lock (_contextRegistrySync) {
        if (!_isListening) {
          var msg = "The method is canceled.";

          throw new HttpListenerException (995, msg);
        }

        var ares = new HttpListenerAsyncResult (callback, state, _log);

        if (_contextQueue.Count == 0) {
          _waitQueue.Enqueue (ares);

          return ares;
        }

        var ctx = _contextQueue.Dequeue ();

        ares.Complete (ctx, true);

        return ares;
      }
    }

    private void cleanupContextQueue (bool force)
    {
      if (_contextQueue.Count == 0)
        return;

      if (force) {
        _contextQueue.Clear ();

        return;
      }

      var ctxs = _contextQueue.ToArray ();

      _contextQueue.Clear ();

      foreach (var ctx in ctxs)
        ctx.SendError (503);
    }

    private void cleanupContextRegistry ()
    {
      var cnt = _contextRegistry.Count;

      if (cnt == 0)
        return;

      var ctxs = new HttpListenerContext[cnt];

      lock (_contextRegistrySync) {
        _contextRegistry.CopyTo (ctxs, 0);
        _contextRegistry.Clear ();
      }

      foreach (var ctx in ctxs)
        ctx.Connection.Close (true);
    }

    private void cleanupWaitQueue (string message)
    {
      if (_waitQueue.Count == 0)
        return;

      var aress = _waitQueue.ToArray ();

      _waitQueue.Clear ();

      foreach (var ares in aress) {
        var ex = new HttpListenerException (995, message);

        ares.Complete (ex);
      }
    }

    private void close (bool force)
    {
      lock (_sync) {
        if (_disposed)
          return;

        lock (_contextRegistrySync) {
          if (!_isListening) {
            _disposed = true;

            return;
          }

          _isListening = false;
        }

        cleanupContextQueue (force);
        cleanupContextRegistry ();

        var msg = "The listener is closed.";

        cleanupWaitQueue (msg);

        EndPointManager.RemoveListener (this);

        _disposed = true;
      }
    }

    private string getRealm ()
    {
      var realm = _realm;

      return realm != null && realm.Length > 0 ? realm : _defaultRealm;
    }

    private bool registerContext (HttpListenerContext context)
    {
      if (!_isListening)
        return false;

      lock (_contextRegistrySync) {
        if (!_isListening)
          return false;

        context.Listener = this;

        _contextRegistry.AddLast (context);

        if (_waitQueue.Count == 0) {
          _contextQueue.Enqueue (context);

          return true;
        }

        var ares = _waitQueue.Dequeue ();

        ares.Complete (context, false);

        return true;
      }
    }

    private AuthenticationSchemes selectAuthenticationScheme (
      HttpListenerRequest request
    )
    {
      var selector = _authSchemeSelector;

      if (selector == null)
        return _authSchemes;

      try {
        return selector (request);
      }
      catch {
        return AuthenticationSchemes.None;
      }
    }

    #endregion

    #region Internal Methods

    internal void CheckDisposed ()
    {
      if (_disposed)
        throw new ObjectDisposedException (ObjectName);
    }

    internal bool RegisterContext (HttpListenerContext context)
    {
      if (!authenticateClient (context))
        return false;

      if (!registerContext (context)) {
        context.SendError (503);

        return false;
      }

      return true;
    }

    internal void UnregisterContext (HttpListenerContext context)
    {
      lock (_contextRegistrySync)
        _contextRegistry.Remove (context);
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
    }

    /// <summary>
    /// Begins getting an incoming request asynchronously.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   This asynchronous operation must be ended by calling
    ///   the <see cref="EndGetContext"/> method.
    ///   </para>
    ///   <para>
    ///   Typically, the <see cref="EndGetContext"/> method is called by
    ///   <paramref name="callback"/>.
    ///   </para>
    /// </remarks>
    /// <returns>
    /// An <see cref="IAsyncResult"/> instance that represents the status of
    /// the asynchronous operation.
    /// </returns>
    /// <param name="callback">
    ///   <para>
    ///   An <see cref="AsyncCallback"/> delegate.
    ///   </para>
    ///   <para>
    ///   It specifies the delegate called when the asynchronous operation is
    ///   complete.
    ///   </para>
    /// </param>
    /// <param name="state">
    /// An <see cref="object"/> that specifies a user defined object to pass to
    /// <paramref name="callback"/>.
    /// </param>
    /// <exception cref="HttpListenerException">
    /// This method is canceled.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   <para>
    ///   This listener has not been started or is currently stopped.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   This listener has no URI prefix on which listens.
    ///   </para>
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This listener has been closed.
    /// </exception>
    public IAsyncResult BeginGetContext (AsyncCallback callback, object state)
    {
      if (_disposed)
        throw new ObjectDisposedException (ObjectName);

      if (!_isListening) {
        var msg = "The listener has not been started.";

        throw new InvalidOperationException (msg);
      }

      if (_prefixes.Count == 0) {
        var msg = "The listener has no URI prefix on which listens.";

        throw new InvalidOperationException (msg);
      }

      return beginGetContext (callback, state);
    }

    /// <summary>
    /// Shuts down the listener.
    /// </summary>
    public void Close ()
    {
      if (_disposed)
        return;

      close (false);
    }

    /// <summary>
    /// Ends an asynchronous operation to get an incoming request.
    /// </summary>
    /// <remarks>
    /// This method ends an asynchronous operation started by calling
    /// the <see cref="BeginGetContext"/> method.
    /// </remarks>
    /// <returns>
    /// A <see cref="HttpListenerContext"/> that represents a request.
    /// </returns>
    /// <param name="asyncResult">
    /// An <see cref="IAsyncResult"/> instance obtained by calling
    /// the <see cref="BeginGetContext"/> method.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="asyncResult"/> was not obtained by calling
    /// the <see cref="BeginGetContext"/> method.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="asyncResult"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="HttpListenerException">
    /// This method is canceled.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   <para>
    ///   This listener has not been started or is currently stopped.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   This method was already called for <paramref name="asyncResult"/>.
    ///   </para>
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This listener has been closed.
    /// </exception>
    public HttpListenerContext EndGetContext (IAsyncResult asyncResult)
    {
      if (_disposed)
        throw new ObjectDisposedException (ObjectName);

      if (!_isListening) {
        var msg = "The listener has not been started.";

        throw new InvalidOperationException (msg);
      }

      if (asyncResult == null)
        throw new ArgumentNullException ("asyncResult");

      var ares = asyncResult as HttpListenerAsyncResult;

      if (ares == null) {
        var msg = "A wrong IAsyncResult instance.";

        throw new ArgumentException (msg, "asyncResult");
      }

      lock (ares.SyncRoot) {
        if (ares.EndCalled) {
          var msg = "This IAsyncResult instance cannot be reused.";

          throw new InvalidOperationException (msg);
        }

        ares.EndCalled = true;
      }

      if (!ares.IsCompleted)
        ares.AsyncWaitHandle.WaitOne ();

      return ares.Context;
    }

    /// <summary>
    /// Gets an incoming request.
    /// </summary>
    /// <remarks>
    /// This method waits for an incoming request and returns when
    /// a request is received.
    /// </remarks>
    /// <returns>
    /// A <see cref="HttpListenerContext"/> that represents a request.
    /// </returns>
    /// <exception cref="HttpListenerException">
    /// This method is canceled.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   <para>
    ///   This listener has not been started or is currently stopped.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   This listener has no URI prefix on which listens.
    ///   </para>
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This listener has been closed.
    /// </exception>
    public HttpListenerContext GetContext ()
    {
      if (_disposed)
        throw new ObjectDisposedException (ObjectName);

      if (!_isListening) {
        var msg = "The listener has not been started.";

        throw new InvalidOperationException (msg);
      }

      if (_prefixes.Count == 0) {
        var msg = "The listener has no URI prefix on which listens.";

        throw new InvalidOperationException (msg);
      }

      var ares = beginGetContext (null, null);

      ares.EndCalled = true;

      if (!ares.IsCompleted)
        ares.AsyncWaitHandle.WaitOne ();

      return ares.Context;
    }

    /// <summary>
    /// Starts receiving incoming requests.
    /// </summary>
    /// <exception cref="ObjectDisposedException">
    /// This listener has been closed.
    /// </exception>
    public void Start ()
    {
      if (_disposed)
        throw new ObjectDisposedException (ObjectName);

      lock (_sync) {
        if (_disposed)
          throw new ObjectDisposedException (ObjectName);

        lock (_contextRegistrySync) {
          if (_isListening)
            return;

          EndPointManager.AddListener (this);

          _isListening = true;
        }
      }
    }

    /// <summary>
    /// Stops receiving incoming requests.
    /// </summary>
    /// <exception cref="ObjectDisposedException">
    /// This listener has been closed.
    /// </exception>
    public void Stop ()
    {
      if (_disposed)
        throw new ObjectDisposedException (ObjectName);

      lock (_sync) {
        if (_disposed)
          throw new ObjectDisposedException (ObjectName);

        lock (_contextRegistrySync) {
          if (!_isListening)
            return;

          _isListening = false;
        }

        cleanupContextQueue (false);
        cleanupContextRegistry ();

        var msg = "The listener is stopped.";

        cleanupWaitQueue (msg);

        EndPointManager.RemoveListener (this);
      }
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
    }

    #endregion
  }
}
