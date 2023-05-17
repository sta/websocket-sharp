using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp.Net;
using WebSocketSharp.Net.WebSockets;

// ReSharper disable UnusedMember.Global

namespace WebSocketSharp
{
	public sealed class ServerWebSocket : WebSocket
	{
		private string base64Key;
		private Action closeContext;
		private Func<WebSocketContext, string> handshakeRequestChecker;
		private bool ignoreExtensions;
		private WebSocketContext socketContext;


		// As server
		internal ServerWebSocket(HttpListenerWebSocketContext context, string protocol)
			: base(TimeSpan.FromSeconds(1))
		{
			this.socketContext = context;
			this.protocol = protocol;

			closeContext = context.Close;
			logger = context.Log;
			IsSecure = context.IsSecureConnection;
			socketStream = context.Stream;
		}


		// As server
		internal ServerWebSocket(TcpListenerWebSocketContext context, string protocol)
			: base(TimeSpan.FromSeconds(1))
		{
			this.socketContext = context;
			this.protocol = protocol;

			closeContext = context.Close;
			logger = context.Log;
			IsSecure = context.IsSecureConnection;
			socketStream = context.Stream;
		}


		public override bool IsSecure { get; }

		/*
		//internal CookieCollection CookieCollection {
		//  get => _cookies;
		//}*/

		// As server
		internal Func<WebSocketContext, string> CustomHandshakeRequestChecker
		{
			get
			{
				return handshakeRequestChecker;
			}

			set
			{
				handshakeRequestChecker = value;
			}
		}

		// As server
		internal bool IgnoreExtensions
		{
			get
			{
				return ignoreExtensions;
			}

			set
			{
				ignoreExtensions = value;
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
				return socketContext?.RequestUri;
			}
		}


		// As server
		private bool AcceptInternal()
		{
			// this is server code.. the chance for cross thread call here is relatively low

			var webSocketState = readyState;

			if (webSocketState == WebSocketState.Open)
			{
				logger.Warn("The handshake request has already been accepted.");

				return false;
			}

			if (webSocketState == WebSocketState.Closing)
			{
				logger.Error("The close process has set in.");

				CallOnError("An interruption has occurred while attempting to accept.", null);

				return false;
			}

			if (webSocketState == WebSocketState.Closed)
			{
				logger.Error("The connection has been closed.");

				CallOnError("An interruption has occurred while attempting to accept.", null);

				return false;
			}

			try
			{
				// this does send inside and acquires locks
				// I really doubt accept can becalled in parallel, ifi it is, it is bad design and should fail setting _readyState
				// and most probably it is never called. AcceptInternal() is

				if (!AcceptHandshake())
					return false;
			}
			catch (Exception ex)
			{
				logger.Fatal(ex.Message);
				logger.Debug(ex.ToString());

				Fatal("An exception has occurred while attempting to accept.", ex);

				return false;
			}

			lock (forState)
			{
				if (readyState != WebSocketState.Connecting)
				{
					Fatal($"Socket state error, expected Connecting, was: {readyState}", null);

					return false;
				}

				readyState = WebSocketState.Open;
				return true;
			} // lock
		}


		// As server
		private bool AcceptHandshake()
		{
			logger.Debug($"A handshake request from {socketContext.UserEndPoint}: {socketContext}");

			if (!CheckHandshakeRequest(socketContext, out var msg))
			{
				logger.Error(msg);

				RefuseHandshake(
								CloseStatusCode.ProtocolError,
								"A handshake error has occurred while attempting to accept."
							   );

				return false;
			}

			var customCheck = CustomCheckHandshakeRequest(socketContext, out msg);
			if (!customCheck)
			{
				logger.Error(msg);

				RefuseHandshake(
								CloseStatusCode.PolicyViolation,
								"A handshake error has occurred while attempting to accept."
							   );

				return false;
			}

			base64Key = socketContext.Headers["Sec-WebSocket-Key"];

			if (protocol != null)
			{
				var vals = socketContext.SecWebSocketProtocols;
				if (!vals.Contains(val => val == protocol))
					protocol = null;
			}

			if (!ignoreExtensions)
			{
				var val = socketContext.Headers["Sec-WebSocket-Extensions"];
				if (val != null)
				{
					var buff = new StringBuilder(80);

					foreach (var elm in val.SplitHeaderValue(','))
					{
						var extension = elm.Trim();
						if (extension.Length == 0)
							continue;

						if (extension.IsCompressionExtension(CompressionMethod.Deflate))
						{
							var compressionMethod = CompressionMethod.Deflate;

							buff.AppendFormat("{0}, ", compressionMethod.ToExtensionString("client_no_context_takeover", "server_no_context_takeover"));

							compression = compressionMethod;

							break;
						}
					}

					var len = buff.Length;
					if (len > 2)
					{
						buff.Length = len - 2;
						extensions = buff.ToString();
					}
				}
			}

			return SendHttpResponse(CreateHandshakeResponse());
		}


		// As server
		private bool CheckHandshakeRequest(
			WebSocketContext context, out string message
		)
		{
			message = null;

			if (!context.IsWebSocketRequest)
			{
				message = "Not a handshake request.";
				return false;
			}

			if (context.RequestUri == null)
			{
				message = "It specifies an invalid Request-URI.";
				return false;
			}

			var headers = context.Headers;

			var key = headers["Sec-WebSocket-Key"];
			if (key == null)
			{
				message = "It includes no Sec-WebSocket-Key header.";
				return false;
			}

			if (key.Length == 0)
			{
				message = "It includes an invalid Sec-WebSocket-Key header.";
				return false;
			}

			var versionString = headers["Sec-WebSocket-Version"];
			if (versionString == null)
			{
				message = "It includes no Sec-WebSocket-Version header.";
				return false;
			}

			if (versionString != version)
			{
				message = "It includes an invalid Sec-WebSocket-Version header.";
				return false;
			}

			var protocolString = headers["Sec-WebSocket-Protocol"];
			if (protocolString != null && protocolString.Length == 0)
			{
				message = "It includes an invalid Sec-WebSocket-Protocol header.";
				return false;
			}

			if (!ignoreExtensions)
			{
				var extensionsString = headers["Sec-WebSocket-Extensions"];
				if (extensionsString != null && extensionsString.Length == 0)
				{
					message = "It includes an invalid Sec-WebSocket-Extensions header.";
					return false;
				}
			}

			return true;
		}


		// As server
		private void RefuseHandshake(CloseStatusCode code, string reason)
		{
			readyState = WebSocketState.Closing;

			var res = CreateHandshakeFailureResponse(HttpStatusCode.BadRequest);
			SendHttpResponse(res);

			ReleaseServerResources();

			readyState = WebSocketState.Closed;

			CallOnClose(new CloseEventArgs(code, reason));
		}


		// As server
		private HttpResponse CreateHandshakeResponse()
		{
			var ret = HttpResponse.CreateWebSocketResponse();

			var headers = ret.Headers;
			headers["Sec-WebSocket-Accept"] = CreateResponseKey(base64Key);

			if (protocol != null)
				headers["Sec-WebSocket-Protocol"] = protocol;

			if (extensions != null)
				headers["Sec-WebSocket-Extensions"] = extensions;

			SetResponseCookies(ret);

			return ret;
		}


		// As server
		private bool CustomCheckHandshakeRequest(
			WebSocketContext context, out string message
		)
		{
			message = null;

			if (handshakeRequestChecker == null)
				return true;

			message = handshakeRequestChecker(context);
			return message == null;
		}


		// As server
		private HttpResponse CreateHandshakeFailureResponse(HttpStatusCode code)
		{
			var ret = HttpResponse.CreateCloseResponse(code);
			ret.Headers["Sec-WebSocket-Version"] = version;

			return ret;
		}


		// As server
		private void ReleaseServerResources()
		{
			if (closeContext == null)
				return;

			closeContext();
			closeContext = null;
			socketStream = null;
			socketContext = null;
		}


		// As server
		private bool SendHttpResponse(HttpResponse response)
		{
			logger.Debug($"A response to {socketContext.UserEndPoint}: {response}");

			var stream = socketStream;
			if (stream == null)
				return false;

			lock (forSend)
			{
				return sendBytesInternal(stream, response.ToByteArray());
			}
		}


		private protected override void PerformCloseSequence(PayloadData payloadData, bool send, bool receive, bool received)
		{
			Stream streamForLater;
			ManualResetEvent receivingExitedForLater;

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
				receivingExitedForLater = receivingExitedEvent;

				ReleaseServerResources();
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


		protected override void MessageHandler(MessageEventArgs e)
		{
			CallOnMessage(e);

			e = DequeueNextMessage();
			if (e == null)
				return;

			// process next message
			Task.Factory.StartNew(() => MessageHandler(e));
		}


		// As server
		internal void CloseResponse(HttpResponse response)
		{
			readyState = WebSocketState.Closing;

			SendHttpResponse(response);
			ReleaseServerResources();

			readyState = WebSocketState.Closed;
		}


		// As server
		internal void Close(HttpStatusCode code)
		{
			CloseResponse(CreateHandshakeFailureResponse(code));
		}


		// As server
		internal void PerformCloseSessionSequence(PayloadData payloadData, byte[] frameAsBytes)
		{
			Stream streamForLater;
			ManualResetEvent receivingExitedForLater;

			bool SendClosingBytes()
			{
				var clean = false;

				try
				{
					if (frameAsBytes != null && streamForLater != null)
					{
						bool sent;

						lock (forSend)
						{
							sent = sendBytesInternal(streamForLater, frameAsBytes);
						}

						var received = sent && receivingExitedForLater != null && receivingExitedForLater.WaitOne(WaitTime, true);

						clean = sent && received;

						logger.Debug($"SendClosingBytes: Was clean?: {clean} sent: {sent} received: {received}");
					}
				}
				catch
				{
				}

				// stream is not disposed on server

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

				readyState = WebSocketState.Closing;

				streamForLater = socketStream;
				receivingExitedForLater = receivingExitedEvent;

				ReleaseServerResources();
				ReleaseCommonResources(false);

				readyState = WebSocketState.Closed;
			} // lock

			logger.Trace("Begin closing the connection.");

			// call outside lock
			var wasClean = SendClosingBytes();

			logger.Trace("End closing the connection.");

			CallOnClose(new CloseEventArgs(payloadData)
			{
				WasClean = wasClean
			});
		}


		// As server
		internal void InternalAccept()
		{
			// called from websocket behavior

			try
			{
				if (!AcceptHandshake())
					return;
			}
			catch (Exception ex)
			{
				logger.Fatal(ex.Message);
				logger.Debug(ex.ToString());

				Fatal("An exception has occurred while attempting to accept.", ex);

				return;
			}

			readyState = WebSocketState.Open;

			open();
		}


		// As server
		internal bool Ping(byte[] frameAsBytes, TimeSpan timeout)
		{
			return HandlePing(frameAsBytes, timeout);
		}


		// As server
		internal void Send(Opcode opcode, byte[] data, Dictionary<CompressionMethod, byte[]> cache)
		{
			if (readyState != WebSocketState.Open)
			{
				logger.Error("The connection is closing.");
				return;
			}

			var compressionMethod = compression;

			if (!cache.TryGetValue(compressionMethod, out var found))
			{
				found = CreateFrame(Fin.Final, opcode, data.Compress(compressionMethod), compressionMethod != CompressionMethod.None).ToArray();

				cache.Add(compressionMethod, found);
			}

			var stream = socketStream;
			if (stream == null)
			{
				logger.Error("The stream is null.");
				return;
			}

			lock (forSend)
			{
				sendBytesInternal(stream, found);
			}
		}


		// As server
		internal void Send(Opcode opcode, Stream stream, Dictionary<CompressionMethod, Stream> cache)
		{
			var compressionMethod = compression;

			lock (forSend)
			{
				Stream found;
				if (!cache.TryGetValue(compressionMethod, out found))
				{
					found = stream.Compress(compressionMethod);
					cache.Add(compressionMethod, found);
				}
				else
				{
					found.Position = 0;
				}

				SendFragmentedInternal(opcode, found, compressionMethod != CompressionMethod.None);
			}
		}


		/// <summary>
		///     Accepts the handshake request.
		/// </summary>
		/// <remarks>
		///     This method does nothing if the handshake request has already been
		///     accepted.
		/// </remarks>
		/// <exception cref="InvalidOperationException">
		///     <para>
		///         This instance is a client.
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
		///         The connection has already been closed.
		///     </para>
		/// </exception>
		public void Accept()
		{
			if (readyState == WebSocketState.Closing)
			{
				throw new InvalidOperationException("The close process is in progress.");
			}

			if (readyState == WebSocketState.Closed)
			{
				throw new InvalidOperationException("The connection has already been closed.");
			}

			if (AcceptInternal())
				open();
		}


		/// <summary>
		///     Accepts the handshake request asynchronously.
		/// </summary>
		/// <remarks>
		///     <para>
		///         This method does not wait for the accept process to be complete.
		///     </para>
		///     <para>
		///         This method does nothing if the handshake request has already been
		///         accepted.
		///     </para>
		/// </remarks>
		/// <exception cref="InvalidOperationException">
		///     <para>
		///         This instance is a client.
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
		///         The connection has already been closed.
		///     </para>
		/// </exception>
		public void AcceptAsync()
		{
			if (readyState == WebSocketState.Closing)
			{
				throw new InvalidOperationException("The close process is in progress.");
			}

			if (readyState == WebSocketState.Closed)
			{
				throw new InvalidOperationException("The connection has already been closed.");
			}

#if NET_CORE
			var task = Task.Factory.StartNew(AcceptInternal);

			task.ContinueWith((t) =>
			{
				if (!t.IsFaulted && t.Exception == null && t.Result)
				{
					open();
				}
				else
				{
					//close(1006, "could not open"); // untested
				}
			});
#else
			Func<bool> acceptor = AcceptInternal;

			acceptor.BeginInvoke(
			  ar =>
			  {
				  if (acceptor.EndInvoke(ar))
					  open();
			  },
			  null
			);
#endif
		}


		private protected override WebSocketFrame CreateCloseFrame(PayloadData payloadData)
		{
			return WebSocketFrame.CreateCloseFrame(payloadData, false);
		}


		private protected override WebSocketFrame CreatePongFrame(PayloadData payloadData)
		{
			return WebSocketFrame.CreatePongFrame(payloadData, false);
		}


		private protected override WebSocketFrame CreateFrame(Fin fin, Opcode opcode, byte[] data, bool compressed)
		{
			return new WebSocketFrame(fin, opcode, data, compressed, false);
		}


		private protected override void CheckCode(ushort code)
		{
			if (code == 1010)
			{
				throw new ArgumentException("1010 cannot be used.", nameof(code));
			}
		}


		private protected override void CheckCloseStatus(CloseStatusCode code)
		{
			if (code == CloseStatusCode.MandatoryExtension)
			{
				throw new ArgumentException("MandatoryExtension cannot be used.", nameof(code));
			}
		}


		private protected override string CheckFrameMask(WebSocketFrame frame)
		{
			if (!frame.IsMasked)
			{
				return "A frame from a client is not masked.";
			}

			return null;
		}


		private protected override void UnmaskFrame(WebSocketFrame frame)
		{
		}
	}
}
