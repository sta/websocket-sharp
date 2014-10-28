namespace WebSocketSharp
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.IO;
	using System.Threading;

	internal class WebSocketStreamReader
	{
		private readonly Stream _innerStream;

		private readonly bool _unmask;

		public WebSocketStreamReader(Stream innerStream, bool unmask = true)
		{
			_innerStream = innerStream;
			_unmask = unmask;
		}

		public IEnumerable<WebSocketMessage> Read()
		{
			var waitHandle = new ManualResetEventSlim(false);
			while (true)
			{
				waitHandle.Reset();
				var header = ReadHeader();

				var readInfo = GetStreamReadInfo(header);

				//byte[] data = null;

				//if (len > 0)
				//{
				//	// Check if allowable max length.
				//	if (header.PayloadLength > 126 && len > PayloadData.MaxLength)
				//	{
				//		throw new WebSocketException(CloseStatusCode.TooBig, "The length of 'Payload Data' of a frame is greater than the allowable max length.");
				//	}

				//	data = header.PayloadLength > 126
				//	? _innerStream.ReadBytes((long)len, 1024)
				//	: _innerStream.ReadBytes((int)len);

				//	if (data.LongLength != (long)len)
				//	{
				//		throw new WebSocketException("The 'Payload Data' of a frame cannot be read from the data source.");
				//	}
				//}
				//else
				//{
				//	data = new byte[0];
				//}

				//frame._payloadData = new PayloadData(data, masked);
				//if (unmask && masked)
				//{
				//	frame.Unmask();
				//}

				WebSocketMessage msg;
				switch (header.Opcode)
				{
					case Opcode.Binary:
						msg = new FragmentedMessage(header.Opcode, _innerStream, readInfo, GetStreamReadInfo, waitHandle);
						yield return msg;
						break;
					default:
						msg = new SimpleMessage(header.Opcode, waitHandle);
						msg.Consume();
						break;
				}

				waitHandle.Wait();
			}
		}

		private StreamReadInfo GetStreamReadInfo()
		{
			var h = ReadHeader();
			return GetStreamReadInfo(h);
		}

		private StreamReadInfo GetStreamReadInfo(WebSocketFrameHeader header)
		{
			/* Extended Payload Length */

			var size = header.PayloadLength < 126 ? 0 : header.PayloadLength == 126 ? 2 : 8;

			var extPayloadLen = size > 0 ? _innerStream.ReadBytes(size) : new byte[0];
			if (size > 0 && extPayloadLen.Length != size)
			{
				throw new WebSocketException("The 'Extended Payload Length' of a frame cannot be read from the data source.");
			}

			//frame._extPayloadLength = extPayloadLen;

			/* Masking Key */

			var masked = header.Mask == Mask.Mask;
			var maskingKey = masked ? _innerStream.ReadBytes(4) : new byte[0];
			if (masked && maskingKey.Length != 4)
			{
				throw new WebSocketException("The 'Masking Key' of a frame cannot be read from the data source.");
			}

			//frame._maskingKey = maskingKey;

			/* Payload Data */

			ulong len = header.PayloadLength < 126
							? header.PayloadLength
							: header.PayloadLength == 126
								  ? extPayloadLen.ToUInt16(ByteOrder.Big)
								  : extPayloadLen.ToUInt64(ByteOrder.Big);

			return new StreamReadInfo(header.Fin == Fin.Final, len, maskingKey);
		}

		private WebSocketFrameHeader ReadHeader()
		{
			var header = new byte[2];
			var headerLength = _innerStream.Read(header, 0, 2);
			if (headerLength != 2)
			{
				throw new WebSocketException("The header part of a frame cannot be read from the data source.");
			}

			var frameHeader = new WebSocketFrameHeader(header);
			var validation = WebSocketFrameHeader.Validate(frameHeader);

			if (validation != null)
			{
				throw new WebSocketException(CloseStatusCode.ProtocolError, validation);
			}

			return frameHeader;
		}
	}
}