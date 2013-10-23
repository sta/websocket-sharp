//
// HttpListener.cs
//	Copied from System.Net.HttpListener.cs
//
// Author:
//	Gonzalo Paniagua Javier (gonzalo@novell.com)
//	sta (sta.blockhead@gmail.com)
//
// Copyright (c) 2005 Novell, Inc. (http://www.novell.com)
// Copyright (c) 2012-2013 sta.blockhead
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

// TODO: logging
namespace WebSocketSharp.Net {

	/// <summary>
	/// Provides a simple, programmatically controlled HTTP listener.
	/// </summary>
	public sealed class HttpListener : IDisposable {

		#region Private Fields

		AuthenticationSchemes                                auth_schemes;
		AuthenticationSchemeSelector                         auth_selector;
		string                                               cert_folder_path;
		Dictionary<HttpConnection, HttpConnection>           connections;
		List<HttpListenerContext>                            ctx_queue;
		X509Certificate2                                     default_cert;
		bool                                                 disposed;
		bool                                                 ignore_write_exceptions;
		bool                                                 listening;
		HttpListenerPrefixCollection                         prefixes;
		string                                               realm;
		Dictionary<HttpListenerContext, HttpListenerContext> registry;
		bool                                                 unsafe_ntlm_auth;
		List<ListenerAsyncResult>                            wait_queue;

		#endregion

		#region Public Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="HttpListener"/> class.
		/// </summary>
		public HttpListener ()
		{
			prefixes     = new HttpListenerPrefixCollection (this);
			registry     = new Dictionary<HttpListenerContext, HttpListenerContext> ();
			connections  = new Dictionary<HttpConnection, HttpConnection> ();
			ctx_queue    = new List<HttpListenerContext> ();
			wait_queue   = new List<ListenerAsyncResult> ();
			auth_schemes = AuthenticationSchemes.Anonymous;
		}

		#endregion

		#region Public Properties

		/// <summary>
		/// Gets or sets the scheme used to authenticate the clients.
		/// </summary>
		/// <value>
		/// One of the <see cref="WebSocketSharp.Net.AuthenticationSchemes"/> values that indicates
		/// the scheme used to authenticate the clients.
		/// The default value is <see cref="WebSocketSharp.Net.AuthenticationSchemes.Anonymous"/>.
		/// </value>
		/// <exception cref="ObjectDisposedException">
		/// This object has been closed.
		/// </exception>
		public AuthenticationSchemes AuthenticationSchemes {
			// TODO: Digest, NTLM and Negotiate require ControlPrincipal
			get {
				CheckDisposed ();
				return auth_schemes;
			}
			set {
				CheckDisposed ();
				auth_schemes = value;
			}
		}

		/// <summary>
		/// Gets or sets the delegate called to determine the scheme used to authenticate clients.
		/// </summary>
		/// <value>
		/// A <see cref="AuthenticationSchemeSelector"/> delegate that invokes the method(s) used to select
		/// an authentication scheme. The default value is <see langword="null"/>.
		/// </value>
		/// <exception cref="ObjectDisposedException">
		/// This object has been closed.
		/// </exception>
		public AuthenticationSchemeSelector AuthenticationSchemeSelectorDelegate {
			get {
				CheckDisposed ();
				return auth_selector;
			}
			set {
				CheckDisposed ();
				auth_selector = value;
			}
		}

		/// <summary>
		/// Gets or sets the path to the folder stored the certificate files used to authenticate
		/// the server on the secure connection.
		/// </summary>
		/// <remarks>
		/// This property represents the path to the folder stored the certificate files associated with
		/// the port number of each added URI prefix. A set of the certificate files is a pair of the
		/// <c>'port number'.cer</c> (DER) and <c>'port number'.key</c> (DER, RSA Private Key).
		/// </remarks>
		/// <value>
		/// A <see cref="string"/> that contains the path to the certificate folder. The default value is
		/// the result of <c>Environment.GetFolderPath</c> (<see cref="Environment.SpecialFolder.ApplicationData"/>).
		/// </value>
		/// <exception cref="ObjectDisposedException">
		/// This object has been closed.
		/// </exception>
		public string CertificateFolderPath {
			get {
				CheckDisposed ();
				if (cert_folder_path.IsNullOrEmpty ())
					cert_folder_path = Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData);

				return cert_folder_path;
			}

			set {
				CheckDisposed ();
				cert_folder_path = value;
			}
		}

		/// <summary>
		/// Gets or sets the default certificate used to authenticate the server on the secure connection.
		/// </summary>
		/// <value>
		/// A <see cref="X509Certificate2"/> used to authenticate the server if the certificate associated with
		/// the port number of each added URI prefix is not found in the <see cref="CertificateFolderPath"/>.
		/// </value>
		/// <exception cref="ObjectDisposedException">
		/// This object has been closed.
		/// </exception>
		public X509Certificate2 DefaultCertificate {
			get {
				CheckDisposed ();
				return default_cert;
			}

			set {
				CheckDisposed ();
				default_cert = value;
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether the <see cref="HttpListener"/> returns exceptions
		/// that occur when sending the response to the client.
		/// </summary>
		/// <value>
		/// <c>true</c> if does not return exceptions that occur when sending the response to the client;
		/// otherwise, <c>false</c>. The default value is <c>false</c>.
		/// </value>
		/// <exception cref="ObjectDisposedException">
		/// This object has been closed.
		/// </exception>
		public bool IgnoreWriteExceptions {
			get {
				CheckDisposed ();
				return ignore_write_exceptions;
			}
			set {
				CheckDisposed ();
				ignore_write_exceptions = value;
			}
		}

		/// <summary>
		/// Gets a value indicating whether the <see cref="HttpListener"/> has been started.
		/// </summary>
		/// <value>
		/// <c>true</c> if the <see cref="HttpListener"/> has been started; otherwise, <c>false</c>.
		/// </value>
		public bool IsListening {
			get { return listening; }
		}

		/// <summary>
		/// Gets a value indicating whether the <see cref="HttpListener"/> can be used with the current operating system.
		/// </summary>
		/// <value>
		/// <c>true</c>.
		/// </value>
		public static bool IsSupported {
			get { return true; }
		}

		/// <summary>
		/// Gets the URI prefixes handled by the <see cref="HttpListener"/>.
		/// </summary>
		/// <value>
		/// A <see cref="HttpListenerPrefixCollection"/> that contains the URI prefixes.
		/// </value>
		/// <exception cref="ObjectDisposedException">
		/// This object has been closed.
		/// </exception>
		public HttpListenerPrefixCollection Prefixes {
			get {
				CheckDisposed ();
				return prefixes;
			}
		}

		/// <summary>
		/// Gets or sets the name of the realm associated with the <see cref="HttpListener"/>.
		/// </summary>
		/// <value>
		/// A <see cref="string"/> that contains the name of the realm.
		/// </value>
		/// <exception cref="ObjectDisposedException">
		/// This object has been closed.
		/// </exception>
		public string Realm {
			// TODO: Use this
			get {
				CheckDisposed ();
				return realm;
			}
			set {
				CheckDisposed ();
				realm = value;
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether, when NTLM authentication is used,
		/// the authentication information of first request is used to authenticate
		/// additional requests on the same connection.
		/// </summary>
		/// <value>
		/// <c>true</c> if the authentication information of first request is used;
		/// otherwise, <c>false</c>. The default value is <c>false</c>.
		/// </value>
		/// <exception cref="ObjectDisposedException">
		/// This object has been closed.
		/// </exception>
		public bool UnsafeConnectionNtlmAuthentication {
			// TODO: Support for NTLM needs some loving.
			get {
				CheckDisposed ();
				return unsafe_ntlm_auth;
			}
			set {
				CheckDisposed ();
				unsafe_ntlm_auth = value;
			}
		}

		#endregion

		#region Private Methods

		void Cleanup (bool force)
		{
			lock (((ICollection)registry).SyncRoot) {
				if (!force)
					SendServiceUnavailable ();

				CleanupContextRegistry ();
				CleanupConnections ();
				CleanupWaitQueue ();
			}
		}

		void CleanupConnections ()
		{
			lock (((ICollection)connections).SyncRoot) {
				if (connections.Count == 0)
					return;

				// Need to copy this since closing will call RemoveConnection
				ICollection keys = connections.Keys;
				var conns = new HttpConnection [keys.Count];
				keys.CopyTo (conns, 0);
				connections.Clear ();
				for (int i = conns.Length - 1; i >= 0; i--)
					conns [i].Close (true);
			}
		}

		void CleanupContextRegistry ()
		{
			lock (((ICollection)registry).SyncRoot) {
				if (registry.Count == 0)
					return;

				// Need to copy this since closing will call UnregisterContext
				ICollection keys = registry.Keys;
				var all = new HttpListenerContext [keys.Count];
				keys.CopyTo (all, 0);
				registry.Clear ();
				for (int i = all.Length - 1; i >= 0; i--)
					all [i].Connection.Close (true);
			}
		}

		void CleanupWaitQueue ()
		{
			lock (((ICollection)wait_queue).SyncRoot) {
				if (wait_queue.Count == 0)
					return;

				var exc = new ObjectDisposedException (GetType ().ToString ());
				foreach (var ares in wait_queue) {
					ares.Complete (exc);
				}

				wait_queue.Clear ();
			}
		}

		void Close (bool force)
		{
			EndPointManager.RemoveListener (this);
			Cleanup (force);
		}

		// Must be called with a lock on ctx_queue
		HttpListenerContext GetContextFromQueue ()
		{
			if (ctx_queue.Count == 0)
				return null;

			var context = ctx_queue [0];
			ctx_queue.RemoveAt (0);
			return context;
		}

		void SendServiceUnavailable ()
		{
			lock (((ICollection)ctx_queue).SyncRoot) {
				if (ctx_queue.Count == 0)
					return;

				var ctxs = ctx_queue.ToArray ();
				ctx_queue.Clear ();
				foreach (var ctx in ctxs) {
					var res = ctx.Response;
					res.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
					res.Close();
				}
			}
		}

		#endregion

		#region Internal Methods

		internal void AddConnection (HttpConnection cnc)
		{
			connections [cnc] = cnc;
		}

		internal void CheckDisposed ()
		{
			if (disposed)
				throw new ObjectDisposedException (GetType ().ToString ());
		}

		internal void RegisterContext (HttpListenerContext context)
		{
			lock (((ICollection)registry).SyncRoot)
				registry [context] = context;

			ListenerAsyncResult ares = null;
			lock (((ICollection)wait_queue).SyncRoot) {
				if (wait_queue.Count == 0) {
					lock (((ICollection)ctx_queue).SyncRoot)
						ctx_queue.Add (context);
				} else {
					ares = wait_queue [0];
					wait_queue.RemoveAt (0);
				}
			}

			if (ares != null)
				ares.Complete (context);
		}

		internal void RemoveConnection (HttpConnection cnc)
		{
			connections.Remove (cnc);
		}

		internal AuthenticationSchemes SelectAuthenticationScheme (HttpListenerContext context)
		{
			if (AuthenticationSchemeSelectorDelegate != null)
				return AuthenticationSchemeSelectorDelegate (context.Request);
			else
				return auth_schemes;
		}

		internal void UnregisterContext (HttpListenerContext context)
		{
			lock (((ICollection)registry).SyncRoot)
				registry.Remove (context);

			lock (((ICollection)ctx_queue).SyncRoot) {
				int idx = ctx_queue.IndexOf (context);
				if (idx >= 0)
					ctx_queue.RemoveAt (idx);
			}
		}

		#endregion

		#region Public Methods

		/// <summary>
		/// Shuts down the <see cref="HttpListener"/> immediately.
		/// </summary>
		public void Abort ()
		{
			if (disposed)
				return;

			Close (true);
			disposed = true;
		}

		/// <summary>
		/// Begins getting an incoming request information asynchronously.
		/// </summary>
		/// <remarks>
		/// This asynchronous operation must be completed by calling the <see cref="EndGetContext"/> method.
		/// Typically, the method is invoked by the <paramref name="callback"/> delegate.
		/// </remarks>
		/// <returns>
		/// An <see cref="IAsyncResult"/> that contains the status of the asynchronous operation.
		/// </returns>
		/// <param name="callback">
		/// An <see cref="AsyncCallback"/> delegate that references the method(s)
		/// called when the asynchronous operation completes.
		/// </param>
		/// <param name="state">
		/// An <see cref="object"/> that contains a user defined object to pass to the <paramref name="callback"/> delegate.
		/// </param>
		/// <exception cref="ObjectDisposedException">
		/// This object has been closed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The <see cref="HttpListener"/> has not been started or is stopped currently.
		/// </exception>
		public IAsyncResult BeginGetContext (AsyncCallback callback, Object state)
		{
			CheckDisposed ();
			if (!listening)
				throw new InvalidOperationException ("Please, call Start before using this method.");

			ListenerAsyncResult ares = new ListenerAsyncResult (callback, state);

			// lock wait_queue early to avoid race conditions
			lock (((ICollection)wait_queue).SyncRoot) {
				lock (((ICollection)ctx_queue).SyncRoot) {
					HttpListenerContext ctx = GetContextFromQueue ();
					if (ctx != null) {
						ares.Complete (ctx, true);
						return ares;
					}
				}

				wait_queue.Add (ares);
			}

			return ares;
		}

		/// <summary>
		/// Shuts down the <see cref="HttpListener"/>.
		/// </summary>
		public void Close ()
		{
			if (disposed)
				return;

			Close (false);
			disposed = true;
		}

		/// <summary>
		/// Ends an asynchronous operation to get an incoming request information.
		/// </summary>
		/// <remarks>
		/// This method completes an asynchronous operation started by calling the <see cref="BeginGetContext"/> method.
		/// </remarks>
		/// <returns>
		/// A <see cref="HttpListenerContext"/> that contains a client's request information.
		/// </returns>
		/// <param name="asyncResult">
		/// An <see cref="IAsyncResult"/> obtained by calling the <see cref="BeginGetContext"/> method.
		/// </param>
		/// <exception cref="ObjectDisposedException">
		/// This object has been closed.
		/// </exception>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="asyncResult"/> is <see langword="null"/>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="asyncResult"/> was not obtained by calling the <see cref="BeginGetContext"/> method.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The EndGetContext method was already called for the specified <paramref name="asyncResult"/>.
		/// </exception>
		public HttpListenerContext EndGetContext (IAsyncResult asyncResult)
		{
			CheckDisposed ();
			if (asyncResult == null)
				throw new ArgumentNullException ("asyncResult");

			ListenerAsyncResult ares = asyncResult as ListenerAsyncResult;
			if (ares == null)
				throw new ArgumentException ("Wrong IAsyncResult.", "asyncResult");

			if (ares.EndCalled)
				throw new InvalidOperationException ("Cannot reuse this IAsyncResult.");
			ares.EndCalled = true;

			if (!ares.IsCompleted)
				ares.AsyncWaitHandle.WaitOne ();

			lock (((ICollection)wait_queue).SyncRoot) {
				int idx = wait_queue.IndexOf (ares);
				if (idx >= 0)
					wait_queue.RemoveAt (idx);
			}

			HttpListenerContext context = ares.GetContext ();
			context.ParseAuthentication (SelectAuthenticationScheme (context));
			return context; // This will throw on error.
		}

		/// <summary>
		/// Gets an incoming request information.
		/// </summary>
		/// <remarks>
		/// This method waits for an incoming request and returns the request information
		/// when received the request.
		/// </remarks>
		/// <returns>
		/// A <see cref="HttpListenerContext"/> that contains a client's request information.
		/// </returns>
		/// <exception cref="InvalidOperationException">
		/// <para>
		/// The <see cref="HttpListener"/> does not have any URI prefixes to listen on.
		/// </para>
		/// <para>
		/// -or-
		/// </para>
		/// <para>
		/// The <see cref="HttpListener"/> has not been started or is stopped currently.
		/// </para>
		/// </exception>
		/// <exception cref="ObjectDisposedException">
		/// This object has been closed.
		/// </exception>
		public HttpListenerContext GetContext ()
		{
			// The prefixes are not checked when using the async interface!?
			if (prefixes.Count == 0)
				throw new InvalidOperationException ("Please, call AddPrefix before using this method.");

			ListenerAsyncResult ares = (ListenerAsyncResult) BeginGetContext (null, null);
			ares.InGet = true;
			return EndGetContext (ares);
		}

		/// <summary>
		/// Starts to receive incoming requests.
		/// </summary>
		/// <exception cref="ObjectDisposedException">
		/// This object has been closed.
		/// </exception>
		public void Start ()
		{
			CheckDisposed ();
			if (listening)
				return;

			EndPointManager.AddListener (this);
			listening = true;
		}

		/// <summary>
		/// Stops receiving incoming requests.
		/// </summary>
		/// <exception cref="ObjectDisposedException">
		/// This object has been closed.
		/// </exception>
		public void Stop ()
		{
			CheckDisposed ();
			if (!listening)
				return;

			listening = false;
			EndPointManager.RemoveListener (this);
			SendServiceUnavailable ();
		}

		#endregion

		#region Explicit Interface Implementation

		/// <summary>
		/// Releases all resource used by the <see cref="HttpListener"/>.
		/// </summary>
		void IDisposable.Dispose ()
		{
			if (disposed)
				return;

			Close (true); // TODO: Should we force here or not?
			disposed = true;
		}

		#endregion
	}
}
