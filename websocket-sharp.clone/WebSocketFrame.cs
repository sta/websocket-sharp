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

namespace WebSocketSharp
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;

    internal class WebSocketFrame : IEnumerable<byte>
    {
        private readonly byte[] _extPayloadLength;
        private readonly Fin _fin;
        private readonly Mask _mask;
        private readonly byte[] _maskingKey;
        private readonly Opcode _opcode;
        private readonly PayloadData _payloadData;
        private readonly byte _payloadLength;
        private readonly Rsv _rsv1;
        private readonly Rsv _rsv2;
        private readonly Rsv _rsv3;

        internal static readonly byte[] EmptyUnmaskPingBytes;

        static WebSocketFrame()
        {
            EmptyUnmaskPingBytes = CreatePingFrame(false).ToByteArray().Result;
        }

        internal WebSocketFrame(Fin fin, Opcode opcode, byte[] data, bool compressed, bool mask)
            : this(fin, opcode, new PayloadData(data), compressed, mask)
        {
        }

        private WebSocketFrame(Fin fin, Opcode opcode, PayloadData payloadData, bool compressed, bool mask)
        {
            _fin = fin;
            _rsv1 = IsData(opcode) && compressed ? Rsv.On : Rsv.Off;
            _rsv2 = Rsv.Off;
            _rsv3 = Rsv.Off;
            _opcode = opcode;

            var len = payloadData.Length;
            if (len < 126)
            {
                _payloadLength = (byte)len;
                _extPayloadLength = new byte[0];
            }
            else if (len < 0x010000)
            {
                _payloadLength = 126;
                _extPayloadLength = ((ushort)len).InternalToByteArray(ByteOrder.Big);
            }
            else
            {
                _payloadLength = 127;
                _extPayloadLength = len.InternalToByteArray(ByteOrder.Big);
            }

            if (mask)
            {
                _mask = Mask.Mask;
                _maskingKey = CreateMaskingKey();
                payloadData.Mask(_maskingKey);
            }
            else
            {
                _mask = Mask.Unmask;
                _maskingKey = new byte[0];
            }

            _payloadData = payloadData;
        }

        private static byte[] CreateMaskingKey()
        {
            var key = new byte[4];
            var rand = new Random();
            rand.NextBytes(key);

            return key;
        }

        private static bool IsData(Opcode opcode)
        {
            return opcode == Opcode.Text || opcode == Opcode.Binary;
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

        public IEnumerator<byte> GetEnumerator()
        {
            return ((IEnumerable<byte>)ToByteArray()).GetEnumerator();
        }

        public async Task<byte[]> ToByteArray()
        {
            using (var buff = new MemoryStream())
            {
                var header = (int)_fin;
                header = (header << 1) + (int)_rsv1;
                header = (header << 1) + (int)_rsv2;
                header = (header << 1) + (int)_rsv3;
                header = (header << 4) + (int)_opcode;
                header = (header << 1) + (int)_mask;
                header = (header << 7) + _payloadLength;
                buff.Write(((ushort)header).InternalToByteArray(ByteOrder.Big), 0, 2);

                if (_payloadLength > 125)
                {
                    buff.Write(_extPayloadLength, 0, _extPayloadLength.Length);
                }

                if (_mask == Mask.Mask)
                {
                    await buff.WriteAsync(_maskingKey, 0, _maskingKey.Length).ConfigureAwait(false);
                }

                if (_payloadLength > 0)
                {
                    var payload = _payloadData.ToByteArray();
                    if (_payloadLength < 127)
                    {
                        await buff.WriteAsync(payload, 0, payload.Length).ConfigureAwait(false);
                    }
                    else
                    {
                        await buff.WriteBytes(payload).ConfigureAwait(false);
                    }
                }

                buff.Close();
                return buff.ToArray();
            }
        }

        public override string ToString()
        {
            var byteArray = ToByteArray().Result;
            return BitConverter.ToString(byteArray);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
