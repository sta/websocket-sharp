using System;
using System.Diagnostics;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using WebSocketSharp.Net;

// ReSharper disable UnusedMember.Global

namespace WebSocketSharp
{
	public sealed class ClientWebSocket : WebSocket
	{
		private const int maxRetryCountForConnect = 10;
		private readonly string base64Key;

		private readonly string[] protocols;

		private Uri uri;
		private AuthenticationChallenge authChallenge;
		private NetworkCredential credentials;
		private bool enableRedirection;
		private bool extensionsRequested;

		private int insideHandshakeBlock;
		private uint nonceCount;
		private string origin;
		private bool preAuth;
		private bool protocolsRequested;
		private NetworkCredential proxyCredentials;
		private Uri proxyUri;
		private int retryCountForConnect;
		private bool secure;
		private ClientSslConfiguration sslConfig;

		private TcpClient tcpClient;


		/// <summary>
		///     Initializes a new instance of the <see cref="WebSocket" /> class with
		///     <paramref name="url" /> and optionally <paramref name="protocols" />.
		/// </summary>
		/// <param name="url">
		///     <para>
		///         A <see cref="string" /> that specifies the URL to which to connect.
		///     </para>
		///     <para>
		///         The scheme of the URL must be ws or wss.
		///     </para>
		///     <para>
		///         The new instance uses a secure connection if the scheme is wss.
		///     </para>
		/// </param>
		/// <param name="protocols">
		///     <para>
		///         An array of <see cref="string" /> that specifies the names of
		///         the subprotocols if necessary.
		///     </para>
		///     <para>
		///         Each value of the array must be a token defined in
		///         <see href="http://tools.ietf.org/html/rfc2616#section-2.2">
		///             RFC 2616
		///         </see>
		///         .
		///     </para>
		/// </param>
		/// <exception cref="ArgumentNullException">
		///     <paramref name="url" /> is <see langword="null" />.
		/// </exception>
		/// <exception cref="ArgumentException">
		///     <para>
		///         <paramref name="url" /> is an empty string.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         <paramref name="url" /> is an invalid WebSocket URL string.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         <paramref name="protocols" /> contains a value that is not a token.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         <paramref name="protocols" /> contains a value twice.
		///     </para>
		/// </exception>
		public ClientWebSocket(string url, params string[] protocols)
			: base(TimeSpan.FromSeconds(5))
		{
			if (url == null)
				throw new ArgumentNullException(nameof(url));

			if (url.Length == 0)
				throw new ArgumentException("An empty string.", nameof(url));

			string msg;
			if (!url.TryCreateWebSocketUri(out uri, out msg))
				throw new ArgumentException(msg, nameof(url));

			if (protocols != null && protocols.Length > 0)
			{
				if (!CheckProtocols(protocols, out msg))
					throw new ArgumentException(msg, nameof(protocols));

				this.protocols = protocols;
			}

			base64Key = CreateBase64Key();
			logger = new Logger();
			secure = uri.Scheme == "wss"; // can be changed later !?
		}


		public override bool IsSecure
		{
			get
			{
				return secure;
			}
		}


		/// <summary>
		///     Gets or sets underlying socket read or write timeout.
		/// </summary>
		public override int ReadWriteTimeout
		{
			get
			{
				return base.ReadWriteTimeout;
			}

			set
			{
				base.ReadWriteTimeout = value;

#if !XAMARIN
				if (tcpClient != null)
				{
					tcpClient.ReceiveTimeout = value;
					tcpClient.SendTimeout = value;
				}
#endif
			}
		}


		/// <summary>
		///     Gets the configuration for secure connection.
		/// </summary>
		/// <remarks>
		///     This configuration will be referenced when attempts to connect,
		///     so it must be configured before any connect method is called.
		/// </remarks>
		/// <value>
		///     A <see cref="ClientSslConfiguration" /> that represents
		///     the configuration used to establish a secure connection.
		/// </value>
		/// <exception cref="InvalidOperationException">
		///     <para>
		///         This instance is not a client.
		///     </para>
		///     <para>
		///         This instance does not use a secure connection.
		///     </para>
		/// </exception>
		public ClientSslConfiguration SslConfiguration
		{
			get
			{
				if (!secure)
				{
					throw new InvalidOperationException("This instance does not use a secure connection.");
				}

				return GetSslConfiguration();
			}
		}

		/// <summary>
		///     Gets the URL to which to connect.
		/// </summary>
		/// <value>
		///     A <see cref="Uri" /> that represents the URL to which to connect.
		/// </value>
		public override Uri Url
		{
			get
			{
				return uri;
			}
		}


		/// <summary>
		///     Gets the credentials for the HTTP authentication (Basic/Digest).
		/// </summary>
		/// <value>
		///     <para>
		///         A <see cref="NetworkCredential" /> that represents the credentials
		///         used to authenticate the client.
		///     </para>
		///     <para>
		///         The default value is <see langword="null" />.
		///     </para>
		/// </value>
		public NetworkCredential Credentials
		{
			get
			{
				return credentials;
			}
		}


		/// <summary>
		///     Gets or sets the compression method used to compress a message.
		/// </summary>
		/// <remarks>
		///     The set operation does nothing if the connection has already been
		///     established or it is closing.
		/// </remarks>
		/// <value>
		///     <para>
		///         One of the <see cref="CompressionMethod" /> enum values.
		///     </para>
		///     <para>
		///         It specifies the compression method used to compress a message.
		///     </para>
		///     <para>
		///         The default value is <see cref="CompressionMethod.None" />.
		///     </para>
		/// </value>
		/// <exception cref="InvalidOperationException">
		///     The set operation is not available if this instance is not a client.
		/// </exception>
		public CompressionMethod Compression
		{
			get
			{
				return compression;
			}

			set
			{
				if (compression == value)
					return;

				lock (forState)
				{
					if (!CanModifyConnectionProperties(out var msg))
					{
						logger.Warn(msg);
						return;
					}

					compression = value;
				}
			}
		}

		/// <summary>
		///     Gets or sets a value indicating whether the URL redirection for
		///     the handshake request is allowed.
		/// </summary>
		/// <remarks>
		///     The set operation does nothing if the connection has already been
		///     established or it is closing.
		/// </remarks>
		/// <value>
		///     <para>
		///         <c>true</c> if this instance allows the URL redirection for
		///         the handshake request; otherwise, <c>false</c>.
		///     </para>
		///     <para>
		///         The default value is <c>false</c>.
		///     </para>
		/// </value>
		/// <exception cref="InvalidOperationException">
		///     The set operation is not available if this instance is not a client.
		/// </exception>
		public bool EnableRedirection
		{
			get
			{
				return enableRedirection;
			}

			set
			{
				lock (forState)
				{
					if (!CanModifyConnectionProperties(out var msg))
					{
						logger.Warn(msg);
						return;
					}

					enableRedirection = value;
				}
			}
		}


		/// <summary>
		///     Gets or sets the value of the HTTP Origin header to send with
		///     the handshake request.
		/// </summary>
		/// <remarks>
		///     <para>
		///         The HTTP Origin header is defined in
		///         <see href="http://tools.ietf.org/html/rfc6454#section-7">
		///             Section 7 of RFC 6454
		///         </see>
		///         .
		///     </para>
		///     <para>
		///         This instance sends the Origin header if this property has any.
		///     </para>
		///     <para>
		///         The set operation does nothing if the connection has already been
		///         established or it is closing.
		///     </para>
		/// </remarks>
		/// <value>
		///     <para>
		///         A <see cref="string" /> that represents the value of the Origin
		///         header to send.
		///     </para>
		///     <para>
		///         The syntax is &lt;scheme&gt;://&lt;host&gt;[:&lt;port&gt;].
		///     </para>
		///     <para>
		///         The default value is <see langword="null" />.
		///     </para>
		/// </value>
		/// <exception cref="InvalidOperationException">
		///     The set operation is not available if this instance is not a client.
		/// </exception>
		/// <exception cref="ArgumentException">
		///     <para>
		///         The value specified for a set operation is not an absolute URI string.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         The value specified for a set operation includes the path segments.
		///     </para>
		/// </exception>
		public string Origin
		{
			get
			{
				return origin;
			}

			set
			{
				if (!value.IsNullOrEmpty())
				{
					if (!Uri.TryCreate(value, UriKind.Absolute, out var result))
					{
						throw new ArgumentException("Not an absolute URI string.", nameof(value));
					}

					if (result.Segments.Length > 1)
					{
						throw new ArgumentException("It includes the path segments.", nameof(value));
					}
				}

				lock (forState)
				{
					if (!CanModifyConnectionProperties(out var msg))
					{
						logger.Warn(msg);
						return;
					}

					origin = !value.IsNullOrEmpty() ? value.TrimEnd('/') : value;
				}
			}
		}


		private static bool CheckProtocols(string[] protocols, out string message)
		{
			message = null;

			if (protocols.Contains(protocol => protocol.IsNullOrEmpty() || !protocol.IsToken()))
			{
				message = "It contains a value that is not a token.";
				return false;
			}

			if (protocols.ContainsTwice())
			{
				message = "It contains a value twice.";
				return false;
			}

			return true;
		}


		protected override void MessageHandler(MessageEventArgs e)
		{
			for (; ; )
			{
				CallOnMessage(e);

				e = DequeueNextMessage();
				if (e == null)
					break;
			}
		}


		private bool CheckHandshakeResponse(HttpResponse response, out string message)
		{
			message = null;

			if (response.IsRedirect)
			{
				message = "Indicates the redirection.";
				return false;
			}

			if (response.IsUnauthorized)
			{
				message = "Requires the authentication.";
				return false;
			}

			if (!response.IsWebSocketResponse)
			{
				message = "Not a WebSocket handshake response.";
				return false;
			}

			var headers = response.Headers;
			if (!ValidateSecWebSocketAcceptHeader(headers["Sec-WebSocket-Accept"]))
			{
				message = "Includes no Sec-WebSocket-Accept header, or it has an invalid value.";
				return false;
			}

			if (!ValidateSecWebSocketProtocolServerHeader(headers["Sec-WebSocket-Protocol"]))
			{
				message = "Includes no Sec-WebSocket-Protocol header, or it has an invalid value.";
				return false;
			}

			if (!ValidateSecWebSocketExtensionsServerHeader(headers["Sec-WebSocket-Extensions"]))
			{
				message = "Includes an invalid Sec-WebSocket-Extensions header.";
				return false;
			}

			if (!ValidateSecWebSocketVersionServerHeader(headers["Sec-WebSocket-Version"]))
			{
				message = "Includes an invalid Sec-WebSocket-Version header.";
				return false;
			}

			return true;
		}


		// As client
		private bool PerformConnectSequence()
		{
			bool TryEnterHandshakeBlock()
			{
				// if (insideHandshakeBlock == 0) insideHandshakeBlock = 1
				// returns previous value
				return Interlocked.CompareExchange(ref insideHandshakeBlock, 1, 0) > 0;
			}

			{
				var errorAction = 0;

				lock (forState)
				{
					if (readyState == WebSocketState.Open)
						errorAction = 1;
					else if (readyState == WebSocketState.Closing)
						errorAction = 2;
					else if (retryCountForConnect > maxRetryCountForConnect)
						errorAction = 3;

					readyState = WebSocketState.Connecting;
				} // lock

				// do this outside lock
				switch (errorAction)
				{
					case 1:
						logger.Warn("The connection has already been established.");
						return false;
					case 2:
						logger.Error("The close process has set in.");
						CallOnError("An interruption has occurred while attempting to connect.", null);
						return false;
					case 3:
						logger.Error("An opportunity for reconnecting has been lost.");
						CallOnError("An interruption has occurred while attempting to connect.", null);
						return false;
				}
			}

			if (TryEnterHandshakeBlock())
			{
				// alredy in the handshake.. What does it do here twice at all.
				Fatal("Connect - doHandshake doing it twice!", null);
				return false;
			}

			try
			{
				// this acquires send lock
				// i'll release _forState lock and then acquire it after
				// and protect for double parallel call of doHandshake with interlocked

				DoHandshake();
			}
			catch (Exception ex)
			{
				lock (forState)
				{
					retryCountForConnect++;
				}

				logger.Fatal(ex.Message);
				logger.Debug(ex.ToString());

				Fatal("An exception has occurred while attempting to connect.", ex);

				return false;
			}
			finally
			{
				insideHandshakeBlock = 0;
			}

			lock (forState)
			{
				if (readyState != WebSocketState.Connecting)
				{
					Fatal($"Socket state error, expected Connecting, was: {readyState}", null);

					return false;
				}

				retryCountForConnect = 1;
				readyState = WebSocketState.Open;
				return true;
			} // lock
		}


		// As client
		private string CreateExtensions()
		{
			var buff = new StringBuilder(80);

			var compressionMethod = compression;

			if (compressionMethod != CompressionMethod.None)
			{
				var str = compressionMethod.ToExtensionString("server_no_context_takeover", "client_no_context_takeover");

				buff.AppendFormat("{0}, ", str);
			}

			var len = buff.Length;
			if (len > 2)
			{
				buff.Length = len - 2;
				return buff.ToString();
			}

			return null;
		}


		// As client
		private void DoHandshake()
		{
			SetClientStream();
			var res = SendHandshakeRequest();

			string msg;
			if (!CheckHandshakeResponse(res, out msg))
				throw new WebSocketException(CloseStatusCode.ProtocolError, msg);

			if (protocolsRequested)
			{
				var resHeader = res.Headers["Sec-WebSocket-Protocol"];
				protocol = resHeader;
			}

			if (extensionsRequested)
			{
				var resHeader = res.Headers["Sec-WebSocket-Extensions"];

				if (resHeader != null)
				{
					extensions = resHeader;
				}
				else
				{
					compression = CompressionMethod.None;
				}
			}

			AssignCookieCollection(res.Cookies);
		}


		// As client
		private HttpRequest CreateHandshakeRequest()
		{
			var ret = HttpRequest.CreateWebSocketRequest(uri);

			var headers = ret.Headers;
			if (!origin.IsNullOrEmpty())
				headers["Origin"] = origin;

			headers["Sec-WebSocket-Key"] = base64Key;

			protocolsRequested = protocols != null;
			if (protocolsRequested)
				headers["Sec-WebSocket-Protocol"] = protocols.ToString(", ");

			extensionsRequested = compression != CompressionMethod.None;
			if (extensionsRequested)
				headers["Sec-WebSocket-Extensions"] = CreateExtensions();

			headers["Sec-WebSocket-Version"] = version;

			AuthenticationResponse authRes = null;
			if (authChallenge != null && credentials != null)
			{
				authRes = new AuthenticationResponse(authChallenge, credentials, nonceCount);
				nonceCount = authRes.NonceCount;
			}
			else if (preAuth)
			{
				authRes = new AuthenticationResponse(credentials);
			}

			if (authRes != null)
				headers["Authorization"] = authRes.ToString();

			SetRequestCookies(ret);

			return ret;
		}


		// As client
		private void SendProxyConnectRequest()
		{
			var req = HttpRequest.CreateConnectRequest(uri);
			var res = SendHttpRequest(req, 90000);
			if (res.IsProxyAuthenticationRequired)
			{
				var chal = res.Headers["Proxy-Authenticate"];
				logger.Warn($"Received a proxy authentication requirement for '{chal}'.");

				if (chal.IsNullOrEmpty())
					throw new WebSocketException("No proxy authentication challenge is specified.");

				var authChal = AuthenticationChallenge.Parse(chal);
				if (authChal == null)
					throw new WebSocketException("An invalid proxy authentication challenge is specified.");

				if (proxyCredentials != null)
				{
					if (res.HasConnectionClose)
					{
						ReleaseClientResources(true);
						tcpClient = ConnectTcpClient(proxyUri.DnsSafeHost, proxyUri.Port, ConnectTimeout, ReadWriteTimeout);
						socketStream = tcpClient.GetStream();
					}

					var authRes = new AuthenticationResponse(authChal, proxyCredentials, 0);
					req.Headers["Proxy-Authorization"] = authRes.ToString();
					res = SendHttpRequest(req, 15000);
				}

				if (res.IsProxyAuthenticationRequired)
					throw new WebSocketException("A proxy authentication is required.");
			}

			if (res.StatusCode[0] != '2')
				throw new WebSocketException("The proxy has failed a connection to the requested host and port.");
		}


		// As client
		private void SetClientStream()
		{
			if (proxyUri != null)
			{
				tcpClient = ConnectTcpClient(proxyUri.DnsSafeHost, proxyUri.Port, ConnectTimeout, ReadWriteTimeout);
				socketStream = tcpClient.GetStream();
				SendProxyConnectRequest();
			}
			else
			{
				tcpClient = ConnectTcpClient(uri.DnsSafeHost, uri.Port, ConnectTimeout, ReadWriteTimeout);
				socketStream = tcpClient.GetStream();
			}

			if (secure)
			{
				var conf = GetSslConfiguration();
				var host = conf.TargetHost;
				if (host != uri.DnsSafeHost)
					throw new WebSocketException(CloseStatusCode.TlsHandshakeFailure, "An invalid host name is specified.");

				try
				{
					var sslStream = new SslStream(
												  socketStream,
												  false,
												  conf.ServerCertificateValidationCallback,
												  conf.ClientCertificateSelectionCallback);

					sslStream.AuthenticateAsClient(
												   host,
												   conf.ClientCertificates,
												   conf.EnabledSslProtocols,
												   conf.CheckCertificateRevocation);

					socketStream = sslStream;
				}
				catch (Exception ex)
				{
					throw new WebSocketException(CloseStatusCode.TlsHandshakeFailure, ex);
				}
			}
		}


		// As client


		// As client
		private void ReleaseClientResources(bool dispose)
		{
			try
			{
				if (dispose)
					socketStream?.Dispose();
			}
			catch
			{
			}

			socketStream = null;

			try
			{
				if (dispose)
					tcpClient?.Close();
			}
			catch
			{
			}

			tcpClient = null;
		}


		private static void OnEndConnect(IAsyncResult asyncResult)
		{
			var state = (TcpClientAsyncState)asyncResult.AsyncState;

			try
			{
				state.Client?.EndConnect(asyncResult);
			}
			catch (ObjectDisposedException)
			{
			}
			catch (Exception e)
			{
				// this catches for example DNS lookup failures
				state.Exception = e;
			}

			try
			{
				asyncResult.AsyncWaitHandle.Close();
			}
			catch
			{
			}

			try
			{
				state.EndConnectSignal.Set();
			}
			catch
			{
				// could be disposed already
			}
		}


		// ReSharper disable once UnusedParameter.Local
		private static TcpClient ConnectTcpClient(string hostname, int port, int connectTimeout, int readWriteTimeout)
		{
#if XAMARIN
			var client = new TcpClient(AddressFamily.InterNetworkV6);
#else
			var client = new TcpClient();
#endif
			using (var endConnectSignal = new ManualResetEvent(false))
			{
				var state = new TcpClientAsyncState
				{
					Client = client,
					EndConnectSignal = endConnectSignal
				};

				var result = client.BeginConnect(hostname, port, OnEndConnect, state);
				// this one:
				// bool success = result.AsyncWaitHandle.WaitOne(connectTimeout, true);
				// does not work reliably, because
				// result.AsyncWaitHandle is signalled sooner than EndConnect is 
				// on Mono, MD reported it is set even before connected = true;

				// the solution below is neither modern nor exciting but it should work
				// and not lose exception messages in endconnect which help us with troubleshooting on location

				try
				{
					var sw = new Stopwatch();
					sw.Restart();

					var waitOk = result.CompletedSynchronously || endConnectSignal.WaitOne(connectTimeout, true);
					endConnectSignal.Close();
					sw.Stop();

					// waitOk does not mean it is connected..
					// it means that the wait completed before timeout, meaning there was maybe an exception in EndConnect

					if (client.Connected && state.Exception == null && waitOk)
					{
						// Debug.Print($"Connection looks good! {hostname}:{port}");
					}
					else
					{
						var spent = sw.ElapsedMilliseconds;

						try
						{
							client.Close(); // can this throw?
						}
						catch
						{
						}

						if (state.Exception != null) // there was an exception in endconnect.... I did not want to put it into inner exception, logging then takes more effort and space 
							throw state.Exception;
						else if (!waitOk)
							throw new TimeoutException($"Failed to connect to server {hostname}:{port} timeout={connectTimeout} spent={spent}ms");
						else
							throw new TimeoutException($"Failed to connect to server {hostname}:{port} not connected (!) spent={spent}ms");
					}
				}
				catch (ObjectDisposedException)
				{
					// toto: log
				}
			} // using

#if !XAMARIN
			client.ReceiveTimeout = readWriteTimeout;
			client.SendTimeout = readWriteTimeout;
#endif

			return client;
		}


		// As client
		private bool ValidateSecWebSocketAcceptHeader(string value)
		{
			return value != null && value == CreateResponseKey(base64Key);
		}


		// As client
		private bool ValidateSecWebSocketExtensionsServerHeader(string value)
		{
			if (value == null)
				return true;

			if (value.Length == 0)
				return false;

			if (!extensionsRequested)
				return false;

			var compressionMethod = compression;
			var comp = compressionMethod != CompressionMethod.None;
			foreach (var e in value.SplitHeaderValue(','))
			{
				var ext = e.Trim();
				if (comp && ext.IsCompressionExtension(compressionMethod))
				{
					if (!ext.Contains("server_no_context_takeover"))
					{
						logger.Error("The server hasn't sent back 'server_no_context_takeover'.");
						return false;
					}

					if (!ext.Contains("client_no_context_takeover"))
						logger.Warn("The server hasn't sent back 'client_no_context_takeover'.");

					var method = compressionMethod.ToExtensionString();
					var invalid =
						ext.SplitHeaderValue(';').Contains(
														   t =>
														   {
															   t = t.Trim();
															   return t != method
																	  && t != "server_no_context_takeover"
																	  && t != "client_no_context_takeover";
														   }
														  );

					if (invalid)
						return false;
				}
				else
				{
					return false;
				}
			}

			return true;
		}


		// As client
		private bool ValidateSecWebSocketProtocolServerHeader(string value)
		{
			if (value == null)
				return !protocolsRequested;

			if (value.Length == 0)
				return false;

			return protocolsRequested && protocols.Contains(p => p == value);
		}


		// As client
		private bool ValidateSecWebSocketVersionServerHeader(string value)
		{
			return value == null || value == version;
		}


		// As client
		private HttpResponse SendHandshakeRequest()
		{
			var req = CreateHandshakeRequest();
			var res = SendHttpRequest(req, 90000);
			if (res.IsUnauthorized)
			{
				var chal = res.Headers["WWW-Authenticate"];
				logger.Warn($"Received an authentication requirement for '{chal}'.");
				if (chal.IsNullOrEmpty())
				{
					logger.Error("No authentication challenge is specified.");
					return res;
				}

				authChallenge = AuthenticationChallenge.Parse(chal);
				if (authChallenge == null)
				{
					logger.Error("An invalid authentication challenge is specified.");
					return res;
				}

				if (credentials != null &&
					(!preAuth || authChallenge.Scheme == AuthenticationSchemes.Digest))
				{
					if (res.HasConnectionClose)
					{
						ReleaseClientResources(true);
						SetClientStream();
					}

					var authRes = new AuthenticationResponse(authChallenge, credentials, nonceCount);
					nonceCount = authRes.NonceCount;
					req.Headers["Authorization"] = authRes.ToString();
					res = SendHttpRequest(req, 15000);
				}
			}

			if (res.IsRedirect)
			{
				var url = res.Headers["Location"];
				logger.Warn($"Received a redirection to '{url}'.");
				if (enableRedirection)
				{
					if (url.IsNullOrEmpty())
					{
						logger.Error("No url to redirect is located.");
						return res;
					}

					if (!url.TryCreateWebSocketUri(out var result, out var msg))
					{
						logger.Error("An invalid url to redirect is located: " + msg);
						return res;
					}

					ReleaseClientResources(true);

					this.uri = result;
					secure = result.Scheme == "wss";

					SetClientStream();
					return SendHandshakeRequest();
				}
			}

			return res;
		}


		// As client
		private HttpResponse SendHttpRequest(HttpRequest request, int millisecondsTimeout)
		{
			logger.Debug($"A request to the server: {request}");
			var res = request.GetResponse(socketStream, millisecondsTimeout);
			logger.Debug($"A response to this request: {res}");

			return res;
		}


		private ClientSslConfiguration GetSslConfiguration()
		{
			if (sslConfig == null)
				sslConfig = new ClientSslConfiguration(uri.DnsSafeHost);

			return sslConfig;
		}


		private protected override void PerformCloseSequence(PayloadData payloadData, bool send, bool receive, bool received)
		{
			Stream streamForLater;
			ManualResetEvent receivingExitedForLater;
			TcpClient tcpClientForLater;

			bool DoClosingHandshake()
			{
				var clean = false;

				try
				{
					clean = CloseHandshake(streamForLater, receivingExitedForLater, payloadData, send, receive, received);
				}
				catch
				{
				}

				try
				{
					streamForLater?.Dispose();
				}
				catch
				{
				}

				try
				{
					tcpClientForLater?.Close();
				}
				catch
				{
				}

				try
				{
					receivingExitedForLater?.Dispose();
				}
				catch
				{
				}

				return clean;
			}

			lock (forState)
			{
				if (readyState == WebSocketState.Closing)
				{
					logger.Info("The closing is already in progress.");
					return;
				}

				if (readyState == WebSocketState.Closed)
				{
					logger.Info("The connection has already been closed.");
					return;
				}

				send = send && readyState == WebSocketState.Open;
				receive = send && receive;

				readyState = WebSocketState.Closing;

				streamForLater = socketStream;
				tcpClientForLater = tcpClient;
				receivingExitedForLater = receivingExitedEvent;

				ReleaseClientResources(false); // no disposal

				ReleaseCommonResources(false); // no disposal of _receivingExited

				readyState = WebSocketState.Closed;
			} // lock

			logger.Trace("Begin closing the connection.");

			// call outside lock
			var wasClean = DoClosingHandshake();

			logger.Trace("End closing the connection.");

			CallOnClose(new CloseEventArgs(payloadData)
			{
				WasClean = wasClean
			});
		}


		internal static string CreateBase64Key()
		{
			var src = new byte[16];
			RandomNumber.GetBytes(src);

			return Convert.ToBase64String(src);
		}


		/// <summary>
		///     Establishes a connection.
		/// </summary>
		/// <remarks>
		///     This method does nothing if the connection has already been established.
		/// </remarks>
		/// <exception cref="InvalidOperationException">
		///     <para>
		///         This instance is not a client.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         The close process is in progress.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         A series of reconnecting has failed.
		///     </para>
		/// </exception>
		public void Connect()
		{
			if (readyState == WebSocketState.Closing)
			{
				throw new InvalidOperationException("The close process is in progress.");
			}

			if (retryCountForConnect > maxRetryCountForConnect)
			{
				throw new InvalidOperationException("A series of reconnecting has failed.");
			}

			if (PerformConnectSequence())
				open();
		}


		/// <summary>
		///     Establishes a connection asynchronously.
		/// </summary>
		/// <remarks>
		///     <para>
		///         This method does not wait for the connect process to be complete.
		///     </para>
		///     <para>
		///         This method does nothing if the connection has already been
		///         established.
		///     </para>
		/// </remarks>
		/// <exception cref="InvalidOperationException">
		///     <para>
		///         This instance is not a client.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         The close process is in progress.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         A series of reconnecting has failed.
		///     </para>
		/// </exception>
		public void ConnectAsync()
		{
			if (readyState == WebSocketState.Closing)
			{
				throw new InvalidOperationException("The close process is in progress.");
			}

			if (retryCountForConnect > maxRetryCountForConnect)
			{
				throw new InvalidOperationException("A series of reconnecting has failed.");
			}

#if NET_CORE
			var task = System.Threading.Tasks.Task.Factory.StartNew(PerformConnectSequence);

			task.ContinueWith((t) =>
			{
				if (!t.IsFaulted && t.Exception == null && t.Result)
				{
					open();
				}
				else
				{
					PerformCloseSequence(1006, "could not open");
				}
			});
#else
			Func<bool> connector = PerformConnectSequence;

			connector.BeginInvoke(
								  ar =>
								  {
									  if (connector.EndInvoke(ar))
										  open();
								  },
								  null
								 );
#endif
		}


		/// <summary>
		///     Sets the credentials for the HTTP authentication (Basic/Digest).
		/// </summary>
		/// <remarks>
		///     This method does nothing if the connection has already been
		///     established or it is closing.
		/// </remarks>
		/// <param name="username">
		///     <para>
		///         A <see cref="string" /> that represents the username associated with
		///         the credentials.
		///     </para>
		///     <para>
		///         <see langword="null" /> or an empty string if initializes
		///         the credentials.
		///     </para>
		/// </param>
		/// <param name="password">
		///     <para>
		///         A <see cref="string" /> that represents the password for the username
		///         associated with the credentials.
		///     </para>
		///     <para>
		///         <see langword="null" /> or an empty string if not necessary.
		///     </para>
		/// </param>
		/// <param name="isPreAuth">
		///     <c>true</c> if sends the credentials for the Basic authentication in
		///     advance with the first handshake request; otherwise, <c>false</c>.
		/// </param>
		/// <exception cref="InvalidOperationException">
		///     This instance is not a client.
		/// </exception>
		/// <exception cref="ArgumentException">
		///     <para>
		///         <paramref name="username" /> contains an invalid character.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         <paramref name="password" /> contains an invalid character.
		///     </para>
		/// </exception>
		public void SetCredentials(string username, string password, bool isPreAuth)
		{
			if (!username.IsNullOrEmpty())
			{
				if (username.Contains(':') || !username.IsText())
				{
					throw new ArgumentException("It contains an invalid character.", nameof(username));
				}
			}

			if (!password.IsNullOrEmpty())
			{
				if (!password.IsText())
				{
					throw new ArgumentException("It contains an invalid character.", nameof(password));
				}
			}

			if (!CanModifyConnectionProperties(out var msg))
			{
				logger.Warn(msg);
				return;
			}

			lock (forState)
			{
				if (!CanModifyConnectionProperties(out msg))
				{
					logger.Warn(msg);
					return;
				}

				if (username.IsNullOrEmpty())
				{
					credentials = null;
					this.preAuth = false;

					return;
				}

				credentials = new NetworkCredential(
													username, password, uri.PathAndQuery
												   );

				this.preAuth = isPreAuth;
			}
		}


		/// <summary>
		///     Sets the URL of the HTTP proxy server through which to connect and
		///     the credentials for the HTTP proxy authentication (Basic/Digest).
		/// </summary>
		/// <remarks>
		///     This method does nothing if the connection has already been
		///     established or it is closing.
		/// </remarks>
		/// <param name="url">
		///     <para>
		///         A <see cref="string" /> that represents the URL of the proxy server
		///         through which to connect.
		///     </para>
		///     <para>
		///         The syntax is http://&lt;host&gt;[:&lt;port&gt;].
		///     </para>
		///     <para>
		///         <see langword="null" /> or an empty string if initializes the URL and
		///         the credentials.
		///     </para>
		/// </param>
		/// <param name="username">
		///     <para>
		///         A <see cref="string" /> that represents the username associated with
		///         the credentials.
		///     </para>
		///     <para>
		///         <see langword="null" /> or an empty string if the credentials are not
		///         necessary.
		///     </para>
		/// </param>
		/// <param name="password">
		///     <para>
		///         A <see cref="string" /> that represents the password for the username
		///         associated with the credentials.
		///     </para>
		///     <para>
		///         <see langword="null" /> or an empty string if not necessary.
		///     </para>
		/// </param>
		/// <exception cref="InvalidOperationException">
		///     This instance is not a client.
		/// </exception>
		/// <exception cref="ArgumentException">
		///     <para>
		///         <paramref name="url" /> is not an absolute URI string.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         The scheme of <paramref name="url" /> is not http.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         <paramref name="url" /> includes the path segments.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         <paramref name="username" /> contains an invalid character.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         <paramref name="password" /> contains an invalid character.
		///     </para>
		/// </exception>
		public void SetProxy(string url, string username, string password)
		{
			string msg;

			Uri theUri = null;

			if (!url.IsNullOrEmpty())
			{
				if (!Uri.TryCreate(url, UriKind.Absolute, out theUri))
				{
					throw new ArgumentException("Not an absolute URI string.", nameof(url));
				}

				if (theUri.Scheme != "http")
				{
					throw new ArgumentException("The scheme part is not http.", nameof(url));
				}

				if (theUri.Segments.Length > 1)
				{
					throw new ArgumentException("It includes the path segments.", nameof(url));
				}
			}

			if (!username.IsNullOrEmpty())
			{
				if (username.Contains(':') || !username.IsText())
				{
					throw new ArgumentException("It contains an invalid character.", nameof(username));
				}
			}

			if (!password.IsNullOrEmpty())
			{
				if (!password.IsText())
				{
					throw new ArgumentException("It contains an invalid character.", nameof(password));
				}
			}

			if (!CanModifyConnectionProperties(out msg))
			{
				logger.Warn(msg);
				return;
			}

			lock (forState)
			{
				if (!CanModifyConnectionProperties(out msg))
				{
					logger.Warn(msg);
					return;
				}

				if (url.IsNullOrEmpty())
				{
					proxyUri = null;
					proxyCredentials = null;

					return;
				}

				proxyUri = theUri;
				proxyCredentials = !username.IsNullOrEmpty() ? new NetworkCredential(username, password, $"{this.uri.DnsSafeHost}:{this.uri.Port}") : null;
			}
		}


		private protected override WebSocketFrame CreateCloseFrame(PayloadData payloadData)
		{
			return WebSocketFrame.CreateCloseFrame(payloadData, true);
		}


		private protected override WebSocketFrame CreatePongFrame(PayloadData payloadData)
		{
			return WebSocketFrame.CreatePongFrame(payloadData, true);
		}


		private protected override WebSocketFrame CreateFrame(Fin fin, Opcode opcode, byte[] data, bool compressed)
		{
			return new WebSocketFrame(fin, opcode, data, compressed, true);
		}


		private protected override void CheckCode(ushort code)
		{
			if (code == 1011)
			{
				throw new ArgumentException("1011 cannot be used.", nameof(code));
			}
		}


		private protected override void CheckCloseStatus(CloseStatusCode code)
		{
			if (code == CloseStatusCode.ServerError)
			{
				throw new ArgumentException("ServerError cannot be used.", nameof(code));
			}
		}


		private protected override string CheckFrameMask(WebSocketFrame frame)
		{
			if (frame.IsMasked)
			{
				return "A frame from the server is masked.";
			}

			return null;
		}


		private protected override void UnmaskFrame(WebSocketFrame frame)
		{
			frame.Unmask();
		}


		private class TcpClientAsyncState
		{
			public TcpClient Client;
			public ManualResetEvent EndConnectSignal;
			public Exception Exception;
		}
	}
}
