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

namespace WebSocketSharp
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
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
		internal const int FragmentLength = 1016; // Max value is int.MaxValue - 14.

		private const string GuidId = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
		private const string Version = "13";

		private readonly bool _client;

		private AuthenticationChallenge _authChallenge;
		private string _base64Key;
		private RemoteCertificateValidationCallback _certValidationCallback;
		private Action _closeContext;
		private CompressionMethod _compression;
		private WebSocketContext _context;
		private CookieCollection _cookies;
		private NetworkCredential _credentials;
		private string _extensions;
		private AutoResetEvent _exitReceiving;
		private object _forConn;
		private object _forEvent;
		private object _forMessageEventQueue;
		private object _forSend;
		private Func<WebSocketContext, string> _handshakeRequestChecker;
		private Queue<MessageEventArgs> _messageEventQueue;
		private uint _nonceCount;
		private string _origin;
		private bool _preAuth;
		private string _protocol;
		private string[] _protocols;
		private NetworkCredential _proxyCredentials;
		private Uri _proxyUri;
		private volatile WebSocketState _readyState;
		private AutoResetEvent _receivePong;
		private bool _secure;
		private Stream _stream;
		private TcpClient _tcpClient;
		private Uri _uri;
		
		/// <summary>
		/// Initializes a new instance of the <see cref="WebSocket"/> class with the specified
		/// WebSocket URL and subprotocols.
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
			: this()
		{
			if (url == null)
				throw new ArgumentNullException("url");

			string msg;
			if (!url.TryCreateWebSocketUri(out _uri, out msg))
				throw new ArgumentException(msg, "url");

			if (protocols != null && protocols.Length > 0)
			{
				msg = protocols.CheckIfValidProtocols();
				if (msg != null)
					throw new ArgumentException(msg, "protocols");

				_protocols = protocols;
			}

			_base64Key = CreateBase64Key();
			_client = true;
			_secure = _uri.Scheme == "wss";

			Init();
		}

		// As server
		internal WebSocket(HttpListenerWebSocketContext context, string protocol)
			: this()
		{
			_context = context;
			_protocol = protocol;

			_closeContext = context.Close;
			_secure = context.IsSecureConnection;
			_stream = context.Stream;

			Init();
		}

		// As server
		internal WebSocket(TcpListenerWebSocketContext context, string protocol)
			: this()
		{
			_context = context;
			_protocol = protocol;

			_closeContext = context.Close;
			_secure = context.IsSecureConnection;
			_stream = context.Stream;

			Init();
		}

		private WebSocket()
		{
			_exitReceiving = new AutoResetEvent(false);
			_receivePong = new AutoResetEvent(false);
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
		/// Gets or sets the compression method used to compress the message on the WebSocket
		/// connection.
		/// </summary>
		/// <value>
		/// One of the <see cref="CompressionMethod"/> enum values, indicates the compression method
		/// used to compress the message. The default value is <see cref="CompressionMethod.None"/>.
		/// </value>
		public CompressionMethod Compression
		{
			get
			{
				return _compression;
			}

			set
			{
				lock (_forConn)
				{
					var msg = this.CheckIfAvailable("Set operation of Compression", false, false);
					if (msg != null)
					{
						Error(msg);

						return;
					}

					_compression = value;
				}
			}
		}

		/// <summary>
		/// Gets the HTTP cookies included in the WebSocket connection request and response.
		/// </summary>
		/// <value>
		/// An IEnumerable&lt;Cookie&gt; instance that provides an enumerator which supports the
		/// iteration over the collection of the cookies.
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

		/// <summary>
		/// Gets the credentials for the HTTP authentication (Basic/Digest).
		/// </summary>
		/// <value>
		/// A <see cref="NetworkCredential"/> that represents the credentials for the HTTP
		/// authentication. The default value is <see langword="null"/>.
		/// </value>
		public NetworkCredential Credentials
		{
			get
			{
				return _credentials;
			}
		}

		/// <summary>
		/// Gets the WebSocket extensions selected by the server.
		/// </summary>
		/// <value>
		/// A <see cref="string"/> that represents the extensions if any. The default value is
		/// <see cref="string.Empty"/>.
		/// </value>
		public string Extensions
		{
			get
			{
				return _extensions ?? string.Empty;
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
		/// Gets or sets the value of the Origin header to send with the WebSocket connection request
		/// to the server.
		/// </summary>
		/// <remarks>
		/// The <see cref="WebSocket"/> sends the Origin header if this property has any.
		/// </remarks>
		/// <value>
		///   <para>
		///   A <see cref="string"/> that represents the value of the
		///   <see href="http://tools.ietf.org/html/rfc6454#section-7">HTTP Origin
		///   header</see> to send. The default value is <see langword="null"/>.
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
					var msg = this.CheckIfAvailable("Set operation of Origin", false, false);
					if (msg == null)
					{
						if (value.IsNullOrEmpty())
						{
							_origin = value;
							return;
						}

						Uri origin;
						if (!Uri.TryCreate(value, UriKind.Absolute, out origin) || origin.Segments.Length > 1)
							msg = "The syntax of Origin must be '<scheme>://<host>[:<port>]'.";
					}

					if (msg != null)
					{
						Error(msg);

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
		/// A <see cref="string"/> that represents the subprotocol if any. The default value is
		/// <see cref="string.Empty"/>.
		/// </value>
		public string Protocol
		{
			get
			{
				return _protocol ?? string.Empty;
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

		/// <summary>
		/// Gets or sets the callback used to validate the certificate supplied by the server.
		/// </summary>
		/// <remarks>
		/// If the value of this property is <see langword="null"/>, the validation does nothing with
		/// the server certificate, always returns valid.
		/// </remarks>
		/// <value>
		/// A <see cref="RemoteCertificateValidationCallback"/> delegate that references the method(s)
		/// used to validate the server certificate. The default value is <see langword="null"/>.
		/// </value>
		public RemoteCertificateValidationCallback ServerCertificateValidationCallback
		{
			get
			{
				return _certValidationCallback;
			}

			set
			{
				lock (_forConn)
				{
					var msg = this.CheckIfAvailable("Set operation of ServerCertificateValidationCallback", false, false);

					if (msg != null)
					{
						Error(msg);

						return;
					}

					_certValidationCallback = value;
				}
			}
		}

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

		public TimeSpan WaitTime { get; set; }

		/// <summary>
		/// Closes the WebSocket connection, and releases all associated resources.
		/// </summary>
		public void Close()
		{
			var msg = _readyState.CheckIfClosable();
			if (msg != null)
			{
				Error(msg);

				return;
			}

			var send = _readyState == WebSocketState.Open;
			this.InnerClose(new PayloadData(), send, send);
		}

		/// <summary>
		/// Closes the WebSocket connection with the specified <see cref="ushort"/>,
		/// and releases all associated resources.
		/// </summary>
		/// <remarks>
		/// This method emits a <see cref="OnError"/> event if <paramref name="code"/>
		/// isn't in the allowable range of the WebSocket close status code.
		/// </remarks>
		/// <param name="code">
		/// A <see cref="ushort"/> that represents the status code indicating the reason for the close.
		/// </param>
		public void Close(ushort code)
		{
			Close(code, null);
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
			Close(code, null);
		}

		/// <summary>
		/// Closes the WebSocket connection with the specified <see cref="ushort"/>
		/// and <see cref="string"/>, and releases all associated resources.
		/// </summary>
		/// <remarks>
		/// This method emits a <see cref="OnError"/> event if <paramref name="code"/>
		/// isn't in the allowable range of the WebSocket close status code or the size
		/// of <paramref name="reason"/> is greater than 123 bytes.
		/// </remarks>
		/// <param name="code">
		/// A <see cref="ushort"/> that represents the status code indicating the reason for the close.
		/// </param>
		/// <param name="reason">
		/// A <see cref="string"/> that represents the reason for the close.
		/// </param>
		public void Close(ushort code, string reason)
		{
			byte[] data = null;
			var msg = _readyState.CheckIfClosable() ??
					  code.CheckIfValidCloseStatusCode() ??
					  (data = code.Append(reason)).CheckIfValidControlData("reason");

			if (msg != null)
			{
				Error(msg, null);

				return;
			}

			var send = _readyState == WebSocketState.Open && !code.IsReserved();
			this.InnerClose(new PayloadData(data), send, send);
		}

		/// <summary>
		/// Closes the WebSocket connection with the specified <see cref="CloseStatusCode"/>
		/// and <see cref="string"/>, and releases all associated resources.
		/// </summary>
		/// <remarks>
		/// This method emits a <see cref="OnError"/> event if the size
		/// of <paramref name="reason"/> is greater than 123 bytes.
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
			byte[] data = null;
			var msg = _readyState.CheckIfClosable() ??
					  (data = ((ushort)code).Append(reason)).CheckIfValidControlData("reason");

			if (msg != null)
			{
				Error(msg, null);

				return;
			}

			var send = _readyState == WebSocketState.Open && !code.IsReserved();
			this.InnerClose(new PayloadData(data), send, send);
		}

		/// <summary>
		/// Closes the WebSocket connection asynchronously, and releases all associated resources.
		/// </summary>
		/// <remarks>
		/// This method doesn't wait for the close to be complete.
		/// </remarks>
		public async Task CloseAsync()
		{
			var msg = _readyState.CheckIfClosable();
			if (msg != null)
			{
				Error(msg);

				return;
			}

			var send = _readyState == WebSocketState.Open;
			await this.InnerCloseAsync(new PayloadData(), send, send);
		}

		/// <summary>
		/// Closes the WebSocket connection asynchronously with the specified <see cref="ushort"/>,
		/// and releases all associated resources.
		/// </summary>
		/// <remarks>
		///   <para>
		///   This method doesn't wait for the close to be complete.
		///   </para>
		///   <para>
		///   This method emits a <see cref="OnError"/> event if <paramref name="code"/>
		///   isn't in the allowable range of the WebSocket close status code.
		///   </para>
		/// </remarks>
		/// <param name="code">
		/// A <see cref="ushort"/> that represents the status code indicating the reason for the close.
		/// </param>
		public Task CloseAsync(ushort code)
		{
			return CloseAsync(code, null);
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
			return CloseAsync(code, null);
		}

		/// <summary>
		/// Closes the WebSocket connection asynchronously with the specified <see cref="ushort"/>
		/// and <see cref="string"/>, and releases all associated resources.
		/// </summary>
		/// <remarks>
		///   <para>
		///   This method doesn't wait for the close to be complete.
		///   </para>
		///   <para>
		///   This method emits a <see cref="OnError"/> event if <paramref name="code"/>
		///   isn't in the allowable range of the WebSocket close status code or the size
		///   of <paramref name="reason"/> is greater than 123 bytes.
		///   </para>
		/// </remarks>
		/// <param name="code">
		/// A <see cref="ushort"/> that represents the status code indicating the reason for the close.
		/// </param>
		/// <param name="reason">
		/// A <see cref="string"/> that represents the reason for the close.
		/// </param>
		public async Task CloseAsync(ushort code, string reason)
		{
			byte[] data = null;
			var msg = _readyState.CheckIfClosable() ??
					  code.CheckIfValidCloseStatusCode() ??
					  (data = code.Append(reason)).CheckIfValidControlData("reason");

			if (msg != null)
			{
				Error(msg, null);

				return;
			}

			var send = _readyState == WebSocketState.Open && !code.IsReserved();
			await this.InnerCloseAsync(new PayloadData(data), send, send);
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
		///   This method emits a <see cref="OnError"/> event if the size
		///   of <paramref name="reason"/> is greater than 123 bytes.
		///   </para>
		/// </remarks>
		/// <param name="code">
		/// One of the <see cref="CloseStatusCode"/> enum values, represents the status code
		/// indicating the reason for the close.
		/// </param>
		/// <param name="reason">
		/// A <see cref="string"/> that represents the reason for the close.
		/// </param>
		public async Task CloseAsync(CloseStatusCode code, string reason)
		{
			byte[] data = null;
			var msg = _readyState.CheckIfClosable() ?? (data = ((ushort)code).Append(reason)).CheckIfValidControlData("reason");

			if (msg != null)
			{
				Error(msg);

				return;
			}

			var send = _readyState == WebSocketState.Open && !code.IsReserved();
			await this.InnerCloseAsync(new PayloadData(data), send, send);
		}

		/// <summary>
		/// Establishes a WebSocket connection.
		/// </summary>
		public void Connect()
		{
			var msg = this.CheckIfCanConnect();
			if (msg != null)
			{
				Error(msg);

				return;
			}

			if (this.InnerConnect())
			{
				Open();
			}
		}

		/// <summary>
		/// Establishes a WebSocket connection asynchronously.
		/// </summary>
		/// <remarks>
		/// This method doesn't wait for the connect to be complete.
		/// </remarks>
		public async Task ConnectAsync()
		{
			var msg = this.CheckIfCanConnect();
			if (msg != null)
			{
				Error(msg);

				return;
			}

			Func<bool> connector = this.InnerConnect;
			var isopen = await Task.Factory.FromAsync<bool>(connector.BeginInvoke, connector.EndInvoke, null);
			if (isopen)
			{
				Open();
			}
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
			return _client
				   ? Ping(WebSocketFrame.CreatePingFrame(true).ToByteArray(), 5000)
				   : Ping(WebSocketFrame.EmptyUnmaskPingBytes, TimeSpan.FromMilliseconds(1000));
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
			if (string.IsNullOrEmpty(message))
			{
				return Ping();
			}

			var data = Encoding.UTF8.GetBytes(message);
			var msg = data.CheckIfValidControlData("message");
			if (msg != null)
			{
				Error(msg);

				return false;
			}

			return _client
				   ? Ping(WebSocketFrame.CreatePingFrame(data, true).ToByteArray(), 5000)
				   : Ping(WebSocketFrame.CreatePingFrame(data, false).ToByteArray(), 1000);
		}

		/// <summary>
		/// Sends a binary <paramref name="data"/> using the WebSocket connection.
		/// </summary>
		/// <param name="data">
		/// An array of <see cref="byte"/> that represents the binary data to send.
		/// </param>
		public void Send(byte[] data)
		{
			var msg = _readyState.CheckIfOpen() ?? data.CheckIfValidSendData();
			if (msg != null)
			{
				Error(msg);

				return;
			}

			InnerSend(Opcode.Binary, new MemoryStream(data));
		}

		/// <summary>
		/// Sends the specified <paramref name="file"/> as a binary data
		/// using the WebSocket connection.
		/// </summary>
		/// <param name="file">
		/// A <see cref="FileInfo"/> that represents the file to send.
		/// </param>
		public void Send(FileInfo file)
		{
			var msg = _readyState.CheckIfOpen() ?? file.CheckIfValidSendData();
			if (msg != null)
			{
				Error(msg);

				return;
			}

			InnerSend(Opcode.Binary, file.OpenRead());
		}

		/// <summary>
		/// Sends a text <paramref name="data"/> using the WebSocket connection.
		/// </summary>
		/// <param name="data">
		/// A <see cref="string"/> that represents the text data to send.
		/// </param>
		public void Send(string data)
		{
			var msg = _readyState.CheckIfOpen() ?? data.CheckIfValidSendData();
			if (msg != null)
			{
				Error(msg);

				return;
			}

			InnerSend(Opcode.Text, new MemoryStream(Encoding.UTF8.GetBytes(data)));
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
		public Task<bool> SendAsync(byte[] data)
		{
			var msg = _readyState.CheckIfOpen() ?? data.CheckIfValidSendData();
			if (msg != null)
			{
				Error(msg);

				return Task.FromResult(false);
			}

			return InnerSendAsync(Opcode.Binary, new MemoryStream(data));
		}

		/// <summary>
		/// Sends the specified <paramref name="file"/> as a binary data asynchronously
		/// using the WebSocket connection.
		/// </summary>
		/// <remarks>
		/// This method doesn't wait for the send to be complete.
		/// </remarks>
		/// <param name="file">
		/// A <see cref="FileInfo"/> that represents the file to send.
		/// </param>
		public Task<bool> SendAsync(FileInfo file)
		{
			var msg = _readyState.CheckIfOpen() ?? file.CheckIfValidSendData();
			if (msg != null)
			{
				Error(msg);

				return Task.FromResult(false);
			}

			return InnerSendAsync(Opcode.Binary, file.OpenRead());
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
			var msg = _readyState.CheckIfOpen() ?? data.CheckIfValidSendData();
			if (msg != null)
			{
				Error(msg);

				return Task.FromResult(false);
			}

			return InnerSendAsync(Opcode.Text, new MemoryStream(Encoding.UTF8.GetBytes(data)));
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
		public async Task<bool> SendAsync(Stream stream, int length)
		{
			var msg = _readyState.CheckIfOpen() ??
					  stream.CheckIfCanRead() ??
					  (length < 1 ? "'length' must be greater than 0." : null);

			if (msg != null)
			{
				Error(msg);

				return false;
			}

			try
			{
				var data = await stream.ReadBytesAsync(length);

				var len = data.Length;
				if (len == 0)
				{
					msg = "A data cannot be read from 'stream'.";
					Error(msg);

					return false;
				}

				var sent = InnerSend(Opcode.Binary, new MemoryStream(data));

				return sent;
			}
			catch (Exception ex)
			{
				Error("An exception has occurred while sending a data.", ex);

				return false;
			}
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
				var msg = this.CheckIfAvailable("SetCookie", false, false) ??
						  (cookie == null ? "'cookie' must not be null." : null);

				if (msg != null)
				{
					Error(msg);

					return;
				}

				lock (_cookies.SyncRoot)
				{
					_cookies.SetOrRemove(cookie);
				}
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
				var msg = this.CheckIfAvailable("SetCredentials", false, false);
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
					Error(msg);

					return;
				}

				_credentials = new NetworkCredential(username, password, _uri.PathAndQuery);
				_preAuth = preAuth;
			}
		}

		/// <summary>
		/// Sets the HTTP Proxy server URL to connect through, and a pair of <paramref name="username"/>
		/// and <paramref name="password"/> for the proxy server authentication (Basic/Digest).
		/// </summary>
		/// <param name="url">
		/// A <see cref="string"/> that represents the HTTP Proxy server URL to connect through.
		/// </param>
		/// <param name="username">
		/// A <see cref="string"/> that represents the user name used to authenticate.
		/// </param>
		/// <param name="password">
		/// A <see cref="string"/> that represents the password for <paramref name="username"/>
		/// used to authenticate.
		/// </param>
		public void SetHttpProxy(string url, string username, string password)
		{
			lock (_forConn)
			{
				var msg = this.CheckIfAvailable("SetHttpProxy", false, false);
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
						msg = "The syntax of proxy url must be 'http://<host>[:<port>]'.";
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
					Error(msg);

					return;
				}

				_proxyCredentials = new NetworkCredential(
				  username, password, string.Format("{0}:{1}", _uri.DnsSafeHost, _uri.Port));
			}
		}

		/// <summary>
		/// Closes the WebSocket connection, and releases all associated resources.
		/// </summary>
		/// <remarks>
		/// This method closes the WebSocket connection with <see cref="CloseStatusCode.Away"/>.
		/// </remarks>
		public void Dispose()
		{
			Close(CloseStatusCode.Away, null);
		}

		// As client
		internal static string CreateBase64Key()
		{
			var src = new byte[16];
			var rand = new Random();
			rand.NextBytes(src);

			return Convert.ToBase64String(src);
		}

		internal static string CreateResponseKey(string base64Key)
		{
			var buff = new StringBuilder(base64Key, 64);
			buff.Append(GuidId);
			SHA1 sha1 = new SHA1CryptoServiceProvider();
			var src = sha1.ComputeHash(Encoding.UTF8.GetBytes(buff.ToString()));

			return Convert.ToBase64String(src);
		}

		// As server
		internal void Close(HttpResponse response)
		{
			_readyState = WebSocketState.Closing;

			SendHttpResponse(response);
			this.CloseServerResources();

			_readyState = WebSocketState.Closed;
		}

		// As server
		internal void Close(HttpStatusCode code)
		{
			Close(InnerCreateHandshakeCloseResponse(code));
		}

		// As server
		internal void Close(CloseEventArgs e, byte[] frameAsBytes, TimeSpan timeout)
		{
			lock (_forConn)
			{
				if (_readyState == WebSocketState.Closing || _readyState == WebSocketState.Closed)
				{
					return;
				}

				_readyState = WebSocketState.Closing;
			}

			e.WasClean = this.CloseHandshake(frameAsBytes, timeout, this.CloseServerResources);

			_readyState = WebSocketState.Closed;
			try
			{
				OnClose.Emit(this, e);
			}
			catch (Exception)
			{
			}
		}

		// As server
		internal void ConnectAsServer()
		{
			try
			{
				if (this.AcceptHandshake())
				{
					_readyState = WebSocketState.Open;
					Open();
				}
			}
			catch (Exception ex)
			{
				ProcessException(ex, "An exception has occurred while connecting.");
			}
		}

		internal bool Ping(byte[] frameAsBytes, int timeoutMilliseconds)
		{
			return Ping(frameAsBytes, TimeSpan.FromMilliseconds(timeoutMilliseconds));
		}

		internal bool Ping(byte[] frameAsBytes, TimeSpan timeout)
		{
			try
			{
				AutoResetEvent pong;
				return _readyState == WebSocketState.Open &&
					   InnerSend(frameAsBytes) &&
					   (pong = _receivePong) != null &&
					   pong.WaitOne(timeout);
			}
			catch (Exception)
			{
				return false;
			}
		}

		// As server, used to broadcast
		internal void Send(Opcode opcode, byte[] data, Dictionary<CompressionMethod, byte[]> cache)
		{
			lock (_forSend)
			{
				lock (_forConn)
				{
					if (_readyState != WebSocketState.Open)
					{
						return;
					}

					try
					{
						byte[] cached;
						if (!cache.TryGetValue(_compression, out cached))
						{
							cached = new WebSocketFrame(
							  Fin.Final,
							  opcode,
							  data.Compress(_compression),
							  _compression != CompressionMethod.None,
							  false)
							  .ToByteArray();

							cache.Add(_compression, cached);
						}

						WriteBytes(cached);
					}
					catch (Exception)
					{
					}
				}
			}
		}

		// As server, used to broadcast
		internal void Send(Opcode opcode, Stream stream, Dictionary<CompressionMethod, Stream> cache)
		{
			lock (_forSend)
			{
				try
				{
					Stream cached;
					if (!cache.TryGetValue(_compression, out cached))
					{
						cached = stream.Compress(_compression);
						cache.Add(_compression, cached);
					}
					else
					{
						cached.Position = 0;
					}

					InnerSend(opcode, Mask.Unmask, cached, _compression != CompressionMethod.None);
				}
				catch (Exception)
				{
				}
			}
		}

		// As server
		private bool AcceptHandshake()
		{
			var msg = this.CheckIfValidHandshakeRequest(_context);
			if (msg != null)
			{
				Error("An error has occurred while connecting.");
				Close(HttpStatusCode.BadRequest);

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

			return SendHttpResponse(this.CreateHandshakeResponse());
		}

		private string CheckIfAvailable(string operation, bool availableAsServer, bool availableAsConnected)
		{
			return !_client && !availableAsServer
				   ? operation + " isn't available as a server."
				   : !availableAsConnected
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
				   ? "Invalid request url."
				   : !context.IsWebSocketRequest
					 ? "Not WebSocket connection request."
					 : !ValidateSecWebSocketKeyHeader(headers["Sec-WebSocket-Key"])
					   ? "Invalid Sec-WebSocket-Key header."
					   : !InnerValidateSecWebSocketVersionClientHeader(headers["Sec-WebSocket-Version"])
						 ? "Invalid Sec-WebSocket-Version header."
						 : CustomHandshakeRequestChecker(context);
		}

		// As client
		private string CheckIfValidHandshakeResponse(HttpResponse response)
		{
			var headers = response.Headers;
			return response.IsUnauthorized
				   ? "HTTP authentication is required."
				   : !response.IsWebSocketResponse
					 ? "Not WebSocket connection response."
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

		private void InnerClose(CloseStatusCode code, string reason, bool wait)
		{
			this.InnerClose(new PayloadData(((ushort)code).Append(reason)), !code.IsReserved(), wait);
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
			e.WasClean =
			  _client
			  ? this.CloseHandshake(send ? WebSocketFrame.CreateCloseFrame(payload, true).ToByteArray() : null, TimeSpan.FromMilliseconds(wait ? 5000 : 0), this.CloseClientResources)
			  : this.CloseHandshake(send ? WebSocketFrame.CreateCloseFrame(payload, false).ToByteArray() : null, TimeSpan.FromMilliseconds(wait ? 1000 : 0), this.CloseServerResources);

			e.WasClean = this.CloseHandshake(
			  send ? WebSocketFrame.CreateCloseFrame(e.PayloadData, _client).ToByteArray() : null,
			  wait ? WaitTime : TimeSpan.Zero,
			  _client ? (Action)this.CloseClientResources : this.CloseServerResources);

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

		private Task InnerCloseAsync(PayloadData payload, bool send, bool wait)
		{
			Action<PayloadData, bool, bool> closer = this.InnerClose;

			return Task.Factory.FromAsync(closer.BeginInvoke, closer.EndInvoke, payload, send, wait, null);
		}

		// As client
		private void CloseClientResources()
		{
			if (_stream != null)
			{
				_stream.Dispose();
				_stream = null;
			}

			if (_tcpClient != null)
			{
				_tcpClient.Close();
				_tcpClient = null;
			}
		}

		private bool CloseHandshake(byte[] frameAsBytes, TimeSpan millisecondsTimeout, Action release)
		{
			var sent = frameAsBytes != null && WriteBytes(frameAsBytes);
			var received =
			  millisecondsTimeout.Equals(TimeSpan.Zero) ||
			  (sent && _exitReceiving != null && _exitReceiving.WaitOne(millisecondsTimeout));

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

			var result = sent && received;

			return result;
		}

		// As server
		private void CloseServerResources()
		{
			if (_closeContext == null)
			{
				return;
			}

			_closeContext();
			_closeContext = null;
			_stream = null;
			_context = null;
		}

		private bool ConcatenateFragmentsInto(Stream dest)
		{
			while (true)
			{
				var frame = WebSocketFrame.Read(_stream, true);
				if (frame.IsFinal)
				{
					/* FINAL */

					// CONT
					if (frame.IsContinuation)
					{
						dest.WriteBytes(frame.PayloadData.ApplicationData);
						break;
					}

					// PING
					if (frame.IsPing)
					{
						ProcessPingFrame(frame);
						continue;
					}

					// PONG
					if (frame.IsPong)
					{
						ProcessPongFrame(frame);
						continue;
					}

					// CLOSE
					if (frame.IsClose)
					{
						return ProcessCloseFrame(frame);
					}
				}
				else
				{
					/* MORE */

					// CONT
					if (frame.IsContinuation)
					{
						dest.WriteBytes(frame.PayloadData.ApplicationData);
						continue;
					}
				}

				// ?
				return ProcessUnsupportedFrame(
				  frame,
				  CloseStatusCode.IncorrectData,
				  "An incorrect data has been received while receiving fragmented data.");
			}

			return true;
		}

		private bool InnerConnect()
		{
			lock (_forConn)
			{
				var msg = _readyState.CheckIfConnectable();
				if (msg != null)
				{
					Error(msg);

					return false;
				}

				try
				{
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
			{
				buff.Append(_compression.ToExtensionString());
			}

			return buff.Length > 0
				   ? buff.ToString()
				   : null;
		}

		// As server
		private HttpResponse InnerCreateHandshakeCloseResponse(HttpStatusCode code)
		{
			var res = HttpResponse.CreateCloseResponse(code);
			res.Headers["Sec-WebSocket-Version"] = Version;

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

			var extensions = this.CreateExtensions();
			if (extensions != null)
			{
				headers["Sec-WebSocket-Extensions"] = extensions;
			}

			headers["Sec-WebSocket-Version"] = Version;

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
			{
				headers["Authorization"] = authRes.ToString();
			}

			if (_cookies.Count > 0)
			{
				req.SetCookies(_cookies);
			}

			return req;
		}

		// As server
		private HttpResponse CreateHandshakeResponse()
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

		private MessageEventArgs DequeueFromMessageEventQueue()
		{
			lock (_forMessageEventQueue)
			{
				return _messageEventQueue.Count > 0 ? _messageEventQueue.Dequeue() : null;
			}
		}

		// As client
		private bool DoHandshake()
		{
			SetClientStream();
			var res = SendHandshakeRequest();
			var msg = this.CheckIfValidHandshakeResponse(res);
			if (msg != null)
			{
				msg = "An error has occurred while connecting.";
				Error(msg);
				this.InnerClose(CloseStatusCode.Abnormal, msg, false);

				return false;
			}

			var cookies = res.Cookies;
			if (cookies.Count > 0)
				_cookies.SetOrRemove(cookies);

			return true;
		}

		private void EnqueueToMessageEventQueue(MessageEventArgs e)
		{
			lock (_forMessageEventQueue)
				_messageEventQueue.Enqueue(e);
		}

		private void Error(string message, Exception exception = null)
		{
			try
			{
				OnError.Emit(this, new ErrorEventArgs(message, exception));
			}
			catch
			{
			}
		}

		private void Init()
		{
			_compression = CompressionMethod.None;
			_cookies = new CookieCollection();
			_forConn = new object();
			_forEvent = new object();
			_forSend = new object();
			_messageEventQueue = new Queue<MessageEventArgs>();
			_forMessageEventQueue = ((ICollection)_messageEventQueue).SyncRoot;
			_readyState = WebSocketState.Connecting;
		}

		private void Open()
		{
			try
			{
				this.StartReceiving();

				lock (_forEvent)
				{
					try
					{
						OnOpen.Emit(this, EventArgs.Empty);
					}
					catch (Exception ex)
					{
						ProcessException(ex, "An exception has occurred while OnOpen.");
					}
				}
			}
			catch (Exception ex)
			{
				ProcessException(ex, "An exception has occurred while opening.");
			}
		}

		private bool ProcessCloseFrame(WebSocketFrame frame)
		{
			var payload = frame.PayloadData;
			this.InnerClose(payload, !payload.IncludesReservedCloseStatusCode, false);

			return false;
		}

		private bool ProcessDataFrame(WebSocketFrame frame)
		{
			var e = frame.IsCompressed
					? new MessageEventArgs(
						frame.Opcode, frame.PayloadData.ApplicationData.Decompress(_compression))
					: new MessageEventArgs(frame.Opcode, frame.PayloadData.ToByteArray());

			EnqueueToMessageEventQueue(e);
			return true;
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
			if (_readyState == WebSocketState.Connecting && !_client)
			{
				Close(HttpStatusCode.BadRequest);
			}
			else
			{
				this.InnerClose(code, reason ?? code.GetMessage(), false);
			}
		}

		private bool ProcessFragmentedFrame(WebSocketFrame frame)
		{
			return frame.IsContinuation || this.ProcessFragments(frame);
		}

		private bool ProcessFragments(WebSocketFrame first)
		{
			using (var buff = new MemoryStream())
			{
				buff.WriteBytes(first.PayloadData.ApplicationData);
				if (!this.ConcatenateFragmentsInto(buff))
					return false;

				byte[] data;
				if (_compression != CompressionMethod.None)
				{
					data = buff.DecompressToArray(_compression);
				}
				else
				{
					buff.Close();
					data = buff.ToArray();
				}

				EnqueueToMessageEventQueue(new MessageEventArgs(first.Opcode, data));
				return true;
			}
		}

		private bool ProcessPingFrame(WebSocketFrame frame)
		{
			var mask = _client ? Mask.Mask : Mask.Unmask;

			return InnerSend(WebSocketFrame.CreatePongFrame(frame.PayloadData.ToByteArray(), mask == Mask.Mask).ToByteArray());
		}

		private bool ProcessPongFrame(WebSocketFrame frame)
		{
			_receivePong.Set();

			return true;
		}

		// As server
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

		private bool ProcessUnsupportedFrame(WebSocketFrame frame, CloseStatusCode code, string reason)
		{
			ProcessException(new WebSocketException(code, reason), null);

			return false;
		}

		private bool ProcessWebSocketFrame(WebSocketFrame frame)
		{
			return frame.IsCompressed && _compression == CompressionMethod.None
				   ? ProcessUnsupportedFrame(
					   frame,
					   CloseStatusCode.IncorrectData,
					   "A compressed data has been received without available decompression method.")
				   : frame.IsFragmented
					 ? ProcessFragmentedFrame(frame)
					 : frame.IsData
					   ? ProcessDataFrame(frame)
					   : frame.IsPing
						 ? ProcessPingFrame(frame)
						 : frame.IsPong
						   ? ProcessPongFrame(frame)
						   : frame.IsClose
							 ? ProcessCloseFrame(frame)
							 : ProcessUnsupportedFrame(frame, CloseStatusCode.PolicyViolation, null);
		}

		private bool InnerSend(byte[] frameAsBytes)
		{
			lock (_forConn)
			{
				if (_readyState != WebSocketState.Open)
				{
					return false;
				}

				return WriteBytes(frameAsBytes);
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

					sent = InnerSend(opcode, _client ? Mask.Mask : Mask.Unmask, stream, compressed);
					if (!sent)
						Error("Sending a data has been interrupted.");
				}
				catch (Exception ex)
				{
					Error("An exception has occurred while sending a data.", ex);
				}
				finally
				{
					if (compressed)
						stream.Dispose();

					src.Dispose();
				}

				return sent;
			}
		}

		private bool InnerSend(Opcode opcode, Mask mask, Stream stream, bool compressed)
		{
			var len = stream.Length;

			/* Not fragmented */

			if (len == 0)
				return InnerSend(Fin.Final, opcode, mask, new byte[0], compressed);

			var quo = len / FragmentLength;
			var rem = (int)(len % FragmentLength);

			byte[] buff = null;
			if (quo == 0)
			{
				buff = new byte[rem];
				return stream.Read(buff, 0, rem) == rem &&
					   InnerSend(Fin.Final, opcode, mask, buff, compressed);
			}

			buff = new byte[FragmentLength];
			if (quo == 1 && rem == 0)
				return stream.Read(buff, 0, FragmentLength) == FragmentLength &&
					   InnerSend(Fin.Final, opcode, mask, buff, compressed);

			/* Send fragmented */

			// Begin
			if (stream.Read(buff, 0, FragmentLength) != FragmentLength ||
				!InnerSend(Fin.More, opcode, mask, buff, compressed))
				return false;

			var n = rem == 0 ? quo - 2 : quo - 1;
			for (long i = 0; i < n; i++)
				if (stream.Read(buff, 0, FragmentLength) != FragmentLength ||
					!InnerSend(Fin.More, Opcode.Cont, mask, buff, compressed))
					return false;

			// End
			if (rem == 0)
				rem = FragmentLength;
			else
				buff = new byte[rem];

			return stream.Read(buff, 0, rem) == rem &&
				   InnerSend(Fin.Final, Opcode.Cont, mask, buff, compressed);
		}

		private bool InnerSend(Fin fin, Opcode opcode, Mask mask, byte[] data, bool compressed)
		{
			lock (_forConn)
			{
				if (_readyState != WebSocketState.Open)
				{
					return false;
				}

				return WriteBytes(new WebSocketFrame(fin, opcode, data, compressed, mask == Mask.Mask).ToByteArray());
			}
		}

		private Task<bool> InnerSendAsync(Opcode opcode, Stream stream)
		{
			Func<Opcode, Stream, bool> sender = InnerSend;
			return Task.Factory.FromAsync<Opcode, Stream, bool>(sender.BeginInvoke, sender.EndInvoke, opcode, stream, null);
		}

		// As client
		private HttpResponse SendHandshakeRequest()
		{
			var req = this.CreateHandshakeRequest();
			var res = SendHttpRequest(req, 90000);
			if (res.IsUnauthorized)
			{
				_authChallenge = res.AuthenticationChallenge;
				if (_credentials != null &&
					(!_preAuth || _authChallenge.Scheme == AuthenticationSchemes.Digest))
				{
					if (res.Headers.Contains("Connection", "close"))
					{
						this.CloseClientResources();
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
			return WriteBytes(response.ToByteArray());
		}

		// As client
		private HttpResponse SendProxyConnectRequest()
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
						this.CloseClientResources();
						_tcpClient = new TcpClient(_proxyUri.DnsSafeHost, _proxyUri.Port);
						_stream = _tcpClient.GetStream();
					}

					var authRes = new AuthenticationResponse(authChal, _proxyCredentials, 0);
					req.Headers["Proxy-Authorization"] = authRes.ToString();
					res = SendHttpRequest(req, 15000);
				}
			}

			return res;
		}

		// As client
		private void SetClientStream()
		{
			var proxy = _proxyUri != null;
			_tcpClient = proxy
						 ? new TcpClient(_proxyUri.DnsSafeHost, _proxyUri.Port)
						 : new TcpClient(_uri.DnsSafeHost, _uri.Port);

			_stream = _tcpClient.GetStream();

			if (proxy)
			{
				var res = SendProxyConnectRequest();
				if (res.IsProxyAuthenticationRequired)
					throw new WebSocketException("Proxy authentication is required.");

				if (res.StatusCode[0] != '2')
					throw new WebSocketException(
					  "The proxy has failed a connection to the requested host and port.");
			}

			if (_secure)
			{
				var sslStream = new SslStream(
				  _stream,
				  false,
				  _certValidationCallback ?? ((sender, certificate, chain, sslPolicyErrors) => true));

				sslStream.AuthenticateAsClient(_uri.DnsSafeHost);
				_stream = sslStream;
			}
		}

		private async Task StartReceiving()
		{
			if (_messageEventQueue.Count > 0)
			{
				_messageEventQueue.Clear();
			}

			while (true)
			{
				var frame = await WebSocketFrame.ReadAsync(_stream);

				if (ProcessWebSocketFrame(frame) && _readyState != WebSocketState.Closed)
				{
					if (!frame.IsData)
					{
						return;
					}

					lock (_forEvent)
					{
						try
						{
							var e = this.DequeueFromMessageEventQueue();
							if (e != null && _readyState == WebSocketState.Open)
							{
								OnMessage.Emit(this, e);
							}
						}
						catch (Exception ex)
						{
							ProcessException(ex, "An exception has occurred while OnMessage.");
						}
					}
				}
				else if (_exitReceiving != null)
				{
					_exitReceiving.Set();
				}
			}
		}

		// As client
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
					_compression = CompressionMethod.None;

				return true;
			}

			if (!compress)
				return false;

			var extensions = value.SplitHeaderValue(',');
			if (extensions.Contains(
				  extension => extension.Trim() != _compression.ToExtensionString()))
				return false;

			_extensions = value;
			return true;
		}

		// As server
		private bool ValidateSecWebSocketKeyHeader(string value)
		{
			if (string.IsNullOrEmpty(value))
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
			return value != null && value == Version;
		}

		// As client
		private bool InnerValidateSecWebSocketVersionServerHeader(string value)
		{
			return value == null || value == Version;
		}

		private bool WriteBytes(byte[] data)
		{
			try
			{
				_stream.Write(data, 0, data.Length);
				return true;
			}
			catch
			{
				return false;
			}
		}
	}
}