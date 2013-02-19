//
// HttpListenerResponse.cs
//	Copied from System.Net.HttpListenerResponse.cs
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
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;

namespace WebSocketSharp.Net {

	public sealed class HttpListenerResponse : IDisposable
	{
		#region Private Fields

		bool                chunked;
		bool                cl_set;
		Encoding            content_encoding;
		long                content_length;
		string              content_type;
		HttpListenerContext context;
		CookieCollection    cookies;
		bool                disposed;
		bool                force_close_chunked;
		WebHeaderCollection headers;
		bool                keep_alive;
		string              location;
		ResponseStream      output_stream;
		int                 status_code;
		string              status_description;
		Version             version;

		#endregion

		#region Internal Fields

		internal bool HeadersSent;

		#endregion

		#region Constructor

		internal HttpListenerResponse (HttpListenerContext context)
		{
			this.context = context;
			Init ();
		}

		#endregion

		#region Internal Property

		internal bool ForceCloseChunked {
			get { return force_close_chunked; }
		}

		#endregion

		#region Public Properties

		public Encoding ContentEncoding {
			get {
				if (content_encoding == null)
					content_encoding = Encoding.Default;
				return content_encoding;
			}
			set {
				if (disposed)
					throw new ObjectDisposedException (GetType ().ToString ());

				// TODO: is null ok?
				if (HeadersSent)
					throw new InvalidOperationException ("Cannot be changed after headers are sent.");

				content_encoding = value;
			}
		}

		public long ContentLength64 {
			get { return content_length; }
			set {
				if (disposed)
					throw new ObjectDisposedException (GetType ().ToString ());

				if (HeadersSent)
					throw new InvalidOperationException ("Cannot be changed after headers are sent.");

				if (value < 0)
					throw new ArgumentOutOfRangeException ("Must be >= 0", "value");

				cl_set = true;
				content_length = value;
			}
		}
		
		public string ContentType {
			get { return content_type; }
			set {
				// TODO: is null ok?
				if (disposed)
					throw new ObjectDisposedException (GetType ().ToString ());

				if (HeadersSent)
					throw new InvalidOperationException ("Cannot be changed after headers are sent.");

				content_type = value;
			}
		}

		// RFC 2109, 2965 + the netscape specification at http://wp.netscape.com/newsref/std/cookie_spec.html
		public CookieCollection Cookies {
			get {
				if (cookies == null)
					cookies = new CookieCollection ();
				return cookies;
			}
			set { cookies = value; } // null allowed?
		}

		public WebHeaderCollection Headers {
			get { return headers; }
			set {
		/**
		 *	"If you attempt to set a Content-Length, Keep-Alive, Transfer-Encoding, or
		 *	WWW-Authenticate header using the Headers property, an exception will be
		 *	thrown. Use the KeepAlive or ContentLength64 properties to set these headers.
		 *	You cannot set the Transfer-Encoding or WWW-Authenticate headers manually."
		*/
		// TODO: check if this is marked readonly after headers are sent.
				headers = value;
			}
		}

		public bool KeepAlive {
			get { return keep_alive; }
			set {
				if (disposed)
					throw new ObjectDisposedException (GetType ().ToString ());

				if (HeadersSent)
					throw new InvalidOperationException ("Cannot be changed after headers are sent.");

				keep_alive = value;
			}
		}

		public Stream OutputStream {
			get {
				if (output_stream == null)
					output_stream = context.Connection.GetResponseStream ();

				return output_stream;
			}
		}

		public Version ProtocolVersion {
			get { return version; }
			set {
				if (disposed)
					throw new ObjectDisposedException (GetType ().ToString ());

				if (HeadersSent)
					throw new InvalidOperationException ("Cannot be changed after headers are sent.");
					
				if (value == null)
					throw new ArgumentNullException ("value");

				if (value.Major != 1 || (value.Minor != 0 && value.Minor != 1))
					throw new ArgumentException ("Must be 1.0 or 1.1", "value");

				if (disposed)
					throw new ObjectDisposedException (GetType ().ToString ());

				version = value;
			}
		}

		public string RedirectLocation {
			get { return location; }
			set {
				if (disposed)
					throw new ObjectDisposedException (GetType ().ToString ());

				if (HeadersSent)
					throw new InvalidOperationException ("Cannot be changed after headers are sent.");

				location = value;
			}
		}

		public bool SendChunked {
			get { return chunked; }
			set {
				if (disposed)
					throw new ObjectDisposedException (GetType ().ToString ());

				if (HeadersSent)
					throw new InvalidOperationException ("Cannot be changed after headers are sent.");

				chunked = value;
			}
		}

		public int StatusCode {
			get { return status_code; }
			set {
				if (disposed)
					throw new ObjectDisposedException (GetType ().ToString ());

				if (HeadersSent)
					throw new InvalidOperationException ("Cannot be changed after headers are sent.");

				if (value < 100 || value > 999)
					throw new ProtocolViolationException ("StatusCode must be between 100 and 999.");

				status_code = value;
				status_description = value.GetStatusDescription ();
			}
		}

		public string StatusDescription {
			get { return status_description; }
			set {
				status_description = value;
			}
		}

		#endregion

		#region Private Methods

		void Close (bool force)
		{
			disposed = true;
			context.Connection.Close (force);
		}

		bool FindCookie (Cookie cookie)
		{
			string name = cookie.Name;
			string domain = cookie.Domain;
			string path = cookie.Path;
			foreach (Cookie c in cookies) {
				if (name != c.Name)
					continue;
				if (domain != c.Domain)
					continue;
				if (path == c.Path)
					return true;
			}

			return false;
		}

		void Init ()
		{
			headers = new WebHeaderCollection ();
			keep_alive = true;
			status_code = 200;
			status_description = "OK";
			version = HttpVersion.Version11;
		}

		#endregion

		#region Internal Method

		internal void SendHeaders (bool closing, MemoryStream ms)
		{
			Encoding encoding = content_encoding;
			if (encoding == null)
				encoding = Encoding.Default;

			if (content_type != null) {
				if (content_encoding != null && content_type.IndexOf ("charset=", StringComparison.Ordinal) == -1) {
					string enc_name = content_encoding.WebName;
					headers.SetInternal ("Content-Type", content_type + "; charset=" + enc_name);
				} else {
					headers.SetInternal ("Content-Type", content_type);
				}
			}

			if (headers ["Server"] == null)
				headers.SetInternal ("Server", "Mono-HTTPAPI/1.0");

			CultureInfo inv = CultureInfo.InvariantCulture;
			if (headers ["Date"] == null)
				headers.SetInternal ("Date", DateTime.UtcNow.ToString ("r", inv));

			if (!chunked) {
				if (!cl_set && closing) {
					cl_set = true;
					content_length = 0;
				}

				if (cl_set)
					headers.SetInternal ("Content-Length", content_length.ToString (inv));
			}

			Version v = context.Request.ProtocolVersion;
			if (!cl_set && !chunked && v >= HttpVersion.Version11)
				chunked = true;
				
			/* Apache forces closing the connection for these status codes:
			 *	HttpStatusCode.BadRequest 				400
			 *	HttpStatusCode.RequestTimeout 			408
			 *	HttpStatusCode.LengthRequired 			411
			 *	HttpStatusCode.RequestEntityTooLarge 	413
			 *	HttpStatusCode.RequestUriTooLong 		414
			 *	HttpStatusCode.InternalServerError 		500
			 *	HttpStatusCode.ServiceUnavailable 		503
			 */
			bool conn_close = (status_code == 400 || status_code == 408 || status_code == 411 ||
					status_code == 413 || status_code == 414 || status_code == 500 ||
					status_code == 503);

			if (conn_close == false)
				conn_close = !context.Request.KeepAlive;

			// They sent both KeepAlive: true and Connection: close!?
			if (!keep_alive || conn_close) {
				headers.SetInternal ("Connection", "close");
				conn_close = true;
			}

			if (chunked)
				headers.SetInternal ("Transfer-Encoding", "chunked");

			int reuses = context.Connection.Reuses;
			if (reuses >= 100) {
				force_close_chunked = true;
				if (!conn_close) {
					headers.SetInternal ("Connection", "close");
					conn_close = true;
				}
			}

			if (!conn_close) {
				headers.SetInternal ("Keep-Alive", String.Format ("timeout=15,max={0}", 100 - reuses));
				if (context.Request.ProtocolVersion <= HttpVersion.Version10)
					headers.SetInternal ("Connection", "keep-alive");
			}

			if (location != null)
				headers.SetInternal ("Location", location);

			if (cookies != null) {
				foreach (Cookie cookie in cookies)
					headers.SetInternal ("Set-Cookie", cookie.ToClientString ());
			}

			StreamWriter writer = new StreamWriter (ms, encoding, 256);
			writer.Write ("HTTP/{0} {1} {2}\r\n", version, status_code, status_description);
			string headers_str = headers.ToStringMultiValue ();
			writer.Write (headers_str);
			writer.Flush ();
			int preamble = (encoding.CodePage == 65001) ? 3 : encoding.GetPreamble ().Length;
			if (output_stream == null)
				output_stream = context.Connection.GetResponseStream ();

			/* Assumes that the ms was at position 0 */
			ms.Position = preamble;
			HeadersSent = true;
		}

		#endregion

		#region Explicit Interface Implementation

		void IDisposable.Dispose ()
		{
			Close (true); // TODO: Abort or Close?
		}

		#endregion

		#region Public Methods

		public void Abort ()
		{
			if (disposed)
				return;

			Close (true);
		}

		public void AddHeader (string name, string value)
		{
			if (name == null)
				throw new ArgumentNullException ("name");

			if (name == "")
				throw new ArgumentException ("'name' cannot be empty", "name");

			// TODO: check for forbidden headers and invalid characters
			if (value.Length > 65535)
				throw new ArgumentOutOfRangeException ("value");

			headers.Set (name, value);
		}

		public void AppendCookie (Cookie cookie)
		{
			if (cookie == null)
				throw new ArgumentNullException ("cookie");

			Cookies.Add (cookie);
		}

		public void AppendHeader (string name, string value)
		{
			if (name == null)
				throw new ArgumentNullException ("name");

			if (name == "")
				throw new ArgumentException ("'name' cannot be empty", "name");

			if (value.Length > 65535)
				throw new ArgumentOutOfRangeException ("value");

			headers.Add (name, value);
		}

		public void Close ()
		{
			if (disposed)
				return;

			Close (false);
		}

		public void Close (byte [] responseEntity, bool willBlock)
		{
			if (disposed)
				return;

			if (responseEntity == null)
				throw new ArgumentNullException ("responseEntity");

			// TODO: if willBlock -> BeginWrite + Close ?
			ContentLength64 = responseEntity.Length;
			OutputStream.Write (responseEntity, 0, (int) content_length);
			Close (false);
		}

		public void CopyFrom (HttpListenerResponse templateResponse)
		{
			headers.Clear ();
			headers.Add (templateResponse.headers);
			content_length = templateResponse.content_length;
			status_code = templateResponse.status_code;
			status_description = templateResponse.status_description;
			keep_alive = templateResponse.keep_alive;
			version = templateResponse.version;
		}

		public void Redirect (string url)
		{
			StatusCode = 302; // Found
			location = url;
		}

		public void SetCookie (Cookie cookie)
		{
			if (cookie == null)
				throw new ArgumentNullException ("cookie");

			if (cookies != null) {
				if (FindCookie (cookie))
					throw new ArgumentException ("The cookie already exists.");
			} else {
				cookies = new CookieCollection ();
			}

			cookies.Add (cookie);
		}

		#endregion
	}
}
