//
// EndPointListener.cs
//	Copied from System.Net.EndPointListener
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
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace WebSocketSharp.Net {

	sealed class EndPointListener {

		#region Fields

		List<ListenerPrefix>                     all; // host = '+'
		X509Certificate2                         cert;
		IPEndPoint                               endpoint;
		AsymmetricAlgorithm                      key;
		Dictionary<ListenerPrefix, HttpListener> prefixes;
		bool                                     secure;
		Socket                                   sock;
		List<ListenerPrefix>                     unhandled; // host = '*'
		Hashtable                                unregistered;

		#endregion

		#region Constructor

		public EndPointListener (IPAddress addr, int port, bool secure)
		{
			if (secure) {
				this.secure = secure;
				LoadCertificateAndKey (addr, port);
			}

			endpoint = new IPEndPoint (addr, port);
			sock = new Socket (addr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
			sock.Bind (endpoint);
			sock.Listen (500);
			var args = new SocketAsyncEventArgs ();
			args.UserToken = this;
			args.Completed += OnAccept;
			sock.AcceptAsync (args);
			prefixes = new Dictionary<ListenerPrefix, HttpListener> ();
			unregistered = Hashtable.Synchronized (new Hashtable ());
		}

		#endregion

		#region Private Methods

		void AddSpecial (List<ListenerPrefix> coll, ListenerPrefix prefix)
		{
			if (coll == null)
				return;

			foreach (ListenerPrefix p in coll) {
				if (p.Path == prefix.Path) // TODO: code
					throw new HttpListenerException (400, "Prefix already in use.");
			}
			coll.Add (prefix);
		}

		void CheckIfRemove ()
		{
			if (prefixes.Count > 0)
				return;

			var list = unhandled;
			if (list != null && list.Count > 0)
				return;

			list = all;
			if (list != null && list.Count > 0)
				return;

			EndPointManager.RemoveEndPoint (this, endpoint);
		}

		RSACryptoServiceProvider CreateRSAFromFile (string filename)
		{
			if (filename == null)
				throw new ArgumentNullException ("filename");

			var rsa = new RSACryptoServiceProvider ();
			byte[] pvk = null;
			using (FileStream fs = File.Open (filename, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				pvk = new byte [fs.Length];
				fs.Read (pvk, 0, pvk.Length);
			}
			rsa.ImportCspBlob (pvk);
			return rsa;
		}

		void LoadCertificateAndKey (IPAddress addr, int port)
		{
			// Actually load the certificate
			try {
				string dirname = Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData);
				string path = Path.Combine (dirname, ".mono");
				path = Path.Combine (path, "httplistener");
				string cert_file = Path.Combine (path, String.Format ("{0}.cer", port));
				string pvk_file = Path.Combine (path, String.Format ("{0}.pvk", port));
				cert = new X509Certificate2 (cert_file);
				key = CreateRSAFromFile (pvk_file);
			} catch {
				// ignore errors
			}
		}

		HttpListener MatchFromList (
			string host, string path, List<ListenerPrefix> list, out ListenerPrefix prefix)
		{
			prefix = null;
			if (list == null)
				return null;

			HttpListener best_match = null;
			int best_length = -1;
			
			foreach (ListenerPrefix p in list) {
				string ppath = p.Path;
				if (ppath.Length < best_length)
					continue;

				if (path.StartsWith (ppath)) {
					best_length = ppath.Length;
					best_match = p.Listener;
					prefix = p;
				}
			}

			return best_match;
		}

		static void OnAccept (object sender, EventArgs e)
		{
			SocketAsyncEventArgs args = (SocketAsyncEventArgs) e;
			EndPointListener epl = (EndPointListener) args.UserToken;
			Socket accepted = null;
			if (args.SocketError == SocketError.Success) {
				accepted = args.AcceptSocket;
				args.AcceptSocket = null;
			}

			try {
				if (epl.sock != null)
					epl.sock.AcceptAsync (args);
			} catch {
				if (accepted != null) {
					try {
						accepted.Close ();
					} catch {}
					accepted = null;
				}
			} 

			if (accepted == null)
				return;

			if (epl.secure && (epl.cert == null || epl.key == null)) {
				accepted.Close ();
				return;
			}
			HttpConnection conn = new HttpConnection (accepted, epl, epl.secure, epl.cert, epl.key);
			epl.unregistered [conn] = conn;
			conn.BeginReadRequest ();
		}

		bool RemoveSpecial (List<ListenerPrefix> coll, ListenerPrefix prefix)
		{
			if (coll == null)
				return false;

			int c = coll.Count;
			for (int i = 0; i < c; i++) {
				ListenerPrefix p = coll [i];
				if (p.Path == prefix.Path) {
					coll.RemoveAt (i);
					return true;
				}
			}
			return false;
		}

		HttpListener SearchListener (Uri uri, out ListenerPrefix prefix)
		{
			prefix = null;
			if (uri == null)
				return null;

			string host = uri.Host;
			int port = uri.Port;
			string path = HttpUtility.UrlDecode (uri.AbsolutePath);
			string path_slash = path [path.Length - 1] == '/' ? path : path + "/";
			
			HttpListener best_match = null;
			int best_length = -1;

			if (host != null && host != "") {
				var p_ro = prefixes;
				foreach (ListenerPrefix p in p_ro.Keys) {
					string ppath = p.Path;
					if (ppath.Length < best_length)
						continue;

					if (p.Host != host || p.Port != port)
						continue;

					if (path.StartsWith (ppath) || path_slash.StartsWith (ppath)) {
						best_length = ppath.Length;
						best_match = p_ro [p];
						prefix = p;
					}
				}
				if (best_length != -1)
					return best_match;
			}

			var list = unhandled;
			best_match = MatchFromList (host, path, list, out prefix);
			if (path != path_slash && best_match == null)
				best_match = MatchFromList (host, path_slash, list, out prefix);
			if (best_match != null)
				return best_match;

			list = all;
			best_match = MatchFromList (host, path, list, out prefix);
			if (path != path_slash && best_match == null)
				best_match = MatchFromList (host, path_slash, list, out prefix);
			if (best_match != null)
				return best_match;

			return null;
		}

		#endregion

		#region Internal Method

		internal void RemoveConnection (HttpConnection conn)
		{
			unregistered.Remove (conn);
		}

		#endregion

		#region Public Methods

		public void AddPrefix (ListenerPrefix prefix, HttpListener listener)
		{
			List<ListenerPrefix> current;
			List<ListenerPrefix> future;
			if (prefix.Host == "*") {
				do {
					current = unhandled;
					future = (current != null)
						? new List<ListenerPrefix> (current)
						: new List<ListenerPrefix> ();
					prefix.Listener = listener;
					AddSpecial (future, prefix);
				} while (Interlocked.CompareExchange (ref unhandled, future, current) != current);
				return;
			}

			if (prefix.Host == "+") {
				do {
					current = all;
					future = (current != null)
						? new List<ListenerPrefix> (current)
						: new List<ListenerPrefix> ();
					prefix.Listener = listener;
					AddSpecial (future, prefix);
				} while (Interlocked.CompareExchange (ref all, future, current) != current);
				return;
			}

			Dictionary<ListenerPrefix, HttpListener> prefs, p2;
			do {
				prefs = prefixes;
				if (prefs.ContainsKey (prefix)) {
					HttpListener other = prefs [prefix];
					if (other != listener) // TODO: code.
						throw new HttpListenerException (400, "There's another listener for " + prefix);
					return;
				}
				p2 = new Dictionary<ListenerPrefix, HttpListener> (prefs);
				p2 [prefix] = listener;
			} while (Interlocked.CompareExchange (ref prefixes, p2, prefs) != prefs);
		}

		public bool BindContext (HttpListenerContext context)
		{
			HttpListenerRequest req = context.Request;
			ListenerPrefix prefix;
			HttpListener listener = SearchListener (req.Url, out prefix);
			if (listener == null)
				return false;

			context.Listener = listener;
			context.Connection.Prefix = prefix;
			return true;
		}

		public void Close ()
		{
			sock.Close ();
			lock (unregistered.SyncRoot) {
				Hashtable copy = (Hashtable) unregistered.Clone ();
				foreach (HttpConnection c in copy.Keys)
					c.Close (true);
				copy.Clear ();
				unregistered.Clear ();
			}
		}

		public void RemovePrefix (ListenerPrefix prefix, HttpListener listener)
		{
			List<ListenerPrefix> current;
			List<ListenerPrefix> future;
			if (prefix.Host == "*") {
				do {
					current = unhandled;
					future = (current != null)
						? new List<ListenerPrefix> (current)
						: new List<ListenerPrefix> ();
					if (!RemoveSpecial (future, prefix))
						break; // Prefix not found
				} while (Interlocked.CompareExchange (ref unhandled, future, current) != current);
				CheckIfRemove ();
				return;
			}

			if (prefix.Host == "+") {
				do {
					current = all;
					future = (current != null)
						? new List<ListenerPrefix> (current)
						: new List<ListenerPrefix> ();
					if (!RemoveSpecial (future, prefix))
						break; // Prefix not found
				} while (Interlocked.CompareExchange (ref all, future, current) != current);
				CheckIfRemove ();
				return;
			}

			Dictionary<ListenerPrefix, HttpListener> prefs, p2;
			do {
				prefs = prefixes;
				if (!prefs.ContainsKey (prefix))
					break;

				p2 = new Dictionary<ListenerPrefix, HttpListener> (prefs);
				p2.Remove (prefix);
			} while (Interlocked.CompareExchange (ref prefixes, p2, prefs) != prefs);
			CheckIfRemove ();
		}

		public void UnbindContext (HttpListenerContext context)
		{
			if (context == null || context.Request == null)
				return;

			context.Listener.UnregisterContext (context);
		}

		#endregion
	}
}
