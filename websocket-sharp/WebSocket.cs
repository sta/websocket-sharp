#region License
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
#endregion

#region Contributors
/*
 * Contributors:
 * - Frank Razenberg <frank@zzattack.org>
 * - David Wood <dpwood@gmail.com>
 * - Liryna <liryna.stark@gmail.com>
 */
#endregion

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
        internal const int FragmentLength = 102392; // Max value is int.MaxValue - 14.

		private const string GuidId = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
        private const string SocketVersion = "13";

        private readonly Uri _uri;
        private readonly bool _secure;
        private readonly bool _client;
        private readonly string[] _protocols;
        private readonly ClientSslConfiguration _sslConfig;

        private bool _istransmitting;
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
        private object _forEvent;
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

        /// <summary>
        /// Initializes a new instance of the <see cref="WebSocket"/> class with
        /// the specified WebSocket URL and subprotocols.
        /// </summary>
        /// <param name="url">
        /// A <see cref="string"/> that represents the WebSocket URL to connect.
        /// </param>
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
        public WebSocket(string url, params string[] protocols)
            : this(url, null, protocols)
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
        public WebSocket(string url, ClientSslConfiguration sslAuthConfiguration, params string[] protocols)
        {
            if (url == null)
            {
                throw new ArgumentNullException("url");
            }

            string msg;
            if (!url.TryCreateWebSocketUri(out _uri, out msg))
            {
                throw new ArgumentException(msg, "url");
            }

            if (protocols != null && protocols.Length > 0)
            {
                msg = protocols.CheckIfValidProtocols();
                if (msg != null)
                {
                    throw new ArgumentException(msg, "protocols");
                }

                _protocols = protocols;
            }

            _sslConfig = sslAuthConfiguration;
            _base64Key = CreateBase64Key();
            _client = true;
            _secure = _uri.Scheme == "wss";
            _waitTime = TimeSpan.FromSeconds(5);

            InnerInit();
        }

        // As server
        internal WebSocket(HttpListenerWebSocketContext context, string protocol)
        {
            _context = context;
            _protocol = protocol;

            _closeContext = context.Close;
            _secure = context.IsSecureConnection;
            _stream = context.Stream;
            _waitTime = TimeSpan.FromSeconds(1);

            InnerInit();
        }

        // As server
        internal WebSocket(TcpListenerWebSocketContext context, string protocol)
        {
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
        public event EventHandler<CloseEventArgs> OnClose;

        /// <summary>
        /// Occurs when the <see cref="WebSocket"/> gets an error.
        /// </summary>
        public event EventHandler<ErrorEventArgs> OnError;

        /// <summary>
        /// Occurs when the <see cref="WebSocket"/> receives a message.
        /// </summary>
        public event EventHandler<MessageEventArgs> OnMessage;

        /// <summary>
        /// Occurs when the WebSocket connection has been established.
        /// </summary>
        public event EventHandler OnOpen;

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
        public string Extensions
        {
            get
            {
                return _extensions ?? String.Empty;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the WebSocket connection is alive.
        /// </summary>
        /// <value>
        /// <c>true</c> if the connection is alive; otherwise, <c>false</c>.
        /// </value>
        public bool IsAlive
        {
            get
            {
                return Ping();
            }
        }

        /// <summary>
        /// Gets a value indicating whether the WebSocket connection is secure.
        /// </summary>
        /// <value>
        /// <c>true</c> if the connection is secure; otherwise, <c>false</c>.
        /// </value>
        public bool IsSecure
        {
            get
            {
                return _secure;
            }
        }

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
        public WebSocketState ReadyState
        {
            get
            {
                return _readyState;
            }
        }

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
        public Uri Url
        {
            get
            {
                return _client
                       ? _uri
                       : _context.RequestUri;
            }
        }

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

        internal CookieCollection CookieCollection
        {
            get
            {
                return _cookies;
            }
        }

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

        internal bool IsConnected
        {
            get
            {
                return _readyState == WebSocketState.Open || _readyState == WebSocketState.Closing;
            }
        }

        /// <summary>
        /// Closes the WebSocket connection, and releases all associated resources.
        /// </summary>
        public void Close()
        {
            var msg = _readyState.CheckIfClosable();
            if (msg != null)
            {
                Error("An error has occurred in closing the connection.", null);

                return;
            }

            var send = _readyState == WebSocketState.Open;
            InnerClose(new CloseEventArgs(), send, send);
        }

        /// <summary>
        /// Closes the WebSocket connection with the specified <see cref="CloseStatusCode"/>,
        /// and releases all associated resources.
        /// </summary>
        /// <param name="code">
        /// One of the <see cref="CloseStatusCode"/> enum values, represents the status code
        /// indicating the reason for the close.
        /// </param>
        public void Close(CloseStatusCode code)
        {
            var msg = _readyState.CheckIfClosable();
            if (msg != null)
            {
                Error("An error has occurred in closing the connection.", null);

                return;
            }

            var send = _readyState == WebSocketState.Open && !code.IsReserved();
            InnerClose(new CloseEventArgs(code), send, send);
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
        public void Close(CloseStatusCode code, string reason)
        {
            CloseEventArgs e = null;
            var msg = _readyState.CheckIfClosable() ??
                      (e = new CloseEventArgs(code, reason)).RawData.CheckIfValidControlData("reason");

            if (msg != null)
            {
                Error("An error has occurred in closing the connection.", null);

                return;
            }

            var send = _readyState == WebSocketState.Open && !code.IsReserved();
            InnerClose(e, send, send);
        }

        /// <summary>
        /// Closes the WebSocket connection asynchronously, and releases all associated resources.
        /// </summary>
        /// <remarks>
        /// This method doesn't wait for the close to be complete.
        /// </remarks>
        public Task CloseAsync()
        {
            return Task.Run(() => Close());
        }

        /// <summary>
        /// Closes the WebSocket connection asynchronously with the specified
        /// <see cref="CloseStatusCode"/>, and releases all associated resources.
        /// </summary>
        /// <remarks>
        /// This method doesn't wait for the close to be complete.
        /// </remarks>
        /// <param name="code">
        /// One of the <see cref="CloseStatusCode"/> enum values, represents the status code
        /// indicating the reason for the close.
        /// </param>
        public Task CloseAsync(CloseStatusCode code)
        {
            return Task.Run(() => Close(code));
        }

        /// <summary>
        /// Closes the WebSocket connection asynchronously with the specified
        /// <see cref="CloseStatusCode"/> and <see cref="string"/>, and releases
        /// all associated resources.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///   This method doesn't wait for the close to be complete.
        ///   </para>
        ///   <para>
        ///   This method emits a <see cref="OnError"/> event if the size of <paramref name="reason"/>
        ///   is greater than 123 bytes.
        ///   </para>
        /// </remarks>
        /// <param name="code">
        /// One of the <see cref="CloseStatusCode"/> enum values, represents the status code
        /// indicating the reason for the close.
        /// </param>
        /// <param name="reason">
        /// A <see cref="string"/> that represents the reason for the close.
        /// </param>
        public Task CloseAsync(CloseStatusCode code, string reason)
        {
            return Task.Run(() => Close(code, reason));
        }

        /// <summary>
        /// Establishes a WebSocket connection.
        /// </summary>
        public bool Connect()
        {
            var msg = CheckIfCanConnect();
            if (msg != null)
            {
                Error("An error has occurred in connecting.", null);

                return false;
            }

            if (InnerConnect())
            {
                InnerOpen();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Establishes a WebSocket connection asynchronously.
        /// </summary>
        /// <remarks>
        /// This method doesn't wait for the connect to be complete.
        /// </remarks>
        public Task<bool> ConnectAsync()
        {
            return Task.Run(() => Connect());
        }

        /// <summary>
        /// Sends a Ping using the WebSocket connection.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the <see cref="WebSocket"/> receives a Pong to this Ping in a time;
        /// otherwise, <c>false</c>.
        /// </returns>
        public bool Ping()
        {
            var bytes = _client
                        ? WebSocketFrame.CreatePingFrame(true).ToByteArray()
                        : WebSocketFrame.EmptyUnmaskPingBytes;

            return InnerPing(bytes, _waitTime);
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
        public bool Ping(string message)
        {
            if (message == null || message.Length == 0)
                return Ping();

            var data = Encoding.UTF8.GetBytes(message);
            var msg = data.CheckIfValidControlData("message");
            if (msg != null)
            {
                Error("An error has occurred in sending the ping.", null);

                return false;
            }

            return InnerPing(WebSocketFrame.CreatePingFrame(data, _client).ToByteArray(), _waitTime);
        }

        /// <summary>
        /// Sends a binary <paramref name="data"/> using the WebSocket connection.
        /// </summary>
        /// <param name="data">
        /// An array of <see cref="byte"/> that represents the binary data to send.
        /// </param>
        public bool Send(byte[] data)
        {
            var msg = _readyState.CheckIfOpen() ?? data.CheckIfValidSendData();
            if (msg != null)
            {
                Error("An error has occurred in sending the data.", null);

                return false;
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
        public bool Send(Stream stream)
        {
            if (stream == null)
            {
                return false;
            }

            var msg = _readyState.CheckIfOpen();
            if (msg != null)
            {
                Error("An error has occurred in sending the data.", null);

                return false;
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
        public bool Send(Stream stream, long length)
        {
            if (stream == null)
            {
                return false;
            }

            var msg = _readyState.CheckIfOpen();
            if (msg != null)
            {
                Error("An error has occurred in sending the data.", null);

                return false;
            }

            return InnerSend(Opcode.Binary, stream, length);
        }

        /// <summary>
        /// Sends a text <paramref name="data"/> using the WebSocket connection.
        /// </summary>
        /// <param name="data">
        /// A <see cref="string"/> that represents the text data to send.
        /// </param>
        public bool Send(string data)
        {
            var msg = _readyState.CheckIfOpen() ?? data.CheckIfValidSendData();
            if (msg != null)
            {
                Error("An error has occurred in sending the data.", null);

                return false;
            }

            return InnerSend(Opcode.Text, new MemoryStream(Encoding.UTF8.GetBytes(data)));
        }

        /// <summary>
        /// Sends a binary <paramref name="data"/> asynchronously using the WebSocket connection.
        /// </summary>
        /// <remarks>
        /// This method doesn't wait for the send to be complete.
        /// </remarks>
        /// <param name="data">
        /// An array of <see cref="byte"/> that represents the binary data to send.
        /// </param>
        /// <param name="completed">
        /// An <c>Action&lt;bool&gt;</c> delegate that references the method(s) called when
        /// the send is complete. A <see cref="bool"/> passed to this delegate is <c>true</c>
        /// if the send is complete successfully.
        /// </param>
        public Task<bool> SendAsync(byte[] data)
        {
            return Task.Run(() => Send(data));
        }

        /// <summary>
        /// Sends the specified <paramref name="stream"/> as a binary data asynchronously
        /// using the WebSocket connection.
        /// </summary>
        /// <remarks>
        /// This method doesn't wait for the send to be complete.
        /// </remarks>
        /// <param name="stream">
        /// A <see cref="FileInfo"/> that represents the file to send.
        /// </param>
        public Task<bool> SendAsync(Stream stream)
        {
            return Task.Run(() => Send(stream));
        }

        /// <summary>
        /// Sends a text <paramref name="data"/> asynchronously using the WebSocket connection.
        /// </summary>
        /// <remarks>
        /// This method doesn't wait for the send to be complete.
        /// </remarks>
        /// <param name="data">
        /// A <see cref="string"/> that represents the text data to send.
        /// </param>
        public Task<bool> SendAsync(string data)
        {
            return Task.Run(() => Send(data));
        }

        /// <summary>
        /// Sends a binary data from the specified <see cref="Stream"/> asynchronously
        /// using the WebSocket connection.
        /// </summary>
        /// <remarks>
        /// This method doesn't wait for the send to be complete.
        /// </remarks>
        /// <param name="stream">
        /// A <see cref="Stream"/> from which contains the binary data to send.
        /// </param>
        /// <param name="length">
        /// An <see cref="int"/> that represents the number of bytes to send.
        /// </param>
        public Task<bool> SendAsync(Stream stream, long length)
        {
            return Task.Run(() => Send(stream, length));
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
                    Error("An error has occurred in setting the credentials.", null);

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
            var send = _readyState == WebSocketState.Open;
            InnerClose(new CloseEventArgs(CloseStatusCode.Away), send, send);
        }

        // As server
        internal void InnerClose(HttpResponse response)
        {
            _readyState = WebSocketState.Closing;

            SendHttpResponse(response);
            ReleaseServerResources();

            _readyState = WebSocketState.Closed;
        }

        // As server
        internal void InnerClose(HttpStatusCode code)
        {
            InnerClose(CreateHandshakeCloseResponse(code));
        }

        internal bool InnerPing(byte[] frameAsBytes, TimeSpan timeout)
        {
            try
            {
                AutoResetEvent pong;
                return _readyState == WebSocketState.Open &&
                       InnerSend(frameAsBytes) &&
                       (pong = _receivePong) != null &&
                       pong.WaitOne(timeout);
            }
            catch
            {
                return false;
            }
        }

        // As server, used to broadcast
        internal bool InnerSend(Opcode opcode, byte[] data)
        {
            return InnerSend(opcode, new MemoryStream(data), _compression == CompressionMethod.Deflate);
        }

        internal bool InnerSend(Fin final, Opcode opcode, byte[] data)
        {
            var frame = new WebSocketFrame(final, opcode, data, _compression != CompressionMethod.None, false);
            return SendBytes(frame.ToByteArray());
        }

        // As server
        internal void InnerClose(CloseEventArgs e, byte[] frameAsBytes, TimeSpan timeout)
        {
            lock (_forConn)
            {
                if (_readyState == WebSocketState.Closing || _readyState == WebSocketState.Closed)
                {
                    return;
                }

                _readyState = WebSocketState.Closing;
            }

            e.WasClean = CloseHandshake(frameAsBytes, timeout, ReleaseServerResources);

            _readyState = WebSocketState.Closed;

            OnClose.Emit(this, e);
        }

        // As server
        internal void ConnectAsServer()
        {
            try
            {
                if (AcceptHandshake())
                {
                    _readyState = WebSocketState.Open;
                    InnerOpen();
                }
            }
            catch (Exception ex)
            {
                ProcessException(ex, "An exception has occurred while connecting.");
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
        private bool AcceptHandshake()
        {
            var msg = CheckIfValidHandshakeRequest(_context);
            if (msg != null)
            {
                Error("An error has occurred while connecting.", null);
                InnerClose(HttpStatusCode.BadRequest);

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

            return SendHttpResponse(InnerCreateHandshakeResponse());
        }
        
        // As server
        private void InnerClose(CloseStatusCode code, string reason, bool wait)
        {
            InnerClose(new PayloadData(((ushort)code).Append(reason)), !code.IsReserved(), wait);
        }

        private void InnerClose(PayloadData payload, bool send, bool wait)
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
            e.WasClean = CloseHandshake(
              send ? WebSocketFrame.CreateCloseFrame(e.PayloadData, _client).ToByteArray() : null,
              wait ? WaitTime : TimeSpan.Zero,
              _client ? (Action)ReleaseClientResources : ReleaseServerResources);

            _readyState = WebSocketState.Closed;
            try
            {
                OnClose.Emit(this, e);
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

        private void InnerClose(CloseEventArgs e, bool send, bool wait)
        {
            lock (_forConn)
            {
                if (_readyState == WebSocketState.Closing || _readyState == WebSocketState.Closed)
                {
                    return;
                }

                _readyState = WebSocketState.Closing;
            }

            e.WasClean = CloseHandshake(
              send ? WebSocketFrame.CreateCloseFrame(e.PayloadData, _client).ToByteArray() : null,
              wait ? _waitTime : TimeSpan.Zero,
              _client ? (Action)ReleaseClientResources : ReleaseServerResources);

            _readyState = WebSocketState.Closed;
            try
            {
                OnClose.Emit(this, e);
            }
            catch (Exception ex)
            {
                Error("An exception has occurred during an OnClose event.", ex);
            }
        }
        
        private bool CloseHandshake(byte[] frameAsBytes, TimeSpan timeout, Action release)
        {
            lock (_forSend)
            {
                while (_istransmitting)
                {
                    Monitor.Wait(_forSend);
                }

                _istransmitting = true;
            }

            var sent = frameAsBytes != null && SendBytes(frameAsBytes);

            lock (_forSend)
            {
                _istransmitting = false;

                Monitor.Pulse(_forSend);
            }

            var received = timeout == TimeSpan.Zero || (sent && _exitReceiving != null && _exitReceiving.WaitOne(timeout));

            release();
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

        private bool InnerConnect()
        {
            lock (_forConn)
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
                    if (_client ? DoHandshake() : AcceptHandshake())
                    {
                        _readyState = WebSocketState.Open;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    ProcessException(ex, "An exception has occurred while connecting.");
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
                headers["Origin"] = _origin;

            headers["Sec-WebSocket-Key"] = _base64Key;

            if (_protocols != null)
                headers["Sec-WebSocket-Protocol"] = _protocols.ToString(", ");

            var extensions = CreateExtensions();
            if (extensions != null)
                headers["Sec-WebSocket-Extensions"] = extensions;

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
                headers["Sec-WebSocket-Protocol"] = _protocol;

            if (_extensions != null)
                headers["Sec-WebSocket-Extensions"] = _extensions;

            if (_cookies.Count > 0)
                res.SetCookies(_cookies);

            return res;
        }

        // As client
        private bool DoHandshake()
        {
            SetClientStream();
            var res = SendHandshakeRequest();
            var msg = InnerCheckIfValidHandshakeResponse(res);
            if (msg != null)
            {
                msg = "An error has occurred while connecting.";
                Error(msg, null);
                InnerClose(new CloseEventArgs(CloseStatusCode.Abnormal, msg), false, false);

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
            OnError.Emit(this, new ErrorEventArgs(message, exception));
        }

        private void InnerInit()
        {
            _compression = CompressionMethod.None;
            _cookies = new CookieCollection();
            _forConn = new object();
            _forEvent = new object();
            _forSend = new object();
            _readyState = WebSocketState.Connecting;
        }

        private void InnerOpen()
        {
            try
            {
                StartReceiving();

                lock (_forEvent)
                {
                    try
                    {
                        OnOpen.Emit(this, EventArgs.Empty);
                    }
                    catch (Exception ex)
                    {
                        ProcessException(ex, "An exception has occurred during an OnOpen event.");
                    }
                }
            }
            catch (Exception ex)
            {
                ProcessException(ex, "An exception has occurred while opening.");
            }
        }

        private void ProcessCloseFrame(WebSocketMessage message)
        {
            var payload = message.RawData.ToByteArray();
            InnerClose(new PayloadData(payload), !payload.IncludesReservedCloseStatusCode(), false);
        }

        private void ProcessException(Exception exception, string message)
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
                InnerClose(HttpStatusCode.BadRequest);
            }
            else
            {
                InnerClose(code, reason ?? code.GetMessage(), false);
            }
        }

        private void ProcessPingFrame(WebSocketMessage message)
        {
            //send(new WebSocketFrame(Opcode.Pong, message.RawData.ToByteArray(), _client).ToByteArray());

            InnerSend(WebSocketFrame.CreatePongFrame(message.RawData.ToByteArray(), _client).ToByteArray());
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

        private void ProcessUnsupportedFrame(CloseStatusCode code, string reason)
        {
            ProcessException(new WebSocketException(code, reason), null);
        }

        // As client
        private void ReleaseClientResources()
        {
            if (_stream != null)
            {
                _stream.Flush();
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
        private void ReleaseServerResources()
        {
            if (_closeContext == null)
            {
                return;
            }

            _closeContext();
            _closeContext = null;
            _stream.Flush();
            _stream = null;
            _context = null;
        }

        private bool InnerSend(byte[] frameAsBytes)
        {
            lock (_forConn)
            {
                if (_readyState != WebSocketState.Open)
                {
                    return false;
                }

                return SendBytes(frameAsBytes);
            }
        }

        private bool InnerSend(Opcode opcode, Stream stream)
        {
            lock (_forSend)
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

                    sent = InnerSend(opcode, stream, compressed);
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
        }

        private bool InnerSend(Opcode opcode, Stream stream, long length)
        {
            lock (_forSend)
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

                    sent = InnerSend(opcode, stream, compressed);
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
        }

        private bool InnerSend(Opcode opcode, Stream stream, bool compressed)
        {
            lock (_forSend)
            {
                while (_istransmitting)
                {
                    Monitor.Wait(_forSend);
                }

                _istransmitting = true;
            }

            int bytesRead;
            do
            {
                var buffer = new byte[FragmentLength];
                bytesRead = stream.Read(buffer, 0, FragmentLength);
                var finalCode = bytesRead < FragmentLength ? Fin.Final : Fin.More;

                var data = bytesRead == FragmentLength ? buffer : buffer.SubArray(0, bytesRead);

                if (!InnerSend(finalCode, opcode, data, compressed))
                {
                    return false;
                }

                opcode = Opcode.Cont;
            }
            while (bytesRead == FragmentLength);

            _stream.Flush();

            lock (_forSend)
            {
                _istransmitting = false;
                Monitor.Pulse(_forSend);
            }

            return true;
        }
        
        private bool InnerSend(Fin fin, Opcode opcode, byte[] data, bool compressed)
        {
            lock (_forConn)
            {
                if (_readyState != WebSocketState.Open)
                {
                    return false;
                }

                return SendBytes(new WebSocketFrame(fin, opcode, data, compressed, _client).ToByteArray());
            }
        }
        
        private bool SendBytes(byte[] bytes)
        {
            try
            {
                _stream.Write(bytes, 0, bytes.Length);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return false;
            }
        }
        
        // As client
        private HttpResponse SendHandshakeRequest()
        {
            var req = CreateHandshakeRequest();
            var res = SendHttpRequest(req, 90000);
            if (res.IsUnauthorized)
            {
                _authChallenge = res.AuthenticationChallenge;
                if (_credentials != null &&
                    (!_preAuth || _authChallenge.Scheme == AuthenticationSchemes.Digest))
                {
                    if (res.Headers.Contains("Connection", "close"))
                    {
                        ReleaseClientResources();
                        SetClientStream();
                    }

                    var authRes = new AuthenticationResponse(_authChallenge, _credentials, _nonceCount);
                    _nonceCount = authRes.NonceCount;
                    req.Headers["Authorization"] = authRes.ToString();
                    res = SendHttpRequest(req, 15000);
                }
            }

            return res;
        }

        // As client
        private HttpResponse SendHttpRequest(HttpRequest request, int millisecondsTimeout)
        {
            var res = request.GetResponse(_stream, millisecondsTimeout);

            return res;
        }

        // As server
        private bool SendHttpResponse(HttpResponse response)
        {
            return SendBytes(response.ToByteArray());
        }

        // As client
        private void SendProxyConnectRequest()
        {
            var req = HttpRequest.CreateConnectRequest(_uri);
            var res = SendHttpRequest(req, 90000);
            if (res.IsProxyAuthenticationRequired)
            {
                var authChal = res.ProxyAuthenticationChallenge;
                if (authChal != null && _proxyCredentials != null)
                {
                    if (res.Headers.Contains("Connection", "close"))
                    {
                        ReleaseClientResources();
                        _tcpClient = new TcpClient(_proxyUri.DnsSafeHost, _proxyUri.Port);
                        _stream = _tcpClient.GetStream();
                    }

                    var authRes = new AuthenticationResponse(authChal, _proxyCredentials, 0);
                    req.Headers["Proxy-Authorization"] = authRes.ToString();
                    res = SendHttpRequest(req, 15000);
                }

                if (res.IsProxyAuthenticationRequired)
                    throw new WebSocketException("A proxy authentication is required.");
            }

            if (res.StatusCode[0] != '2')
                throw new WebSocketException(
                  "The proxy has failed a connection to the requested host and port.");
        }

        // As client
        private void SetClientStream()
        {
            if (_proxyUri != null)
            {
                _tcpClient = new TcpClient(_proxyUri.DnsSafeHost, _proxyUri.Port);
                _stream = _tcpClient.GetStream();
                SendProxyConnectRequest();
            }
            else
            {
                _tcpClient = new TcpClient(_uri.DnsSafeHost, _uri.Port);
                _stream = _tcpClient.GetStream();
            }

            if (_secure)
            {
                var certSelectionCallback = _sslConfig != null ? _sslConfig.CertificateSelection : null;
                var certificateValidationCallback = _sslConfig != null && _sslConfig.CertificateValidationCallback != null
                                                        ? _sslConfig.CertificateValidationCallback
                                                        : ((sender, certificate, chain, sslPolicyErrors) => true);
                var sslStream = new SslStream(
                  _stream,
                  false,
                  certificateValidationCallback,
                  certSelectionCallback ?? ((sender, targetHost, localCertificates, remoteCertificate, acceptableIssuers) => null));

                if (_sslConfig == null)
                {
                    sslStream.AuthenticateAsClient(_uri.DnsSafeHost);
                }
                else
                {
                    sslStream.AuthenticateAsClient(
                        _uri.DnsSafeHost,
                        _sslConfig.ClientCertificates,
                        _sslConfig.EnabledSslProtocols,
                        _sslConfig.CheckCertificateRevocation);
                }

                _stream = sslStream;
            }
        }

        private Task StartReceiving()
        {
            return Task.Run(
                () =>
                {
                    var reader = new WebSocketStreamReader(_stream);
                    foreach (var message in reader.Read())
                    {
                        switch (message.Opcode)
                        {
                            case Opcode.Cont:
                                break;
                            case Opcode.Text:
                            case Opcode.Binary:
                                if (!OnMessage.Emit(this, new MessageEventArgs(message)))
                                {
                                    message.Consume();
                                }

                                break;
                            case Opcode.Close:
                                ProcessCloseFrame(message);
                                break;
                            case Opcode.Ping:
                                ProcessPingFrame(message);
                                break;
                            case Opcode.Pong:
                                ProcessPongFrame();
                                break;
                            default:
                                ProcessUnsupportedFrame(CloseStatusCode.IncorrectData, "An incorrect data has been received.");
                                break;
                        }
                    }
                });
        }

        private bool ValidateSecWebSocketAcceptHeader(string value)
        {
            return value != null && value == CreateResponseKey(_base64Key);
        }

        // As client
        private bool ValidateSecWebSocketExtensionsHeader(string value)
        {
            var compress = _compression != CompressionMethod.None;
            if (value == null || value.Length == 0)
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
            if (value == null || value.Length == 0)
                return false;

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
