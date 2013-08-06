#region License
//
// HttpListenerRequest.cs
//	Copied from System.Net.HttpListenerRequest.cs
//
// Author:
//	Gonzalo Paniagua Javier (gonzalo@novell.com)
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
#endregion

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace WebSocketSharp.Net
{
	/// <summary>
	/// Provides access to a request to a <see cref="HttpListener"/> instance.
	/// </summary>
	/// <remarks>
	/// The HttpListenerRequest class cannot be inherited.
	/// </remarks>
	public sealed class HttpListenerRequest
	{
		#region Private Static Fields

		private static byte [] _100continue = Encoding.ASCII.GetBytes ("HTTP/1.1 100 Continue\r\n\r\n");

		#endregion

		#region Private Fields

		private string []           _acceptTypes;
		private bool                _chunked;
		private Encoding            _contentEncoding;
		private long                _contentLength;
		private bool                _contentLengthWasSet;
		private HttpListenerContext _context;
		private CookieCollection    _cookies;
		private WebHeaderCollection _headers;
		private Guid                _identifier;
		private Stream              _inputStream;
		private bool                _keepAlive;
		private bool                _keepAliveWasSet;
		private string              _method;
		private NameValueCollection _queryString;
		private string              _rawUrl;
		private Uri                 _referer;
		private Uri                 _url;
		private string []           _userLanguages;
		private Version             _version;

		#endregion

		#region Internal Constructors

		internal HttpListenerRequest (HttpListenerContext context)
		{
			_context = context;
			_contentLength = -1;
			_headers = new WebHeaderCollection ();
			_identifier = Guid.NewGuid ();
			_version = HttpVersion.Version10;
		}

		#endregion

		#region Public Properties

		/// <summary>
		/// Gets the media types which are acceptable for the response.
		/// </summary>
		/// <value>
		/// An array of <see cref="string"/> that contains the media type names in the Accept request-header
		/// or <see langword="null"/> if the request did not include an Accept header.
		/// </value>
		public string [] AcceptTypes {
			get {
				return _acceptTypes;
			}
		}

		/// <summary>
		/// Gets an error code that identifies a problem with the client's certificate.
		/// </summary>
		/// <value>
		/// Always returns <c>0</c>.
		/// </value>
		public int ClientCertificateError {
			get {
				// TODO: Always returns 0.
/*
				if (no_get_certificate)
					throw new InvalidOperationException (
						"Call GetClientCertificate method before accessing this property.");

				return client_cert_error;
*/
				return 0;
			}
		}

		/// <summary>
		/// Gets the encoding used with the entity body data included in the request.
		/// </summary>
		/// <value>
		/// A <see cref="Encoding"/> that indicates the encoding used with the entity body data
		/// or <see cref="Encoding.Default"/> if the request did not include the information about the encoding.
		/// </value>
		public Encoding ContentEncoding {
			get {
				if (_contentEncoding == null)
					_contentEncoding = Encoding.Default;

				return _contentEncoding;
			}
		}

		/// <summary>
		/// Gets the size of the entity body data included in the request.
		/// </summary>
		/// <value>
		/// A <see cref="long"/> that contains the value of the Content-Length entity-header.
		/// The value is a number of bytes in the entity body data. <c>-1</c> if the size is not known.
		/// </value>
		public long ContentLength64 {
			get {
				return _contentLength;
			}
		}

		/// <summary>
		/// Gets the media type of the entity body included in the request.
		/// </summary>
		/// <value>
		/// A <see cref="string"/> that contains the value of the Content-Type entity-header.
		/// </value>
		public string ContentType {
			get {
				return _headers ["Content-Type"];
			}
		}

		/// <summary>
		/// Gets the cookies included in the request.
		/// </summary>
		/// <value>
		/// A <see cref="CookieCollection"/> that contains the cookies included in the request.
		/// </value>
		public CookieCollection Cookies {
			get {
				if (_cookies == null)
					_cookies = _headers.GetCookies (false);

				return _cookies;
			}
		}

		/// <summary>
		/// Gets a value indicating whether the request has the entity body.
		/// </summary>
		/// <value>
		/// <c>true</c> if the request has the entity body; otherwise, <c>false</c>.
		/// </value>
		public bool HasEntityBody {
			get {
				return _contentLength > 0 || _chunked;
			}
		}

		/// <summary>
		/// Gets the HTTP headers used in the request.
		/// </summary>
		/// <value>
		/// A <see cref="NameValueCollection"/> that contains the HTTP headers used in the request.
		/// </value>
		public NameValueCollection Headers {
			get {
				return _headers;
			}
		}

		/// <summary>
		/// Gets the HTTP method used in the request.
		/// </summary>
		/// <value>
		/// A <see cref="string"/> that contains the HTTP method used in the request.
		/// </value>
		public string HttpMethod {
			get {
				return _method;
			}
		}

		/// <summary>
		/// Gets a <see cref="Stream"/> that contains the entity body data included in the request.
		/// </summary>
		/// <value>
		/// A <see cref="Stream"/> that contains the entity body data included in the request.
		/// </value>
		public Stream InputStream {
			get {
				if (_inputStream == null)
					_inputStream = HasEntityBody
					             ? _context.Connection.GetRequestStream (_chunked, _contentLength)
					             : Stream.Null;

				return _inputStream;
			}
		}

		/// <summary>
		/// Gets a value indicating whether the client that sent the request is authenticated.
		/// </summary>
		/// <value>
		/// Always returns <c>false</c>.
		/// </value>
		public bool IsAuthenticated {
			get {
				// TODO: Always returns false.
				return false;
			}
		}

		/// <summary>
		/// Gets a value indicating whether the request is sent from the local computer.
		/// </summary>
		/// <value>
		/// <c>true</c> if the request is sent from the local computer; otherwise, <c>false</c>.
		/// </value>
		public bool IsLocal {
			get {
				return RemoteEndPoint.Address.IsLocal ();
			}
		}

		/// <summary>
		/// Gets a value indicating whether the HTTP connection is secured using the SSL protocol.
		/// </summary>
		/// <value>
		/// <c>true</c> if the HTTP connection is secured; otherwise, <c>false</c>.
		/// </value>
		public bool IsSecureConnection {
			get {
				return _context.Connection.IsSecure;
			}
		}

		/// <summary>
		/// Gets a value indicating whether the request is a WebSocket connection request.
		/// </summary>
		/// <value>
		/// <c>true</c> if the request is a WebSocket connection request; otherwise, <c>false</c>.
		/// </value>
		public bool IsWebSocketRequest {
			get {
				return _method == "GET" &&
				       _version >= HttpVersion.Version11 &&
				       _headers.Contains ("Upgrade", "websocket") &&
				       _headers.Contains ("Connection", "Upgrade");
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
				if (!_keepAliveWasSet) {
					_keepAlive = _headers.Contains ("Connection", "keep-alive") || _version == HttpVersion.Version11
					           ? true
					           : _headers.Contains ("Keep-Alive")
					             ? !_headers.Contains ("Keep-Alive", "closed")
					             : false;

					_keepAliveWasSet = true;
				}

				return _keepAlive;
			}
		}

		/// <summary>
		/// Gets the server endpoint as an IP address and a port number.
		/// </summary>
		/// <value>
		/// A <see cref="IPEndPoint"/> that contains the server endpoint.
		/// </value>
		public IPEndPoint LocalEndPoint {
			get {
				return _context.Connection.LocalEndPoint;
			}
		}

		/// <summary>
		/// Gets the HTTP version used in the request.
		/// </summary>
		/// <value>
		/// A <see cref="Version"/> that contains the HTTP version used in the request.
		/// </value>
		public Version ProtocolVersion {
			get {
				return _version;
			}
		}

		/// <summary>
		/// Gets the collection of query string variables used in the request.
		/// </summary>
		/// <value>
		/// A <see cref="NameValueCollection"/> that contains the collection of query string variables used in the request.
		/// </value>
		public NameValueCollection QueryString {
			get {
				return _queryString;
			}
		}

		/// <summary>
		/// Gets the raw URL (without the scheme, host and port) requested by the client.
		/// </summary>
		/// <value>
		/// A <see cref="string"/> that contains the raw URL requested by the client.
		/// </value>
		public string RawUrl {
			get {
				return _rawUrl;
			}
		}

		/// <summary>
		/// Gets the client endpoint as an IP address and a port number.
		/// </summary>
		/// <value>
		/// A <see cref="IPEndPoint"/> that contains the client endpoint.
		/// </value>
		public IPEndPoint RemoteEndPoint {
			get {
				return _context.Connection.RemoteEndPoint;
			}
		}

		/// <summary>
		/// Gets the request identifier of a incoming HTTP request.
		/// </summary>
		/// <value>
		/// A <see cref="Guid"/> that contains the identifier of a request.
		/// </value>
		public Guid RequestTraceIdentifier {
			get {
				return _identifier;
			}
		}

		/// <summary>
		/// Gets the URL requested by the client.
		/// </summary>
		/// <value>
		/// A <see cref="Uri"/> that contains the URL requested by the client.
		/// </value>
		public Uri Url {
			get {
				return _url;
			}
		}

		/// <summary>
		/// Gets the URL of the resource from which the requested URL was obtained.
		/// </summary>
		/// <value>
		/// A <see cref="Uri"/> that contains the value of the Referer request-header
		/// or <see langword="null"/> if the request did not include an Referer header.
		/// </value>
		public Uri UrlReferrer {
			get {
				return _referer;
			}
		}

		/// <summary>
		/// Gets the information about the user agent originating the request.
		/// </summary>
		/// <value>
		/// A <see cref="string"/> that contains the value of the User-Agent request-header.
		/// </value>
		public string UserAgent {
			get {
				return _headers ["User-Agent"];
			}
		}

		/// <summary>
		/// Gets the server endpoint as an IP address and a port number.
		/// </summary>
		/// <value>
		/// A <see cref="string"/> that contains the server endpoint.
		/// </value>
		public string UserHostAddress {
			get {
				return LocalEndPoint.ToString ();
			}
		}

		/// <summary>
		/// Gets the internet host name and port number (if present) specified by the client.
		/// </summary>
		/// <value>
		/// A <see cref="string"/> that contains the value of the Host request-header.
		/// </value>
		public string UserHostName {
			get {
				return _headers ["Host"];
			}
		}

		/// <summary>
		/// Gets the natural languages which are preferred for the response.
		/// </summary>
		/// <value>
		/// An array of <see cref="string"/> that contains the natural language names in the Accept-Language request-header
		/// or <see langword="null"/> if the request did not include an Accept-Language header.
		/// </value>
		public string [] UserLanguages {
			get {
				return _userLanguages;
			}
		}

		#endregion

		#region Private Methods

		private void CreateQueryString (string query)
		{
			if (query == null || query.Length == 0) {
				_queryString = new NameValueCollection (1);
				return;
			}

			_queryString = new NameValueCollection ();
			if (query [0] == '?')
				query = query.Substring (1);

			var components = query.Split ('&');
			foreach (var kv in components) {
				var pos = kv.IndexOf ('=');
				if (pos == -1) {
					_queryString.Add (null, HttpUtility.UrlDecode (kv));
				} else {
					var key = HttpUtility.UrlDecode (kv.Substring (0, pos));
					var val = HttpUtility.UrlDecode (kv.Substring (pos + 1));
					_queryString.Add (key, val);
				}
			}
		}

		#endregion

		#region Internal Methods

		internal void AddHeader (string header)
		{
			var colon = header.IndexOf (':');
			if (colon <= 0) {
				_context.ErrorMessage = "Invalid header";
				return;
			}

			var name = header.Substring (0, colon).Trim ();
			var val = header.Substring (colon + 1).Trim ();
			var lower = name.ToLower (CultureInfo.InvariantCulture);
			_headers.SetInternal (name, val, false);

			if (lower == "accept") {
				_acceptTypes = val.SplitHeaderValue (',').ToArray ();
				return;
			}

			if (lower == "accept-language") {
				_userLanguages = val.Split (',');
				return;
			}

			if (lower == "content-length") {
				long length;
				if (Int64.TryParse (val, out length) && length >= 0) {
					_contentLength = length;
					_contentLengthWasSet = true;
				} else {
					_context.ErrorMessage = "Invalid Content-Length header";
				}

				return;
			}

			if (lower == "content-type") {
				var contents = val.Split (';');
				foreach (var content in contents) {
					var tmp = content.Trim ();
					if (tmp.StartsWith ("charset")) {
						var charset = tmp.GetValue ("=");
						if (!charset.IsNullOrEmpty ()) {
							try {
								_contentEncoding = Encoding.GetEncoding (charset);
							} catch {
								_context.ErrorMessage = "Invalid Content-Type header";
							}
						}

						break;
					}
				}

				return;
			}

			if (lower == "referer")
				_referer = val.ToUri ();
		}

		internal void FinishInitialization ()
		{
			var host = UserHostName;
			if (_version > HttpVersion.Version10 && host.IsNullOrEmpty ()) {
				_context.ErrorMessage = "Invalid Host header";
				return;
			}

			Uri rawUri = null;
			var path = _rawUrl.MaybeUri () && Uri.TryCreate (_rawUrl, UriKind.Absolute, out rawUri)
			         ? rawUri.PathAndQuery
			         : HttpUtility.UrlDecode (_rawUrl);

			if (host.IsNullOrEmpty ())
				host = UserHostAddress;

			if (rawUri != null)
				host = rawUri.Host;

			var colon = host.IndexOf (':');
			if (colon >= 0)
				host = host.Substring (0, colon);

			var baseUri = String.Format ("{0}://{1}:{2}",
				IsSecureConnection ? "https" : "http",
				host,
				LocalEndPoint.Port);

			if (!Uri.TryCreate (baseUri + path, UriKind.Absolute, out _url)) {
				_context.ErrorMessage = "Invalid request url: " + baseUri + path;
				return;
			}

			CreateQueryString (_url.Query);

			var encoding = Headers ["Transfer-Encoding"];
			if (_version >= HttpVersion.Version11 && !encoding.IsNullOrEmpty ()) {
				_chunked = encoding.ToLower () == "chunked";
				// 'identity' is not valid!
				if (!_chunked) {
					_context.ErrorMessage = String.Empty;
					_context.ErrorStatus = 501;

					return;
				}
			}

			if (!_chunked && !_contentLengthWasSet) {
				var method = _method.ToLower ();
				if (method == "post" || method == "put") {
					_context.ErrorMessage = String.Empty;
					_context.ErrorStatus = 411;

					return;
				}
			}

			var expect = Headers ["Expect"];
			if (!expect.IsNullOrEmpty () && expect.ToLower () == "100-continue") {
				var output = _context.Connection.GetResponseStream ();
				output.InternalWrite (_100continue, 0, _100continue.Length);
			}
		}

		// Returns true is the stream could be reused.
		internal bool FlushInput ()
		{
			if (!HasEntityBody)
				return true;

			var length = 2048;
			if (_contentLength > 0)
				length = (int) Math.Min (_contentLength, (long) length);

			var buffer = new byte [length];
			while (true) {
				// TODO: Test if MS has a timeout when doing this.
				try {
					var ares = InputStream.BeginRead (buffer, 0, length, null, null);
					if (!ares.IsCompleted && !ares.AsyncWaitHandle.WaitOne (100))
						return false;

					if (InputStream.EndRead (ares) <= 0)
						return true;
				} catch {
					return false;
				}
			}
		}

		internal void SetRequestLine (string requestLine)
		{
			var parts = requestLine.Split (new char [] { ' ' }, 3);
			if (parts.Length != 3) {
				_context.ErrorMessage = "Invalid request line (parts)";
				return;
			}

			_method = parts [0];
			if (!_method.IsToken ()) {
				_context.ErrorMessage = "Invalid request line (method)";
				return;
			}

			_rawUrl = parts [1];

			if (parts [2].Length != 8 || !parts [2].StartsWith ("HTTP/")) {
				_context.ErrorMessage = "Invalid request line (version)";
				return;
			}

			try {
				_version = new Version (parts [2].Substring (5));
				if (_version.Major < 1)
					throw new Exception ();
			} catch {
				_context.ErrorMessage = "Invalid request line (version)";
			}
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

		/// <summary>
		/// Returns a <see cref="string"/> that represents the current <see cref="HttpListenerRequest"/>.
		/// </summary>
		/// <returns>
		/// A <see cref="string"/> that represents the current <see cref="HttpListenerRequest"/>.
		/// </returns>
		public override string ToString ()
		{
			var buffer = new StringBuilder (64);
			buffer.AppendFormat ("{0} {1} HTTP/{2}\r\n", _method, _rawUrl, _version);
			foreach (string key in _headers.AllKeys)
				buffer.AppendFormat ("{0}: {1}\r\n", key, _headers [key]);

			buffer.Append ("\r\n");
			return buffer.ToString ();
		}

		#endregion
	}
}
