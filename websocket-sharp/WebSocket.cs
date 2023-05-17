#region License

/*
 * WebSocket.cs
 *
 * This code is derived from WebSocket.java
 * (http://github.com/adamac/Java-WebSocket-client).
 *
 * The MIT License
 *
 * Copyright (c) 2009 Adam MacBeth
 * Copyright (c) 2010-2016 sta.blockhead
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using WebSocketSharp.Net;

// ReSharper disable UnusedMember.Global

namespace WebSocketSharp
{
	/// <summary>
	///     Implements the WebSocket interface.
	/// </summary>
	/// <remarks>
	///     <para>
	///         This class provides a set of methods and properties for two-way
	///         communication using the WebSocket protocol.
	///     </para>
	///     <para>
	///         The WebSocket protocol is defined in
	///         <see href="http://tools.ietf.org/html/rfc6455">RFC 6455</see>.
	///     </para>
	/// </remarks>
	public abstract class WebSocket : IDisposable
	{
		protected const string version = "13";
		private const string guid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

		/// <summary>
		///     Represents the empty array of <see cref="byte" /> used internally.
		/// </summary>
		internal static readonly byte[] EmptyBytes;

		/// <summary>
		///     Represents the length used to determine whether the data should be fragmented in sending.
		/// </summary>
		/// <remarks>
		///     <para>
		///         The data will be fragmented if that length is greater than the value of this field.
		///     </para>
		///     <para>
		///         If you would like to change the value, you must set it to a value between <c>125</c> and
		///         <c>Int32.MaxValue - 14</c> inclusive.
		///     </para>
		/// </remarks>
		internal static readonly int FragmentLength;

		/// <summary>
		///     Represents the random number generator used internally.
		/// </summary>
		internal static readonly RandomNumberGenerator RandomNumber;

		protected readonly object forSend = new object();
		protected readonly object forState = new object(); // locks _readyState, _retryCountForConnect

		protected CompressionMethod compression = CompressionMethod.None;
		protected string extensions;
		protected string protocol;
		protected volatile WebSocketState readyState = WebSocketState.Connecting;
		protected ManualResetEvent receivingExitedEvent; // receiving completely stopped (when socket closes)

		protected Stream socketStream;

		protected volatile Logger logger;

		private readonly CookieCollection cookies = new CookieCollection(); // the cookies that are put into response

		private readonly Queue<MessageEventArgs> messageEventQueue = new Queue<MessageEventArgs>();

		private MemoryStream fragmentsBuffer;
		private bool fragmentsCompressed;
		private Opcode fragmentsOpcode;
		private bool inContinuation;
		private volatile bool inMessage;
		private int insidePingBlock;

		private ManualResetEvent pongReceivedEvent;
		private TimeSpan waitTime;


		static WebSocket()
		{
#if NET_CORE
			EmptyBytes = Array.Empty<byte>();
#else
			EmptyBytes = new byte[0];
#endif
			FragmentLength = 1016;
			RandomNumber = new RNGCryptoServiceProvider();
		}


		protected WebSocket(TimeSpan waitTime)
		{
			this.waitTime = waitTime;
		}


		/// <summary>
		///     Gets the HTTP cookies included in the handshake request/response.
		/// </summary>
		/// <value>
		///     <para>
		///         An <see cref="T:System.Collections.Generic.IEnumerable{WebSocketSharp.Net.Cookie}" />
		///         instance.
		///     </para>
		///     <para>
		///         It provides an enumerator which supports the iteration over
		///         the collection of the cookies.
		///     </para>
		/// </value>
		public IEnumerable<Cookie> Cookies
		{
			get
			{
				lock (cookies.SyncRoot)
				{
					foreach (Cookie cookie in cookies)
						yield return cookie;
				}
			}
		}

		internal bool HasMessage
		{
			get
			{
				lock (messageEventQueue)
					return messageEventQueue.Count > 0;
			}
		}

		internal bool IsConnected
		{
			get
			{
				var webSocketState = readyState;
				return webSocketState == WebSocketState.Open || webSocketState == WebSocketState.Closing;
			}
		}


		/// <summary>
		///     Gets or sets underlying socket connect timeout.
		/// </summary>
		public int ConnectTimeout { get; set; } = 5000;

		/// <summary>
		///     Gets or sets underlying socket read or write timeout.
		/// </summary>
		public virtual int ReadWriteTimeout { get; set; } = 5000;


		public abstract Uri Url { get; }

		/// <summary>
		///     Gets or sets a value indicating whether a <see cref="OnMessage" /> event
		///     is emitted when a ping is received.
		/// </summary>
		/// <value>
		///     <para>
		///         <c>true</c> if this instance emits a <see cref="OnMessage" /> event
		///         when receives a ping; otherwise, <c>false</c>.
		///     </para>
		///     <para>
		///         The default value is <c>false</c>.
		///     </para>
		/// </value>
		public bool EmitOnPing { get; set; }


		/// <summary>
		///     Gets the extensions selected by server.
		/// </summary>
		/// <value>
		///     A <see cref="string" /> that will be a list of the extensions
		///     negotiated between client and server, or an empty string if
		///     not specified or selected.
		/// </value>
		public string Extensions
		{
			get
			{
				return extensions ?? String.Empty;
			}
		}

		/// <summary>
		///     Gets a value indicating whether the connection is alive.
		/// </summary>
		/// <remarks>
		///     The get operation returns the value by using a ping/pong
		///     if the current state of the connection is Open.
		/// </remarks>
		/// <value>
		///     <c>true</c> if the connection is alive; otherwise, <c>false</c>.
		/// </value>
		public bool IsAlive
		{
			get
			{
				return PingInternal(EmptyBytes);
			}
		}

		/// <summary>
		///     Gets a value indicating whether a secure connection is used.
		/// </summary>
		/// <value>
		///     <c>true</c> if this instance uses a secure connection; otherwise,
		///     <c>false</c>.
		/// </value>
		// ReSharper disable once UnusedMemberInSuper.Global
		public abstract bool IsSecure { get; }

		/// <summary>
		///     Gets the logging function.
		/// </summary>
		/// <remarks>
		///     The default logging level is <see cref="LogLevel.Error" />.
		/// </remarks>
		/// <value>
		///     A <see cref="Logger" /> that provides the logging function.
		/// </value>
		public Logger Log
		{
			get
			{
				// note: can be called from inside lock!
				return logger;
			}

			internal set
			{
				logger = value;
			}
		}


		/// <summary>
		///     Gets the name of subprotocol selected by the server.
		/// </summary>
		/// <value>
		///     <para>
		///         A <see cref="string" /> that will be one of the names of
		///         subprotocols specified by client.
		///     </para>
		///     <para>
		///         An empty string if not specified or selected.
		///     </para>
		/// </value>
		public string Protocol
		{
			get
			{
				return protocol ?? String.Empty;
			}

			internal set
			{
				protocol = value;
			}
		}

		/// <summary>
		///     Gets the current state of the connection.
		/// </summary>
		/// <value>
		///     <para>
		///         One of the <see cref="WebSocketState" /> enum values.
		///     </para>
		///     <para>
		///         It indicates the current state of the connection.
		///     </para>
		///     <para>
		///         The default value is <see cref="WebSocketState.Connecting" />.
		///     </para>
		/// </value>
		public WebSocketState ReadyState
		{
			get
			{
				return readyState;
			}
		}


		/// <summary>
		///     Gets or sets the time to wait for the response to the ping or close.
		/// </summary>
		/// <remarks>
		///     The set operation does nothing if the connection has already been
		///     established or it is closing.
		/// </remarks>
		/// <value>
		///     <para>
		///         A <see cref="TimeSpan" /> to wait for the response.
		///     </para>
		///     <para>
		///         The default value is the same as 5 seconds if this instance is
		///         a client.
		///     </para>
		/// </value>
		/// <exception cref="ArgumentOutOfRangeException">
		///     The value specified for a set operation is zero or less.
		/// </exception>
		public TimeSpan WaitTime
		{
			get
			{
				return waitTime;
			}

			set
			{
				if (value <= TimeSpan.Zero)
					throw new ArgumentOutOfRangeException(nameof(value), "Zero or less.");

				lock (forState)
				{
					if (!CanModifyConnectionProperties(out var msg))
					{
						logger.Warn(msg);
						return;
					}

					waitTime = value;
				}
			}
		}


		/// <summary>
		///     Closes the connection and releases all associated resources.
		/// </summary>
		/// <remarks>
		///     <para>
		///         This method closes the connection with close status 1001 (going away).
		///     </para>
		///     <para>
		///         And this method does nothing if the current state of the connection is
		///         Closing or Closed.
		///     </para>
		/// </remarks>
		void IDisposable.Dispose()
		{
			PerformCloseSequence(1001, String.Empty);
		}


		protected abstract void MessageHandler(MessageEventArgs e);


		private protected MessageEventArgs DequeueNextMessage()
		{
			lock (messageEventQueue)
			{
				MessageEventArgs e;

				if (messageEventQueue.Count == 0 || readyState != WebSocketState.Open)
					e = null;
				else
					e = messageEventQueue.Dequeue();

				if (e == null)
					inMessage = false;

				return e;
			}
		}


		//internal CookieCollection CookieCollection {
		//  get => _cookies;
		//}


		/// <summary>
		///     Sets an HTTP cookie to send with the handshake request.
		/// </summary>
		/// <remarks>
		///     This method does nothing if the connection has already been
		///     established or it is closing.
		/// </remarks>
		/// <param name="cookie">
		///     A <see cref="Cookie" /> that represents the cookie to send.
		/// </param>
		/// <exception cref="InvalidOperationException">
		///     This instance is not a client.
		/// </exception>
		/// <exception cref="ArgumentNullException">
		///     <paramref name="cookie" /> is <see langword="null" />.
		/// </exception>
		public void SetCookie(Cookie cookie)
		{
			if (cookie == null)
				throw new ArgumentNullException(nameof(cookie));

			lock (forState)
			{
				if (!CanModifyConnectionProperties(out var msg))
				{
					logger.Warn(msg);
					return;
				}
			}

			// this should be in the lock above but better not. no lock nesting
			lock (cookies.SyncRoot)
			{
				cookies.SetOrRemove(cookie);
			}
		}


		private protected void SetResponseCookies(HttpResponse ret)
		{
			lock (cookies.SyncRoot)
			{
				if (cookies.Count > 0)
					ret.SetCookies(cookies);
			}
		}


		private protected void SetRequestCookies(HttpRequest ret)
		{
			lock (cookies.SyncRoot)
			{
				if (cookies.Count > 0)
					ret.SetCookies(cookies);
			}
		}


		private protected void AssignCookieCollection(CookieCollection cookieCollection)
		{
			if (cookieCollection == null)
				return;

			lock (cookies.SyncRoot)
			{
				cookies.SetOrRemove(cookieCollection);
			}
		}


		/// <summary>
		///     Occurs when the WebSocket connection has been closed.
		/// </summary>
		public event EventHandler<CloseEventArgs> OnClose;

		/// <summary>
		///     Occurs when the <see cref="WebSocket" /> gets an error.
		/// </summary>
		public event EventHandler<ErrorEventArgs> OnError;

		/// <summary>
		///     Occurs when the <see cref="WebSocket" /> receives a message.
		/// </summary>
		public event EventHandler<MessageEventArgs> OnMessage;

		/// <summary>
		///     Occurs when the WebSocket connection has been established.
		/// </summary>
		public event EventHandler OnOpen;


		protected bool CanModifyConnectionProperties(out string message)
		{
			var webSocketState = readyState;

			message = null;

			if (webSocketState == WebSocketState.Open)
			{
				message = "The connection has already been established.";
				return false;
			}

			if (webSocketState == WebSocketState.Closing)
			{
				message = "The connection is closing.";
				return false;
			}

			return true;
		}


		private bool CheckReceivedFrame(WebSocketFrame frame, out string message)
		{
			message = CheckFrameMask(frame);
			if (!string.IsNullOrEmpty(message))
				return false;

			if (inContinuation && frame.IsData)
			{
				message = "A data frame has been received while receiving continuation frames.";
				return false;
			}

			if (frame.IsCompressed && compression == CompressionMethod.None)
			{
				message = "A compressed frame has been received without any agreement for it.";
				return false;
			}

			if (frame.Rsv2 == Rsv.On)
			{
				message = "The RSV2 of a frame is non-zero without any negotiation for it.";
				return false;
			}

			if (frame.Rsv3 == Rsv.On)
			{
				message = "The RSV3 of a frame is non-zero without any negotiation for it.";
				return false;
			}

			return true;
		}


		protected void PerformCloseSequence(ushort code, string reason)
		{
			var webSocketState = readyState;

			if (webSocketState == WebSocketState.Closing)
			{
				logger.Info("The closing is already in progress.");
				return;
			}

			if (webSocketState == WebSocketState.Closed)
			{
				logger.Info("The connection has already been closed.");
				return;
			}

			if (code == 1005)
			{
				// == no status
				PerformCloseSequence(PayloadData.Empty, true, true, false);
				return;
			}

			var send = !code.IsReserved();
			PerformCloseSequence(new PayloadData(code, reason), send, send, false);
		}


		private protected abstract void PerformCloseSequence(PayloadData payloadData, bool send, bool receive, bool received);


		private void StartCloseAsyncTask(ushort code, string reason)
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

			if (code == 1005)
			{
				// == no status
				StartCloseAsyncTask(PayloadData.Empty, true, true, false);
				return;
			}

			var send = !code.IsReserved();
			StartCloseAsyncTask(new PayloadData(code, reason), send, send, false);
		}


		private void StartCloseAsyncTask(
			PayloadData payloadData, bool send, bool receive, bool received
		)
		{
#if NET_CORE
			_ = System.Threading.Tasks.Task.Factory.StartNew(() => PerformCloseSequence(payloadData, send, receive, received));
#else
			Action<PayloadData, bool, bool, bool> closer = PerformCloseSequence;

			closer.BeginInvoke(
				payloadData, send, receive, received, ar => closer.EndInvoke(ar), null
				);
#endif
		}


		//private bool closeHandshake (byte[] frameAsBytes, bool receive, bool received)
		//{
		//  var sent = frameAsBytes != null && sendBytes (frameAsBytes);

		//  var wait = !received && sent && receive && _receivingExited != null;
		//  if (wait)
		//    received = _receivingExited.WaitOne (_waitTime);

		//  var ret = sent && received;

		//  _logger.Debug (
		//    String.Format (
		//      "Was clean?: {0} sent: {1} received: {2}", ret, sent, received
		//    )
		//  );

		//  return ret;
		//}


		private protected bool CloseHandshake(Stream stream, ManualResetEvent receivingExited, PayloadData payloadData, bool send, bool receive, bool received)
		{
			var sent = false;

			if (send)
			{
				if (stream != null)
				{
					var frame = CreateCloseFrame(payloadData);

					lock (forSend)
					{
						sent = sendBytesInternal(stream, frame.ToArray());
					}

					UnmaskFrame(frame);
				}
			}

			var wait = !received && sent && receive && receivingExited != null;
			if (wait)
				received = receivingExited.WaitOne(waitTime, true);

			var ret = sent && received;

			logger.Debug($"Was clean?: {ret} sent: {sent} received: {received}");

			return ret;
		}


		//private MessageEventArgs dequeueFromMessageEventQueue ()
		//{
		//  lock (_messageEventQueue)
		//    return _messageEventQueue.Count > 0 ? _messageEventQueue.Dequeue () : null;
		//}


		private void EnqueueToMessageEventQueue(MessageEventArgs e)
		{
			lock (messageEventQueue)
				messageEventQueue.Enqueue(e);
		}


		protected void CallOnError(string message, Exception exception)
		{
			try
			{
				OnError.Emit(this, new ErrorEventArgs(message, exception));
			}
			catch (Exception ex)
			{
				logger.Error(ex.Message);
				logger.Debug(ex.ToString());
			}
		}


		protected void Fatal(string message, Exception exception)
		{
			try
			{
				var code = exception is WebSocketException
					? ((WebSocketException)exception).Code
					: CloseStatusCode.Abnormal;

				var payload = new PayloadData((ushort)code, message, exception);

				PerformCloseSequence(payload, !code.IsReserved(), false, false);
			}
			catch
			{
			}
		}


		protected void Fatal(string message, CloseStatusCode code)
		{
			try
			{
				var payload = new PayloadData((ushort)code, message);
				PerformCloseSequence(payload, !code.IsReserved(), false, false);
			}
			catch
			{
			}
		}


		private void message()
		{
			MessageEventArgs e;
			lock (messageEventQueue)
			{
				if (inMessage || messageEventQueue.Count == 0 || readyState != WebSocketState.Open)
					return;

				inMessage = true;
				e = messageEventQueue.Dequeue();
			}

			MessageHandler(e);
		}


		protected void open()
		{
			inMessage = true;
			startReceiving();
			try
			{
				OnOpen.Emit(this, EventArgs.Empty);
			}
			catch (Exception ex)
			{
				logger.Error(ex.ToString());
				CallOnError("An error has occurred during the OnOpen event.", ex);
			}

			MessageEventArgs e;
			lock (messageEventQueue)
			{
				if (messageEventQueue.Count == 0 || readyState != WebSocketState.Open)
				{
					inMessage = false;
					return;
				}

				e = messageEventQueue.Dequeue();
			}

#if NET_CORE
			_ = System.Threading.Tasks.Task.Factory.StartNew(() =>
			{
				MessageHandler(e);
			});
#else
			Action<MessageEventArgs> handler = MessageHandler;

			handler.BeginInvoke(e, ar => handler.EndInvoke(ar), null);
#endif
		}


		private bool PingInternal(byte[] data)
		{
			// client ping

			var frame = CreateFrame(Fin.Final, Opcode.Ping, data, false);

			return HandlePing(frame.ToArray(), waitTime);
		}


		private bool ProcessCloseFrame(WebSocketFrame frame)
		{
			// if there are unprocessed messages, process them
			while (HasMessage)
				message();

			var payload = frame.PayloadData;
			PerformCloseSequence(payload, !payload.HasReservedCode, false, true);

			return false;
		}


		private bool processDataFrame(WebSocketFrame frame)
		{
			EnqueueToMessageEventQueue(
									   frame.IsCompressed
										   ? new MessageEventArgs(
																  frame.Opcode, frame.PayloadData.ApplicationData.Decompress(compression))
										   : new MessageEventArgs(frame));

			return true;
		}


		private bool processFragmentFrame(WebSocketFrame frame)
		{
			if (!inContinuation)
			{
				// Must process first fragment.
				if (frame.IsContinuation)
					return true;

				fragmentsOpcode = frame.Opcode;
				fragmentsCompressed = frame.IsCompressed;
				fragmentsBuffer = new MemoryStream();
				inContinuation = true;
			}

			fragmentsBuffer.WriteBytes(frame.PayloadData.ApplicationData, 1024);
			if (frame.IsFinal)
			{
				using (fragmentsBuffer)
				{
					var data = fragmentsCompressed
						? fragmentsBuffer.DecompressToArray(compression)
						: fragmentsBuffer.ToArray();

					EnqueueToMessageEventQueue(new MessageEventArgs(fragmentsOpcode, data));
				}

				fragmentsBuffer = null;
				inContinuation = false;
			}

			return true;
		}


		private bool ProcessPingFrame(WebSocketFrame frame)
		{
			logger.Trace("A ping was received.");

			if (readyState != WebSocketState.Open)
			{
				logger.Error("The connection is closing.");
				return true;
			}

			var pong = CreatePongFrame(frame.PayloadData);
			var stream = this.socketStream;
			if (stream == null)
				return false;

			lock (forSend)
			{
				if (!sendBytesInternal(stream, pong.ToArray()))
					return false;
			}

			logger.Trace("A pong to this ping has been sent.");

			if (EmitOnPing)
			{
				UnmaskFrame(pong);

				EnqueueToMessageEventQueue(new MessageEventArgs(frame));
			}

			return true;
		}


		private bool ProcessPongFrame()
		{
			logger.Trace("A pong was received.");

			try
			{
				pongReceivedEvent?.Set();

				logger.Trace("Pong has been signaled.");

				return true;
			}
			catch (Exception ex)
			{
				logger.Error(ex.Message);
				logger.Debug(ex.ToString());

				return false;
			}
		}


		private bool ProcessReceivedFrame(WebSocketFrame frame)
		{
			string msg;
			if (!CheckReceivedFrame(frame, out msg))
				throw new WebSocketException(CloseStatusCode.ProtocolError, msg);

			frame.Unmask();
			return frame.IsFragment
				? processFragmentFrame(frame)
				: frame.IsData
					? processDataFrame(frame)
					: frame.IsPing
						? ProcessPingFrame(frame)
						: frame.IsPong
							? ProcessPongFrame()
							: frame.IsClose
								? ProcessCloseFrame(frame)
								: ProcessUnsupportedFrame(frame);
		}


		private bool ProcessUnsupportedFrame(WebSocketFrame frame)
		{
			logger.Fatal("An unsupported frame:" + frame.PrintToString(false));
			Fatal("There is no way to handle it.", CloseStatusCode.PolicyViolation);

			return false;
		}


		protected void ReleaseCommonResources(bool disposeReceivingExited)
		{
			try
			{
				fragmentsBuffer?.Dispose();
			}
			catch
			{
			}

			fragmentsBuffer = null;
			inContinuation = false;

			DisposePongReceived();

			DisposeReceivingExited(disposeReceivingExited);
		}


		private bool SendCompressFragmented(Opcode opcode, Stream stream)
		{
			string onErrorMessage = null;
			Exception onErrorException = null;

			try
			{
				lock (forSend)
				{
					var src = stream;
					var compressed = false;
					var sent = false;
					try
					{
						var compressionMethod = compression;

						if (compressionMethod != CompressionMethod.None)
						{
							stream = stream.Compress(compressionMethod);
							compressed = true;
						}

						sent = SendFragmentedInternal(opcode, stream, compressed);
						if (!sent)
						{
							onErrorMessage = $"Send failed. {opcode}";
						}
					}
					catch (Exception ex)
					{
						onErrorMessage = "An error has occurred during a send.";
						onErrorException = ex;
					}
					finally
					{
						if (compressed)
						{
							try
							{
								stream.Dispose();
							}
							catch
							{
							}
						}

						src.Dispose();
					}

					return sent;
				} // lock
			}
			finally
			{
				// call outside lock
				if (onErrorException != null)
					logger.Error(onErrorException.ToString());

				if (!string.IsNullOrEmpty(onErrorMessage))
					CallOnError(onErrorMessage, onErrorException);

			}
		}


		protected static int ReadFromStream(Stream stream, byte[] buff, int length)
		{
			var done = 0;

			while (done < length)
			{
				var reallyRead = stream.Read(buff, done, length - done);
				if (reallyRead <= 0)
					break; // ?! eof

				done += reallyRead;
			}

			return done;
		}


		private protected bool SendFragmentedInternal(Opcode opcode, Stream inputStream, bool compressed)
		{
			// caller locks

			var outputStream = socketStream;

			// returns false if send failed. there should be no other reason
			var len = inputStream.Length;
			if (len == 0)
				return SendSingleFragmentInternal(outputStream, Fin.Final, opcode, EmptyBytes, false); // returns false if not sent

			var quo = len / FragmentLength;
			var rem = (int)(len % FragmentLength);

			byte[] buff;
			if (quo == 0)
			{
				buff = new byte[rem];
				return ReadFromStream(inputStream, buff, rem) == rem
					   && SendSingleFragmentInternal(outputStream, Fin.Final, opcode, buff, compressed);
			}

			if (quo == 1 && rem == 0)
			{
				buff = new byte[FragmentLength];
				return ReadFromStream(inputStream, buff, FragmentLength) == FragmentLength
					   && SendSingleFragmentInternal(outputStream, Fin.Final, opcode, buff, compressed);
			}

			/* Send fragments */

			// Begin
			buff = new byte[FragmentLength];
			var sent = ReadFromStream(inputStream, buff, FragmentLength) == FragmentLength
					   && SendSingleFragmentInternal(outputStream, Fin.More, opcode, buff, compressed);

			if (!sent)
				return false;

			var n = rem == 0 ? quo - 2 : quo - 1;
			for (long i = 0; i < n; i++)
			{
				sent = ReadFromStream(inputStream, buff, FragmentLength) == FragmentLength
					   && SendSingleFragmentInternal(outputStream, Fin.More, Opcode.Cont, buff, false);

				if (!sent)
					return false;
			}

			// End
			if (rem == 0)
				rem = FragmentLength;
			else
				buff = new byte[rem];

			return ReadFromStream(inputStream, buff, rem) == rem
				   && SendSingleFragmentInternal(outputStream, Fin.Final, Opcode.Cont, buff, false);
		}


		private bool SendSingleFragmentInternal(Stream stream, Fin fin, Opcode opcode, byte[] data, bool compressed)
		{
			// caller locks

			if (readyState != WebSocketState.Open)
			{
				logger.Error("The connection is closing.");
				return false;
			}

			if (stream == null)
			{
				logger.Error("The stream is null.");
				return false;
			}

			var frame = CreateFrame(fin, opcode, data, compressed);

			return sendBytesInternal(stream, frame.ToArray());
		}


		private void SendCompressFragmentedAsync(Opcode opcode, Stream stream, Action<bool> completed)
		{
#if NET_CORE
			var task = System.Threading.Tasks.Task.Factory.StartNew(() =>
			{
				var s = SendCompressFragmented(opcode, stream);
				return s;
			});

			task.ContinueWith((t) =>
			{
				if (!t.IsFaulted && t.Exception == null && t.Result)
				{
					if (completed != null)
						completed(t.Result);
				}
				else
				{
					logger.Error(t.Exception?.ToString());
					CallOnError("An error has occurred during the callback for an async send.", t.Exception == null ? null : t.Exception);
				}
			});
#else
			Func<Opcode, Stream, bool> sender = SendCompressFragmented;

			sender.BeginInvoke(opcode, stream, ar =>
		{
			try
			{
				var sent = sender.EndInvoke(ar);
				if (completed != null)
					completed(sent);
			}
			catch (Exception ex)
			{
				logger.Error(ex.ToString());
				CallOnError("An error has occurred during the callback for an async send.", ex);
			}
		},
		null
	  );
#endif
		}


		protected bool sendBytesInternal(Stream stream, byte[] bytes)
		{
			// caller locks

			try
			{
				stream.Write(bytes, 0, bytes.Length);
			}
			catch (Exception ex)
			{
				logger.Error(ex.Message);
				logger.Debug(ex.ToString());

				return false;
			}

			return true;
		}


		private void startReceiving()
		{
			lock (messageEventQueue)
			{
				if (messageEventQueue.Count > 0)
					messageEventQueue.Clear();
			}

			DisposePongReceived();
			DisposeReceivingExited(true);

			pongReceivedEvent = new ManualResetEvent(false);
			receivingExitedEvent = new ManualResetEvent(false);

			ReceiveLoop();
		}


		private void ReceiveLoop()
		{
			void OnReadCompleted(WebSocketFrame frame)
			{
				var receivedFrameResult = ProcessReceivedFrame(frame);
				var closed = readyState == WebSocketState.Closed;

				if (!receivedFrameResult || closed)
				{
					logger.Info($"ReceiveLoop exit closed={closed} receivedFrameResult={receivedFrameResult}");

					receivingExitedEvent?.Set();
					return;
				}

				// Receive next asap because the Ping or Close needs a response to it.
				ReceiveLoop();

				if (inMessage || !HasMessage || readyState != WebSocketState.Open)
					return;

				message();
			}

			void OnReadFailed(Exception ex)
			{
				logger.Fatal(ex.ToString());
				Fatal("An exception has occurred while receiving.", ex);
			}

			WebSocketFrame.ReadFrameAsync(socketStream, false, OnReadCompleted, OnReadFailed);
		}


		private void DisposeReceivingExited(bool disposeReceivingExited)
		{
			try
			{
				if (disposeReceivingExited)
					receivingExitedEvent?.Dispose();
			}
			catch
			{
			}

			receivingExitedEvent = null;
		}


		private void DisposePongReceived()
		{
			try
			{
				pongReceivedEvent?.Dispose();
			}
			catch
			{
			}

			pongReceivedEvent = null;
		}


		private protected void CallOnMessage(MessageEventArgs args)
		{
			try
			{
				OnMessage.Emit(this, args);
			}
			catch (Exception ex)
			{
				logger.Error(ex.ToString());
				CallOnError("An error has occurred during an OnMessage event.", ex);
			}
		}


		private protected void CallOnClose(CloseEventArgs args)
		{
			try
			{
				OnClose.Emit(this, args);
			}
			catch (Exception ex)
			{
				logger.Error(ex.ToString());
				CallOnError("An error has occurred during the OnClose event.", ex);
			}
		}


		internal static string CreateResponseKey(string base64Key)
		{
			var buff = new StringBuilder(base64Key, 64);
			buff.Append(guid);
			SHA1 sha1 = new SHA1CryptoServiceProvider();
			var src = sha1.ComputeHash(buff.ToString().UTF8Encode());

			return Convert.ToBase64String(src);
		}


		protected bool HandlePing(byte[] frameAsBytes, TimeSpan timeout)
		{
			bool TryEnterPingBlock()
			{
				// if (insidePingBlock == 0) insidePingBlock = 1
				// returns previous value
				return Interlocked.CompareExchange(ref insidePingBlock, 1, 0) > 0;
			}

			if (readyState != WebSocketState.Open)
				return false;

			var pongReceived = this.pongReceivedEvent;
			if (pongReceived == null)
				return false;

			if (TryEnterPingBlock())
			{
				// already in ping.. wait for result

				try
				{
					return pongReceived.WaitOne(timeout, true);
				}
				catch (Exception ex)
				{
					logger.Fatal($"HandlePing (a) {ex.Message}");

					return false;
				}
			}
			else
			{
				// send request and wait for reply

				try
				{
					pongReceived.Reset();

					if (readyState != WebSocketState.Open)
						return false;

					var stream = this.socketStream;
					if (stream == null)
						return false;

					lock (forSend)
					{
						if (!sendBytesInternal(stream, frameAsBytes))
							return false;
					}

					return pongReceived.WaitOne(timeout, true);
				}
				catch (Exception ex)
				{
					logger.Fatal($"HandlePing (r) {ex.Message}");

					return false;
				}
				finally
				{
					insidePingBlock = 0;
				}
			}
		}


		/// <summary>
		///     Closes the connection.
		/// </summary>
		/// <remarks>
		///     This method does nothing if the current state of the connection is
		///     Closing or Closed.
		/// </remarks>
		public void Close()
		{
			PerformCloseSequence(1005, String.Empty);
		}


		/// <summary>
		///     Closes the connection with the specified code.
		/// </summary>
		/// <remarks>
		///     This method does nothing if the current state of the connection is
		///     Closing or Closed.
		/// </remarks>
		/// <param name="code">
		///     <para>
		///         A <see cref="ushort" /> that represents the status code indicating
		///         the reason for the close.
		///     </para>
		///     <para>
		///         The status codes are defined in
		///         <see href="http://tools.ietf.org/html/rfc6455#section-7.4">
		///             Section 7.4
		///         </see>
		///         of RFC 6455.
		///     </para>
		/// </param>
		/// <exception cref="ArgumentOutOfRangeException">
		///     <paramref name="code" /> is less than 1000 or greater than 4999.
		/// </exception>
		/// <exception cref="ArgumentException">
		///     <para>
		///         <paramref name="code" /> is 1011 (server error).
		///         It cannot be used by clients.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         <paramref name="code" /> is 1010 (mandatory extension).
		///         It cannot be used by servers.
		///     </para>
		/// </exception>
		public void Close(ushort code)
		{
			if (!code.IsCloseStatusCode())
			{
				throw new ArgumentOutOfRangeException(nameof(code), "Less than 1000 or greater than 4999.");
			}

			CheckCode(code);

			PerformCloseSequence(code, String.Empty);
		}


		/// <summary>
		///     Closes the connection with the specified code.
		/// </summary>
		/// <remarks>
		///     This method does nothing if the current state of the connection is
		///     Closing or Closed.
		/// </remarks>
		/// <param name="code">
		///     <para>
		///         One of the <see cref="CloseStatusCode" /> enum values.
		///     </para>
		///     <para>
		///         It represents the status code indicating the reason for the close.
		///     </para>
		/// </param>
		/// <exception cref="ArgumentException">
		///     <para>
		///         <paramref name="code" /> is
		///         <see cref="CloseStatusCode.ServerError" />.
		///         It cannot be used by clients.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         <paramref name="code" /> is
		///         <see cref="CloseStatusCode.MandatoryExtension" />.
		///         It cannot be used by servers.
		///     </para>
		/// </exception>
		public void Close(CloseStatusCode code)
		{
			CheckCloseStatus(code);

			PerformCloseSequence((ushort)code, String.Empty);
		}


		/// <summary>
		///     Closes the connection with the specified code and reason.
		/// </summary>
		/// <remarks>
		///     This method does nothing if the current state of the connection is
		///     Closing or Closed.
		/// </remarks>
		/// <param name="code">
		///     <para>
		///         A <see cref="ushort" /> that represents the status code indicating
		///         the reason for the close.
		///     </para>
		///     <para>
		///         The status codes are defined in
		///         <see href="http://tools.ietf.org/html/rfc6455#section-7.4">
		///             Section 7.4
		///         </see>
		///         of RFC 6455.
		///     </para>
		/// </param>
		/// <param name="reason">
		///     <para>
		///         A <see cref="string" /> that represents the reason for the close.
		///     </para>
		///     <para>
		///         The size must be 123 bytes or less in UTF-8.
		///     </para>
		/// </param>
		/// <exception cref="ArgumentOutOfRangeException">
		///     <para>
		///         <paramref name="code" /> is less than 1000 or greater than 4999.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         The size of <paramref name="reason" /> is greater than 123 bytes.
		///     </para>
		/// </exception>
		/// <exception cref="ArgumentException">
		///     <para>
		///         <paramref name="code" /> is 1011 (server error).
		///         It cannot be used by clients.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         <paramref name="code" /> is 1010 (mandatory extension).
		///         It cannot be used by servers.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         <paramref name="code" /> is 1005 (no status) and there is reason.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         <paramref name="reason" /> could not be UTF-8-encoded.
		///     </para>
		/// </exception>
		public void Close(ushort code, string reason)
		{
			if (!code.IsCloseStatusCode())
			{
				throw new ArgumentOutOfRangeException(nameof(code), "Less than 1000 or greater than 4999.");
			}

			CheckCode(code);

			if (reason.IsNullOrEmpty())
			{
				PerformCloseSequence(code, String.Empty);
				return;
			}

			if (code == 1005)
			{
				throw new ArgumentException("1005 cannot be used.", nameof(code));
			}

			byte[] bytes;
			if (!reason.TryGetUTF8EncodedBytes(out bytes))
			{
				throw new ArgumentException("It could not be UTF-8-encoded.", nameof(reason));
			}

			if (bytes.Length > 123)
			{
				throw new ArgumentOutOfRangeException(nameof(reason), "Its size is greater than 123 bytes.");
			}

			PerformCloseSequence(code, reason);
		}


		/// <summary>
		///     Closes the connection with the specified code and reason.
		/// </summary>
		/// <remarks>
		///     This method does nothing if the current state of the connection is
		///     Closing or Closed.
		/// </remarks>
		/// <param name="code">
		///     <para>
		///         One of the <see cref="CloseStatusCode" /> enum values.
		///     </para>
		///     <para>
		///         It represents the status code indicating the reason for the close.
		///     </para>
		/// </param>
		/// <param name="reason">
		///     <para>
		///         A <see cref="string" /> that represents the reason for the close.
		///     </para>
		///     <para>
		///         The size must be 123 bytes or less in UTF-8.
		///     </para>
		/// </param>
		/// <exception cref="ArgumentException">
		///     <para>
		///         <paramref name="code" /> is
		///         <see cref="CloseStatusCode.ServerError" />.
		///         It cannot be used by clients.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         <paramref name="code" /> is
		///         <see cref="CloseStatusCode.MandatoryExtension" />.
		///         It cannot be used by servers.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         <paramref name="code" /> is
		///         <see cref="CloseStatusCode.NoStatus" /> and there is reason.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         <paramref name="reason" /> could not be UTF-8-encoded.
		///     </para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		///     The size of <paramref name="reason" /> is greater than 123 bytes.
		/// </exception>
		public void Close(CloseStatusCode code, string reason)
		{
			CheckCloseStatus(code);

			if (reason.IsNullOrEmpty())
			{
				PerformCloseSequence((ushort)code, String.Empty);
				return;
			}

			if (code == CloseStatusCode.NoStatus)
			{
				throw new ArgumentException("NoStatus cannot be used.", nameof(code));
			}

			byte[] bytes;
			if (!reason.TryGetUTF8EncodedBytes(out bytes))
			{
				throw new ArgumentException("It could not be UTF-8-encoded.", nameof(reason));
			}

			if (bytes.Length > 123)
			{
				throw new ArgumentOutOfRangeException(nameof(reason), "Its size is greater than 123 bytes.");
			}

			PerformCloseSequence((ushort)code, reason);
		}


		/// <summary>
		///     Closes the connection asynchronously.
		/// </summary>
		/// <remarks>
		///     <para>
		///         This method does not wait for the close to be complete.
		///     </para>
		///     <para>
		///         This method does nothing if the current state of the connection is
		///         Closing or Closed.
		///     </para>
		/// </remarks>
		public void CloseAsync()
		{
			StartCloseAsyncTask(1005, String.Empty);
		}


		/// <summary>
		///     Closes the connection asynchronously with the specified code.
		/// </summary>
		/// <remarks>
		///     <para>
		///         This method does not wait for the close to be complete.
		///     </para>
		///     <para>
		///         This method does nothing if the current state of the connection is
		///         Closing or Closed.
		///     </para>
		/// </remarks>
		/// <param name="code">
		///     <para>
		///         A <see cref="ushort" /> that represents the status code indicating
		///         the reason for the close.
		///     </para>
		///     <para>
		///         The status codes are defined in
		///         <see href="http://tools.ietf.org/html/rfc6455#section-7.4">
		///             Section 7.4
		///         </see>
		///         of RFC 6455.
		///     </para>
		/// </param>
		/// <exception cref="ArgumentOutOfRangeException">
		///     <paramref name="code" /> is less than 1000 or greater than 4999.
		/// </exception>
		/// <exception cref="ArgumentException">
		///     <para>
		///         <paramref name="code" /> is 1011 (server error).
		///         It cannot be used by clients.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         <paramref name="code" /> is 1010 (mandatory extension).
		///         It cannot be used by servers.
		///     </para>
		/// </exception>
		public void CloseAsync(ushort code)
		{
			if (!code.IsCloseStatusCode())
			{
				throw new ArgumentOutOfRangeException(nameof(code), "Less than 1000 or greater than 4999.");
			}

			CheckCode(code);

			StartCloseAsyncTask(code, String.Empty);
		}


		/// <summary>
		///     Closes the connection asynchronously with the specified code.
		/// </summary>
		/// <remarks>
		///     <para>
		///         This method does not wait for the close to be complete.
		///     </para>
		///     <para>
		///         This method does nothing if the current state of the connection is
		///         Closing or Closed.
		///     </para>
		/// </remarks>
		/// <param name="code">
		///     <para>
		///         One of the <see cref="CloseStatusCode" /> enum values.
		///     </para>
		///     <para>
		///         It represents the status code indicating the reason for the close.
		///     </para>
		/// </param>
		/// <exception cref="ArgumentException">
		///     <para>
		///         <paramref name="code" /> is
		///         <see cref="CloseStatusCode.ServerError" />.
		///         It cannot be used by clients.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         <paramref name="code" /> is
		///         <see cref="CloseStatusCode.MandatoryExtension" />.
		///         It cannot be used by servers.
		///     </para>
		/// </exception>
		public void CloseAsync(CloseStatusCode code)
		{
			CheckCloseStatus(code);

			StartCloseAsyncTask((ushort)code, String.Empty);
		}


		/// <summary>
		///     Closes the connection asynchronously with the specified code and reason.
		/// </summary>
		/// <remarks>
		///     <para>
		///         This method does not wait for the close to be complete.
		///     </para>
		///     <para>
		///         This method does nothing if the current state of the connection is
		///         Closing or Closed.
		///     </para>
		/// </remarks>
		/// <param name="code">
		///     <para>
		///         A <see cref="ushort" /> that represents the status code indicating
		///         the reason for the close.
		///     </para>
		///     <para>
		///         The status codes are defined in
		///         <see href="http://tools.ietf.org/html/rfc6455#section-7.4">
		///             Section 7.4
		///         </see>
		///         of RFC 6455.
		///     </para>
		/// </param>
		/// <param name="reason">
		///     <para>
		///         A <see cref="string" /> that represents the reason for the close.
		///     </para>
		///     <para>
		///         The size must be 123 bytes or less in UTF-8.
		///     </para>
		/// </param>
		/// <exception cref="ArgumentOutOfRangeException">
		///     <para>
		///         <paramref name="code" /> is less than 1000 or greater than 4999.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         The size of <paramref name="reason" /> is greater than 123 bytes.
		///     </para>
		/// </exception>
		/// <exception cref="ArgumentException">
		///     <para>
		///         <paramref name="code" /> is 1011 (server error).
		///         It cannot be used by clients.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         <paramref name="code" /> is 1010 (mandatory extension).
		///         It cannot be used by servers.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         <paramref name="code" /> is 1005 (no status) and there is reason.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         <paramref name="reason" /> could not be UTF-8-encoded.
		///     </para>
		/// </exception>
		public void CloseAsync(ushort code, string reason)
		{
			if (!code.IsCloseStatusCode())
			{
				throw new ArgumentOutOfRangeException(nameof(code), "Less than 1000 or greater than 4999.");
			}

			CheckCode(code);

			if (reason.IsNullOrEmpty())
			{
				StartCloseAsyncTask(code, String.Empty);
				return;
			}

			if (code == 1005)
			{
				throw new ArgumentException("1005 cannot be used.", nameof(code));
			}

			byte[] bytes;
			if (!reason.TryGetUTF8EncodedBytes(out bytes))
			{
				throw new ArgumentException("It could not be UTF-8-encoded.", nameof(reason));
			}

			if (bytes.Length > 123)
			{
				throw new ArgumentOutOfRangeException(nameof(reason), "Its size is greater than 123 bytes.");
			}

			StartCloseAsyncTask(code, reason);
		}


		/// <summary>
		///     Closes the connection asynchronously with the specified code and reason.
		/// </summary>
		/// <remarks>
		///     <para>
		///         This method does not wait for the close to be complete.
		///     </para>
		///     <para>
		///         This method does nothing if the current state of the connection is
		///         Closing or Closed.
		///     </para>
		/// </remarks>
		/// <param name="code">
		///     <para>
		///         One of the <see cref="CloseStatusCode" /> enum values.
		///     </para>
		///     <para>
		///         It represents the status code indicating the reason for the close.
		///     </para>
		/// </param>
		/// <param name="reason">
		///     <para>
		///         A <see cref="string" /> that represents the reason for the close.
		///     </para>
		///     <para>
		///         The size must be 123 bytes or less in UTF-8.
		///     </para>
		/// </param>
		/// <exception cref="ArgumentException">
		///     <para>
		///         <paramref name="code" /> is
		///         <see cref="CloseStatusCode.ServerError" />.
		///         It cannot be used by clients.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         <paramref name="code" /> is
		///         <see cref="CloseStatusCode.MandatoryExtension" />.
		///         It cannot be used by servers.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         <paramref name="code" /> is
		///         <see cref="CloseStatusCode.NoStatus" /> and there is reason.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         <paramref name="reason" /> could not be UTF-8-encoded.
		///     </para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		///     The size of <paramref name="reason" /> is greater than 123 bytes.
		/// </exception>
		public void CloseAsync(CloseStatusCode code, string reason)
		{
			CheckCloseStatus(code);

			if (reason.IsNullOrEmpty())
			{
				StartCloseAsyncTask((ushort)code, String.Empty);
				return;
			}

			if (code == CloseStatusCode.NoStatus)
			{
				throw new ArgumentException("NoStatus cannot be used.", nameof(code));
			}

			byte[] bytes;
			if (!reason.TryGetUTF8EncodedBytes(out bytes))
			{
				throw new ArgumentException("It could not be UTF-8-encoded.", nameof(reason));
			}

			if (bytes.Length > 123)
			{
				throw new ArgumentOutOfRangeException(nameof(reason), "Its size is greater than 123 bytes.");
			}

			StartCloseAsyncTask((ushort)code, reason);
		}


		/// <summary>
		///     Sends a ping using the WebSocket connection.
		/// </summary>
		/// <returns>
		///     <c>true</c> if the send has done with no error and a pong has been
		///     received within a time; otherwise, <c>false</c>.
		/// </returns>
		public bool Ping()
		{
			return PingInternal(EmptyBytes);
		}


		/// <summary>
		///     Sends a ping with <paramref name="message" /> using the WebSocket
		///     connection.
		/// </summary>
		/// <returns>
		///     <c>true</c> if the send has done with no error and a pong has been
		///     received within a time; otherwise, <c>false</c>.
		/// </returns>
		/// <param name="message">
		///     <para>
		///         A <see cref="string" /> that represents the message to send.
		///     </para>
		///     <para>
		///         The size must be 125 bytes or less in UTF-8.
		///     </para>
		/// </param>
		/// <exception cref="ArgumentException">
		///     <paramref name="message" /> could not be UTF-8-encoded.
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		///     The size of <paramref name="message" /> is greater than 125 bytes.
		/// </exception>
		public bool Ping(string message)
		{
			if (message.IsNullOrEmpty())
				return PingInternal(EmptyBytes);

			byte[] bytes;
			if (!message.TryGetUTF8EncodedBytes(out bytes))
			{
				throw new ArgumentException("It could not be UTF-8-encoded.", nameof(message));
			}

			if (bytes.Length > 125)
			{
				throw new ArgumentOutOfRangeException(nameof(message), "Its size is greater than 125 bytes.");
			}

			return PingInternal(bytes);
		}


		/// <summary>
		///     Sends the specified data using the WebSocket connection.
		/// </summary>
		/// <param name="data">
		///     An array of <see cref="byte" /> that represents the binary data to send.
		/// </param>
		/// <exception cref="InvalidOperationException">
		///     The current state of the connection is not Open.
		/// </exception>
		/// <exception cref="ArgumentNullException">
		///     <paramref name="data" /> is <see langword="null" />.
		/// </exception>
		public bool Send(byte[] data)
		{
			if (readyState != WebSocketState.Open)
			{
				throw new InvalidOperationException("The current state of the connection is not Open.");
			}

			if (data == null)
				throw new ArgumentNullException(nameof(data));

			return SendCompressFragmented(Opcode.Binary, new MemoryStream(data));
		}


		/// <summary>
		///     Sends the specified file using the WebSocket connection.
		/// </summary>
		/// <param name="fileInfo">
		///     <para>
		///         A <see cref="FileInfo" /> that specifies the file to send.
		///     </para>
		///     <para>
		///         The file is sent as the binary data.
		///     </para>
		/// </param>
		/// <exception cref="InvalidOperationException">
		///     The current state of the connection is not Open.
		/// </exception>
		/// <exception cref="ArgumentNullException">
		///     <paramref name="fileInfo" /> is <see langword="null" />.
		/// </exception>
		/// <exception cref="ArgumentException">
		///     <para>
		///         The file does not exist.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         The file could not be opened.
		///     </para>
		/// </exception>
		public bool Send(FileInfo fileInfo)
		{
			if (readyState != WebSocketState.Open)
			{
				throw new InvalidOperationException("The current state of the connection is not Open.");
			}

			if (fileInfo == null)
				throw new ArgumentNullException(nameof(fileInfo));

			if (!fileInfo.Exists)
			{
				throw new ArgumentException("The file does not exist.", nameof(fileInfo));
			}

			FileStream stream;
			if (!fileInfo.TryOpenRead(out stream))
			{
				throw new ArgumentException("The file could not be opened.", nameof(fileInfo));
			}

			return SendCompressFragmented(Opcode.Binary, stream);
		}


		/// <summary>
		///     Sends the specified data using the WebSocket connection.
		/// </summary>
		/// <param name="data">
		///     A <see cref="string" /> that represents the text data to send.
		/// </param>
		/// <exception cref="InvalidOperationException">
		///     The current state of the connection is not Open.
		/// </exception>
		/// <exception cref="ArgumentNullException">
		///     <paramref name="data" /> is <see langword="null" />.
		/// </exception>
		/// <exception cref="ArgumentException">
		///     <paramref name="data" /> could not be UTF-8-encoded.
		/// </exception>
		public bool Send(string data)
		{
			if (readyState != WebSocketState.Open)
			{
				throw new InvalidOperationException("The current state of the connection is not Open.");
			}

			if (data == null)
				throw new ArgumentNullException(nameof(data));

			byte[] bytes;
			if (!data.TryGetUTF8EncodedBytes(out bytes))
			{
				throw new ArgumentException("It could not be UTF-8-encoded.", nameof(data));
			}

			return SendCompressFragmented(Opcode.Text, new MemoryStream(bytes));
		}


		/// <summary>
		///     Sends the data from the specified stream using the WebSocket connection.
		/// </summary>
		/// <param name="stream">
		///     <para>
		///         A <see cref="Stream" /> instance from which to read the data to send.
		///     </para>
		///     <para>
		///         The data is sent as the binary data.
		///     </para>
		/// </param>
		/// <param name="length">
		///     An <see cref="int" /> that specifies the number of bytes to send.
		/// </param>
		/// <exception cref="InvalidOperationException">
		///     The current state of the connection is not Open.
		/// </exception>
		/// <exception cref="ArgumentNullException">
		///     <paramref name="stream" /> is <see langword="null" />.
		/// </exception>
		/// <exception cref="ArgumentException">
		///     <para>
		///         <paramref name="stream" /> cannot be read.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         <paramref name="length" /> is less than 1.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         No data could be read from <paramref name="stream" />.
		///     </para>
		/// </exception>
		public bool Send(Stream stream, int length)
		{
			if (readyState != WebSocketState.Open)
			{
				throw new InvalidOperationException("The current state of the connection is not Open.");
			}

			if (stream == null)
				throw new ArgumentNullException(nameof(stream));

			if (!stream.CanRead)
			{
				throw new ArgumentException("It cannot be read.", nameof(stream));
			}

			if (length < 1)
			{
				throw new ArgumentException("Less than 1.", nameof(length));
			}

			var bytes = stream.ReadBytes(length);

			var len = bytes.Length;
			if (len == 0)
			{
				throw new ArgumentException("No data could be read from it.", nameof(stream));
			}

			if (len < length)
			{
				logger.Warn($"Only {len} byte(s) of data could be read from the stream.");
			}

			return SendCompressFragmented(Opcode.Binary, new MemoryStream(bytes));
		}


		/// <summary>
		///     Sends the specified data asynchronously using the WebSocket connection.
		/// </summary>
		/// <remarks>
		///     This method does not wait for the send to be complete.
		/// </remarks>
		/// <param name="data">
		///     An array of <see cref="byte" /> that represents the binary data to send.
		/// </param>
		/// <param name="completed">
		///     <para>
		///         An <c>Action&lt;bool&gt;</c> delegate or <see langword="null" />
		///         if not needed.
		///     </para>
		///     <para>
		///         The delegate invokes the method called when the send is complete.
		///     </para>
		///     <para>
		///         <c>true</c> is passed to the method if the send has done with
		///         no error; otherwise, <c>false</c>.
		///     </para>
		/// </param>
		/// <exception cref="InvalidOperationException">
		///     The current state of the connection is not Open.
		/// </exception>
		/// <exception cref="ArgumentNullException">
		///     <paramref name="data" /> is <see langword="null" />.
		/// </exception>
		public void SendAsync(byte[] data, Action<bool> completed)
		{
			if (readyState != WebSocketState.Open)
			{
				throw new InvalidOperationException("The current state of the connection is not Open.");
			}

			if (data == null)
				throw new ArgumentNullException(nameof(data));

			SendCompressFragmentedAsync(Opcode.Binary, new MemoryStream(data), completed);
		}


		/// <summary>
		///     Sends the specified file asynchronously using the WebSocket connection.
		/// </summary>
		/// <remarks>
		///     This method does not wait for the send to be complete.
		/// </remarks>
		/// <param name="fileInfo">
		///     <para>
		///         A <see cref="FileInfo" /> that specifies the file to send.
		///     </para>
		///     <para>
		///         The file is sent as the binary data.
		///     </para>
		/// </param>
		/// <param name="completed">
		///     <para>
		///         An <c>Action&lt;bool&gt;</c> delegate or <see langword="null" />
		///         if not needed.
		///     </para>
		///     <para>
		///         The delegate invokes the method called when the send is complete.
		///     </para>
		///     <para>
		///         <c>true</c> is passed to the method if the send has done with
		///         no error; otherwise, <c>false</c>.
		///     </para>
		/// </param>
		/// <exception cref="InvalidOperationException">
		///     The current state of the connection is not Open.
		/// </exception>
		/// <exception cref="ArgumentNullException">
		///     <paramref name="fileInfo" /> is <see langword="null" />.
		/// </exception>
		/// <exception cref="ArgumentException">
		///     <para>
		///         The file does not exist.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         The file could not be opened.
		///     </para>
		/// </exception>
		public void SendAsync(FileInfo fileInfo, Action<bool> completed)
		{
			if (readyState != WebSocketState.Open)
			{
				throw new InvalidOperationException("The current state of the connection is not Open.");
			}

			if (fileInfo == null)
				throw new ArgumentNullException(nameof(fileInfo));

			if (!fileInfo.Exists)
			{
				throw new ArgumentException("The file does not exist.", nameof(fileInfo));
			}

			FileStream stream;
			if (!fileInfo.TryOpenRead(out stream))
			{
				throw new ArgumentException("The file could not be opened.", nameof(fileInfo));
			}

			SendCompressFragmentedAsync(Opcode.Binary, stream, completed);
		}


		/// <summary>
		///     Sends the specified data asynchronously using the WebSocket connection.
		/// </summary>
		/// <remarks>
		///     This method does not wait for the send to be complete.
		/// </remarks>
		/// <param name="data">
		///     A <see cref="string" /> that represents the text data to send.
		/// </param>
		/// <param name="completed">
		///     <para>
		///         An <c>Action&lt;bool&gt;</c> delegate or <see langword="null" />
		///         if not needed.
		///     </para>
		///     <para>
		///         The delegate invokes the method called when the send is complete.
		///     </para>
		///     <para>
		///         <c>true</c> is passed to the method if the send has done with
		///         no error; otherwise, <c>false</c>.
		///     </para>
		/// </param>
		/// <exception cref="InvalidOperationException">
		///     The current state of the connection is not Open.
		/// </exception>
		/// <exception cref="ArgumentNullException">
		///     <paramref name="data" /> is <see langword="null" />.
		/// </exception>
		/// <exception cref="ArgumentException">
		///     <paramref name="data" /> could not be UTF-8-encoded.
		/// </exception>
		public void SendAsync(string data, Action<bool> completed)
		{
			if (readyState != WebSocketState.Open)
			{
				throw new InvalidOperationException("The current state of the connection is not Open.");
			}

			if (data == null)
				throw new ArgumentNullException(nameof(data));

			byte[] bytes;
			if (!data.TryGetUTF8EncodedBytes(out bytes))
			{
				throw new ArgumentException("It could not be UTF-8-encoded.", nameof(data));
			}

			SendCompressFragmentedAsync(Opcode.Text, new MemoryStream(bytes), completed);
		}


		/// <summary>
		///     Sends the data from the specified stream asynchronously using
		///     the WebSocket connection.
		/// </summary>
		/// <remarks>
		///     This method does not wait for the send to be complete.
		/// </remarks>
		/// <param name="stream">
		///     <para>
		///         A <see cref="Stream" /> instance from which to read the data to send.
		///     </para>
		///     <para>
		///         The data is sent as the binary data.
		///     </para>
		/// </param>
		/// <param name="length">
		///     An <see cref="int" /> that specifies the number of bytes to send.
		/// </param>
		/// <param name="completed">
		///     <para>
		///         An <c>Action&lt;bool&gt;</c> delegate or <see langword="null" />
		///         if not needed.
		///     </para>
		///     <para>
		///         The delegate invokes the method called when the send is complete.
		///     </para>
		///     <para>
		///         <c>true</c> is passed to the method if the send has done with
		///         no error; otherwise, <c>false</c>.
		///     </para>
		/// </param>
		/// <exception cref="InvalidOperationException">
		///     The current state of the connection is not Open.
		/// </exception>
		/// <exception cref="ArgumentNullException">
		///     <paramref name="stream" /> is <see langword="null" />.
		/// </exception>
		/// <exception cref="ArgumentException">
		///     <para>
		///         <paramref name="stream" /> cannot be read.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         <paramref name="length" /> is less than 1.
		///     </para>
		///     <para>
		///         -or-
		///     </para>
		///     <para>
		///         No data could be read from <paramref name="stream" />.
		///     </para>
		/// </exception>
		public void SendAsync(Stream stream, int length, Action<bool> completed)
		{
			if (readyState != WebSocketState.Open)
			{
				throw new InvalidOperationException("The current state of the connection is not Open.");
			}

			if (stream == null)
				throw new ArgumentNullException(nameof(stream));

			if (!stream.CanRead)
			{
				throw new ArgumentException("It cannot be read.", nameof(stream));
			}

			if (length < 1)
			{
				throw new ArgumentException("Less than 1.", nameof(length));
			}

			var bytes = stream.ReadBytes(length);

			var len = bytes.Length;
			if (len == 0)
			{
				throw new ArgumentException("No data could be read from it.", nameof(stream));
			}

			if (len < length)
			{
				logger.Warn($"Only {len} byte(s) of data could be read from the stream.");
			}

			SendCompressFragmentedAsync(Opcode.Binary, new MemoryStream(bytes), completed);
		}


		private protected abstract WebSocketFrame CreateCloseFrame(PayloadData payloadData);


		private protected abstract WebSocketFrame CreatePongFrame(PayloadData payloadData);


		private protected abstract WebSocketFrame CreateFrame(Fin fin, Opcode opcode, byte[] data, bool compressed);


		private protected abstract void CheckCode(ushort code);


		private protected abstract void CheckCloseStatus(CloseStatusCode code);


		private protected abstract string CheckFrameMask(WebSocketFrame frame);


		private protected abstract void UnmaskFrame(WebSocketFrame frame);
	}
}
