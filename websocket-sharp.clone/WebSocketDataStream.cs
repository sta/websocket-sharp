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
    using System.Threading.Tasks;

    internal class WebSocketDataStream : Stream
    {
        private readonly Func<Task<StreamReadInfo>> _readInfoFunc;
        private readonly Action _consumedAction;
        private readonly Stream _innerStream;
        private StreamReadInfo _readInfo;

        public WebSocketDataStream(Stream innerStream, StreamReadInfo initialReadInfo, Func<Task<StreamReadInfo>> readInfoFunc, Action consumedAction)
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
            var position = offset;
            var bytesRead = 0;

            while (bytesRead < count && _readInfo.PayloadLength > 0)
            {
                var toread = Math.Min((ulong)(count - bytesRead), _readInfo.PayloadLength);
                toread = Math.Min(toread, int.MaxValue);

                var read = _innerStream.Read(buffer, position, (int)toread);
                bytesRead += read;

                _readInfo.PayloadLength -= Convert.ToUInt64(Convert.ToUInt32(read));

                if (_readInfo.MaskingKey.Length > 0)
                {
                    var max = position + (int)toread;
                    //if (max > buffer.Length)
                    //{
                    //}

                    for (var pos = position; pos < max; pos++)
                    {
                        buffer[pos] = (byte)(buffer[pos] ^ _readInfo.MaskingKey[pos % 4]);
                    }
                }

                position += read;
                Position = position;
                if (_readInfo.PayloadLength == 0)
                {
                    if (!_readInfo.IsFinal)
                    {
                        try
                        {
                            _readInfo = _readInfoFunc().Result;
                        }
                        catch
                        {
                            Debug.WriteLine("Failed at position {0}", Position);
                        }
                    }
                    else
                    {
                        _consumedAction();
                    }
                }
            }

            return bytesRead;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override bool CanRead
        {
            get
            {
                return true;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }

        public override long Length
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public override long Position { get; set; }
    }
}