//
// HttpListener.cs
//	Copied from System.Net.HttpListener
//
// Author:
//	Gonzalo Paniagua Javier (gonzalo@novell.com)
//
// Copyright (c) 2005 Novell, Inc. (http://www.novell.com)
// Copyright (c) 2012 sta.blockhead (sta.blockhead@gmail.com)
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
using System.Net;
using System.Threading;

// TODO: logging
namespace WebSocketSharp.Net {

	public sealed class HttpListener : IDisposable {

		#region Fields

		AuthenticationSchemes                                auth_schemes;
		AuthenticationSchemeSelector                         auth_selector; 
		Dictionary<HttpConnection, HttpConnection>           connections;
		List<HttpListenerContext>                            ctx_queue;
		bool                                                 disposed;
		bool                                                 ignore_write_exceptions;
		bool                                                 listening;
		HttpListenerPrefixCollection                         prefixes;
		string                                               realm;
		Dictionary<HttpListenerContext, HttpListenerContext> registry;
		bool                                                 unsafe_ntlm_auth;
		List<ListenerAsyncResult>                            wait_queue;

		#endregion

		#region Constructor

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

		#region Properties

		// TODO: Digest, NTLM and Negotiate require ControlPrincipal
		public AuthenticationSchemes AuthenticationSchemes {
			get { return auth_schemes; }
			set {
				CheckDisposed ();
				auth_schemes = value;
			}
		}

		public AuthenticationSchemeSelector AuthenticationSchemeSelectorDelegate {
			get { return auth_selector; }
			set {
				CheckDisposed ();
				auth_selector = value;
			}
		}

		public bool IgnoreWriteExceptions {
			get { return ignore_write_exceptions; }
			set {
				CheckDisposed ();
				ignore_write_exceptions = value;
			}
		}

		public bool IsListening {
			get { return listening; }
		}

		public static bool IsSupported {
			get { return true; }
		}

		public HttpListenerPrefixCollection Prefixes {
			get {
				CheckDisposed ();
				return prefixes;
			}
		}

		// TODO: Use this
		public string Realm {
			get { return realm; }
			set {
				CheckDisposed ();
				realm = value;
			}
		}

		// TODO: Support for NTLM needs some loving.
		public bool UnsafeConnectionNtlmAuthentication {
			get { return unsafe_ntlm_auth; }
			set {
				CheckDisposed ();
				unsafe_ntlm_auth = value;
			}
		}

		#endregion

		#region Private Methods

		void Cleanup (bool close_existing)
		{
			lock (((ICollection)registry).SyncRoot) {
				if (close_existing) {
					// Need to copy this since closing will call UnregisterContext
					ICollection keys = registry.Keys;
					var all = new HttpListenerContext [keys.Count];
					keys.CopyTo (all, 0);
					registry.Clear ();
					for (int i = all.Length - 1; i >= 0; i--)
						all [i].Connection.Close (true);
				}

				lock (((ICollection)connections).SyncRoot) {
					ICollection keys = connections.Keys;
					var conns = new HttpConnection [keys.Count];
					keys.CopyTo (conns, 0);
					connections.Clear ();
					for (int i = conns.Length - 1; i >= 0; i--)
						conns [i].Close (true);
				}

				lock (((ICollection)ctx_queue).SyncRoot) {
					var ctxs = ctx_queue.ToArray ();
					ctx_queue.Clear ();
					for (int i = ctxs.Length - 1; i >= 0; i--)
						ctxs [i].Connection.Close (true);
				}

				lock (((ICollection)wait_queue).SyncRoot) {
					Exception exc = new ObjectDisposedException ("listener");
					foreach (ListenerAsyncResult ares in wait_queue) {
						ares.Complete (exc);
					}
					wait_queue.Clear ();
				}
			}
		}

		void Close (bool force)
		{
			CheckDisposed ();
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

		void IDisposable.Dispose ()
		{
			if (disposed)
				return;

			Close (true); //TODO: Should we force here or not?
			disposed = true;
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

		public void Abort ()
		{
			if (disposed)
				return;

			if (!listening) {
				return;
			}

			Close (true);
		}

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

		public void Close ()
		{
			if (disposed)
				return;

			if (!listening) {
				disposed = true;
				return;
			}

			Close (true);
			disposed = true;
		}

		public HttpListenerContext EndGetContext (IAsyncResult asyncResult)
		{
			CheckDisposed ();
			if (asyncResult == null)
				throw new ArgumentNullException ("asyncResult");

			ListenerAsyncResult ares = asyncResult as ListenerAsyncResult;
			if (ares == null)
				throw new ArgumentException ("Wrong IAsyncResult.", "asyncResult");
			if (ares.EndCalled)
				throw new ArgumentException ("Cannot reuse this IAsyncResult");
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

		public HttpListenerContext GetContext ()
		{
			// The prefixes are not checked when using the async interface!?
			if (prefixes.Count == 0)
				throw new InvalidOperationException ("Please, call AddPrefix before using this method.");

			ListenerAsyncResult ares = (ListenerAsyncResult) BeginGetContext (null, null);
			ares.InGet = true;
			return EndGetContext (ares);
		}

		public void Start ()
		{
			CheckDisposed ();
			if (listening)
				return;

			EndPointManager.AddListener (this);
			listening = true;
		}

		public void Stop ()
		{
			CheckDisposed ();
			listening = false;
			Close (false);
		}

		#endregion
	}
}
