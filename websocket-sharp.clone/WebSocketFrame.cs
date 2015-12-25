#region License
/*
 * WebSocketFrame.cs
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
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace WebSocketSharp
{
	using System.Threading.Tasks;

	internal class WebSocketFrame : IEnumerable<byte>
	{
	    private byte[] _extPayloadLength;
		private Fin _fin;

	    internal static readonly byte[] EmptyUnmaskPingBytes;

	    static WebSocketFrame()
		{
			EmptyUnmaskPingBytes = CreatePingFrame(false).ToByteArray();
		}

	    private WebSocketFrame()
		{
		}

	    internal WebSocketFrame(Opcode opcode, PayloadData payloadData, bool mask)
			: this(Fin.Final, opcode, payloadData, false, mask)
		{
		}

		internal WebSocketFrame(Fin fin, Opcode opcode, byte[] data, bool compressed, bool mask)
			: this(fin, opcode, new PayloadData(data), compressed, mask)
		{
		}

		internal WebSocketFrame(Fin fin, Opcode opcode, PayloadData payloadData, bool compressed, bool mask)
		{
			_fin = fin;
			Rsv1 = isData(opcode) && compressed ? Rsv.On : Rsv.Off;
			Rsv2 = Rsv.Off;
			Rsv3 = Rsv.Off;
			Opcode = opcode;

			var len = payloadData.Length;
			if (len < 126)
			{
				PayloadLength = (byte)len;
				_extPayloadLength = new byte[0];
			}
			else if (len < 0x010000)
			{
				PayloadLength = (byte)126;
				_extPayloadLength = ((ushort)len).InternalToByteArray(ByteOrder.Big);
			}
			else
			{
				PayloadLength = (byte)127;
				_extPayloadLength = len.InternalToByteArray(ByteOrder.Big);
			}

			if (mask)
			{
				Mask = Mask.Mask;
				MaskingKey = createMaskingKey();
				payloadData.Mask(MaskingKey);
			}
			else
			{
				Mask = Mask.Unmask;
				MaskingKey = new byte[0];
			}

			PayloadData = payloadData;
		}

	    public byte[] ExtendedPayloadLength => _extPayloadLength;

	    public Fin Fin => _fin;

	    public bool IsBinary => Opcode == Opcode.Binary;

	    public bool IsClose => Opcode == Opcode.Close;

	    public bool IsCompressed => Rsv1 == Rsv.On;

	    public bool IsContinuation => Opcode == Opcode.Cont;

	    public bool IsControl => Opcode == Opcode.Close || Opcode == Opcode.Ping || Opcode == Opcode.Pong;

	    public bool IsData => Opcode == Opcode.Binary || Opcode == Opcode.Text;

	    public bool IsFinal => _fin == Fin.Final;

	    public bool IsFragmented => _fin == Fin.More || Opcode == Opcode.Cont;

	    public bool IsMasked => Mask == Mask.Mask;

	    public bool IsPerMessageCompressed => (Opcode == Opcode.Binary || Opcode == Opcode.Text) && Rsv1 == Rsv.On;

	    public bool IsPing => Opcode == Opcode.Ping;

	    public bool IsPong => Opcode == Opcode.Pong;

	    public bool IsText => Opcode == Opcode.Text;

	    public ulong Length => 2 + (ulong)(_extPayloadLength.Length + MaskingKey.Length) + PayloadData.Length;

	    public Mask Mask { get; private set; }

	    public byte[] MaskingKey { get; private set; }

	    public Opcode Opcode { get; private set; }

	    public PayloadData PayloadData { get; private set; }

	    public byte PayloadLength { get; private set; }

	    public Rsv Rsv1 { get; private set; }

	    public Rsv Rsv2 { get; private set; }

	    public Rsv Rsv3 { get; private set; }

	    private static byte[] createMaskingKey()
		{
			var key = new byte[4];
			var rand = new Random();
			rand.NextBytes(key);

			return key;
		}

		private static string dump(WebSocketFrame frame)
		{
			var len = frame.Length;
			var cnt = (long)(len / 4);
			var rem = (int)(len % 4);

			int cntDigit;
			string cntFmt;
			if (cnt < 10000)
			{
				cntDigit = 4;
				cntFmt = "{0,4}";
			}
			else if (cnt < 0x010000)
			{
				cntDigit = 4;
				cntFmt = "{0,4:X}";
			}
			else if (cnt < 0x0100000000)
			{
				cntDigit = 8;
				cntFmt = "{0,8:X}";
			}
			else
			{
				cntDigit = 16;
				cntFmt = "{0,16:X}";
			}

			var spFmt = string.Format("{{0,{0}}}", cntDigit);
			var headerFmt = string.Format(@"
{0} 01234567 89ABCDEF 01234567 89ABCDEF
{0}+--------+--------+--------+--------+\n", spFmt);
			var lineFmt = string.Format("{0}|{{1,8}} {{2,8}} {{3,8}} {{4,8}}|\n", cntFmt);
			var footerFmt = string.Format("{0}+--------+--------+--------+--------+", spFmt);

			var output = new StringBuilder(64);
			Func<Action<string, string, string, string>> linePrinter = () =>
			{
				long lineCnt = 0;
				return (arg1, arg2, arg3, arg4) =>
				  output.AppendFormat(lineFmt, ++lineCnt, arg1, arg2, arg3, arg4);
			};

			output.AppendFormat(headerFmt, string.Empty);

			var printLine = linePrinter();
			var bytes = frame.ToByteArray();
			for (long i = 0; i <= cnt; i++)
			{
				var j = i * 4;
				if (i < cnt)
					printLine(
					  Convert.ToString(bytes[j], 2).PadLeft(8, '0'),
					  Convert.ToString(bytes[j + 1], 2).PadLeft(8, '0'),
					  Convert.ToString(bytes[j + 2], 2).PadLeft(8, '0'),
					  Convert.ToString(bytes[j + 3], 2).PadLeft(8, '0'));
				else if (rem > 0)
					printLine(
					  Convert.ToString(bytes[j], 2).PadLeft(8, '0'),
					  rem >= 2 ? Convert.ToString(bytes[j + 1], 2).PadLeft(8, '0') : string.Empty,
					  rem == 3 ? Convert.ToString(bytes[j + 2], 2).PadLeft(8, '0') : string.Empty,
					  string.Empty);
			}

			output.AppendFormat(footerFmt, string.Empty);
			return output.ToString();
		}

		private static bool isControl(Opcode opcode)
		{
			return opcode == Opcode.Close || opcode == Opcode.Ping || opcode == Opcode.Pong;
		}

		private static bool isData(Opcode opcode)
		{
			return opcode == Opcode.Text || opcode == Opcode.Binary;
		}

		private static string print(WebSocketFrame frame)
		{
			/* Opcode */

			var opcode = frame.Opcode.ToString();

			/* Payload Length */

			var payloadLen = frame.PayloadLength;

			/* Extended Payload Length */

			var extPayloadLen = payloadLen < 126
								? string.Empty
								: payloadLen == 126
								  ? frame._extPayloadLength.ToUInt16(ByteOrder.Big).ToString()
								  : frame._extPayloadLength.ToUInt64(ByteOrder.Big).ToString();

			/* Masking Key */

			var masked = frame.IsMasked;
			var maskingKey = masked ? BitConverter.ToString(frame.MaskingKey) : string.Empty;

			/* Payload Data */

			var payload = payloadLen == 0
						  ? string.Empty
						  : payloadLen > 125
							? string.Format("A {0} frame.", opcode.ToLower())
							: !masked && !frame.IsFragmented && !frame.IsCompressed && frame.IsText
							  ? Encoding.UTF8.GetString(frame.PayloadData.ApplicationData)
							  : frame.PayloadData.ToString();

			var fmt = @"
					FIN: {0}
				   RSV1: {1}
				   RSV2: {2}
				   RSV3: {3}
				 Opcode: {4}
				   MASK: {5}
		 Payload Length: {6}
Extended Payload Length: {7}
			Masking Key: {8}
		   Payload Data: {9}";

			return string.Format(
			  fmt,
			  frame._fin,
			  frame.Rsv1,
			  frame.Rsv2,
			  frame.Rsv3,
			  opcode,
			  frame.Mask,
			  payloadLen,
			  extPayloadLen,
			  maskingKey,
			  payload);
		}

		private static WebSocketFrame read(byte[] header, Stream stream, bool unmask)
		{
			/* Header */

			// FIN
			var fin = (header[0] & 0x80) == 0x80 ? Fin.Final : Fin.More;
			// RSV1
			var rsv1 = (header[0] & 0x40) == 0x40 ? Rsv.On : Rsv.Off;
			// RSV2
			var rsv2 = (header[0] & 0x20) == 0x20 ? Rsv.On : Rsv.Off;
			// RSV3
			var rsv3 = (header[0] & 0x10) == 0x10 ? Rsv.On : Rsv.Off;
			// Opcode
			var opcode = (Opcode)(header[0] & 0x0f);
			// MASK
			var mask = (header[1] & 0x80) == 0x80 ? Mask.Mask : Mask.Unmask;
			// Payload Length
			var payloadLen = (byte)(header[1] & 0x7f);

			// Check if valid header
			var err = isControl(opcode) && payloadLen > 125
					  ? "A control frame has a payload data which is greater than the allowable max size."
					  : isControl(opcode) && fin == Fin.More
						? "A control frame is fragmented."
						: !isData(opcode) && rsv1 == Rsv.On
						  ? "A non data frame is compressed."
						  : null;

			if (err != null)
			{
				throw new WebSocketException(CloseStatusCode.ProtocolError, err);
			}

			var frame = new WebSocketFrame();
			frame._fin = fin;
			frame.Rsv1 = rsv1;
			frame.Rsv2 = rsv2;
			frame.Rsv3 = rsv3;
			frame.Opcode = opcode;
			frame.Mask = mask;
			frame.PayloadLength = payloadLen;

			/* Extended Payload Length */

			var size = payloadLen < 126
					   ? 0
					   : payloadLen == 126
						 ? 2
						 : 8;

			var extPayloadLen = size > 0 ? stream.ReadBytes(size) : new byte[0];
			if (size > 0 && extPayloadLen.Length != size)
			{
				throw new WebSocketException("The 'Extended Payload Length' of a frame cannot be read from the data source.");
			}

			frame._extPayloadLength = extPayloadLen;

			/* Masking Key */

			var masked = mask == Mask.Mask;
			var maskingKey = masked ? stream.ReadBytes(4) : new byte[0];
			if (masked && maskingKey.Length != 4)
			{
				throw new WebSocketException("The 'Masking Key' of a frame cannot be read from the data source.");
			}

			frame.MaskingKey = maskingKey;

			/* Payload Data */

			ulong len = payloadLen < 126
						? payloadLen
						: payloadLen == 126
						  ? extPayloadLen.ToUInt16(ByteOrder.Big)
						  : extPayloadLen.ToUInt64(ByteOrder.Big);

			byte[] data = null;

			if (len > 0)
			{
				// Check if allowable max length.
				if (payloadLen > 126 && len > PayloadData.MaxLength)
				{
					throw new WebSocketException(CloseStatusCode.TooBig, "The length of 'Payload Data' of a frame is greater than the allowable max length.");
				}

				data = payloadLen > 126
					? stream.ReadBytes((long)len, 1024)
				: stream.ReadBytes((int)len);

				if (data.LongLength != (long)len)
				{
					throw new WebSocketException("The 'Payload Data' of a frame cannot be read from the data source.");
				}
			}
			else
			{
				data = new byte[0];
			}

			frame.PayloadData = new PayloadData(data, masked);
			if (unmask && masked)
			{
				frame.Unmask();
			}

			return frame;
		}

	    internal static WebSocketFrame CreateCloseFrame(PayloadData payloadData, bool mask)
		{
			return new WebSocketFrame(Fin.Final, Opcode.Close, payloadData, false, mask);
		}

		internal static WebSocketFrame CreatePingFrame(bool mask)
		{
			return new WebSocketFrame(Fin.Final, Opcode.Ping, new PayloadData(), false, mask);
		}

		internal static WebSocketFrame CreatePingFrame(byte[] data, bool mask)
		{
			return new WebSocketFrame(Fin.Final, Opcode.Ping, new PayloadData(data), false, mask);
		}

		internal static WebSocketFrame CreatePongFrame(byte[] data, bool mask)
		{
			return new WebSocketFrame(Fin.Final, Opcode.Pong, new PayloadData(data), false, mask);
		}

		internal static Task<WebSocketFrame> ReadAsync(Stream stream)
		{
			return ReadAsync(stream, true);
		}

		internal static async Task<WebSocketFrame> ReadAsync(Stream stream, bool unmask)
		{
			var header = await stream.ReadBytesAsync(2).ConfigureAwait(false);

			if (header.Length != 2)
			{
				throw new WebSocketException("The header part of a frame cannot be read from the data source.");
			}

			var frame = read(header, stream, unmask);
			return frame;
		}

		internal void Unmask()
		{
			if (Mask == Mask.Unmask)
			{
				return;
			}

			Mask = Mask.Unmask;
			PayloadData.Mask(MaskingKey);
			MaskingKey = new byte[0];
		}

	    public IEnumerator<byte> GetEnumerator()
		{
			return ((IEnumerable<byte>)ToByteArray()).GetEnumerator();
		}

		public byte[] ToByteArray()
		{
			using (var buff = new MemoryStream())
			{
				var header = (int)_fin;
				header = (header << 1) + (int)Rsv1;
				header = (header << 1) + (int)Rsv2;
				header = (header << 1) + (int)Rsv3;
				header = (header << 4) + (int)Opcode;
				header = (header << 1) + (int)Mask;
				header = (header << 7) + (int)PayloadLength;
				buff.Write(((ushort)header).InternalToByteArray(ByteOrder.Big), 0, 2);

				if (PayloadLength > 125)
				{
					buff.Write(_extPayloadLength, 0, _extPayloadLength.Length);
				}

				if (Mask == Mask.Mask)
				{
					buff.Write(MaskingKey, 0, MaskingKey.Length);
				}

				if (PayloadLength > 0)
				{
					var payload = PayloadData.ToByteArray();
					if (PayloadLength < 127)
					{
						buff.Write(payload, 0, payload.Length);
					}
					else
					{
						buff.WriteBytes(payload);
					}
				}

				buff.Close();
				return buff.ToArray();
			}
		}

		public override string ToString()
		{
			return BitConverter.ToString(ToByteArray());
		}

	    IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}
