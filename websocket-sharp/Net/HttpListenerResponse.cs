//
// HttpListenerResponse.cs
//	Copied from System.Net.HttpListenerResponse.cs
//
// Author:
//	Gonzalo Paniagua Javier (gonzalo@novell.com)
//
// Copyright (c) 2005 Novell, Inc. (http://www.novell.com)
// Copyright (c) 2012-2013 sta.blockhead (sta.blockhead@gmail.com)
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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace WebSocketSharp.Net
{
	/// <summary>
	/// Provides access to a response to a request being processed by a <see cref="HttpListener"/> instance.
	/// </summary>
	/// <remarks>
	/// The HttpListenerResponse class cannot be inherited.
	/// </remarks>
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

		#region Internal Field

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

		/// <summary>
		/// Gets or sets the encoding that can be used with the entity body data included in the response.
		/// </summary>
		/// <value>
		/// A <see cref="Encoding"/> that contains the encoding that can be used with the entity body data.
		/// </value>
		/// <exception cref="ObjectDisposedException">
		/// This object is closed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The response has been sent already.
		/// </exception>
		public Encoding ContentEncoding {
			get {
				if (content_encoding == null)
					content_encoding = Encoding.Default;

				return content_encoding;
			}
			set {
				if (disposed)
					throw new ObjectDisposedException (GetType ().ToString ());

				if (HeadersSent)
					throw new InvalidOperationException ("Cannot be changed after headers are sent.");

				content_encoding = value;
			}
		}

		/// <summary>
		/// Gets or sets the size of the entity body data included in the response.
		/// </summary>
		/// <value>
		/// A <see cref="long"/> that contains the value of the Content-Length entity-header field.
		/// The value is a number of bytes in the entity body data.
		/// </value>
		/// <exception cref="ObjectDisposedException">
		/// This object is closed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The response has been sent already.
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// The value specified for a set operation is less than zero.
		/// </exception>
		public long ContentLength64 {
			get { return content_length; }
			set {
				if (disposed)
					throw new ObjectDisposedException (GetType ().ToString ());

				if (HeadersSent)
					throw new InvalidOperationException ("Cannot be changed after headers are sent.");

				if (value < 0)
					throw new ArgumentOutOfRangeException ("Must be greater than or equal zero.", "value");

				cl_set = true;
				content_length = value;
			}
		}

		/// <summary>
		/// Gets or sets the media type of the entity body included in the response.
		/// </summary>
		/// <value>
		/// The type of the content.
		/// A <see cref="string"/> that contains the value of the Content-Type entity-header field.
		/// </value>
		/// <exception cref="ObjectDisposedException">
		/// This object is closed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The response has been sent already.
		/// </exception>
		/// <exception cref="ArgumentNullException">
		/// The value specified for a set operation is <see langword="null"/>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// The value specified for a set operation is a <see cref="String.Empty"/>.
		/// </exception>
		public string ContentType {
			get { return content_type; }
			set {
				if (disposed)
					throw new ObjectDisposedException (GetType ().ToString ());

				if (HeadersSent)
					throw new InvalidOperationException ("Cannot be changed after headers are sent.");

				if (value == null)
					throw new ArgumentNullException ("value");

				if (value.Length == 0)
					throw new ArgumentException ("Must not be empty.", "value");

				content_type = value;
			}
		}

		/// <summary>
		/// Gets or sets the cookies returned with the response.
		/// </summary>
		/// <value>
		/// A <see cref="CookieCollection"/> that contains the cookies returned with the response.
		/// </value>
		public CookieCollection Cookies {
			get {
				if (cookies == null)
					cookies = new CookieCollection ();

				return cookies;
			}
			set { cookies = value; }
		}

		/// <summary>
		/// Gets or sets the HTTP headers returned to the client.
		/// </summary>
		/// <value>
		/// A <see cref="WebHeaderCollection"/> that contains the HTTP headers returned to the client.
		/// </value>
		public WebHeaderCollection Headers {
			get { return headers; }
			set {
				/*
				 * "If you attempt to set a Content-Length, Keep-Alive, Transfer-Encoding, or
				 * WWW-Authenticate header using the Headers property, an exception will be
				 * thrown. Use the KeepAlive or ContentLength64 properties to set these headers.
				 * You cannot set the Transfer-Encoding or WWW-Authenticate headers manually."
				 */
				// TODO: Support for InvalidOperationException.

				// TODO: check if this is marked readonly after headers are sent.

				headers = value;
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether the server requests a persistent connection.
		/// </summary>
		/// <value>
		/// <c>true</c> if the server requests a persistent connection; otherwise, <c>false</c>.
		/// The default is <c>true</c>.
		/// </value>
		/// <exception cref="ObjectDisposedException">
		/// This object is closed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The response has been sent already.
		/// </exception>
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

		/// <summary>
		/// Gets a <see cref="Stream"/> to use to write the entity body data.
		/// </summary>
		/// <value>
		/// A <see cref="Stream"/> to use to write the entity body data.
		/// </value>
		/// <exception cref="ObjectDisposedException">
		/// This object is closed.
		/// </exception>
		public Stream OutputStream {
			get {
				if (disposed)
					throw new ObjectDisposedException (GetType ().ToString ());

				if (output_stream == null)
					output_stream = context.Connection.GetResponseStream ();

				return output_stream;
			}
		}

		/// <summary>
		/// Gets or sets the HTTP version used in the response.
		/// </summary>
		/// <value>
		/// A <see cref="Version"/> that contains the HTTP version used in the response.
		/// </value>
		/// <exception cref="ObjectDisposedException">
		/// This object is closed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The response has been sent already.
		/// </exception>
		/// <exception cref="ArgumentNullException">
		/// The value specified for a set operation is <see langword="null"/>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// The value specified for a set operation does not have its <see cref="Version.Major">Major</see> property set to 1 or
		/// does not have its <see cref="Version.Minor">Minor</see> property set to either 0 or 1.
		/// </exception>
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

				version = value;
			}
		}

		/// <summary>
		/// Gets or sets the URL to which the client is redirected to locate a requested resource.
		/// </summary>
		/// <value>
		/// A <see cref="string"/> that contains the value of the Location response-header field.
		/// </value>
		/// <exception cref="ObjectDisposedException">
		/// This object is closed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The response has been sent already.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// The value specified for a set operation is a <see cref="String.Empty"/>.
		/// </exception>
		public string RedirectLocation {
			get { return location; }
			set {
				if (disposed)
					throw new ObjectDisposedException (GetType ().ToString ());

				if (HeadersSent)
					throw new InvalidOperationException ("Cannot be changed after headers are sent.");

				if (value.Length == 0)
					throw new ArgumentException ("Must not be empty.", "value");

				location = value;
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether the response uses the chunked transfer encoding.
		/// </summary>
		/// <value>
		/// <c>true</c> if the response uses the chunked transfer encoding; otherwise, <c>false</c>.
		/// </value>
		/// <exception cref="ObjectDisposedException">
		/// This object is closed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The response has been sent already.
		/// </exception>
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

		/// <summary>
		/// Gets or sets the HTTP status code returned to the client.
		/// </summary>
		/// <value>
		/// An <see cref="int"/> that indicates the HTTP status code for the response to the request.
		/// The default is <see cref="HttpStatusCode.OK"/>.
		/// </value>
		/// <exception cref="ObjectDisposedException">
		/// This object is closed.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The response has been sent already.
		/// </exception>
		/// <exception cref="ProtocolViolationException">
		/// The value specified for a set operation is invalid. Valid values are between 100 and 999.
		/// </exception>
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

		/// <summary>
		/// Gets or sets a description of the HTTP status code returned to the client.
		/// </summary>
		/// <value>
		/// A <see cref="String"/> that contains a description of the HTTP status code returned to the client.
		/// </value>
		public string StatusDescription {
			get { return status_description; }
			set {
				status_description = value.IsNullOrEmpty ()
				                   ? status_code.GetStatusDescription ()
				                   : value;
			}
		}

		#endregion

		#region Private Methods

		bool CanAddOrUpdate (Cookie cookie)
		{
			if (Cookies.Count == 0)
				return true;

			var found = FindCookie (cookie);
			if (found.Count() == 0)
				return true;

			foreach (var c in found)
				if (c.Version == cookie.Version)
					return true;

			return false;
		}

		void Close (bool force)
		{
			disposed = true;
			context.Connection.Close (force);
		}

		IEnumerable<Cookie> FindCookie (Cookie cookie)
		{
			var name = cookie.Name;
			var domain = cookie.Domain;
			var path = cookie.Path;

			return from Cookie c in Cookies
			       where String.Compare (name, c.Name, true, CultureInfo.InvariantCulture) == 0 &&
			             String.Compare (domain, c.Domain, true, CultureInfo.InvariantCulture) == 0 &&
			             String.Compare (path, c.Path, false, CultureInfo.InvariantCulture) == 0
			       select c;
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
					headers.SetInternal ("Content-Type", content_type + "; charset=" + enc_name, true);
				} else {
					headers.SetInternal ("Content-Type", content_type, true);
				}
			}

			if (headers ["Server"] == null)
				headers.SetInternal ("Server", "WebSocketSharp-HTTPAPI/1.0", true);

			CultureInfo inv = CultureInfo.InvariantCulture;
			if (headers ["Date"] == null)
				headers.SetInternal ("Date", DateTime.UtcNow.ToString ("r", inv), true);

			if (!chunked) {
				if (!cl_set && closing) {
					cl_set = true;
					content_length = 0;
				}

				if (cl_set)
					headers.SetInternal ("Content-Length", content_length.ToString (inv), true);
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
				headers.SetInternal ("Connection", "close", true);
				conn_close = true;
			}

			if (chunked)
				headers.SetInternal ("Transfer-Encoding", "chunked", true);

			int reuses = context.Connection.Reuses;
			if (reuses >= 100) {
				force_close_chunked = true;
				if (!conn_close) {
					headers.SetInternal ("Connection", "close", true);
					conn_close = true;
				}
			}

			if (!conn_close) {
				headers.SetInternal ("Keep-Alive", String.Format ("timeout=15,max={0}", 100 - reuses), true);
				if (context.Request.ProtocolVersion <= HttpVersion.Version10)
					headers.SetInternal ("Connection", "keep-alive", true);
			}

			if (location != null)
				headers.SetInternal ("Location", location, true);

			if (cookies != null) {
				foreach (Cookie cookie in cookies)
					headers.SetInternal ("Set-Cookie", cookie.ToResponseString (), true);
			}

			StreamWriter writer = new StreamWriter (ms, encoding, 256);
			writer.Write ("HTTP/{0} {1} {2}\r\n", version, status_code, status_description);
			string headers_str = headers.ToStringMultiValue (true);
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

		/// <summary>
		/// Releases all resource used by the <see cref="HttpListenerResponse"/>.
		/// </summary>
		void IDisposable.Dispose ()
		{
			Close (true); // TODO: Abort or Close?
		}

		#endregion

		#region Public Methods

		/// <summary>
		/// Closes the connection to the client without sending a response.
		/// </summary>
		public void Abort ()
		{
			if (disposed)
				return;

			Close (true);
		}

		/// <summary>
		/// Adds the specified HTTP header <paramref name="name"/> and <paramref name="value"/> to
		/// the headers for this response.
		/// </summary>
		/// <param name="name">
		/// A <see cref="string"/> that contains the name of the HTTP header to add.
		/// </param>
		/// <param name="value">
		/// A <see cref="string"/> that contains the value of the HTTP header to add.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="name"/> is <see langword="null"/> or <see cref="String.Empty"/>.
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// The length of <paramref name="value"/> is greater than 65,535 characters.
		/// </exception>
		public void AddHeader (string name, string value)
		{
			if (name.IsNullOrEmpty())
				throw new ArgumentNullException ("name");

			// TODO: Check for forbidden headers and invalid characters.

			if (value.Length > 65535)
				throw new ArgumentOutOfRangeException ("value");

			headers.Set (name, value);
		}

		/// <summary>
		/// Adds the specified <see cref="Cookie"/> to the <see cref="Cookies"/> sent with the response.
		/// </summary>
		/// <param name="cookie">
		/// A <see cref="Cookie"/> to add to the <see cref="Cookies"/>.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="cookie"/> is <see langword="null"/>.
		/// </exception>
		public void AppendCookie (Cookie cookie)
		{
			if (cookie == null)
				throw new ArgumentNullException ("cookie");

			Cookies.Add (cookie);
		}

		/// <summary>
		/// Appends a <paramref name="value"/> to the specified HTTP header sent with the response.
		/// </summary>
		/// <param name="name">
		/// A <see cref="string"/> that contains the name of the HTTP header to append <paramref name="value"/> to.
		/// </param>
		/// <param name="value">
		/// A <see cref="string"/> that contains the value to append to the HTTP header.
		/// </param>
		/// <exception cref="ArgumentException">
		/// <paramref name="name"/> is <see langword="null"/> or <see cref="String.Empty"/>.
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// The length of <paramref name="value"/> is greater than 65,535 characters.
		/// </exception>
		public void AppendHeader (string name, string value)
		{
			// TODO: Check for forbidden headers and invalid characters.
			if (name.IsNullOrEmpty())
				throw new ArgumentException ("'name' cannot be null or empty", "name");

			if (value.Length > 65535)
				throw new ArgumentOutOfRangeException ("value");

			headers.Add (name, value);
		}

		/// <summary>
		/// Sends the response to the client and releases the resources associated with
		/// the <see cref="HttpListenerResponse"/> instance.
		/// </summary>
		public void Close ()
		{
			if (disposed)
				return;

			Close (false);
		}

		/// <summary>
		/// Sends the response with the specified array of <see cref="byte"/> to the client and
		/// releases the resources associated with the <see cref="HttpListenerResponse"/> instance.
		/// </summary>
		/// <param name="responseEntity">
		/// An array of <see cref="byte"/> that contains the response entity body data.
		/// </param>
		/// <param name="willBlock">
		/// <c>true</c> if this method blocks execution while flushing the stream to the client; otherwise, <c>false</c>.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="responseEntity"/> is <see langword="null"/>.
		/// </exception>
		/// <exception cref="ObjectDisposedException">
		/// This object is closed.
		/// </exception>
		public void Close (byte [] responseEntity, bool willBlock)
		{
			if (disposed)
				throw new ObjectDisposedException (GetType ().ToString ());

			if (responseEntity == null)
				throw new ArgumentNullException ("responseEntity");

			// TODO: If willBlock -> BeginWrite + Close?
			ContentLength64 = responseEntity.Length;
			OutputStream.Write (responseEntity, 0, (int) content_length);
			Close (false);
		}

		/// <summary>
		/// Copies properties from the specified <see cref="HttpListenerResponse"/> to this response.
		/// </summary>
		/// <param name="templateResponse">
		/// A <see cref="HttpListenerResponse"/> to copy.
		/// </param>
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

		/// <summary>
		/// Configures the response to redirect the client's request to the specified <paramref name="url"/>.
		/// </summary>
		/// <param name="url">
		/// A <see cref="string"/> that contains a URL to redirect the client's request to.
		/// </param>
		public void Redirect (string url)
		{
			StatusCode = (int) HttpStatusCode.Redirect;
			location = url;
		}

		/// <summary>
		/// Adds or updates a <see cref="Cookie"/> in the <see cref="Cookies"/> sent with the response.
		/// </summary>
		/// <param name="cookie">
		/// A <see cref="Cookie"/> to set.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="cookie"/> is <see langword="null"/>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="cookie"/> already exists in the <see cref="Cookies"/> and
		/// could not be replaced.
		/// </exception>
		public void SetCookie (Cookie cookie)
		{
			if (cookie == null)
				throw new ArgumentNullException ("cookie");

			if (!CanAddOrUpdate (cookie))
				throw new ArgumentException ("Cannot be replaced.", "cookie");

			Cookies.Add (cookie);
		}

		#endregion
	}
}
