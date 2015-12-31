/*
 * HttpListenerRequest.cs
 *
 * This code is derived from System.Net.HttpListenerRequest.cs of Mono
 * (http://www.mono-project.com).
 *
 * The MIT License
 *
 * Copyright (c) 2005 Novell, Inc. (http://www.novell.com)
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
 * Authors:
 * - Gonzalo Paniagua Javier <gonzalo@novell.com>
 */

namespace WebSocketSharp.Net
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Text;

    /// <summary>
    /// Provides the access to a request to the <see cref="HttpListener"/>.
    /// </summary>
    /// <remarks>
    /// The HttpListenerRequest class cannot be inherited.
    /// </remarks>
    internal sealed class HttpListenerRequest
    {
        private static readonly byte[] _100continue;

        private bool _chunked;

        private long _contentLength;
        private bool _contentLengthWasSet;
        private readonly HttpListenerContext _context;
        private CookieCollection _cookies;
        private readonly WebHeaderCollection _headers;
        private Stream _inputStream;
        private bool _keepAlive;
        private bool _keepAliveWasSet;
        private string _method;
        private NameValueCollection _queryString;
        private string _uri;
        private Uri _url;
        private Version _version;
        private bool _websocketRequest;
        private bool _websocketRequestWasSet;

        static HttpListenerRequest()
        {
            _100continue = Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");
        }

        internal HttpListenerRequest(HttpListenerContext context)
        {
            _context = context;
            _contentLength = -1;
            _headers = new WebHeaderCollection();
            Guid.NewGuid();
        }

        /// <summary>
        /// Gets the cookies included in the request.
        /// </summary>
        /// <value>
        /// A <see cref="CookieCollection"/> that contains the cookies included in the request.
        /// </value>
        public CookieCollection Cookies => _cookies ?? (_cookies = _headers.GetCookies(false));

        /// <summary>
        /// Gets a value indicating whether the request has the entity body.
        /// </summary>
        /// <value>
        /// <c>true</c> if the request has the entity body; otherwise, <c>false</c>.
        /// </value>
        public bool HasEntityBody => _contentLength > 0 || _chunked;

        /// <summary>
        /// Gets the HTTP headers used in the request.
        /// </summary>
        /// <value>
        /// A <see cref="NameValueCollection"/> that contains the HTTP headers used in the request.
        /// </value>
        public NameValueCollection Headers => _headers;

        /// <summary>
        /// Gets the HTTP method used in the request.
        /// </summary>
        /// <value>
        /// A <see cref="string"/> that represents the HTTP method used in the request.
        /// </value>
        public string HttpMethod => _method;

        /// <summary>
        /// Gets a <see cref="Stream"/> that contains the entity body data included in the request.
        /// </summary>
        /// <value>
        /// A <see cref="Stream"/> that contains the entity body data included in the request.
        /// </value>
        public Stream InputStream => _inputStream ??
                                     (_inputStream = HasEntityBody
                                                         ? _context.Connection.GetRequestStream(_chunked, _contentLength)
                                                         : Stream.Null);

        /// <summary>
        /// Gets a value indicating whether the client that sent the request is authenticated.
        /// </summary>
        /// <value>
        /// <c>true</c> if the client is authenticated; otherwise, <c>false</c>.
        /// </value>
        public bool IsAuthenticated
        {
            get
            {
                var user = _context.User;
                return user != null && user.Identity.IsAuthenticated;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the request is sent from the local computer.
        /// </summary>
        /// <value>
        /// <c>true</c> if the request is sent from the local computer; otherwise, <c>false</c>.
        /// </value>
        public bool IsLocal => RemoteEndPoint.Address.IsLocal();

        /// <summary>
        /// Gets a value indicating whether the HTTP connection is secured using the SSL protocol.
        /// </summary>
        /// <value>
        /// <c>true</c> if the HTTP connection is secured; otherwise, <c>false</c>.
        /// </value>
        public bool IsSecureConnection => _context.Connection.IsSecure;

        /// <summary>
        /// Gets a value indicating whether the request is a WebSocket connection request.
        /// </summary>
        /// <value>
        /// <c>true</c> if the request is a WebSocket connection request; otherwise, <c>false</c>.
        /// </value>
        public bool IsWebSocketRequest
        {
            get
            {
                if (!_websocketRequestWasSet)
                {
                    _websocketRequest = _method == "GET" &&
                                        _version > HttpVersion.Version10 &&
                                        _headers.Contains("Upgrade", "websocket") &&
                                        _headers.Contains("Connection", "Upgrade");

                    _websocketRequestWasSet = true;
                }

                return _websocketRequest;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the client requests a persistent connection.
        /// </summary>
        /// <value>
        /// <c>true</c> if the client requests a persistent connection; otherwise, <c>false</c>.
        /// </value>
        public bool KeepAlive
        {
            get
            {
                if (!_keepAliveWasSet)
                {
                    string keepAlive;
                    _keepAlive = _version > HttpVersion.Version10 ||
                                 _headers.Contains("Connection", "keep-alive") ||
                                 ((keepAlive = _headers["Keep-Alive"]) != null && keepAlive != "closed");

                    _keepAliveWasSet = true;
                }

                return _keepAlive;
            }
        }

        /// <summary>
        /// Gets the server endpoint as an IP address and a port number.
        /// </summary>
        /// <value>
        /// A <see cref="System.Net.IPEndPoint"/> that represents the server endpoint.
        /// </value>
        public IPEndPoint LocalEndPoint => _context.Connection.LocalEndPoint;

        /// <summary>
        /// Gets the HTTP version used in the request.
        /// </summary>
        /// <value>
        /// A <see cref="Version"/> that represents the HTTP version used in the request.
        /// </value>
        public Version ProtocolVersion => _version;

        /// <summary>
        /// Gets the query string included in the request.
        /// </summary>
        /// <value>
        /// A <see cref="NameValueCollection"/> that contains the query string parameters.
        /// </value>
        public NameValueCollection QueryString => _queryString ??
                                                  (_queryString = HttpUtility.InternalParseQueryString(_url.Query, Encoding.UTF8));

        /// <summary>
        /// Gets the client endpoint as an IP address and a port number.
        /// </summary>
        /// <value>
        /// A <see cref="System.Net.IPEndPoint"/> that represents the client endpoint.
        /// </value>
        public IPEndPoint RemoteEndPoint => _context.Connection.RemoteEndPoint;

        /// <summary>
        /// Gets the URL requested by the client.
        /// </summary>
        /// <value>
        /// A <see cref="Uri"/> that represents the URL requested by the client.
        /// </value>
        public Uri Url => _url;

        /// <summary>
        /// Gets the server endpoint as an IP address and a port number.
        /// </summary>
        /// <value>
        /// A <see cref="string"/> that represents the server endpoint.
        /// </value>
        public string UserHostAddress => LocalEndPoint.ToString();

        private static bool tryCreateVersion(string version, out Version result)
        {
            try
            {
                result = new Version(version);
                return true;
            }
            catch
            {
                result = null;
                return false;
            }
        }

        internal void AddHeader(string header)
        {
            var colon = header.IndexOf(':');
            if (colon == -1)
            {
                _context.ErrorMessage = "Invalid header";
                return;
            }

            var name = header.Substring(0, colon).Trim();
            var val = header.Substring(colon + 1).Trim();
            _headers.InternalSet(name, val, false);

            var lower = name.ToLower(CultureInfo.InvariantCulture);
            if (lower == "accept")
            {
                new List<string>(val.SplitHeaderValue(',')).ToArray();
                return;
            }

            if (lower == "accept-language")
            {
                return;
            }

            if (lower == "content-length")
            {
                long len;
                if (Int64.TryParse(val, out len) && len >= 0)
                {
                    _contentLength = len;
                    _contentLengthWasSet = true;
                }
                else {
                    _context.ErrorMessage = "Invalid Content-Length header";
                }

                return;
            }

            if (lower == "content-type")
            {
                try
                {
                    HttpUtility.GetEncoding(val);
                }
                catch
                {
                    _context.ErrorMessage = "Invalid Content-Type header";
                }

                return;
            }

            if (lower == "referer")
                val.ToUri();
        }

        internal void FinishInitialization()
        {
            var host = _headers["Host"];
            var noHost = host == null || host.Length == 0;
            if (_version > HttpVersion.Version10 && noHost)
            {
                _context.ErrorMessage = "Invalid Host header";
                return;
            }

            if (noHost)
                host = UserHostAddress;

            _url = HttpUtility.CreateRequestUrl(_uri, host, IsWebSocketRequest, IsSecureConnection);
            if (_url == null)
            {
                _context.ErrorMessage = "Invalid request url";
                return;
            }

            var enc = Headers["Transfer-Encoding"];
            if (_version > HttpVersion.Version10 && enc != null && enc.Length > 0)
            {
                _chunked = enc.ToLower() == "chunked";
                if (!_chunked)
                {
                    _context.ErrorMessage = String.Empty;
                    _context.ErrorStatus = 501;

                    return;
                }
            }

            if (!_chunked && !_contentLengthWasSet)
            {
                var method = _method.ToLower();
                if (method == "post" || method == "put")
                {
                    _context.ErrorMessage = String.Empty;
                    _context.ErrorStatus = 411;

                    return;
                }
            }

            var expect = Headers["Expect"];
            if (expect != null && expect.Length > 0 && expect.ToLower() == "100-continue")
            {
                var output = _context.Connection.GetResponseStream();
                output.WriteInternally(_100continue, 0, _100continue.Length);
            }
        }

        // Returns true is the stream could be reused.
        internal bool FlushInput()
        {
            if (!HasEntityBody)
                return true;

            var len = 2048;
            if (_contentLength > 0)
                len = (int)Math.Min(_contentLength, len);

            var buff = new byte[len];
            while (true)
            {
                // TODO: Test if MS has a timeout when doing this.
                try
                {
                    var ares = InputStream.BeginRead(buff, 0, len, null, null);
                    if (!ares.IsCompleted && !ares.AsyncWaitHandle.WaitOne(100))
                        return false;

                    if (InputStream.EndRead(ares) <= 0)
                        return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        internal void SetRequestLine(string requestLine)
        {
            var parts = requestLine.Split(new[] { ' ' }, 3);
            if (parts.Length != 3)
            {
                _context.ErrorMessage = "Invalid request line (parts)";
                return;
            }

            _method = parts[0];
            if (!_method.IsToken())
            {
                _context.ErrorMessage = "Invalid request line (method)";
                return;
            }

            _uri = parts[1];

            if (parts[2].Length != 8 ||
                !parts[2].StartsWith("HTTP/") ||
                !tryCreateVersion(parts[2].Substring(5), out _version) ||
                _version.Major < 1)
                _context.ErrorMessage = "Invalid request line (version)";
        }

        /// <summary>
        /// Returns a <see cref="string"/> that represents the current
        /// <see cref="HttpListenerRequest"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="string"/> that represents the current <see cref="HttpListenerRequest"/>.
        /// </returns>
        public override string ToString()
        {
            var output = new StringBuilder(64);
            output.AppendFormat("{0} {1} HTTP/{2}\r\n", _method, _uri, _version);
            output.Append(_headers);

            return output.ToString();
        }
    }
}
