//
// HttpConnection.cs
//	Copied from System.Net.HttpConnection.cs
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
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using WebSocketSharp.Net.Security;

namespace WebSocketSharp.Net {

	sealed class HttpConnection {

		#region Enums

		enum InputState {
			RequestLine,
			Headers
		}

		enum LineState {
			None,
			CR,
			LF
		}

		#endregion

		#region Private Const Field

		const int BufferSize = 8192;

		#endregion

		#region Private Static Field

		static AsyncCallback onread_cb = new AsyncCallback (OnRead);

		#endregion

		#region Private Fields

		byte []             buffer;
		bool                chunked;
		HttpListenerContext context;
		bool                context_bound;
		StringBuilder       current_line;
		EndPointListener    epl;
		InputState          input_state;
		RequestStream       i_stream;
		AsymmetricAlgorithm key;
		HttpListener        last_listener;
		LineState           line_state;
//		IPEndPoint          local_ep; // never used
		MemoryStream        ms;
		ResponseStream      o_stream;
		int                 position;
		ListenerPrefix      prefix;
		int                 reuses;
		bool                secure;
		Socket              sock;
		Stream              stream;
		int                 s_timeout;
		Timer               timer;

		#endregion

		#region Constructor

		public HttpConnection (
			Socket              sock,
			EndPointListener    epl,
			bool                secure,
			X509Certificate2    cert,
			AsymmetricAlgorithm key
		)
		{
			this.sock   = sock;
			this.epl    = epl;
			this.secure = secure;
			this.key    = key;
//			if (secure == false) {
//				stream = new NetworkStream (sock, false);
//			} else {
//				var ssl_stream = new SslServerStream (new NetworkStream (sock, false), cert, false, false);
//				ssl_stream.PrivateKeyCertSelectionDelegate += OnPVKSelection;
//				stream = ssl_stream;
//			}
			var net_stream = new NetworkStream (sock, false);
			if (!secure) {
				stream = net_stream;
			} else {
				var ssl_stream = new SslStream(net_stream, false);
				ssl_stream.AuthenticateAsServer(cert);
				stream = ssl_stream;
			}
			timer = new Timer (OnTimeout, null, Timeout.Infinite, Timeout.Infinite);
			Init ();
		}

		#endregion

		#region Properties

		public bool IsClosed {
			get { return (sock == null); }
		}

		public bool IsSecure {
			get { return secure; }
		}

		public IPEndPoint LocalEndPoint {
			get { return (IPEndPoint) sock.LocalEndPoint; }
		}

		public ListenerPrefix Prefix {
			get { return prefix; }
			set { prefix = value; }
		}

		public IPEndPoint RemoteEndPoint {
			get { return (IPEndPoint) sock.RemoteEndPoint; }
		}

		public int Reuses {
			get { return reuses; }
		}

		public Stream Stream {
			get { return stream; }
		}

		#endregion

		#region Private Methods

		void CloseSocket ()
		{
			if (sock == null)
				return;

			try {
				sock.Close ();
			} catch {
			} finally {
				sock = null;
			}
			RemoveConnection ();
		}

		void Init ()
		{
			context_bound = false;
			i_stream      = null;
			o_stream      = null;
			prefix        = null;
			chunked       = false;
			ms            = new MemoryStream ();
			position      = 0;
			input_state   = InputState.RequestLine;
			line_state    = LineState.None;
			context       = new HttpListenerContext (this);
			s_timeout     = 90000; // 90k ms for first request, 15k ms from then on
		}

		AsymmetricAlgorithm OnPVKSelection (X509Certificate certificate, string targetHost)
		{
			return key;
		}

		static void OnRead (IAsyncResult ares)
		{
			HttpConnection cnc = (HttpConnection) ares.AsyncState;
			cnc.OnReadInternal (ares);
		}

		void OnReadInternal (IAsyncResult ares)
		{
			timer.Change (Timeout.Infinite, Timeout.Infinite);
			int nread = -1;
			try {
				nread = stream.EndRead (ares);
				ms.Write (buffer, 0, nread);
				if (ms.Length > 32768) {
					SendError ("Bad request", 400);
					Close (true);
					return;
				}
			} catch {
				if (ms != null && ms.Length > 0)
					SendError ();

				if (sock != null) {
					CloseSocket ();
					Unbind ();
				}

				return;
			}

			if (nread == 0) {
				//if (ms.Length > 0)
				//	SendError (); // Why bother?
				CloseSocket ();
				Unbind ();
				return;
			}

			if (ProcessInput (ms)) {
				if (!context.HaveError)
					context.Request.FinishInitialization ();

				if (context.HaveError) {
					SendError ();
					Close (true);
					return;
				}

				if (!epl.BindContext (context)) {
					SendError ("Invalid host", 400);
					Close (true);
					return;
				}

				HttpListener listener = context.Listener;
				if (last_listener != listener) {
					RemoveConnection ();
					listener.AddConnection (this);
					last_listener = listener;
				}

				context_bound = true;
				listener.RegisterContext (context);
				return;
			}

			stream.BeginRead (buffer, 0, BufferSize, onread_cb, this);
		}

		void OnTimeout (object unused)
		{
			CloseSocket ();
			Unbind ();
		}

		// true -> done processing
		// false -> need more input
		bool ProcessInput (MemoryStream ms)
		{
			byte [] buffer = ms.GetBuffer ();
			int len = (int) ms.Length;
			int used = 0;
			string line;

			try {
				line = ReadLine (buffer, position, len - position, ref used);
				position += used;
			} catch {
				context.ErrorMessage = "Bad request";
				context.ErrorStatus = 400;
				return true;
			}

			do {
				if (line == null)
					break;
				if (line == "") {
					if (input_state == InputState.RequestLine)
						continue;
					current_line = null;
					ms = null;
					return true;
				}

				if (input_state == InputState.RequestLine) {
					context.Request.SetRequestLine (line);
					input_state = InputState.Headers;
				} else {
					try {
						context.Request.AddHeader (line);
					} catch (Exception e) {
						context.ErrorMessage = e.Message;
						context.ErrorStatus = 400;
						return true;
					}
				}

				if (context.HaveError)
					return true;

				if (position >= len)
					break;
				try {
					line = ReadLine (buffer, position, len - position, ref used);
					position += used;
				} catch {
					context.ErrorMessage = "Bad request";
					context.ErrorStatus = 400;
					return true;
				}
			} while (line != null);

			if (used == len) {
				ms.SetLength (0);
				position = 0;
			}
			return false;
		}

		string ReadLine (byte [] buffer, int offset, int len, ref int used)
		{
			if (current_line == null)
				current_line = new StringBuilder ();

			int last = offset + len;
			used = 0;
			for (int i = offset; i < last && line_state != LineState.LF; i++) {
				used++;
				byte b = buffer [i];
				if (b == 13) {
					line_state = LineState.CR;
				} else if (b == 10) {
					line_state = LineState.LF;
				} else {
					current_line.Append ((char) b);
				}
			}

			string result = null;
			if (line_state == LineState.LF) {
				line_state = LineState.None;
				result = current_line.ToString ();
				current_line.Length = 0;
			}

			return result;
		}

		void RemoveConnection ()
		{
			if (last_listener == null)
				epl.RemoveConnection (this);
			else
				last_listener.RemoveConnection (this);
		}

		void Unbind ()
		{
			if (context_bound) {
				epl.UnbindContext (context);
				context_bound = false;
			}
		}

		#endregion

		#region Internal Method

		internal void Close (bool force_close)
		{
			if (sock != null) {
				Stream st = GetResponseStream ();
				st.Close ();
				o_stream = null;
			}

			if (sock != null) {
				force_close |= !context.Request.KeepAlive;
				if (!force_close)
					force_close = (context.Response.Headers ["connection"] == "close");

				if (!force_close && context.Request.FlushInput ()) {
					if (chunked && context.Response.ForceCloseChunked == false) {
						// Don't close. Keep working.
						reuses++;
						Unbind ();
						Init ();
						BeginReadRequest ();
						return;
					}

					reuses++;
					Unbind ();
					Init ();
					BeginReadRequest ();
					return;
				}

				Socket s = sock;
				sock = null;
				try {
					if (s != null)
						s.Shutdown (SocketShutdown.Both);
				} catch {
				} finally {
					if (s != null)
						s.Close ();
				}

				Unbind ();
				RemoveConnection ();
				return;
			}
		}

		#endregion

		#region Public Methods

		public void BeginReadRequest ()
		{
			if (buffer == null)
				buffer = new byte [BufferSize];

			try {
				if (reuses == 1)
					s_timeout = 15000;

				timer.Change (s_timeout, Timeout.Infinite);
				stream.BeginRead (buffer, 0, BufferSize, onread_cb, this);
			} catch {
				timer.Change (Timeout.Infinite, Timeout.Infinite);
				CloseSocket ();
				Unbind ();
			}
		}

		public void Close ()
		{
			Close (false);
		}

		public RequestStream GetRequestStream (bool chunked, long contentlength)
		{
			if (i_stream == null) {
				byte [] buffer = ms.GetBuffer ();
				int length = (int) ms.Length;
				ms = null;
				if (chunked) {
					this.chunked = true;
					context.Response.SendChunked = true;
					i_stream = new ChunkedInputStream (context, stream, buffer, position, length - position);
				} else {
					i_stream = new RequestStream (stream, buffer, position, length - position, contentlength);
				}
			}

			return i_stream;
		}

		public ResponseStream GetResponseStream ()
		{
			// TODO: can we get this stream before reading the input?
			if (o_stream == null) {
				HttpListener listener = context.Listener;
				bool ign = (listener == null) ? true : listener.IgnoreWriteExceptions;
				o_stream = new ResponseStream (stream, context.Response, ign);
			}

			return o_stream;
		}

		public void SendError ()
		{
			SendError (context.ErrorMessage, context.ErrorStatus);
		}

		public void SendError (string msg, int status)
		{
			try {
				HttpListenerResponse response = context.Response;
				response.StatusCode = status;
				response.ContentType = "text/html";
				string description = status.GetStatusDescription ();
				string str;
				if (msg != null)
					str = String.Format ("<h1>{0} ({1})</h1>", description, msg);
				else
					str = String.Format ("<h1>{0}</h1>", description);

				byte [] error = context.Response.ContentEncoding.GetBytes (str);
				response.Close (error, false);
			} catch {
				// response was already closed
			}
		}

		#endregion
	}
}
