#region License
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
    private List<HttpListenerContext>                            _contextQueue;
    private Func<IIdentity, NetworkCredential>                   _credentialsFinder;
    private X509Certificate2                                     _defaultCert;
    private bool                                                 _disposed;
    private bool                                                 _ignoreWriteExceptions;
    private bool                                                 _listening;
    private HttpListenerPrefixCollection                         _prefixes;
    private string                                               _realm;
    private Dictionary<HttpListenerContext, HttpListenerContext> _registry;
    private List<ListenerAsyncResult>                            _waitQueue;

    #endregion

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpListener"/> class.
    /// </summary>
    public HttpListener ()
    {
      _authSchemes = AuthenticationSchemes.Anonymous;
      _connections = new Dictionary<HttpConnection, HttpConnection> ();
      _contextQueue = new List<HttpListenerContext> ();
      _prefixes = new HttpListenerPrefixCollection (this);
      _registry = new Dictionary<HttpListenerContext, HttpListenerContext> ();
      _waitQueue = new List<ListenerAsyncResult> ();
    }

    #endregion

    #region Internal Properties

    internal bool IsDisposed {
      get {
        return _disposed;
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
    ///   <para>
    ///   If you set this property, the listener uses the authentication scheme selected by
    ///   the delegate for each request.
    ///   </para>
    ///   <para>
    ///   If you don't set, the listener uses the value of the <c>AuthenticationSchemes</c>
    ///   property as the authentication scheme for all requests.
    ///   </para>
    /// </remarks>
    /// <value>
    /// A <c>Func&lt;<see cref="HttpListenerRequest"/>, <see cref="AuthenticationSchemes"/>&gt;</c>
    /// delegate that invokes the method(s) used to select an authentication scheme. The default
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
    /// Gets or sets the default certificate used to authenticate the server on the secure
    /// connection.
    /// </summary>
    /// <value>
    /// A <see cref="X509Certificate2"/> used to authenticate the server if the certificate
    /// files aren't found in the <see cref="CertificateFolderPath"/>. The default value is
    /// <see langword="null"/>.
    /// </value>
    /// <exception cref="ObjectDisposedException">
    /// This listener has been closed.
    /// </exception>
    public X509Certificate2 DefaultCertificate {
      get {
        CheckDisposed ();
        return _defaultCert;
      }

      set {
        CheckDisposed ();
        _defaultCert = value;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the listener returns exceptions that occur when
    /// sending the response to the client.
    /// </summary>
    /// <value>
    /// <c>true</c> if the listener doesn't return exceptions that occur when sending the response
    /// to the client; otherwise, <c>false</c>. The default value is <c>false</c>.
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
    /// <c>SECRET AREA</c>.
    /// </value>
    /// <exception cref="ObjectDisposedException">
    /// This listener has been closed.
    /// </exception>
    public string Realm {
      get {
        CheckDisposed ();
        return _realm == null || _realm.Length == 0
               ? (_realm = "SECRET AREA")
               : _realm;
      }

      set {
        CheckDisposed ();
        _realm = value;
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
    /// that invokes the method(s) used to find the credentials. The default value is a function
    /// that only returns <see langword="null"/>.
    /// </value>
    /// <exception cref="ObjectDisposedException">
    /// This listener has been closed.
    /// </exception>
    public Func<IIdentity, NetworkCredential> UserCredentialsFinder {
      get {
        CheckDisposed ();
        return _credentialsFinder ?? (_credentialsFinder = identity => null);
      }

      set {
        CheckDisposed ();
        _credentialsFinder = value;
      }
    }

    #endregion

    #region Private Methods

    private void cleanup (bool force)
    {
      lock (((ICollection) _registry).SyncRoot) {
        if (!force)
          sendServiceUnavailable ();

        cleanupContextRegistry ();
        cleanupConnections ();
        cleanupWaitQueue ();
      }
    }

    private void cleanupConnections ()
    {
      lock (((ICollection) _connections).SyncRoot) {
        if (_connections.Count == 0)
          return;

        // Need to copy this since closing will call RemoveConnection.
        var keys = _connections.Keys;
        var conns = new HttpConnection [keys.Count];
        keys.CopyTo (conns, 0);
        _connections.Clear ();
        for (var i = conns.Length - 1; i >= 0; i--)
          conns [i].Close (true);
      }
    }

    private void cleanupContextRegistry ()
    {
      lock (((ICollection) _registry).SyncRoot) {
        if (_registry.Count == 0)
          return;

        // Need to copy this since closing will call UnregisterContext.
        var keys = _registry.Keys;
        var all = new HttpListenerContext [keys.Count];
        keys.CopyTo (all, 0);
        _registry.Clear ();
        for (var i = all.Length - 1; i >= 0; i--)
          all [i].Connection.Close (true);
      }
    }

    private void cleanupWaitQueue ()
    {
      lock (((ICollection) _waitQueue).SyncRoot) {
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

    // Must be called with a lock on _contextQueue.
    private HttpListenerContext getContextFromQueue ()
    {
      if (_contextQueue.Count == 0)
        return null;

      var context = _contextQueue [0];
      _contextQueue.RemoveAt (0);

      return context;
    }

    private void sendServiceUnavailable ()
    {
      lock (((ICollection) _contextQueue).SyncRoot) {
        if (_contextQueue.Count == 0)
          return;

        var contexts = _contextQueue.ToArray ();
        _contextQueue.Clear ();
        foreach (var context in contexts) {
          var res = context.Response;
          res.StatusCode = (int) HttpStatusCode.ServiceUnavailable;
          res.Close ();
        }
      }
    }

    #endregion

    #region Internal Methods

    internal void AddConnection (HttpConnection connection)
    {
      lock (((ICollection) _connections).SyncRoot)
        _connections [connection] = connection;
    }

    internal ListenerAsyncResult BeginGetContext (ListenerAsyncResult asyncResult)
    {
      CheckDisposed ();
      if (_prefixes.Count == 0)
        throw new InvalidOperationException ("Please, call AddPrefix before using this method.");

      if (!_listening)
        throw new InvalidOperationException ("Please, call Start before using this method.");

      // Lock _waitQueue early to avoid race conditions.
      lock (((ICollection) _waitQueue).SyncRoot) {
        lock (((ICollection) _contextQueue).SyncRoot) {
          var context = getContextFromQueue ();
          if (context != null) {
            asyncResult.Complete (context, true);
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
      lock (((ICollection) _registry).SyncRoot)
        _registry [context] = context;

      ListenerAsyncResult ares = null;
      lock (((ICollection) _waitQueue).SyncRoot) {
        if (_waitQueue.Count == 0) {
          lock (((ICollection) _contextQueue).SyncRoot)
            _contextQueue.Add (context);
        }
        else {
          ares = _waitQueue [0];
          _waitQueue.RemoveAt (0);
        }
      }

      if (ares != null)
        ares.Complete (context);
    }

    internal void RemoveConnection (HttpConnection connection)
    {
      lock (((ICollection) _connections).SyncRoot)
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
      lock (((ICollection) _registry).SyncRoot)
        _registry.Remove (context);

      lock (((ICollection) _contextQueue).SyncRoot) {
        var i = _contextQueue.IndexOf (context);
        if (i >= 0)
          _contextQueue.RemoveAt (i);
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
    /// Begins getting an incoming request information asynchronously.
    /// </summary>
    /// <remarks>
    /// This asynchronous operation must be completed by calling the <c>EndGetContext</c> method.
    /// Typically, that method is invoked by the <paramref name="callback"/> delegate.
    /// </remarks>
    /// <returns>
    /// An <see cref="IAsyncResult"/> that represents the status of the asynchronous operation.
    /// </returns>
    /// <param name="callback">
    /// An <see cref="AsyncCallback"/> delegate that references the method(s) to invoke
    /// when the asynchronous operation completes.
    /// </param>
    /// <param name="state">
    /// An <see cref="object"/> that contains a user defined object to pass to
    /// the <paramref name="callback"/> delegate.
    /// </param>
    /// <exception cref="InvalidOperationException">
    ///   <para>
    ///   This listener doesn't have any URI prefixes to listen on.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   This listener hasn't been started or is stopped currently.
    ///   </para>
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This listener has been closed.
    /// </exception>
    public IAsyncResult BeginGetContext (AsyncCallback callback, Object state)
    {
      return BeginGetContext (new ListenerAsyncResult (callback, state));
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
    /// Ends an asynchronous operation to get an incoming request information.
    /// </summary>
    /// <remarks>
    /// This method completes an asynchronous operation started by calling
    /// the <c>BeginGetContext</c> method.
    /// </remarks>
    /// <returns>
    /// A <see cref="HttpListenerContext"/> that contains a request information.
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

      var ares = asyncResult as ListenerAsyncResult;
      if (ares == null)
        throw new ArgumentException ("Wrong IAsyncResult.", "asyncResult");

      if (ares.EndCalled)
        throw new InvalidOperationException ("Cannot reuse this IAsyncResult.");

      ares.EndCalled = true;
      if (!ares.IsCompleted)
        ares.AsyncWaitHandle.WaitOne ();

      lock (((ICollection) _waitQueue).SyncRoot) {
        var i = _waitQueue.IndexOf (ares);
        if (i >= 0)
          _waitQueue.RemoveAt (i);
      }

      var context = ares.GetContext ();
      var scheme = SelectAuthenticationScheme (context);
      if (scheme != AuthenticationSchemes.Anonymous)
        context.SetUser (scheme, Realm, UserCredentialsFinder);

      return context; // This will throw on error.
    }

    /// <summary>
    /// Gets an incoming request information.
    /// </summary>
    /// <remarks>
    /// This method waits for an incoming request and returns a request information
    /// when the listener receives a request.
    /// </remarks>
    /// <returns>
    /// A <see cref="HttpListenerContext"/> that contains a request information.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    ///   <para>
    ///   This listener doesn't have any URI prefixes to listen on.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   This listener hasn't been started or is stopped currently.
    ///   </para>
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This listener has been closed.
    /// </exception>
    public HttpListenerContext GetContext ()
    {
      var ares = BeginGetContext (new ListenerAsyncResult (null, null));
      ares.InGet = true;

      return EndGetContext (ares);
    }

    /// <summary>
    /// Starts to receive incoming requests.
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

    #region Explicit Interface Implementation

    /// <summary>
    /// Releases all resource used by the listener.
    /// </summary>
    void IDisposable.Dispose ()
    {
      if (_disposed)
        return;

      close (true); // TODO: Should we force here or not?
      _disposed = true;
    }

    #endregion
  }
}
