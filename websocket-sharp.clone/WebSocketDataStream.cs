// --------------------------------------------------------------------------------------------------------------------
// <copyright file="WebSocketDataStream.cs" company="Reimers.dk">
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
//   Defines the WebSocketDataStream type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace WebSocketSharp
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;

    internal class WebSocketDataStream : Stream
    {
        private readonly Func<Task<StreamReadInfo>> _readInfoFunc;
        private readonly Func<Task> _consumedAction;
        private readonly Stream _innerStream;
        private StreamReadInfo _readInfo;

        private long _position;

        public WebSocketDataStream(Stream innerStream, StreamReadInfo initialReadInfo, Func<Task<StreamReadInfo>> readInfoFunc, Func<Task> consumedAction)
        {
            _innerStream = innerStream;
            _readInfo = initialReadInfo;
            _readInfoFunc = readInfoFunc;
            _consumedAction = consumedAction;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var task = ReadAsync(buffer, offset, count, CancellationToken.None);
            return task.Result;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var position = offset;
            var bytesRead = 0;

            while (bytesRead < count && _readInfo.PayloadLength > 0)
            {
                var toread = Math.Min((ulong)(count - bytesRead), _readInfo.PayloadLength);
                toread = Math.Min(toread, int.MaxValue);

                var read = await _innerStream.ReadAsync(buffer, position, (int)toread, cancellationToken).ConfigureAwait(false);
                bytesRead += read;

                _readInfo.PayloadLength -= Convert.ToUInt64(Convert.ToUInt32(read));

                if (_readInfo.MaskingKey.Length > 0)
                {
                    var max = position + (int)toread;

                    for (var pos = position; pos < max; pos++)
                    {
                        buffer[pos] = (byte)(buffer[pos] ^ _readInfo.MaskingKey[pos % 4]);
                    }
                }

                position += read;
                _position = position;
                if (_readInfo.PayloadLength == 0)
                {
                    if (!_readInfo.IsFinal)
                    {
                        try
                        {
                            _readInfo = await _readInfoFunc().ConfigureAwait(false);
                        }
                        catch
                        {
                            Debug.WriteLine("Failed at position {0}", Position);
                        }
                    }
                    else
                    {
                        await _consumedAction().ConfigureAwait(false);
                    }
                }
            }

            return bytesRead;
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            throw new NotSupportedException();
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            throw new NotSupportedException();
        }

        public override int ReadByte()
        {
            var buffer = new byte[1];
            var bytesRead = Read(buffer, 0, 1);
            return bytesRead == 0 ? -1 : buffer[0];
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public override long Position
        {
            get
            {
                return _position;
            }
            set
            {
                throw new NotSupportedException();
            }
        }
    }
}