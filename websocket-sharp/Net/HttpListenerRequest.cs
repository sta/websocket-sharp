//
// HttpListenerRequest.cs
//	Copied from System.Net.HttpListenerRequest.cs
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
using System.Collections;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace WebSocketSharp.Net {

	/// <summary>
	/// Provides access to the HTTP request objects sent to a <see cref="HttpListener"/> instance.
	/// </summary>
	/// <remarks>
	/// The HttpListenerRequest class cannot be inherited.
	/// </remarks>
	public sealed class HttpListenerRequest {

		#region Private Static Fields

		static char [] separators   = new char [] { ' ' };
		static byte [] _100continue = Encoding.ASCII.GetBytes ("HTTP/1.1 100 Continue\r\n\r\n");

		#endregion

		#region Private Fields

		string []           accept_types;
//		int                 client_cert_error;
		bool                cl_set;
		Encoding            content_encoding;
		long                content_length;
		HttpListenerContext context;
		CookieCollection    cookies;
		WebHeaderCollection headers;
		Stream              input_stream;
		bool                is_chunked;
		bool                ka_set;
		bool                keep_alive;
		string              method;
//		bool                no_get_certificate;
		Version             version;
		NameValueCollection query_string; // check if null is ok, check if read-only, check case-sensitiveness
		string              raw_url;
		Uri                 referrer;
		Uri                 url;
		string []           user_languages;

		#endregion

		#region Constructor

		internal HttpListenerRequest (HttpListenerContext context)
		{
			this.context = context;
			headers      = new WebHeaderCollection ();
			version      = HttpVersion.Version10;
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets the media types which are acceptable for the response.
		/// </summary>
		/// <value>
		/// An array of <see cref="string"/> that contains the media type names in the Accept request-header field
		/// or <see langword="null"/> if the request did not include an Accept header.
		/// </value>
		public string [] AcceptTypes {
			get { return accept_types; }
		}

		/// <summary>
		/// Gets an error code that identifies a problem with the client's certificate.
		/// </summary>
		/// <value>
		/// Always returns <c>0</c>.
		/// </value>
		public int ClientCertificateError {
			// TODO: Always returns 0
			get {
/*				
				if (no_get_certificate)
					throw new InvalidOperationException (
						"Call GetClientCertificate() before calling this method.");
				return client_cert_error;
*/
				return 0;
			}
		}

		/// <summary>
		/// Gets the encoding that can be used with the entity body data included in the request.
		/// </summary>
		/// <value>
		/// A <see cref="Encoding"/> that contains the encoding that can be used with entity body data.
		/// </value>
		public Encoding ContentEncoding {
			// TODO: Always returns Encoding.Default
			get {
				if (content_encoding == null)
					content_encoding = Encoding.Default;
				return content_encoding;
			}
		}

		/// <summary>
		/// Gets the size of the entity body data included in the request.
		/// </summary>
		/// <value>
		/// A <see cref="long"/> that contains the value of the Content-Length entity-header field.
		/// <c>-1</c> if the size is not known.
		/// </value>
		public long ContentLength64 {
			get { return content_length; }
		}

		/// <summary>
		/// Gets the media type of the entity body included in the request.
		/// </summary>
		/// <value>
		/// A <see cref="string"/> that contains the value of the Content-Type entity-header field.
		/// </value>
		public string ContentType {
			get { return headers ["content-type"]; }
		}

		/// <summary>
		/// Gets the cookies included in the request.
		/// </summary>
		/// <value>
		/// A <see cref="CookieCollection"/> that contains the cookies included in the request.
		/// </value>
		public CookieCollection Cookies {
			get {
				// TODO: check if the collection is read-only
				if (cookies == null)
					cookies = new CookieCollection ();
				return cookies;
			}
		}

		/// <summary>
		/// Gets a value indicating whether the request has the entity body.
		/// </summary>
		/// <value>
		/// <c>true</c> if the request has the entity body; otherwise, <c>false</c>.
		/// </value>
		public bool HasEntityBody {
			get { return (content_length > 0 || is_chunked); }
		}

		/// <summary>
		/// Gets the HTTP headers used in the request.
		/// </summary>
		/// <value>
		/// A <see cref="NameValueCollection"/> that contains the HTTP headers used in the request.
		/// </value>
		public NameValueCollection Headers {
			get { return headers; }
		}

		/// <summary>
		/// Gets the HTTP method used in the request.
		/// </summary>
		/// <value>
		/// A <see cref="string"/> that contains the HTTP method used in the request.
		/// </value>
		public string HttpMethod {
			get { return method; }
		}

		/// <summary>
		/// Gets a <see cref="Stream"/> that contains the entity body data included in the request.
		/// </summary>
		/// <value>
		/// A <see cref="Stream"/> that contains the entity body data included in the request.
		/// </value>
		public Stream InputStream {
			get {
				if (input_stream == null) {
					if (is_chunked || content_length > 0)
						input_stream = context.Connection.GetRequestStream (is_chunked, content_length);
					else
						input_stream = Stream.Null;
				}

				return input_stream;
			}
		}

		/// <summary>
		/// Gets a value indicating whether the client that sent the request is authenticated.
		/// </summary>
		/// <value>
		/// Always returns <c>false</c>.
		/// </value>
		public bool IsAuthenticated {
			// TODO: Always returns false
			get { return false; }
		}

		/// <summary>
		/// Gets a value indicating whether the request is sent from the local computer.
		/// </summary>
		/// <value>
		/// <c>true</c> if the request is sent from the local computer; otherwise, <c>false</c>.
		/// </value>
		public bool IsLocal {
			get { return RemoteEndPoint.Address.IsLocal(); }
		}

		/// <summary>
		/// Gets a value indicating whether the HTTP connection is secured using the SSL protocol.
		/// </summary>
		/// <value>
		/// <c>true</c> if the HTTP connection is secured; otherwise, <c>false</c>.
		/// </value>
		public bool IsSecureConnection {
			get { return context.Connection.IsSecure; } 
		}

		/// <summary>
		/// Gets a value indicating whether the request is a WebSocket connection request.
		/// </summary>
		/// <value>
		/// <c>true</c> if the request is a WebSocket connection request; otherwise, <c>false</c>.
		/// </value>
		public bool IsWebSocketRequest {
			get {
				return method != "GET"
					? false
					: version < HttpVersion.Version11
						? false
						: !headers.Exists("Upgrade", "websocket")
							? false
							: !headers.Exists("Connection", "Upgrade")
								? false
								: !headers.Exists("Host")
									? false
									: !headers.Exists("Sec-WebSocket-Key")
										? false
										: headers.Exists("Sec-WebSocket-Version");
			}
		}

		/// <summary>
		/// Gets a value indicating whether the client requests a persistent connection.
		/// </summary>
		/// <value>
		/// <c>true</c> if the client requests a persistent connection; otherwise, <c>false</c>.
		/// </value>
		public bool KeepAlive {
			get {
				if (ka_set)
					return keep_alive;

				ka_set = true;
				// 1. Connection header
				// 2. Protocol (1.1 == keep-alive by default)
				// 3. Keep-Alive header
				string cnc = headers ["Connection"];
				if (!String.IsNullOrEmpty (cnc)) {
					keep_alive = (0 == String.Compare (cnc, "keep-alive", StringComparison.OrdinalIgnoreCase));
				} else if (version == HttpVersion.Version11) {
					keep_alive = true;
				} else {
					cnc = headers ["keep-alive"];
					if (!String.IsNullOrEmpty (cnc))
						keep_alive = (0 != String.Compare (cnc, "closed", StringComparison.OrdinalIgnoreCase));
				}
				return keep_alive;
			}
		}

		/// <summary>
		/// Gets the server endpoint as an IP address and a port number.
		/// </summary>
		/// <value>
		/// A <see cref="IPEndPoint"/> that contains the server endpoint.
		/// </value>
		public IPEndPoint LocalEndPoint {
			get { return context.Connection.LocalEndPoint; }
		}

		/// <summary>
		/// Gets the HTTP version used in the request.
		/// </summary>
		/// <value>
		/// A <see cref="Version"/> that contains the HTTP version used in the request.
		/// </value>
		public Version ProtocolVersion {
			get { return version; }
		}

		/// <summary>
		/// Gets the collection of query string variables used in the request.
		/// </summary>
		/// <value>
		/// A <see cref="NameValueCollection"/> that contains the collection of query string variables used in the request.
		/// </value>
		public NameValueCollection QueryString {
			get { return query_string; }
		}

		/// <summary>
		/// Gets the raw URL (without the scheme, host and port) requested by the client.
		/// </summary>
		/// <value>
		/// A <see cref="string"/> that contains the raw URL requested by the client.
		/// </value>
		public string RawUrl {
			get { return raw_url; }
		}

		/// <summary>
		/// Gets the client endpoint as an IP address and a port number.
		/// </summary>
		/// <value>
		/// A <see cref="IPEndPoint"/> that contains the client endpoint.
		/// </value>
		public IPEndPoint RemoteEndPoint {
			get { return context.Connection.RemoteEndPoint; }
		}

		/// <summary>
		/// Gets the identifier of a request.
		/// </summary>
		/// <value>
		/// A <see cref="Guid"/> that contains the identifier of a request.
		/// </value>
		public Guid RequestTraceIdentifier {
			// TODO: Always returns Guid.Empty
			get { return Guid.Empty; }
		}

		/// <summary>
		/// Gets the URL requested by the client.
		/// </summary>
		/// <value>
		/// A <see cref="Uri"/> that contains the URL requested by the client.
		/// </value>
		public Uri Url {
			get { return url; }
		}

		/// <summary>
		/// Gets the URL of the resource from which the requested URL was obtained.
		/// </summary>
		/// <value>
		/// A <see cref="Uri"/> that contains the value of the Referer request-header field.
		/// </value>
		public Uri UrlReferrer {
			get { return referrer; }
		}

		/// <summary>
		/// Gets the information about the user agent originating the request.
		/// </summary>
		/// <value>
		/// A <see cref="string"/> that contains the value of the User-Agent request-header field.
		/// </value>
		public string UserAgent {
			get { return headers ["user-agent"]; }
		}

		/// <summary>
		/// Gets the server endpoint as an IP address and a port number.
		/// </summary>
		/// <value>
		/// A <see cref="string"/> that contains the server endpoint.
		/// </value>
		public string UserHostAddress {
			get { return LocalEndPoint.ToString (); }
		}

		/// <summary>
		/// Gets the internet host name and port number (if present) of the resource being requested.
		/// </summary>
		/// <value>
		/// A <see cref="string"/> that contains the value of the Host request-header field.
		/// </value>
		public string UserHostName {
			get { return headers ["host"]; }
		}

		/// <summary>
		/// Gets the natural languages that are preferred as a response to the request.
		/// </summary>
		/// <value>
		/// An array of <see cref="string"/> that contains the natural language names in the Accept-Language request-header field.
		/// </value>
		public string [] UserLanguages {
			get { return user_languages; }
		}

		#endregion

		#region Private Methods

		void CreateQueryString (string query)
		{
			if (query == null || query.Length == 0) {
				query_string = new NameValueCollection (1);
				return;
			}

			query_string = new NameValueCollection ();
			if (query [0] == '?')
				query = query.Substring (1);
			string [] components = query.Split ('&');
			foreach (string kv in components) {
				int pos = kv.IndexOf ('=');
				if (pos == -1) {
					query_string.Add (null, HttpUtility.UrlDecode (kv));
				} else {
					string key = HttpUtility.UrlDecode (kv.Substring (0, pos));
					string val = HttpUtility.UrlDecode (kv.Substring (pos + 1));
					query_string.Add (key, val);
				}
			}
		}

		#endregion

		#region Internal Methods

		internal void AddHeader (string header)
		{
			int colon = header.IndexOf (':');
			if (colon == -1 || colon == 0) {
				context.ErrorMessage = "Bad Request";
				context.ErrorStatus = 400;
				return;
			}

			string name = header.Substring (0, colon).Trim ();
			string val = header.Substring (colon + 1).Trim ();
			string lower = name.ToLower (CultureInfo.InvariantCulture);
			headers.SetInternal (name, val);
			switch (lower) {
				case "accept-language":
					user_languages = val.Split (','); // yes, only split with a ','
					break;
				case "accept":
					accept_types = val.Split (','); // yes, only split with a ','
					break;
				case "content-length":
					try {
						//TODO: max. content_length?
						content_length = Int64.Parse (val.Trim ());
						if (content_length < 0)
							context.ErrorMessage = "Invalid Content-Length.";
						cl_set = true;
					} catch {
						context.ErrorMessage = "Invalid Content-Length.";
					}

					break;
				case "referer":
					try {
						referrer = new Uri (val);
					} catch {
						referrer = new Uri ("http://someone.is.screwing.with.the.headers.com/");
					}
					break;
				case "cookie":
					if (cookies == null)
						cookies = new CookieCollection();

					string[] cookieStrings = val.Split(new char[] {',', ';'});
					Cookie current = null;
					int version = 0;
					foreach (string cookieString in cookieStrings) {
						string str = cookieString.Trim ();
						if (str.Length == 0)
							continue;
						if (str.StartsWith ("$Version")) {
							version = Int32.Parse (Unquote (str.Substring (str.IndexOf ('=') + 1)));
						} else if (str.StartsWith ("$Path")) {
							if (current != null)
								current.Path = str.Substring (str.IndexOf ('=') + 1).Trim ();
						} else if (str.StartsWith ("$Domain")) {
							if (current != null)
								current.Domain = str.Substring (str.IndexOf ('=') + 1).Trim ();
						} else if (str.StartsWith ("$Port")) {
							if (current != null)
								current.Port = str.Substring (str.IndexOf ('=') + 1).Trim ();
						} else {
							if (current != null) {
								cookies.Add (current);
							}
							current = new Cookie ();
							int idx = str.IndexOf ('=');
							if (idx > 0) {
								current.Name = str.Substring (0, idx).Trim ();
								current.Value =  str.Substring (idx + 1).Trim ();
							} else {
								current.Name = str.Trim ();
								current.Value = String.Empty;
							}
							current.Version = version;
						}
					}
					if (current != null) {
						cookies.Add (current);
					}
					break;
			}
		}

		internal void FinishInitialization ()
		{
			string host = UserHostName;
			if (version > HttpVersion.Version10 && (host == null || host.Length == 0)) {
				context.ErrorMessage = "Invalid host name";
				return;
			}

			string path;
			Uri raw_uri = null;
			if (raw_url.MaybeUri () && Uri.TryCreate (raw_url, UriKind.Absolute, out raw_uri))
				path = raw_uri.PathAndQuery;
			else
				path = HttpUtility.UrlDecode (raw_url);

			if ((host == null || host.Length == 0))
				host = UserHostAddress;

			if (raw_uri != null)
				host = raw_uri.Host;

			int colon = host.IndexOf (':');
			if (colon >= 0)
				host = host.Substring (0, colon);

			string base_uri = String.Format ("{0}://{1}:{2}",
								(IsSecureConnection) ? "https" : "http",
								host,
								LocalEndPoint.Port);

			if (!Uri.TryCreate (base_uri + path, UriKind.Absolute, out url)){
				context.ErrorMessage = "Invalid url: " + base_uri + path;
				return;
			}

			CreateQueryString (url.Query);

			if (version >= HttpVersion.Version11) {
				string t_encoding = Headers ["Transfer-Encoding"];
				is_chunked = (t_encoding != null && String.Compare (t_encoding, "chunked", StringComparison.OrdinalIgnoreCase) == 0);
				// 'identity' is not valid!
				if (t_encoding != null && !is_chunked) {
					context.Connection.SendError (null, 501);
					return;
				}
			}

			if (!is_chunked && !cl_set) {
				if (String.Compare (method, "POST", StringComparison.OrdinalIgnoreCase) == 0 ||
				    String.Compare (method, "PUT", StringComparison.OrdinalIgnoreCase) == 0) {
					context.Connection.SendError (null, 411);
					return;
				}
			}

			if (String.Compare (Headers ["Expect"], "100-continue", StringComparison.OrdinalIgnoreCase) == 0) {
				ResponseStream output = context.Connection.GetResponseStream ();
				output.InternalWrite (_100continue, 0, _100continue.Length);
			}
		}

		// returns true is the stream could be reused.
		internal bool FlushInput ()
		{
			if (!HasEntityBody)
				return true;

			int length = 2048;
			if (content_length > 0)
				length = (int) Math.Min (content_length, (long) length);

			byte [] bytes = new byte [length];
			while (true) {
				// TODO: test if MS has a timeout when doing this
				try {
					IAsyncResult ares = InputStream.BeginRead (bytes, 0, length, null, null);
					if (!ares.IsCompleted && !ares.AsyncWaitHandle.WaitOne (100))
						return false;

					if (InputStream.EndRead (ares) <= 0)
						return true;
				} catch {
					return false;
				}
			}
		}

		internal void SetRequestLine (string req)
		{
			string [] parts = req.Split (separators, 3);
			if (parts.Length != 3) {
				context.ErrorMessage = "Invalid request line (parts).";
				return;
			}

			method = parts [0];
			foreach (char c in method){
				int ic = (int) c;

				if ((ic >= 'A' && ic <= 'Z') ||
				    (ic > 32 && c < 127 && c != '(' && c != ')' && c != '<' &&
				     c != '<' && c != '>' && c != '@' && c != ',' && c != ';' &&
				     c != ':' && c != '\\' && c != '"' && c != '/' && c != '[' &&
				     c != ']' && c != '?' && c != '=' && c != '{' && c != '}'))
					continue;

				context.ErrorMessage = "(Invalid verb)";
				return;
			}

			raw_url = parts [1];
			if (parts [2].Length != 8 || !parts [2].StartsWith ("HTTP/")) {
				context.ErrorMessage = "Invalid request line (version).";
				return;
			}

			try {
				version = new Version (parts [2].Substring (5));
				if (version.Major < 1)
					throw new Exception ();
			} catch {
				context.ErrorMessage = "Invalid request line (version).";
				return;
			}
		}

		internal static string Unquote (String str)
		{
			int start = str.IndexOf ('\"');
			int end = str.LastIndexOf ('\"');
			if (start >= 0 && end >=0)
				str = str.Substring (start + 1, end - 1);
			return str.Trim ();
		}

		#endregion

		#region Public Methods

		/// <summary>
		/// Begins getting the client's X.509 v.3 certificate asynchronously.
		/// </summary>
		/// <remarks>
		/// This asynchronous operation must be completed by calling the <see cref="EndGetClientCertificate"/> method.
		/// Typically, the method is invoked by the <paramref name="requestCallback"/> delegate.
		/// </remarks>
		/// <returns>
		/// An <see cref="IAsyncResult"/> that contains the status of the asynchronous operation.
		/// </returns>
		/// <param name="requestCallback">
		/// An <see cref="AsyncCallback"/> delegate that references the method(s)
		/// called when the asynchronous operation completes.
		/// </param>
		/// <param name="state">
		/// An <see cref="object"/> that contains a user defined object to pass to the <paramref name="requestCallback"/> delegate.
		/// </param>
		/// <exception cref="NotImplementedException">
		/// This method is not implemented.
		/// </exception>
		public IAsyncResult BeginGetClientCertificate (AsyncCallback requestCallback, Object state)
		{
			// TODO: Not Implemented.
			throw new NotImplementedException ();
		}

		/// <summary>
		/// Ends an asynchronous operation to get the client's X.509 v.3 certificate.
		/// </summary>
		/// <remarks>
		/// This method completes an asynchronous operation started by calling the <see cref="BeginGetClientCertificate"/> method.
		/// </remarks>
		/// <returns>
		/// A <see cref="X509Certificate2"/> that contains the client's X.509 v.3 certificate.
		/// </returns>
		/// <param name="asyncResult">
		/// An <see cref="IAsyncResult"/> obtained by calling the <see cref="BeginGetClientCertificate"/> method.
		/// </param>
		/// <exception cref="NotImplementedException">
		/// This method is not implemented.
		/// </exception>
		public X509Certificate2 EndGetClientCertificate (IAsyncResult asyncResult)
		{
			// TODO: Not Implemented.
			throw new NotImplementedException ();
		}

		/// <summary>
		/// Gets the client's X.509 v.3 certificate.
		/// </summary>
		/// <returns>
		/// A <see cref="X509Certificate2"/> that contains the client's X.509 v.3 certificate.
		/// </returns>
		/// <exception cref="NotImplementedException">
		/// This method is not implemented.
		/// </exception>
		public X509Certificate2 GetClientCertificate ()
		{
			// TODO: Not Implemented.
			throw new NotImplementedException ();
		}

		#endregion
	}
}
