/*
 * WebSocket.cs
 *
 * A C# implementation of the WebSocket interface.
 *
 * This code is derived from WebSocket.java
 * (http://github.com/adamac/Java-WebSocket-client).
 *
 * The MIT License
 *
 * Copyright (c) 2009 Adam MacBeth
 * Copyright (c) 2010-2014 sta.blockhead
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
 * - Frank Razenberg <frank@zzattack.org>
 * - David Wood <dpwood@gmail.com>
 * - Liryna <liryna.stark@gmail.com>
 */

namespace WebSocketSharp
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using WebSocketSharp.Net;
    using WebSocketSharp.Net.WebSockets;

    /// <summary>
    /// Implements the WebSocket interface.
    /// </summary>
    /// <remarks>
    /// The WebSocket class provides a set of methods and properties for two-way communication using
    /// the WebSocket protocol (<see href="http://tools.ietf.org/html/rfc6455">RFC 6455</see>).
    /// </remarks>
    public class WebSocket : IDisposable
    {
        private const string GuidId = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        private const string SocketVersion = "13";

        private readonly int _fragmentLength; // Max value is int.MaxValue - 14.
        private readonly Uri _uri;
        private readonly bool _secure;
        private readonly bool _client;
        private readonly string[] _protocols;
        private readonly ClientSslConfiguration _sslConfig;
        private readonly CancellationToken _cancellationToken;
        private AuthenticationChallenge _authChallenge;
        private string _base64Key;
        private Action _closeContext;
        private CompressionMethod _compression = CompressionMethod.Deflate;
        private WebSocketContext _context;
        private CookieCollection _cookies;
        private NetworkCredential _credentials;
        private string _extensions;
        private AutoResetEvent _exitReceiving;
        private object _forConn;
        private object _forSend;
        private Func<WebSocketContext, string> _handshakeRequestChecker;
        private uint _nonceCount;
        private string _origin;
        private bool _preAuth;
        private string _protocol;
        private NetworkCredential _proxyCredentials;
        private Uri _proxyUri;
        private volatile WebSocketState _readyState;
        private AutoResetEvent _receivePong = new AutoResetEvent(false);
        private Stream _stream;
        private TcpClient _tcpClient;
        private TimeSpan _waitTime;
        private CancellationTokenRegistration _registration;
        private Task _receiveTask;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebSocket"/> class with
        /// the specified WebSocket URL and subprotocols.
        /// </summary>
        /// <param name="url">A <see cref="string"/> that represents the WebSocket URL to connect.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to cancel websocket operations.</param>
        /// <param name="fragmentSize">Set the size of message packages. Smaller size equals less memory overhead when sending streams.</param>
        /// <param name="protocols">
        ///     An array of <see cref="string"/> that contains the WebSocket subprotocols if any.
        ///     Each value of <paramref name="protocols"/> must be a token defined in
        ///     <see href="http://tools.ietf.org/html/rfc2616#section-2.2">RFC 2616</see>.
        /// </param>
        /// <exception cref="ArgumentException">
        ///   <para>
        ///   <paramref name="url"/> is invalid.
        ///   </para>
        ///   <para>
        ///   -or-
        ///   </para>
        ///   <para>
        ///   <paramref name="protocols"/> is invalid.
        ///   </para>
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="url"/> is <see langword="null"/>.
        /// </exception>
        public WebSocket(string url, CancellationToken cancellationToken = default(CancellationToken), int fragmentSize = 102392, params string[] protocols)
            : this(url, null, cancellationToken, fragmentSize, protocols)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WebSocket"/> class with
        /// the specified WebSocket URL and subprotocols.
        /// </summary>
        /// <param name="url">
        /// A <see cref="string"/> that represents the WebSocket URL to connect.
        /// </param>
        /// <param name="sslAuthConfiguration">A <see cref="ClientSslAuthConfiguration"/> for securing the connection.</param>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to cancel websocket operations.</param>
        /// <param name="fragmentSize"></param>
        /// <param name="protocols">
        /// An array of <see cref="string"/> that contains the WebSocket subprotocols if any.
        /// Each value of <paramref name="protocols"/> must be a token defined in
        /// <see href="http://tools.ietf.org/html/rfc2616#section-2.2">RFC 2616</see>.
        /// </param>
        /// <exception cref="ArgumentException">
        ///   <para>
        ///   <paramref name="url"/> is invalid.
        ///   </para>
        ///   <para>
        ///   -or-
        ///   </para>
        ///   <para>
        ///   <paramref name="protocols"/> is invalid.
        ///   </para>
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="url"/> is <see langword="null"/>.
        /// </exception>
        public WebSocket(string url, ClientSslConfiguration sslAuthConfiguration, CancellationToken cancellationToken = default(CancellationToken), int fragmentSize = 102392, params string[] protocols)
        {
            if (url == null)
            {
                throw new ArgumentNullException(nameof(url));
            }

            string msg;
            if (!url.TryCreateWebSocketUri(out _uri, out msg))
            {
                throw new ArgumentException(msg, nameof(url));
            }

            if (protocols != null && protocols.Length > 0)
            {
                msg = protocols.CheckIfValidProtocols();
                if (msg != null)
                {
                    throw new ArgumentException(msg, nameof(protocols));
                }

                _protocols = protocols;
            }

            _fragmentLength = fragmentSize;
            _sslConfig = sslAuthConfiguration;
            _cancellationToken = cancellationToken;
            _registration = _cancellationToken.Register(async () => await Close().ConfigureAwait(false));
            _base64Key = CreateBase64Key();
            _client = true;
            _secure = _uri.Scheme == "wss";
            _waitTime = TimeSpan.FromSeconds(5);

            InnerInit();
        }

        // As server
        internal WebSocket(TcpListenerWebSocketContext context, string protocol, int fragmentSize = 102392)
        {
            _fragmentLength = fragmentSize;
            _context = context;
            _protocol = protocol;

            _closeContext = context.Close;
            _secure = context.IsSecureConnection;
            _stream = context.Stream;
            _waitTime = TimeSpan.FromSeconds(1);

            InnerInit();
        }

        /// <summary>
        /// Occurs when the WebSocket connection has been closed.
        /// </summary>
        public Func<CloseEventArgs, Task> OnClose { get; set; }

        /// <summary>
        /// Occurs when the <see cref="WebSocket"/> gets an error.
        /// </summary>
        public Func<ErrorEventArgs, Task> OnError { get; set; }

        /// <summary>
        /// Occurs when the <see cref="WebSocket"/> receives a message.
        /// </summary>
        public Func<MessageEventArgs, Task> OnMessage { get; set; }

        /// <summary>
        /// Occurs when the WebSocket connection has been established.
        /// </summary>
        public Func<Task> OnOpen { get; set; }

        /// <summary>
        /// Gets the HTTP cookies included in the WebSocket connection request and response.
        /// </summary>
        /// <value>
        /// An <see cref="T:System.Collections.Generic.IEnumerable{WebSocketSharp.Net.Cookie}"/>
        /// instance that provides an enumerator which supports the iteration over the collection of
        /// the cookies.
        /// </value>
        public IEnumerable<Cookie> Cookies
        {
            get
            {
                lock (_cookies.SyncRoot)
                    foreach (Cookie cookie in _cookies)
                        yield return cookie;
            }
        }

        ///// <summary>
        ///// Gets the credentials for the HTTP authentication (Basic/Digest).
        ///// </summary>
        ///// <value>
        ///// A <see cref="NetworkCredential"/> that represents the credentials for the authentication.
        ///// The default value is <see langword="null"/>.
        ///// </value>
        //public NetworkCredential Credentials
        //{
        //	get
        //	{
        //		return _credentials;
        //	}
        //}

        /// <summary>
        /// Gets the WebSocket extensions selected by the server.
        /// </summary>
        /// <value>
        /// A <see cref="string"/> that represents the extensions if any.
        /// The default value is <see cref="String.Empty"/>.
        /// </value>
        public string Extensions => _extensions ?? String.Empty;

        /// <summary>
        /// Gets a value indicating whether the WebSocket connection is alive.
        /// </summary>
        /// <value>
        /// <c>true</c> if the connection is alive; otherwise, <c>false</c>.
        /// </value>
        public Task<bool> IsAlive()
        {
            return Ping();
        }

        /// <summary>
        /// Gets a value indicating whether the WebSocket connection is secure.
        /// </summary>
        /// <value>
        /// <c>true</c> if the connection is secure; otherwise, <c>false</c>.
        /// </value>
        public bool IsSecure => _secure;

        /// <summary>
        /// Gets or sets the value of the HTTP Origin header to send with the WebSocket connection
        /// request to the server.
        /// </summary>
        /// <remarks>
        /// The <see cref="WebSocket"/> sends the Origin header if this property has any.
        /// </remarks>
        /// <value>
        ///   <para>
        ///   A <see cref="string"/> that represents the value of
        ///   the <see href="http://tools.ietf.org/html/rfc6454#section-7">Origin</see> header to send.
        ///   The default value is <see langword="null"/>.
        ///   </para>
        ///   <para>
        ///   The Origin header has the following syntax:
        ///   <c>&lt;scheme&gt;://&lt;host&gt;[:&lt;port&gt;]</c>
        ///   </para>
        /// </value>
        public string Origin
        {
            get
            {
                return _origin;
            }

            set
            {
                lock (_forConn)
                {
                    var msg = CheckIfAvailable(false, false);
                    if (msg == null)
                    {
                        if (value.IsNullOrEmpty())
                        {
                            _origin = value;
                            return;
                        }

                        Uri origin;
                        if (!Uri.TryCreate(value, UriKind.Absolute, out origin) || origin.Segments.Length > 1)
                            msg = "The syntax of the origin must be '<scheme>://<host>[:<port>]'.";
                    }

                    if (msg != null)
                    {
                        Error("An error has occurred in setting the origin.", null);

                        return;
                    }

                    _origin = value.TrimEnd('/');
                }
            }
        }

        /// <summary>
        /// Gets the WebSocket subprotocol selected by the server.
        /// </summary>
        /// <value>
        /// A <see cref="string"/> that represents the subprotocol if any.
        /// The default value is <see cref="String.Empty"/>.
        /// </value>
        public string Protocol
        {
            get
            {
                return _protocol ?? String.Empty;
            }

            internal set
            {
                _protocol = value;
            }
        }

        /// <summary>
        /// Gets the state of the WebSocket connection.
        /// </summary>
        /// <value>
        /// One of the <see cref="WebSocketState"/> enum values, indicates the state of the WebSocket
        /// connection. The default value is <see cref="WebSocketState.Connecting"/>.
        /// </value>
        public WebSocketState ReadyState => _readyState;

        ///// <summary>
        ///// Gets or sets the SSL configuration used to authenticate the server and optionally the client
        ///// on the secure connection.
        ///// </summary>
        ///// <value>
        ///// A <see cref="ClientSslAuthConfiguration"/> that represents the SSL configuration used to
        ///// authenticate the server and optionally the client.
        ///// </value>
        //public ClientSslAuthConfiguration SslConfiguration
        //{
        //	get
        //	{
        //		return _sslConfig;
        //	}

        //	set
        //	{
        //		lock (_forConn)
        //		{
        //			var msg = checkIfAvailable(false, false);
        //			if (msg != null)
        //			{
        //				error("An error has occurred in setting the ssl configuration.", null);

        //				return;
        //			}

        //			_sslConfig = value;
        //		}
        //	}
        //}

        /// <summary>
        /// Gets the WebSocket URL to connect.
        /// </summary>
        /// <value>
        /// A <see cref="Uri"/> that represents the WebSocket URL to connect.
        /// </value>
        public Uri Url => _client
                              ? _uri
                              : _context.RequestUri;

        /// <summary>
        /// Gets or sets the wait time for the response to the Ping or Close.
        /// </summary>
        /// <value>
        /// A <see cref="TimeSpan"/> that represents the wait time. The default value is
        /// the same as 5 seconds, or 1 second if the <see cref="WebSocket"/> is used by
        /// a server.
        /// </value>
        public TimeSpan WaitTime
        {
            get
            {
                return _waitTime;
            }

            set
            {
                lock (_forConn)
                {
                    var msg = CheckIfAvailable(true, false) ?? value.CheckIfValidWaitTime();
                    if (msg != null)
                    {
                        Error("An error has occurred in setting the wait time.", null);

                        return;
                    }

                    _waitTime = value;
                }
            }
        }

        internal CookieCollection CookieCollection => _cookies;

        // As server
        internal Func<WebSocketContext, string> CustomHandshakeRequestChecker
        {
            get
            {
                return _handshakeRequestChecker ?? (context => null);
            }

            set
            {
                _handshakeRequestChecker = value;
            }
        }

        internal bool IsConnected => _readyState == WebSocketState.Open || _readyState == WebSocketState.Closing;

        /// <summary>
        /// Closes the WebSocket connection, and releases all associated resources.
        /// </summary>
        public Task Close()
        {
            var msg = _readyState.CheckIfClosable();
            if (msg != null)
            {
                Error("An error has occurred in closing the connection.", null);

                return Task.FromResult(false);
            }

            var send = _readyState == WebSocketState.Open;
            return InnerClose(new CloseEventArgs(), send, send);
        }

        /// <summary>
        /// Closes the WebSocket connection with the specified <see cref="CloseStatusCode"/>,
        /// and releases all associated resources.
        /// </summary>
        /// <param name="code">
        /// One of the <see cref="CloseStatusCode"/> enum values, represents the status code
        /// indicating the reason for the close.
        /// </param>
        public Task Close(CloseStatusCode code)
        {
            var msg = _readyState.CheckIfClosable();
            if (msg != null)
            {
                Error("An error has occurred in closing the connection.", null);

                return Task.FromResult(false);
            }

            var send = _readyState == WebSocketState.Open && !code.IsReserved();
            return InnerClose(new CloseEventArgs(code), send, send);
        }

        /// <summary>
        /// Closes the WebSocket connection with the specified <see cref="CloseStatusCode"/>
        /// and <see cref="string"/>, and releases all associated resources.
        /// </summary>
        /// <remarks>
        /// This method emits a <see cref="OnError"/> event if the size of <paramref name="reason"/>
        /// is greater than 123 bytes.
        /// </remarks>
        /// <param name="code">
        /// One of the <see cref="CloseStatusCode"/> enum values, represents the status code
        /// indicating the reason for the close.
        /// </param>
        /// <param name="reason">
        /// A <see cref="string"/> that represents the reason for the close.
        /// </param>
        public Task Close(CloseStatusCode code, string reason)
        {
            CloseEventArgs e = null;
            var msg = _readyState.CheckIfClosable() ??
                      (e = new CloseEventArgs(code, reason)).RawData.CheckIfValidControlData("reason");

            if (msg != null)
            {
                Error("An error has occurred in closing the connection.", null);

                return Task.FromResult(false);
            }

            var send = _readyState == WebSocketState.Open && !code.IsReserved();
            return InnerClose(e, send, send);
        }

        /// <summary>
        /// Establishes a WebSocket connection.
        /// </summary>
        public async Task<bool> Connect()
        {
            var msg = CheckIfCanConnect();
            if (msg != null)
            {
                Error("An error has occurred in connecting.", null);

                return false;
            }

            if (await InnerConnect().ConfigureAwait(false))
            {
                await InnerOpen().ConfigureAwait(false);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Sends a Ping using the WebSocket connection.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the <see cref="WebSocket"/> receives a Pong to this Ping in a time;
        /// otherwise, <c>false</c>.
        /// </returns>
        public async Task<bool> Ping()
        {
            var bytes = _client
                        ? await WebSocketFrame.CreatePingFrame(true).ToByteArray().ConfigureAwait(false)
                        : WebSocketFrame.EmptyUnmaskPingBytes;

            return await InnerPing(bytes, _waitTime).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a Ping with the specified <paramref name="message"/> using the WebSocket connection.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the <see cref="WebSocket"/> receives a Pong to this Ping in a time;
        /// otherwise, <c>false</c>.
        /// </returns>
        /// <param name="message">
        /// A <see cref="string"/> that represents the message to send.
        /// </param>
        public async Task<bool> Ping(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return await Ping().ConfigureAwait(false);
            }

            var data = Encoding.UTF8.GetBytes(message);
            var msg = data.CheckIfValidControlData("message");
            if (msg != null)
            {
                Error("An error has occurred in sending the ping.", null);

                return false;
            }

            var frameAsBytes = await WebSocketFrame.CreatePingFrame(data, _client).ToByteArray().ConfigureAwait(false);
            return await InnerPing(frameAsBytes, _waitTime).ConfigureAwait(false);
        }

        /// <summary>
        /// Sends a binary <paramref name="data"/> using the WebSocket connection.
        /// </summary>
        /// <param name="data">
        /// An array of <see cref="byte"/> that represents the binary data to send.
        /// </param>
        public Task<bool> Send(byte[] data)
        {
            var msg = _readyState.CheckIfOpen() ?? data.CheckIfValidSendData();
            if (msg != null)
            {
                Error("An error has occurred in sending the data.", null);

                return Task.FromResult(false);
            }

            return InnerSend(Opcode.Binary, new MemoryStream(data));
        }

        /// <summary>
        /// Sends the specified <paramref name="stream"/> as a binary data
        /// using the WebSocket connection.
        /// </summary>
        /// <param name="stream">
        /// A <see cref="FileInfo"/> that represents the file to send.
        /// </param>
        public Task<bool> Send(Stream stream)
        {
            if (stream == null)
            {
                return Task.FromResult(false);
            }

            var msg = _readyState.CheckIfOpen();
            if (msg != null)
            {
                Error("An error has occurred in sending the data.", null);

                return Task.FromResult(false);
            }

            return InnerSend(Opcode.Binary, stream);
        }

        /// <summary>
        /// Sends the specified <paramref name="stream"/> as a binary data
        /// using the WebSocket connection.
        /// </summary>
        /// <param name="stream">
        /// A <see cref="FileInfo"/> that represents the file to send.
        /// </param>
        public Task<bool> Send(Stream stream, long length)
        {
            if (stream == null)
            {
                return Task.FromResult(false);
            }

            var msg = _readyState.CheckIfOpen();
            if (msg != null)
            {
                Error("An error has occurred in sending the data.", null);

                return Task.FromResult(false);
            }

            return InnerSend(Opcode.Binary, stream, length);
        }

        /// <summary>
        /// Sends a text <paramref name="data"/> using the WebSocket connection.
        /// </summary>
        /// <param name="data">
        /// A <see cref="string"/> that represents the text data to send.
        /// </param>
        public Task<bool> Send(string data)
        {
            var msg = _readyState.CheckIfOpen() ?? data.CheckIfValidSendData();
            if (msg != null)
            {
                Error("An error has occurred in sending the data.", null);

                return Task.FromResult(false);
            }

            return InnerSend(Opcode.Text, new MemoryStream(Encoding.UTF8.GetBytes(data)));
        }

        /// <summary>
        /// Sets an HTTP <paramref name="cookie"/> to send with the WebSocket connection request
        /// to the server.
        /// </summary>
        /// <param name="cookie">
        /// A <see cref="Cookie"/> that represents the cookie to send.
        /// </param>
        public void SetCookie(Cookie cookie)
        {
            lock (_forConn)
            {
                var msg = CheckIfAvailable(false, false) ??
                          (cookie == null ? "'cookie' is null." : null);

                if (msg != null)
                {
                    Error("An error has occurred in setting the cookie.", null);

                    return;
                }

                lock (_cookies.SyncRoot)
                    _cookies.SetOrRemove(cookie);
            }
        }

        /// <summary>
        /// Sets a pair of <paramref name="username"/> and <paramref name="password"/> for
        /// the HTTP authentication (Basic/Digest).
        /// </summary>
        /// <param name="username">
        /// A <see cref="string"/> that represents the user name used to authenticate.
        /// </param>
        /// <param name="password">
        /// A <see cref="string"/> that represents the password for <paramref name="username"/>
        /// used to authenticate.
        /// </param>
        /// <param name="preAuth">
        /// <c>true</c> if the <see cref="WebSocket"/> sends the Basic authentication credentials
        /// with the first connection request to the server; otherwise, <c>false</c>.
        /// </param>
        public void SetCredentials(string username, string password, bool preAuth)
        {
            lock (_forConn)
            {
                var msg = CheckIfAvailable(false, false);
                if (msg == null)
                {
                    if (username.IsNullOrEmpty())
                    {
                        _credentials = null;
                        _preAuth = false;

                        return;
                    }

                    msg = username.Contains(':') || !username.IsText()
                          ? "'username' contains an invalid character."
                          : !password.IsNullOrEmpty() && !password.IsText()
                            ? "'password' contains an invalid character."
                            : null;
                }

                if (msg != null)
                {
                    Error(msg, null);

                    return;
                }

                _credentials = new NetworkCredential(username, password, _uri.PathAndQuery);
                _preAuth = preAuth;
            }
        }

        /// <summary>
        /// Sets an HTTP Proxy server URL to connect through, and if necessary, a pair of
        /// <paramref name="username"/> and <paramref name="password"/> for the proxy server
        /// authentication (Basic/Digest).
        /// </summary>
        /// <param name="url">
        /// A <see cref="string"/> that represents the proxy server URL to connect through.
        /// </param>
        /// <param name="username">
        /// A <see cref="string"/> that represents the user name used to authenticate.
        /// </param>
        /// <param name="password">
        /// A <see cref="string"/> that represents the password for <paramref name="username"/>
        /// used to authenticate.
        /// </param>
        public void SetProxy(string url, string username, string password)
        {
            lock (_forConn)
            {
                var msg = CheckIfAvailable(false, false);
                if (msg == null)
                {
                    if (url.IsNullOrEmpty())
                    {
                        _proxyUri = null;
                        _proxyCredentials = null;

                        return;
                    }

                    Uri uri;
                    if (!Uri.TryCreate(url, UriKind.Absolute, out uri) ||
                        uri.Scheme != "http" ||
                        uri.Segments.Length > 1)
                    {
                        msg = "The syntax of the proxy url must be 'http://<host>[:<port>]'.";
                    }
                    else
                    {
                        _proxyUri = uri;

                        if (username.IsNullOrEmpty())
                        {
                            _proxyCredentials = null;

                            return;
                        }

                        msg = username.Contains(':') || !username.IsText()
                              ? "'username' contains an invalid character."
                              : !password.IsNullOrEmpty() && !password.IsText()
                                ? "'password' contains an invalid character."
                                : null;
                    }
                }

                if (msg != null)
                {
                    Error("An error has occurred in setting the proxy.", null);

                    return;
                }

                _proxyCredentials = new NetworkCredential(
                  username, password, String.Format("{0}:{1}", _uri.DnsSafeHost, _uri.Port));
            }
        }

        /// <summary>
        /// Closes the WebSocket connection, and releases all associated resources.
        /// </summary>
        /// <remarks>
        /// This method closes the connection with <see cref="CloseStatusCode.Away"/>.
        /// </remarks>
        public void Dispose()
        {
            _registration.Dispose();
            var send = _readyState == WebSocketState.Open;
            InnerClose(new CloseEventArgs(CloseStatusCode.Away), send, send).Wait(TimeSpan.FromSeconds(2));
        }

        // As server
        internal async Task InnerClose(HttpResponse response)
        {
            _readyState = WebSocketState.Closing;

            await SendHttpResponse(response).ConfigureAwait(false);
            await ReleaseServerResources().ConfigureAwait(false);

            _readyState = WebSocketState.Closed;
        }

        // As server
        internal Task InnerClose(HttpStatusCode code)
        {
            return InnerClose(CreateHandshakeCloseResponse(code));
        }

        internal async Task<bool> InnerPing(byte[] frameAsBytes, TimeSpan timeout)
        {
            try
            {
                AutoResetEvent pong;
                return _readyState == WebSocketState.Open
                    && await InnerSend(frameAsBytes).ConfigureAwait(false)
                    && (pong = _receivePong) != null
                    && pong.WaitOne(timeout);
            }
            catch
            {
                return false;
            }
        }

        // As server, used to broadcast
        internal Task<bool> InnerSend(Opcode opcode, byte[] data)
        {
            return InnerSend(opcode, new MemoryStream(data), _compression == CompressionMethod.Deflate);
        }

        internal async Task<bool> InnerSend(Fin final, Opcode opcode, byte[] data)
        {
            using (var ml = new MonitorLock(_forSend))
            {
                var frame = new WebSocketFrame(final, opcode, data, _compression != CompressionMethod.None, false);

                var bytes = await frame.ToByteArray().ConfigureAwait(false);
                return await SendBytes(bytes).ConfigureAwait(false);
            }
        }

        // As server
        internal async Task InnerClose(CloseEventArgs e, byte[] frameAsBytes, TimeSpan timeout)
        {
            lock (_forConn)
            {
                if (_readyState == WebSocketState.Closing || _readyState == WebSocketState.Closed)
                {
                    return;
                }

                _readyState = WebSocketState.Closing;
            }

            e.WasClean = await CloseHandshake(frameAsBytes, timeout, ReleaseServerResources).ConfigureAwait(false);

            _readyState = WebSocketState.Closed;

            OnClose?.Invoke(e);
        }

        // As server
        internal async Task ConnectAsServer()
        {
            try
            {
                if (await AcceptHandshake().ConfigureAwait(false))
                {
                    _readyState = WebSocketState.Open;
                    await InnerOpen().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                await ProcessException(ex, "An exception has occurred while connecting.").ConfigureAwait(false);
            }
        }

        // As client
        private static string CreateBase64Key()
        {
            var src = new byte[16];
            var rand = new Random();
            rand.NextBytes(src);

            return Convert.ToBase64String(src);
        }

        private static string CreateResponseKey(string base64Key)
        {
            var buff = new StringBuilder(base64Key, 64);
            buff.Append(GuidId);
            SHA1 sha1 = new SHA1CryptoServiceProvider();
            var src = sha1.ComputeHash(Encoding.UTF8.GetBytes(buff.ToString()));

            return Convert.ToBase64String(src);
        }

        // As server
        private async Task<bool> AcceptHandshake()
        {
            var msg = CheckIfValidHandshakeRequest(_context);
            if (msg != null)
            {
                Error(msg, null);
                await InnerClose(HttpStatusCode.BadRequest).ConfigureAwait(false);

                return false;
            }

            if (_protocol != null && !_context.SecWebSocketProtocols.Contains(protocol => protocol == _protocol))
            {
                _protocol = null;
            }

            var extensions = _context.Headers["Sec-WebSocket-Extensions"];
            if (!string.IsNullOrEmpty(extensions))
            {
                ProcessSecWebSocketExtensionsHeader(extensions);
            }

            var innerCreateHandshakeResponse = InnerCreateHandshakeResponse();
            return await SendHttpResponse(innerCreateHandshakeResponse).ConfigureAwait(false);
        }

        // As server
        private Task InnerClose(CloseStatusCode code, string reason, bool wait)
        {
            return InnerClose(new PayloadData(((ushort)code).Append(reason)), !code.IsReserved(), wait);
        }

        private async Task InnerClose(PayloadData payload, bool send, bool wait)
        {
            lock (_forConn)
            {
                if (_readyState == WebSocketState.Closing || _readyState == WebSocketState.Closed)
                {
                    return;
                }

                _readyState = WebSocketState.Closing;
            }

            var e = new CloseEventArgs(payload);
            e.WasClean = await CloseHandshake(
                send ? await WebSocketFrame.CreateCloseFrame(e.PayloadData, _client).ToByteArray().ConfigureAwait(false) : null,
                wait ? WaitTime : TimeSpan.Zero,
                _client ? (Func<Task>)ReleaseClientResources : ReleaseServerResources)
                .ConfigureAwait(false);

            _readyState = WebSocketState.Closed;
            try
            {
                OnClose?.Invoke(e);
            }
            catch (Exception ex)
            {
                Error("An exception has occurred while OnClose.", ex);
            }
        }

        private string CheckIfAvailable(bool asServer, bool asConnected)
        {
            return !_client && !asServer
                   ? "This operation isn't available as a server."
                   : !asConnected
                     ? _readyState.CheckIfConnectable()
                     : null;
        }

        private string CheckIfCanConnect()
        {
            return !_client && _readyState == WebSocketState.Closed
                   ? "Connect isn't available to reconnect as a server."
                   : _readyState.CheckIfConnectable();
        }

        // As server
        private string CheckIfValidHandshakeRequest(WebSocketContext context)
        {
            var headers = context.Headers;
            return context.RequestUri == null
                   ? "An invalid request url."
                   : !context.IsWebSocketRequest
                     ? "Not a WebSocket connection request."
                     : !ValidateSecWebSocketKeyHeader(headers["Sec-WebSocket-Key"])
                       ? "Invalid Sec-WebSocket-Key header."
                       : !InnerValidateSecWebSocketVersionClientHeader(headers["Sec-WebSocket-Version"])
                         ? "Invalid Sec-WebSocket-Version header."
                         : CustomHandshakeRequestChecker(context);
        }

        // As client
        private string InnerCheckIfValidHandshakeResponse(HttpResponse response)
        {
            var headers = response.Headers;
            return response.IsUnauthorized
                   ? "An HTTP authentication is required."
                   : !response.IsWebSocketResponse
                     ? "Not a WebSocket connection response."
                     : !ValidateSecWebSocketAcceptHeader(headers["Sec-WebSocket-Accept"])
                       ? "Invalid Sec-WebSocket-Accept header."
                       : !ValidateSecWebSocketProtocolHeader(headers["Sec-WebSocket-Protocol"])
                         ? "Invalid Sec-WebSocket-Protocol header."
                         : !ValidateSecWebSocketExtensionsHeader(headers["Sec-WebSocket-Extensions"])
                           ? "Invalid Sec-WebSocket-Extensions header."
                           : !InnerValidateSecWebSocketVersionServerHeader(headers["Sec-WebSocket-Version"])
                             ? "Invalid Sec-WebSocket-Version header."
                             : null;
        }

        private async Task InnerClose(CloseEventArgs e, bool send, bool wait)
        {
            lock (_forConn)
            {
                if (_readyState == WebSocketState.Closing || _readyState == WebSocketState.Closed)
                {
                    return;
                }

                _readyState = WebSocketState.Closing;
            }

            e.WasClean = await CloseHandshake(
                send ? await WebSocketFrame.CreateCloseFrame(e.PayloadData, _client).ToByteArray().ConfigureAwait(false) : null,
                wait ? _waitTime : TimeSpan.Zero,
                _client ? (Func<Task>)ReleaseClientResources : ReleaseServerResources)
                .ConfigureAwait(false);

            _readyState = WebSocketState.Closed;
            try
            {
                OnClose?.Invoke(e);
            }
            catch (Exception ex)
            {
                Error("An exception has occurred during an OnClose event.", ex);
            }
        }

        private async Task<bool> CloseHandshake(byte[] frameAsBytes, TimeSpan timeout, Func<Task> release)
        {
            if (frameAsBytes == null)
            {
                return false;
            }

            bool sent = false;
            using (var ml = new MonitorLock(_forSend))
            {
                sent = await SendBytes(frameAsBytes).ConfigureAwait(false);
            }
            var received = timeout == TimeSpan.Zero || (sent && _exitReceiving != null && _exitReceiving.WaitOne(timeout));

            await release().ConfigureAwait(false);
            if (_receivePong != null)
            {
                _receivePong.Close();
                _receivePong = null;
            }

            if (_exitReceiving != null)
            {
                _exitReceiving.Close();
                _exitReceiving = null;
            }

            var res = sent && received;

            return res;
        }

        private async Task<bool> InnerConnect()
        {
            using (var ml = new MonitorLock(_forConn))
            {
                var msg = _readyState.CheckIfConnectable();
                if (msg != null)
                {
                    Error("An error has occurred in connecting.", null);

                    return false;
                }

                try
                {
                    _readyState = WebSocketState.Connecting;
                    if (_client ? await DoHandshake().ConfigureAwait(false) : await AcceptHandshake().ConfigureAwait(false))
                    {
                        _readyState = WebSocketState.Open;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    await ProcessException(ex, "An exception has occurred while connecting.").ConfigureAwait(false);
                }

                return false;
            }
        }

        // As client
        private string CreateExtensions()
        {
            var buff = new StringBuilder(32);

            if (_compression != CompressionMethod.None)
                buff.Append(_compression.ToExtensionString());

            return buff.Length > 0
                   ? buff.ToString()
                   : null;
        }

        // As server
        private HttpResponse CreateHandshakeCloseResponse(HttpStatusCode code)
        {
            var res = HttpResponse.CreateCloseResponse(code);
            res.Headers["Sec-WebSocket-Version"] = SocketVersion;

            return res;
        }

        // As client
        private HttpRequest CreateHandshakeRequest()
        {
            var req = HttpRequest.CreateWebSocketRequest(_uri);

            var headers = req.Headers;
            if (!_origin.IsNullOrEmpty())
            {
                headers["Origin"] = _origin;
            }

            headers["Sec-WebSocket-Key"] = _base64Key;

            if (_protocols != null)
            {
                headers["Sec-WebSocket-Protocol"] = _protocols.ToString(", ");
            }

            var extensions = CreateExtensions();
            if (extensions != null)
            {
                headers["Sec-WebSocket-Extensions"] = extensions;
            }

            headers["Sec-WebSocket-Version"] = SocketVersion;

            AuthenticationResponse authRes = null;
            if (_authChallenge != null && _credentials != null)
            {
                authRes = new AuthenticationResponse(_authChallenge, _credentials, _nonceCount);
                _nonceCount = authRes.NonceCount;
            }
            else if (_preAuth)
            {
                authRes = new AuthenticationResponse(_credentials);
            }

            if (authRes != null)
                headers["Authorization"] = authRes.ToString();

            if (_cookies.Count > 0)
                req.SetCookies(_cookies);

            return req;
        }

        // As server
        private HttpResponse InnerCreateHandshakeResponse()
        {
            var res = HttpResponse.CreateWebSocketResponse();

            var headers = res.Headers;
            headers["Sec-WebSocket-Accept"] = CreateResponseKey(_base64Key);

            if (_protocol != null)
            {
                headers["Sec-WebSocket-Protocol"] = _protocol;
            }

            if (_extensions != null)
            {
                headers["Sec-WebSocket-Extensions"] = _extensions;
            }

            if (_cookies.Count > 0)
            {
                res.SetCookies(_cookies);
            }

            return res;
        }

        // As client
        private async Task<bool> DoHandshake()
        {
            await SetClientStream().ConfigureAwait(false);
            var res = await SendHandshakeRequest().ConfigureAwait(false);
            var msg = InnerCheckIfValidHandshakeResponse(res);
            if (msg != null)
            {
                msg = "An error has occurred while connecting.";
                Error(msg, null);
                await InnerClose(new CloseEventArgs(CloseStatusCode.Abnormal, msg), false, false).ConfigureAwait(false);

                return false;
            }

            var cookies = res.Cookies;
            if (cookies.Count > 0)
            {
                _cookies.SetOrRemove(cookies);
            }

            return true;
        }

        private void Error(string message, Exception exception)
        {
            OnError?.Invoke(new ErrorEventArgs(message, exception));
        }

        private void InnerInit()
        {
            _compression = CompressionMethod.None;
            _cookies = new CookieCollection();
            _forConn = new object();
            _forSend = new object();
            _readyState = WebSocketState.Connecting;
        }

        private async Task InnerOpen()
        {
            try
            {
                _receiveTask = StartReceiving();

                try
                {
                    OnOpen?.Invoke();
                }
                catch (Exception ex)
                {
                    await ProcessException(ex, "An exception has occurred during an OnOpen event.").ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                await ProcessException(ex, "An exception has occurred while opening.").ConfigureAwait(false);
            }
        }

        private async Task ProcessCloseFrame(WebSocketMessage message)
        {
            var payload = await message.RawData.ToByteArray().ConfigureAwait(false);
            await InnerClose(new PayloadData(payload), !payload.IncludesReservedCloseStatusCode(), false).ConfigureAwait(false);
        }

        private async Task ProcessException(Exception exception, string message)
        {
            var code = CloseStatusCode.Abnormal;
            var reason = message;
            if (exception is WebSocketException)
            {
                var wsex = (WebSocketException)exception;
                code = wsex.Code;
                reason = wsex.Message;
            }

            Error(message ?? code.GetMessage(), exception);
            if (!_client && _readyState == WebSocketState.Connecting)
            {
                await InnerClose(HttpStatusCode.BadRequest).ConfigureAwait(false);
            }
            else
            {
                await InnerClose(code, reason ?? code.GetMessage(), false).ConfigureAwait(false);
            }
        }

        private async Task<bool> ProcessPingFrame(WebSocketMessage message)
        {
            var rawdata = await message.RawData.ToByteArray().ConfigureAwait(false);
            var pongBytes = await WebSocketFrame.CreatePongFrame(rawdata, _client).ToByteArray().ConfigureAwait(false);
            return await InnerSend(pongBytes).ConfigureAwait(false);
        }

        private void ProcessPongFrame()
        {
            _receivePong.Set();
        }

        private void ProcessSecWebSocketExtensionsHeader(string value)
        {
            var buff = new StringBuilder(32);

            var compress = false;
            foreach (var extension in value.SplitHeaderValue(','))
            {
                var trimed = extension.Trim();
                var unprefixed = trimed.RemovePrefix("x-webkit-");
                if (!compress && unprefixed.IsCompressionExtension())
                {
                    var method = unprefixed.ToCompressionMethod();
                    if (method != CompressionMethod.None)
                    {
                        _compression = method;
                        compress = true;

                        buff.Append(trimed + ", ");
                    }
                }
            }

            var len = buff.Length;
            if (len > 0)
            {
                buff.Length = len - 2;
                _extensions = buff.ToString();
            }
        }

        private Task ProcessUnsupportedFrame(CloseStatusCode code, string reason)
        {
            return ProcessException(new WebSocketException(code, reason), null);
        }

        // As client
        private async Task ReleaseClientResources()
        {
            if (_stream != null)
            {
                await _stream.FlushAsync(_cancellationToken).ConfigureAwait(false);
                _stream.Dispose();
                _stream = null;
            }

            if (_tcpClient != null)
            {
                _tcpClient.Close();
                _tcpClient = null;
            }
        }

        // As server
        private async Task ReleaseServerResources()
        {
            if (_closeContext == null)
            {
                return;
            }

            _closeContext();
            _closeContext = null;
            await _stream.FlushAsync(_cancellationToken).ConfigureAwait(false);
            _stream = null;
            _context = null;
        }

        private Task<bool> InnerSend(byte[] frameAsBytes)
        {
            lock (_forConn)
            {
                return _readyState != WebSocketState.Open ? Task.FromResult(false) : SendBytes(frameAsBytes);
            }
        }

        private async Task<bool> InnerSend(Opcode opcode, Stream stream)
        {
            var src = stream;
            var compressed = false;
            var sent = false;
            try
            {
                if (_compression != CompressionMethod.None)
                {
                    stream = stream.Compress(_compression);
                    compressed = true;
                }

                sent = await InnerSend(opcode, stream, compressed).ConfigureAwait(false);
                if (!sent)
                {
                    Error("Sending a data has been interrupted.", null);
                }
            }
            catch (Exception ex)
            {
                Error("An exception has occurred while sending the data.", ex);
            }
            finally
            {
                if (compressed)
                {
                    stream.Dispose();
                }

                src.Dispose();
            }

            return sent;
        }

        private async Task<bool> InnerSend(Opcode opcode, Stream stream, long length)
        {
            var src = stream;
            var compressed = false;
            var sent = false;
            try
            {
                if (_compression != CompressionMethod.None)
                {
                    stream = stream.Compress(length, _compression);
                    compressed = true;
                }

                sent = await InnerSend(opcode, stream, compressed).ConfigureAwait(false);
                if (!sent)
                {
                    Error("Sending a data has been interrupted.", null);
                }
            }
            catch (Exception ex)
            {
                Error("An exception has occurred while sending the data.", ex);
            }
            finally
            {
                if (compressed)
                {
                    stream.Dispose();
                }

                src.Dispose();
            }

            return sent;
        }

        private async Task<bool> InnerSend(Opcode opcode, Stream stream, bool compressed)
        {
            using (var ml = new MonitorLock(_forSend))
            {
                int bytesRead;
                do
                {
                    var buffer = new byte[_fragmentLength];
                    bytesRead = stream.Read(buffer, 0, _fragmentLength);
                    var finalCode = bytesRead < _fragmentLength ? Fin.Final : Fin.More;

                    var data = bytesRead == _fragmentLength ? buffer : buffer.SubArray(0, bytesRead);

                    var bytes = await new WebSocketFrame(finalCode, opcode, data, compressed, _client).ToByteArray().ConfigureAwait(false);
                    if (!await SendBytes(bytes).ConfigureAwait(false))
                    {
                        return false;
                    }

                    opcode = Opcode.Cont;
                }
                while (bytesRead == _fragmentLength && _readyState == WebSocketState.Open);

                return true;
            }
        }

        private async Task<bool> SendBytes(byte[] bytes)
        {
            try
            {
                await _stream.WriteAsync(bytes, 0, bytes.Length, _cancellationToken).ConfigureAwait(false);
                await _stream.FlushAsync(_cancellationToken).ConfigureAwait(false);

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return false;
            }
        }

        // As client
        private async Task<HttpResponse> SendHandshakeRequest()
        {
            var req = CreateHandshakeRequest();
            var res = await SendHttpRequest(req, 90000).ConfigureAwait(false);
            if (res.IsUnauthorized)
            {
                _authChallenge = res.AuthenticationChallenge;
                if (_credentials != null &&
                    (!_preAuth || _authChallenge.Scheme == AuthenticationSchemes.Digest))
                {
                    if (res.Headers.Contains("Connection", "close"))
                    {
                        await ReleaseClientResources().ConfigureAwait(false);
                        await SetClientStream().ConfigureAwait(false);
                    }

                    var authRes = new AuthenticationResponse(_authChallenge, _credentials, _nonceCount);
                    _nonceCount = authRes.NonceCount;
                    req.Headers["Authorization"] = authRes.ToString();
                    res = await SendHttpRequest(req, 15000).ConfigureAwait(false);
                }
            }

            return res;
        }

        // As client
        private Task<HttpResponse> SendHttpRequest(HttpRequest request, int millisecondsTimeout)
        {
            var res = request.GetResponse(_stream, millisecondsTimeout);

            return res;
        }

        // As server
        private Task<bool> SendHttpResponse(HttpBase response)
        {
            return SendBytes(response.ToByteArray());
        }

        // As client
        private async Task SendProxyConnectRequest()
        {
            var req = HttpRequest.CreateConnectRequest(_uri);
            var res = await SendHttpRequest(req, 90000).ConfigureAwait(false);
            if (res.IsProxyAuthenticationRequired)
            {
                var authChal = res.ProxyAuthenticationChallenge;
                if (authChal != null && _proxyCredentials != null)
                {
                    if (res.Headers.Contains("Connection", "close"))
                    {
                        await ReleaseClientResources().ConfigureAwait(false);
                        _tcpClient = new TcpClient(_proxyUri.DnsSafeHost, _proxyUri.Port);
                        _stream = _tcpClient.GetStream();
                    }

                    var authRes = new AuthenticationResponse(authChal, _proxyCredentials, 0);
                    req.Headers["Proxy-Authorization"] = authRes.ToString();
                    res = await SendHttpRequest(req, 15000).ConfigureAwait(false);
                }

                if (res.IsProxyAuthenticationRequired)
                {
                    throw new WebSocketException("A proxy authentication is required.");
                }
            }

            if (res.StatusCode[0] != '2')
                throw new WebSocketException(
                  "The proxy has failed a connection to the requested host and port.");
        }

        // As client
        private async Task SetClientStream()
        {
            if (_proxyUri != null)
            {
                _tcpClient = new TcpClient(_proxyUri.DnsSafeHost, _proxyUri.Port);
                _stream = _tcpClient.GetStream();
                await SendProxyConnectRequest().ConfigureAwait(false);
            }
            else
            {
                _tcpClient = new TcpClient(_uri.DnsSafeHost, _uri.Port);
                _stream = _tcpClient.GetStream();
            }

            if (_secure)
            {
                var sslStream = new SslStream(
                  _stream,
                  false,
                  _sslConfig?.CertificateValidationCallback,
                  _sslConfig?.CertificateSelection);

                if (_sslConfig == null)
                {
                    await sslStream.AuthenticateAsClientAsync(_uri.DnsSafeHost).ConfigureAwait(false);
                }
                else
                {
                    await sslStream.AuthenticateAsClientAsync(
                        _uri.DnsSafeHost,
                        _sslConfig.ClientCertificates,
                        _sslConfig.EnabledSslProtocols,
                        _sslConfig.CheckCertificateRevocation)
                        .ConfigureAwait(false);
                }

                _stream = sslStream;
            }
        }

        private async Task StartReceiving()
        {
            var reader = new WebSocketStreamReader(_stream, _fragmentLength);
            WebSocketMessage message;
            while ((message = await reader.Read(_cancellationToken).ConfigureAwait(false)) != null)
            {
                switch (message.Opcode)
                {
                    case Opcode.Cont:
                        break;
                    case Opcode.Text:
                    case Opcode.Binary:
                        if (OnMessage != null)
                        {
                            await OnMessage.Invoke(new MessageEventArgs(message)).ConfigureAwait(false);
                        }
                        await message.Consume().ConfigureAwait(false);
                        break;
                    case Opcode.Close:
                        await ProcessCloseFrame(message).ConfigureAwait(false);
                        break;
                    case Opcode.Ping:
                        await ProcessPingFrame(message).ConfigureAwait(false);
                        break;
                    case Opcode.Pong:
                        ProcessPongFrame();
                        break;
                    default:
                        await ProcessUnsupportedFrame(CloseStatusCode.IncorrectData, "An incorrect data has been received.").ConfigureAwait(false);
                        break;
                }
            }
        }

        private bool ValidateSecWebSocketAcceptHeader(string value)
        {
            return value != null && value == CreateResponseKey(_base64Key);
        }

        // As client
        private bool ValidateSecWebSocketExtensionsHeader(string value)
        {
            var compress = _compression != CompressionMethod.None;
            if (string.IsNullOrEmpty(value))
            {
                if (compress)
                {
                    _compression = CompressionMethod.None;
                }

                return true;
            }

            if (!compress)
            {
                return false;
            }

            var extensions = value.SplitHeaderValue(',');
            if (extensions.Contains(extension => extension.Trim() != _compression.ToExtensionString()))
            {
                return false;
            }

            _extensions = value;
            return true;
        }

        // As server
        private bool ValidateSecWebSocketKeyHeader(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            _base64Key = value;
            return true;
        }

        // As client
        private bool ValidateSecWebSocketProtocolHeader(string value)
        {
            if (value == null)
            {
                return _protocols == null;
            }

            if (_protocols == null || !_protocols.Contains(protocol => protocol == value))
            {
                return false;
            }

            _protocol = value;
            return true;
        }

        // As server
        private bool InnerValidateSecWebSocketVersionClientHeader(string value)
        {
            return value != null && value == SocketVersion;
        }

        // As client
        private bool InnerValidateSecWebSocketVersionServerHeader(string value)
        {
            return value == null || value == SocketVersion;
        }
    }
}
