// --------------------------------------------------------------------------------------------------------------------
// <copyright file="WebSocketStreamReader.cs" company="Reimers.dk">
//   The MIT License
//   Copyright (c) 2012-2014 sta.blockhead
//   Copyright (c) 2014 Reimers.dk
//   
//   Permission is hereby granted, free of charge, to any person obtaining a copy  of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
//   
//   The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
//   
//   THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// </copyright>
// <summary>
//   Defines the WebSocketStreamReader type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace WebSocketSharp
{
	using System;
	using System.Collections.Generic;
	using System.Diagnostics;
	using System.IO;
	using System.Threading;

	internal class WebSocketStreamReader
	{
		private readonly Stream _innerStream;
		private readonly ManualResetEventSlim _waitHandle = new ManualResetEventSlim(false);
		private bool _isReading;

		public WebSocketStreamReader(Stream innerStream)
		{
			_innerStream = innerStream;
		}

		public IEnumerable<WebSocketMessage> Read()
		{
			lock (_innerStream)
			{
				if (_isReading)
				{
					yield break;
				}

				_isReading = true;
			}

			var closed = false;
			while (!closed)
			{
				_waitHandle.Reset();
				var header = ReadHeader();
				if (header == null)
				{
					yield break;
				}

				var readInfo = GetStreamReadInfo(header);

				var msg = CreateMessage(header, readInfo, _waitHandle);
				if (msg.Opcode == Opcode.Close)
				{
					closed = true;
				}

				yield return msg;

				_waitHandle.Wait();
			}
		}

		private WebSocketMessage CreateMessage(WebSocketFrameHeader header, StreamReadInfo readInfo, ManualResetEventSlim waitHandle)
		{
			switch (header.Opcode)
			{
				case Opcode.Cont:
					throw new WebSocketException(CloseStatusCode.InconsistentData, "Did not expect continuation frame.");
				default:
				case Opcode.Close:
				case Opcode.Text:
				case Opcode.Binary:
					return new FragmentedMessage(header.Opcode, _innerStream, readInfo, GetStreamReadInfo, waitHandle);
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

			/* Masking Key */

			var masked = header.Mask == Mask.Mask;
			var maskingKey = masked ? _innerStream.ReadBytes(4) : new byte[0];
			if (masked && maskingKey.Length != 4)
			{
				throw new WebSocketException("The 'Masking Key' of a frame cannot be read from the data source.");
			}

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

			var headerLength = 0;
			try
			{
				headerLength = _innerStream.Read(header, 0, 2);
			}
			catch (IOException)
			{
				return null;
			}

			if (headerLength == 0)
			{
				return null;
			}

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