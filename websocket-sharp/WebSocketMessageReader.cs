namespace WebSocketSharp
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.IO;
	using System.Text;
	using System.Threading;
	using System.Threading.Tasks;

	using WebSocketSharp.Net;

	internal class WebSocketMessageReader
	{
		private readonly bool _client;
		private readonly Stream _stream;

		private readonly Action<MessageEventArgs> _onMessage;
		private CompressionMethod _compression;
		private object _forMessageEventQueue;
		private volatile WebSocketState _readyState;
		private object _forEvent;
		private AutoResetEvent _exitReceiving;
		private Queue<MessageEventArgs> _messageEventQueue;
		private string _extensions;

		public WebSocketMessageReader(Stream websocketStream, Action<MessageEventArgs> onMessage)
		{
			_messageEventQueue = new Queue<MessageEventArgs>();
			_forMessageEventQueue = ((ICollection)_messageEventQueue).SyncRoot;
			_forEvent = new object();
			_stream = websocketStream;
			_onMessage = onMessage;
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
							var e = DequeueFromMessageEventQueue();
							if (e != null && _readyState == WebSocketState.Open)
							{
								_onMessage(e);
								//OnMessage.Emit(this, e);
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

		private MessageEventArgs DequeueFromMessageEventQueue()
		{
			lock (_forMessageEventQueue)
			{
				return _messageEventQueue.Count > 0 ? _messageEventQueue.Dequeue() : null;
			}
		}

		private void EnqueueToMessageEventQueue(MessageEventArgs e)
		{
			lock (_forMessageEventQueue)
			{
				_messageEventQueue.Enqueue(e);
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
					? new MessageEventArgs(frame.Opcode, frame.PayloadData.ApplicationData.Decompress(_compression))
					: new MessageEventArgs(frame.Opcode, frame.PayloadData.ToByteArray());

			EnqueueToMessageEventQueue(e);
			return true;
		}

		private void ProcessException(Exception exception, string message)
		{
			var code = CloseStatusCode.Abnormal;
			var reason = message;
			var socketException = exception as WebSocketException;
			if (socketException != null)
			{
				var wsex = socketException;
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
				InnerClose(code, reason ?? code.GetMessage(), false);
			}
		}

		private bool ProcessFragmentedFrame(WebSocketFrame frame)
		{
			return frame.IsContinuation || ProcessFragments(frame);
		}

		private MessageEventArgs ProcessFragments(WebSocketFrame first)
		{
			using (var buff = new MemoryStream())
			{
				buff.WriteBytes(first.PayloadData.ApplicationData);
				if (!ConcatenateFragmentsInto(buff))
				{
					return false;
				}

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
				else if (frame.IsContinuation)
				{
					/* MORE */

					// CONT
					dest.WriteBytes(frame.PayloadData.ApplicationData);
				}
				else
				{
					// ?
					return ProcessUnsupportedFrame(
						frame,
						CloseStatusCode.IncorrectData,
						"An incorrect data has been received while receiving fragmented data.");
				}
			}

			return true;
		}

		private bool ProcessUnsupportedFrame(WebSocketFrame frame, CloseStatusCode code, string reason)
		{
			ProcessException(new WebSocketException(code, reason), null);

			return false;
		}
	}
}