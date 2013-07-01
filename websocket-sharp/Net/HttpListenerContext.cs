#region License
//
// HttpListenerContext.cs
//	Copied from System.Net.HttpListenerContext.cs
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
#endregion

using System;
using System.Collections.Specialized;
using System.IO;
using System.Security.Principal;
using System.Text;
using WebSocketSharp.Net.WebSockets;

namespace WebSocketSharp.Net {

	/// <summary>
	/// Provides access to the HTTP request and response objects used by the <see cref="HttpListener"/> class.
	/// </summary>
	/// <remarks>
	/// The HttpListenerContext class cannot be inherited.
	/// </remarks>
	public sealed class HttpListenerContext {

		#region Private Fields

		private HttpConnection       _connection;
		private string               _error;
		private int                  _errorStatus;
		private HttpListenerRequest  _request;
		private HttpListenerResponse _response;
		private IPrincipal           _user;

		#endregion

		#region Internal Fields

		internal HttpListener Listener;

		#endregion

		#region Constructor

		internal HttpListenerContext (HttpConnection connection)
		{
			_connection = connection;
			_errorStatus = 400;
			_request = new HttpListenerRequest (this);
			_response = new HttpListenerResponse (this);
		}

		#endregion

		#region Internal Properties

		internal HttpConnection Connection {
			get {
				return _connection;
			}
		}

		internal string ErrorMessage {
			get {
				return _error;
			}

			set {
				_error = value;
			}
		}

		internal int ErrorStatus {
			get {
				return _errorStatus;
			}

			set {
				_errorStatus = value;
			}
		}

		internal bool HaveError {
			get {
				return _error != null;
			}
		}

		#endregion

		#region Public Properties

		/// <summary>
		/// Gets the <see cref="HttpListenerRequest"/> that contains the HTTP request from a client.
		/// </summary>
		/// <value>
		/// A <see cref="HttpListenerRequest"/> that contains the HTTP request objects.
		/// </value>
		public HttpListenerRequest Request {
			get {
				return _request;
			}
		}

		/// <summary>
		/// Gets the <see cref="HttpListenerResponse"/> that contains the HTTP response to send to
		/// the client in response to the client's request.
		/// </summary>
		/// <value>
		/// A <see cref="HttpListenerResponse"/> that contains the HTTP response objects.
		/// </value>
		public HttpListenerResponse Response {
			get {
				return _response;
			}
		}

		/// <summary>
		/// Gets the client information (identity, authentication information and security roles).
		/// </summary>
		/// <value>
		/// A <see cref="IPrincipal"/> contains the client information.
		/// </value>
		public IPrincipal User {
			get {
				return _user;
			}
		}

		#endregion

		#region Internal Methods

		internal void ParseAuthentication (AuthenticationSchemes expectedSchemes)
		{
			if (expectedSchemes == AuthenticationSchemes.Anonymous)
				return;

			// TODO: Handle NTLM/Digest modes.
			var header = _request.Headers ["Authorization"];
			if (header == null || header.Length < 2)
				return;

			var authData = header.Split (new char [] {' '}, 2);
			if (authData [0].ToLower () == "basic")
				_user = ParseBasicAuthentication (authData [1]);

			// TODO: Throw if malformed -> 400 bad request.
		}

		internal IPrincipal ParseBasicAuthentication (string authData)
		{
			try {
				// HTTP Basic Authentication data is a formatted Base64 string.
				var authString = Encoding.Default.GetString (Convert.FromBase64String (authData));

				// The format is domain\username:password.
				// Domain is optional.

				var pos = authString.IndexOf (':');
				var user = authString.Substring (0, pos);
				var password = authString.Substring (pos + 1);

				// Check if there is a domain.
				pos = user.IndexOf ('\\');
				if (pos > 0)
					user = user.Substring (pos + 1);

				var identity = new System.Net.HttpListenerBasicIdentity (user, password);
				// TODO: What are the roles MS sets?
				return new GenericPrincipal (identity, new string [0]);
			} catch {
				return null;
			} 
		}

		#endregion

		#region Public Method

		/// <summary>
		/// Accepts a WebSocket connection by the <see cref="HttpListener"/>.
		/// </summary>
		/// <returns>
		/// A <see cref="HttpListenerWebSocketContext"/> that contains a WebSocket connection.
		/// </returns>
		public HttpListenerWebSocketContext AcceptWebSocket ()
		{
			return new HttpListenerWebSocketContext (this);
		}

		#endregion
	}
}
