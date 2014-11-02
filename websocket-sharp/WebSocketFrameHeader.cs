// --------------------------------------------------------------------------------------------------------------------
// <copyright file="WebSocketFrameHeader.cs" company="Reimers.dk">
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
//   Defines the WebSocketFrameHeader type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace WebSocketSharp
{
	internal class WebSocketFrameHeader
	{
		public WebSocketFrameHeader(byte[] header)
		{
			/* Header */

			// FIN
			Fin = (header[0] & 0x80) == 0x80 ? Fin.Final : Fin.More;
			// RSV1
			Rsv1 = (header[0] & 0x40) == 0x40 ? Rsv.On : Rsv.Off;
			// RSV2
			Rsv2 = (header[0] & 0x20) == 0x20 ? Rsv.On : Rsv.Off;
			// RSV3
			Rsv3 = (header[0] & 0x10) == 0x10 ? Rsv.On : Rsv.Off;
			// Opcode
			Opcode = (Opcode)(header[0] & 0x0f);
			// MASK
			Mask = (header[1] & 0x80) == 0x80 ? Mask.Mask : Mask.Unmask;
			// Payload Length
			PayloadLength = (byte)(header[1] & 0x7f);
		}

		public Fin Fin { get; private set; }

		public Rsv Rsv1 { get; private set; }

		public Rsv Rsv2 { get; private set; }

		public Rsv Rsv3 { get; private set; }

		public Opcode Opcode { get; private set; }

		public Mask Mask { get; private set; }

		public byte PayloadLength { get; private set; }

		public static string Validate(WebSocketFrameHeader header)
		{
			// Check if valid header
			var err = IsControl(header.Opcode) && header.PayloadLength > 125
					  ? "A control frame has a payload data which is greater than the allowable max size."
					  : IsControl(header.Opcode) && header.Fin == Fin.More
						? "A control frame is fragmented."
						: !IsData(header.Opcode) && header.Rsv1 == Rsv.On
						  ? "A non data frame is compressed."
						  : null;

			return err;
		}

		private static bool IsControl(Opcode opcode)
		{
			return opcode == Opcode.Close || opcode == Opcode.Ping || opcode == Opcode.Pong;
		}

		private static bool IsData(Opcode opcode)
		{
			return opcode == Opcode.Text || opcode == Opcode.Binary;
		}
	}
}