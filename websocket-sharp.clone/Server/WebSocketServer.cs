/*
 * WebSocketServer.cs
 *
 * A C# implementation of the WebSocket protocol server.
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
 * - Juan Manuel Lallana <juan.manuel.lallana@gmail.com>
 * - Jonas Hovgaard <j@jhovgaard.dk>
 * - Liryna <liryna.stark@gmail.com>
 */

namespace WebSocketSharp.Server
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using System.Security.Cryptography.X509Certificates;
    using System.Security.Principal;
    using System.Threading;
    using System.Threading.Tasks;

    using WebSocketSharp.Net;
    using WebSocketSharp.Net.WebSockets;

    using AuthenticationSchemes = WebSocketSharp.Net.AuthenticationSchemes;
    using HttpStatusCode = WebSocketSharp.Net.HttpStatusCode;
    using NetworkCredential = WebSocketSharp.Net.NetworkCredential;

    /// <summary>
    /// Provides a WebSocket protocol server.
    /// </summary>
    /// <remarks>
    /// The WebSocketServer class can provide multiple WebSocket services.
    /// </remarks>
    public class WebSocketServer
    {
        private readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();
        private readonly IPAddress _address;
        private readonly int _port;
        private readonly Uri _uri;
        private readonly ServerSslConfiguration _sslConfig;
        private readonly int _fragmentSize;
        private readonly bool _secure;
        private Task _receiveTask;
        private AuthenticationSchemes _authSchemes;
        private Func<IIdentity, NetworkCredential> _credentialsFinder;
        private TcpListener _listener;
        private string _realm;
        private bool _reuseAddress;
        private WebSocketServiceManager _services;
        private volatile ServerState _state;

        /// <summary>
        /// Initializes a new instance of the <see cref="WebSocketServer"/> class with the specified
        /// <paramref name="address"/>, <paramref name="port"/>, and <paramref name="secure"/>.
        /// </summary>
        /// <remarks>
        /// An instance initialized by this constructor listens for the incoming connection requests
        /// on <paramref name="port"/>.
        /// </remarks>
        /// <param name="address">
        /// A <see cref="System.Net.IPAddress"/> that represents the local IP address of the server.
        /// </param>
        /// <param name="port">
        /// An <see cref="int"/> that represents the port number on which to listen.
        /// </param>
        /// <param name="certificate">
        /// A <see cref="X509Certificate2"/> used to secure the connection.
        /// </param>
        /// <param name="authenticationSchemes">Supported authentication schemes.</param>
        /// <exception cref="ArgumentException">
        ///   <para>
        ///   <paramref name="address"/> isn't a local IP address.
        ///   </para>
        ///   <para>
        ///   -or-
        ///   </para>
        ///   <para>
        ///   Pair of <paramref name="port"/> and <paramref name="certificate"/> is invalid.
        ///   </para>
        /// </exception>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="address"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="port"/> isn't between 1 and 65535.</exception>
        public WebSocketServer(IPAddress address = null, int port = 80, ServerSslConfiguration certificate = null, AuthenticationSchemes authenticationSchemes = AuthenticationSchemes.Anonymous, int fragmentSize = 102392)
        {
            if (address == null)
            {
                address = IPAddress.Any;
            }

            if (!address.IsLocal())
            {
                throw new ArgumentException("Not a local IP address: " + address, nameof(address));
            }

            if (!port.IsPortNumber())
            {
                throw new ArgumentOutOfRangeException("port", "Not between 1 and 65535: " + port);
            }

            var secure = certificate != null;

            if ((port == 80 && secure) || (port == 443 && !secure))
            {
                throw new ArgumentException(string.Format("An invalid pair of 'port' and 'secure': {0}, {1}", port, secure));
            }

            _address = address;
            _port = port;
            _sslConfig = certificate;
            _fragmentSize = fragmentSize;
            _secure = secure;
            _uri = "/".ToUri();

            Init(authenticationSchemes);
        }

        /// <summary>
        /// Gets a value indicating whether the server has started.
        /// </summary>
        /// <value>
        /// <c>true</c> if the server has started; otherwise, <c>false</c>.
        /// </value>
        public bool IsListening => _state == ServerState.Start;

        /// <summary>
        /// Gets a value indicating whether the server provides a secure connection.
        /// </summary>
        /// <value>
        /// <c>true</c> if the server provides a secure connection; otherwise, <c>false</c>.
        /// </value>
        public bool IsSecure => _secure;

        /// <summary>
        /// Gets the port on which to listen for incoming connection requests.
        /// </summary>
        /// <value>
        /// An <see cref="int"/> that represents the port number on which to listen.
        /// </value>
        public int Port => _port;

        /// <summary>
        /// Gets or sets the name of the realm associated with the server.
        /// </summary>
        /// <value>
        /// A <see cref="string"/> that represents the name of the realm.
        /// The default value is <c>"SECRET AREA"</c>.
        /// </value>
        public string Realm
        {
            get
            {
                return _realm ?? (_realm = "SECRET AREA");
            }

            set
            {
                var msg = _state.CheckIfStartable();
                if (msg != null)
                {
                    return;
                }

                _realm = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the server is allowed to be bound to an address
        /// that is already in use.
        /// </summary>
        /// <remarks>
        /// If you would like to resolve to wait for socket in <c>TIME_WAIT</c> state, you should set
        /// this property to <c>true</c>.
        /// </remarks>
        /// <value>
        /// <c>true</c> if the server is allowed to be bound to an address that is already in use;
        /// otherwise, <c>false</c>. The default value is <c>false</c>.
        /// </value>
        public bool ReuseAddress
        {
            get
            {
                return _reuseAddress;
            }

            set
            {
                var msg = _state.CheckIfStartable();
                if (msg != null)
                {
                    return;
                }

                _reuseAddress = value;
            }
        }

        /// <summary>
        /// Gets or sets the delegate called to find the credentials for an identity used to
        /// authenticate a client.
        /// </summary>
        /// <value>
        /// A Func&lt;<see cref="IIdentity"/>, <see cref="NetworkCredential"/>&gt; delegate that
        /// references the method(s) used to find the credentials. The default value is a function
        /// that only returns <see langword="null"/>.
        /// </value>
        public Func<IIdentity, NetworkCredential> UserCredentialsFinder
        {
            get
            {
                return _credentialsFinder ?? (_credentialsFinder = identity => null);
            }

            set
            {
                var msg = _state.CheckIfStartable();
                if (msg != null)
                {
                    return;
                }

                _credentialsFinder = value;
            }
        }

        /// <summary>
        /// Gets or sets the wait time for the response to the WebSocket Ping or Close.
        /// </summary>
        /// <value>
        /// A <see cref="TimeSpan"/> that represents the wait time. The default value is
        /// the same as 1 second.
        /// </value>
        public TimeSpan WaitTime
        {
            get
            {
                return _services.WaitTime;
            }

            set
            {
                var msg = _state.CheckIfStartable() ?? value.CheckIfValidWaitTime();
                if (msg != null)
                {
                    return;
                }

                _services.WaitTime = value;
            }
        }

        /// <summary>
        /// Gets the access to the WebSocket services provided by the server.
        /// </summary>
        /// <value>
        /// A <see cref="WebSocketServiceManager"/> that manages the WebSocket services.
        /// </value>
        public WebSocketServiceManager WebSocketServices => _services;

        /// <summary>
        /// Adds a WebSocket service with the specified behavior and <paramref name="path"/>.
        /// </summary>
        /// <remarks>
        /// This method converts <paramref name="path"/> to URL-decoded string,
        /// and removes <c>'/'</c> from tail end of <paramref name="path"/>.
        /// </remarks>
        /// <param name="path">
        /// A <see cref="string"/> that represents the absolute path to the service to add.
        /// </param>
        /// <typeparam name="TBehaviorWithNew">
        /// The type of the behavior of the service to add. The TBehaviorWithNew must inherit
        /// the <see cref="WebSocketBehavior"/> class, and must have a public parameterless
        /// constructor.
        /// </typeparam>
        public void AddWebSocketService<TBehaviorWithNew>(string path)
          where TBehaviorWithNew : WebSocketBehavior, new()
        {
            AddWebSocketService(path, () => new TBehaviorWithNew());
        }

        /// <summary>
        /// Adds a WebSocket service with the specified behavior, <paramref name="path"/>,
        /// and <paramref name="initializer"/>.
        /// </summary>
        /// <remarks>
        ///   <para>
        ///   This method converts <paramref name="path"/> to URL-decoded string,
        ///   and removes <c>'/'</c> from tail end of <paramref name="path"/>.
        ///   </para>
        ///   <para>
        ///   <paramref name="initializer"/> returns an initialized specified typed
        ///   <see cref="WebSocketBehavior"/> instance.
        ///   </para>
        /// </remarks>
        /// <param name="path">
        /// A <see cref="string"/> that represents the absolute path to the service to add.
        /// </param>
        /// <param name="initializer">
        /// A Func&lt;T&gt; delegate that references the method used to initialize a new specified
        /// typed <see cref="WebSocketBehavior"/> instance (a new <see cref="IWebSocketSession"/>
        /// instance).
        /// </param>
        /// <typeparam name="TBehavior">
        /// The type of the behavior of the service to add. The TBehavior must inherit
        /// the <see cref="WebSocketBehavior"/> class.
        /// </typeparam>
        public void AddWebSocketService<TBehavior>(string path, Func<TBehavior> initializer)
          where TBehavior : WebSocketBehavior
        {
            var msg = path.CheckIfValidServicePath() ??
                      (initializer == null ? "'initializer' is null." : null);

            if (msg != null)
            {
                return;
            }

            _services.Add(path, initializer);
        }

        /// <summary>
        /// Removes the WebSocket service with the specified <paramref name="path"/>.
        /// </summary>
        /// <remarks>
        /// This method converts <paramref name="path"/> to URL-decoded string,
        /// and removes <c>'/'</c> from tail end of <paramref name="path"/>.
        /// </remarks>
        /// <returns>
        /// <c>true</c> if the service is successfully found and removed; otherwise, <c>false</c>.
        /// </returns>
        /// <param name="path">
        /// A <see cref="string"/> that represents the absolute path to the service to find.
        /// </param>
        public Task<bool> RemoveWebSocketService(string path)
        {
            var msg = path.CheckIfValidServicePath();
            return msg != null ? Task.FromResult(false) : _services.Remove(path);
        }

        /// <summary>
        /// Starts receiving the WebSocket connection requests.
        /// </summary>
        public void Start()
        {
            var msg = _state.CheckIfStartable() ?? CheckIfCertificateExists();
            if (msg != null)
            {
                return;
            }

            _services.Start();
            _receiveTask = StartReceiving(_tokenSource.Token);

            _state = ServerState.Start;
        }

        /// <summary>
        /// Stops receiving the WebSocket connection requests.
        /// </summary>
        public async Task Stop()
        {
            var msg = _state.CheckIfStart();
            if (msg != null)
            {
                return;
            }

            _state = ServerState.ShuttingDown;

            StopReceiving();
            await _services.Stop(new CloseEventArgs(), true, true).ConfigureAwait(false);

            _state = ServerState.Stop;
        }

        /// <summary>
        /// Stops receiving the WebSocket connection requests with the specified
        /// <see cref="CloseStatusCode"/> and <see cref="string"/>.
        /// </summary>
        /// <param name="code">
        /// One of the <see cref="CloseStatusCode"/> enum values, represents the status code
        /// indicating the reason for stop.
        /// </param>
        /// <param name="reason">
        /// A <see cref="string"/> that represents the reason for stop.
        /// </param>
        public async Task Stop(CloseStatusCode code, string reason)
        {
            CloseEventArgs e = null;

            var msg =
              _state.CheckIfStart() ??
              (e = new CloseEventArgs(code, reason)).RawData.CheckIfValidControlData("reason");

            if (msg != null)
            {
                return;
            }

            _state = ServerState.ShuttingDown;

            StopReceiving();

            var send = !code.IsReserved();
            await _services.Stop(e, send, send).ConfigureAwait(false);

            _state = ServerState.Stop;
        }

        private string CheckIfCertificateExists()
        {
            return _secure && (_sslConfig?.ServerCertificate == null)
                   ? "The secure connection requires a server certificate."
                   : null;
        }

        private void StopReceiving()
        {
            _listener.Stop();
            if (_tokenSource != null)
            {
                _tokenSource.Cancel();
                _tokenSource.Dispose();
                _receiveTask.Wait();
                _receiveTask.Dispose();
            }
        }

        private async Task Abort()
        {
            if (!IsListening)
            {
                return;
            }

            _state = ServerState.ShuttingDown;

            _listener.Stop();
            await _services.Stop(new CloseEventArgs(CloseStatusCode.ServerError), true, false).ConfigureAwait(false);

            _state = ServerState.Stop;
        }

        private async Task<bool> AuthenticateRequest(AuthenticationSchemes scheme, TcpListenerWebSocketContext context)
        {
            var chal = scheme == AuthenticationSchemes.Basic
                       ? AuthenticationChallenge.CreateBasicChallenge(Realm).ToBasicString()
                       : scheme == AuthenticationSchemes.Digest
                         ? AuthenticationChallenge.CreateDigestChallenge(Realm).ToDigestString()
                         : null;

            if (chal == null)
            {
                await context.Close(HttpStatusCode.Forbidden).ConfigureAwait(false);
                return false;
            }

            var retry = -1;
            var schm = scheme.ToString();
            var realm = Realm;
            var credFinder = UserCredentialsFinder;
            Func<Task<bool>> auth = () => Task.FromResult(false);

            auth = async () =>
            {
                var auth1 = auth;
                retry++;
                if (retry > 99)
                {
                    await context.Close(HttpStatusCode.Forbidden).ConfigureAwait(false);
                    return false;
                }

                var res = await context.GetHeader("Authorization").ConfigureAwait(false);
                if (res == null || !res.StartsWith(schm, StringComparison.OrdinalIgnoreCase))
                {
                    context.SendAuthenticationChallenge(chal);
                    return await auth1().ConfigureAwait(false);
                }

                await context.SetUser(scheme, realm, credFinder).ConfigureAwait(false);
                if (!context.IsAuthenticated)
                {
                    context.SendAuthenticationChallenge(chal);
                    return await auth1().ConfigureAwait(false);
                }

                return true;
            };

            return await auth().ConfigureAwait(false);
        }

        private void Init(AuthenticationSchemes authenticationSchemes)
        {
            _authSchemes = authenticationSchemes;
            _listener = new TcpListener(_address, _port);
            _services = new WebSocketServiceManager(_fragmentSize);
            _state = ServerState.Ready;
        }

        private async Task ProcessWebSocketRequest(TcpListenerWebSocketContext context)
        {
            var uri = await context.GetRequestUri().ConfigureAwait(false);
            if (uri == null)
            {
                await context.Close(HttpStatusCode.BadRequest).ConfigureAwait(false);
                return;
            }

            if (_uri.IsAbsoluteUri)
            {
                var actual = uri.DnsSafeHost;
                var expected = _uri.DnsSafeHost;
                if (Uri.CheckHostName(actual) == UriHostNameType.Dns &&
                    Uri.CheckHostName(expected) == UriHostNameType.Dns &&
                    actual != expected)
                {
                    await context.Close(HttpStatusCode.NotFound).ConfigureAwait(false);
                    return;
                }
            }

            WebSocketServiceHost host;
            if (!_services.InternalTryGetServiceHost(uri.AbsolutePath, out host))
            {
                await context.Close(HttpStatusCode.NotImplemented).ConfigureAwait(false);
                return;
            }

            await host.StartSession(context).ConfigureAwait(false);
        }

        private async Task ReceiveRequest(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);

                    try
                    {
                        var ctx = client.GetWebSocketContext(null, _secure, _sslConfig);
                        if (_authSchemes != AuthenticationSchemes.Anonymous && !await AuthenticateRequest(_authSchemes, ctx).ConfigureAwait(false))
                        {
                            return;
                        }

                        await ProcessWebSocketRequest(ctx).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        client.Close();
                    }
                }
                catch (Exception)
                {
                    break;
                }
            }

            if (IsListening)
            {
                await Abort().ConfigureAwait(false);
            }
        }

        private Task StartReceiving(CancellationToken cancellationToken)
        {
            if (_reuseAddress)
            {
                _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            }

            _listener.Start();
            return ReceiveRequest(cancellationToken);
        }
    }
}
