/*
 * TcpListenerWebSocketContext.cs
 *
 * The MIT License
 *
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

/*
 * Contributors:
 * - Liryna <liryna.stark@gmail.com>
 */

namespace WebSocketSharp.Net.WebSockets
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Security.Principal;
    using System.Text;
    using System.Threading.Tasks;

    using AuthenticationSchemes = WebSocketSharp.Net.AuthenticationSchemes;
    using CookieCollection = WebSocketSharp.Net.CookieCollection;
    using HttpStatusCode = WebSocketSharp.Net.HttpStatusCode;
    using NetworkCredential = WebSocketSharp.Net.NetworkCredential;

    /// <summary>
	/// Provides the properties used to access the information in a WebSocket connection request
	/// received by the <see cref="TcpListener"/>.
	/// </summary>
	internal class TcpListenerWebSocketContext : WebSocketContext
    {
        private readonly bool _secure;
        private readonly Stream _stream;
        private readonly TcpClient _tcpClient;
        private readonly Task<Uri> _uri;
        private readonly WebSocket _websocket;
        private CookieCollection _cookies;
        private NameValueCollection _queryString;
        private Task<HttpRequest> _request;
        private IPrincipal _user;
        private NameValueCollection _headers;

        internal TcpListenerWebSocketContext(
            TcpClient tcpClient,
            string protocol,
            bool secure,
            ServerSslConfiguration sslConfiguration)
        {
            _tcpClient = tcpClient;
            _secure = secure;

            var netStream = tcpClient.GetStream();
            if (secure)
            {
                var sslStream = new SslStream(netStream, false, sslConfiguration.ClientCertificateValidationCallback, sslConfiguration.UserCertificateSelectionCallback);
                sslStream.AuthenticateAsServer(
                    sslConfiguration.ServerCertificate,
                    sslConfiguration.ClientCertificateRequired,
                    sslConfiguration.EnabledSslProtocols,
                    sslConfiguration.CheckCertificateRevocation);

                _stream = sslStream;
            }
            else
            {
                _stream = netStream;
            }

            _request = HttpRequest.Read(_stream, 90000);
            _uri = CreateRequestUri(secure);

            _websocket = new WebSocket(this, protocol);
        }

        internal Stream Stream => _stream;

        /// <summary>
        /// Gets the HTTP cookies included in the request.
        /// </summary>
        /// <value>
        /// A <see cref="WebSocketSharp.Net.CookieCollection"/> that contains the cookies.
        /// </value>
        public override async Task<CookieCollection> GetCookieCollection()
        {
            return _cookies ?? (_cookies = (await _request.ConfigureAwait(false)).Cookies);
        }

        /// <summary>
        /// Gets the HTTP headers included in the request.
        /// </summary>
        /// <value>
        /// A <see cref="NameValueCollection"/> that contains the headers.
        /// </value>
        public override async Task<string> GetHeader(string key)
        {
            var headers = await GetHeaders().ConfigureAwait(false);
            return headers?[key];
        }

        /// <summary>
        /// Gets the value of the Host header included in the request.
        /// </summary>
        /// <value>
        /// A <see cref="string"/> that represents the value of the Host header.
        /// </value>
        public override async Task<string> GetHost()
        {
            var req = await _request.ConfigureAwait(false);
            return req.Headers["Host"];
        }

        /// <summary>
		/// Gets a value indicating whether the client is authenticated.
		/// </summary>
		/// <value>
		/// <c>true</c> if the client is authenticated; otherwise, <c>false</c>.
		/// </value>
		public override bool IsAuthenticated => _user != null && _user.Identity.IsAuthenticated;

        /// <summary>
		/// Gets a value indicating whether the client connected from the local computer.
		/// </summary>
		/// <value>
		/// <c>true</c> if the client connected from the local computer; otherwise, <c>false</c>.
		/// </value>
		public override bool IsLocal => UserEndPoint.Address.IsLocal();

        /// <summary>
		/// Gets a value indicating whether the WebSocket connection is secured.
		/// </summary>
		/// <value>
		/// <c>true</c> if the connection is secured; otherwise, <c>false</c>.
		/// </value>
		public override bool IsSecureConnection => _secure;

        /// <summary>
        /// Gets a value indicating whether the request is a WebSocket connection request.
        /// </summary>
        /// <value>
        /// <c>true</c> if the request is a WebSocket connection request; otherwise, <c>false</c>.
        /// </value>
        public override async Task<bool> IsWebSocketRequest()
        {
            var req = await _request.ConfigureAwait(false);
            return req.IsWebSocketRequest;
        }

        /// <summary>
        /// Gets the value of the Origin header included in the request.
        /// </summary>
        /// <value>
        /// A <see cref="string"/> that represents the value of the Origin header.
        /// </value>
        public override async Task<string> GetOrigin()
        {
            var req = await _request.ConfigureAwait(false);
            return req.Headers["Origin"];
        }

        /// <summary>
        /// Gets the query string included in the request.
        /// </summary>
        /// <value>
        /// A <see cref="NameValueCollection"/> that contains the query string parameters.
        /// </value>
        public override async Task<NameValueCollection> GetQueryString()
        {
            return _queryString ?? (_queryString = HttpUtility.InternalParseQueryString((await _uri.ConfigureAwait(false))?.Query, Encoding.UTF8));
        }

        /// <summary>
        /// Gets the URI requested by the client.
        /// </summary>
        /// <value>
        /// A <see cref="Uri"/> that represents the requested URI.
        /// </value>
        public override Task<Uri> GetRequestUri()
        {
            return _uri;
        }

        /// <summary>
        /// Gets the value of the Sec-WebSocket-Key header included in the request.
        /// </summary>
        /// <remarks>
        /// This property provides a part of the information used by the server to prove that it
        /// received a valid WebSocket connection request.
        /// </remarks>
        /// <value>
        /// A <see cref="string"/> that represents the value of the Sec-WebSocket-Key header.
        /// </value>
        public override Task<string> GetSecWebSocketKey()
        {
            return GetHeader("Sec-WebSocket-Key");
        }

        /// <summary>
		/// Gets the values of the Sec-WebSocket-Protocol header included in the request.
		/// </summary>
		/// <remarks>
		/// This property represents the subprotocols requested by the client.
		/// </remarks>
		/// <value>
		/// An <see cref="T:System.Collections.Generic.IEnumerable{string}"/> instance that provides
		/// an enumerator which supports the iteration over the values of the Sec-WebSocket-Protocol
		/// header.
		/// </value>
		public override async Task<IEnumerable<string>> GetSecWebSocketProtocols()
        {
            var protocols = await GetHeader("Sec-WebSocket-Protocol").ConfigureAwait(false);
            if (protocols != null)
            {
                return protocols.Split(',').Select(x => x.Trim());
            }
            return Enumerable.Empty<string>();
        }

        /// <summary>
        /// Gets the value of the Sec-WebSocket-Version header included in the request.
        /// </summary>
        /// <remarks>
        /// This property represents the WebSocket protocol version.
        /// </remarks>
        /// <value>
        /// A <see cref="string"/> that represents the value of the Sec-WebSocket-Version header.
        /// </value>
        public override Task<string> GetSecWebSocketVersion()
        {
            return GetHeader("Sec-WebSocket-Version");
        }

        /// <summary>
        /// Gets the server endpoint as an IP address and a port number.
        /// </summary>
        /// <value>
        /// A <see cref="System.Net.IPEndPoint"/> that represents the server endpoint.
        /// </value>
        public override IPEndPoint ServerEndPoint => (IPEndPoint)_tcpClient.Client.LocalEndPoint;

        /// <summary>
		/// Gets the client information (identity, authentication, and security roles).
		/// </summary>
		/// <value>
		/// A <see cref="IPrincipal"/> that represents the client information.
		/// </value>
		public override IPrincipal User => _user;

        /// <summary>
		/// Gets the client endpoint as an IP address and a port number.
		/// </summary>
		/// <value>
		/// A <see cref="System.Net.IPEndPoint"/> that represents the client endpoint.
		/// </value>
		public override IPEndPoint UserEndPoint => (IPEndPoint)_tcpClient.Client.RemoteEndPoint;

        /// <summary>
		/// Gets the <see cref="WebSocketSharp.WebSocket"/> instance used for two-way communication
		/// between client and server.
		/// </summary>
		/// <value>
		/// A <see cref="WebSocketSharp.WebSocket"/>.
		/// </value>
		public override WebSocket WebSocket => _websocket;

        internal void Close()
        {
            _stream.Close();
            _tcpClient.Close();
        }

        internal Task Close(HttpStatusCode code)
        {
            return _websocket.InnerClose(HttpResponse.CreateCloseResponse(code));
        }

        internal void SendAuthenticationChallenge(string challenge)
        {
            var buff = HttpResponse.CreateUnauthorizedResponse(challenge).ToByteArray();
            _stream.Write(buff, 0, buff.Length);
            _request = HttpRequest.Read(_stream, 15000);
        }

        internal async Task SetUser(
            AuthenticationSchemes scheme,
            string realm,
            Func<IIdentity, NetworkCredential> credentialsFinder)
        {
            var req = await _request.ConfigureAwait(false);
            var authRes = req.AuthenticationResponse;

            var id = authRes?.ToIdentity();
            if (id == null)
            {
                return;
            }

            NetworkCredential cred = null;
            try
            {
                cred = credentialsFinder(id);
            }
            catch
            {
            }

            if (cred == null)
            {
                return;
            }

            var valid = scheme == AuthenticationSchemes.Basic
                            ? ((HttpBasicIdentity)id).Password == cred.Password
                            : scheme == AuthenticationSchemes.Digest && ((HttpDigestIdentity)id).IsValid(cred.Password, realm, req.HttpMethod, null);

            if (valid) _user = new GenericPrincipal(id, cred.Roles);
        }

        /// <summary>
        /// Returns a <see cref="string"/> that represents the current
        /// <see cref="TcpListenerWebSocketContext"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="string"/> that represents the current
        /// <see cref="TcpListenerWebSocketContext"/>.
        /// </returns>
        public override string ToString()
        {
            return _request.ToString();
        }

        private async Task<NameValueCollection> GetHeaders()
        {
            return _headers ?? (_headers = (await _request.ConfigureAwait(false)).Headers);
        }

        private async Task<Uri> CreateRequestUri(bool secure)
        {
            var req = await _request.ConfigureAwait(false);
            return HttpUtility.CreateRequestUrl(
                    req.RequestUri,
                    req.Headers["Host"],
                    req.IsWebSocketRequest,
                    secure);
        }
    }
}