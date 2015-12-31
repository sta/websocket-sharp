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
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    internal class WebSocketStreamReader
    {
        private readonly Stream _innerStream;
        private readonly int _fragmentLength;
        private readonly ManualResetEventSlim _waitHandle = new ManualResetEventSlim(true);
        private bool _isClosed;

        public WebSocketStreamReader(Stream innerStream, int fragmentLength)
        {
            _innerStream = innerStream;
            _fragmentLength = fragmentLength;
        }

        public async Task<WebSocketMessage> Read(CancellationToken cancellationToken)
        {
            lock (_innerStream)
            {
                if (_isClosed)
                {
                    return null;
                }
            }

            _waitHandle.Wait(cancellationToken);
            _waitHandle.Reset();
            var header = await ReadHeader(cancellationToken).ConfigureAwait(false);
            if (header == null)
            {
                return null;
            }

            var readInfo = await GetStreamReadInfo(header, cancellationToken).ConfigureAwait(false);

            var msg = CreateMessage(header, readInfo, _waitHandle);
            if (msg.Opcode == Opcode.Close)
            {
                _isClosed = true;
            }

            return msg;
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
                    return new FragmentedMessage(header.Opcode, _innerStream, readInfo, GetStreamReadInfo, waitHandle, _fragmentLength);
            }
        }

        private async Task<StreamReadInfo> GetStreamReadInfo()
        {
            var h = await ReadHeader(CancellationToken.None).ConfigureAwait(false);
            return await GetStreamReadInfo(h, CancellationToken.None).ConfigureAwait(false);
        }

        private async Task<StreamReadInfo> GetStreamReadInfo(WebSocketFrameHeader header, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            /* Extended Payload Length */

            var size = header.PayloadLength < 126 ? 0 : header.PayloadLength == 126 ? 2 : 8;

            var extPayloadLen = size > 0 ? await _innerStream.ReadBytesAsync(size).ConfigureAwait(false) : new byte[0];
            if (size > 0 && extPayloadLen.Length != size)
            {
                throw new WebSocketException("The 'Extended Payload Length' of a frame cannot be read from the data source.");
            }

            /* Masking Key */

            var masked = header.Mask == Mask.Mask;
            var maskingKey = masked ? await _innerStream.ReadBytesAsync(4).ConfigureAwait(false) : new byte[0];
            if (masked && maskingKey.Length != 4)
            {
                throw new WebSocketException("The 'Masking Key' of a frame cannot be read from the data source.");
            }

            /* Payload Data */

            var len = header.PayloadLength < 126
                            ? header.PayloadLength
                            : header.PayloadLength == 126
                                  ? extPayloadLen.ToUInt16(ByteOrder.Big)
                                  : extPayloadLen.ToUInt64(ByteOrder.Big);

            return new StreamReadInfo(header.Fin == Fin.Final, len, maskingKey);
        }

        private async Task<WebSocketFrameHeader> ReadHeader(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var header = new byte[2];

            var headerLength = 0;
            try
            {
                headerLength = await _innerStream.ReadAsync(header, 0, 2, cancellationToken).ConfigureAwait(false);
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