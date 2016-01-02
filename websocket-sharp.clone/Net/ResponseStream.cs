/*
 * ResponseStream.cs
 *
 * This code is derived from System.Net.ResponseStream.cs of Mono
 * (http://www.mono-project.com).
 *
 * The MIT License
 *
 * Copyright (c) 2005 Novell, Inc. (http://www.novell.com)
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

/*
 * Authors:
 * - Gonzalo Paniagua Javier <gonzalo@novell.com>
 */

namespace WebSocketSharp.Net
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    // FIXME: Does this buffer the response until Close?
    // Update: we send a single packet for the first non-chunked Write
    // What happens when we set content-length to X and write X-1 bytes then close?
    // what if we don't set content-length at all?
    internal class ResponseStream : Stream
    {
        private static readonly byte[] Crlf = { 13, 10 };

        private bool _disposed;
        private readonly bool _ignoreErrors;
        private readonly HttpListenerResponse _response;
        private readonly Stream _stream;
        private bool _trailerSent;

        internal ResponseStream(Stream stream, HttpListenerResponse response, bool ignoreErrors)
        {
            _stream = stream;
            _response = response;
            _ignoreErrors = ignoreErrors;
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

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
                throw new NotSupportedException();
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        private static byte[] GetChunkSizeBytes(int size, bool final)
        {
            return Encoding.ASCII.GetBytes(string.Format("{0:x}\r\n{1}", size, final ? "\r\n" : ""));
        }

        private Task<MemoryStream> GetHeaders(bool closing)
        {
            if (_response.HeadersSent)
            {
                return Task.FromResult<MemoryStream>(null);
            }

            return _response.SendHeaders(closing);
        }

        internal async Task WriteInternally(byte[] buffer, int offset, int count)
        {
            if (_ignoreErrors)
            {
                try
                {
                    await _stream.WriteAsync(buffer, offset, count).ConfigureAwait(false);
                }
                catch
                {
                }
            }
            else
            {
                await _stream.WriteAsync(buffer, offset, count).ConfigureAwait(false);
            }
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            throw new NotSupportedException();
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            throw new NotSupportedException();
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().ToString());
            }

            var headers = await GetHeaders(false).ConfigureAwait(false);
            var chunked = _response.SendChunked;
            byte[] bytes = null;
            if (headers != null)
            {
                // After the possible preamble for the encoding.
                using (headers)
                {
                    var start = headers.Position;
                    headers.Position = headers.Length;
                    if (chunked)
                    {
                        bytes = GetChunkSizeBytes(count, false);
                        await headers.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
                    }

                    var newCount = Math.Min(count, 16384 - (int)headers.Position + (int)start);
                    await headers.WriteAsync(buffer, offset, newCount, cancellationToken).ConfigureAwait(false);
                    count -= newCount;
                    offset += newCount;
                    await WriteInternally(headers.GetBuffer(), (int)start, (int)(headers.Length - start)).ConfigureAwait(false);
                }
            }
            else if (chunked)
            {
                bytes = GetChunkSizeBytes(count, false);
                await WriteInternally(bytes, 0, bytes.Length).ConfigureAwait(false);
            }

            if (count > 0)
            {
                await WriteInternally(buffer, offset, count).ConfigureAwait(false);
            }

            if (chunked)
            {
                await WriteInternally(Crlf, 0, 2).ConfigureAwait(false);
            }
        }

        public override void Close()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            var headers = GetHeaders(true).Result;
            var chunked = _response.SendChunked;
            byte[] bytes = null;
            if (headers != null)
            {
                using (headers)
                {
                    var start = headers.Position;
                    if (chunked && !_trailerSent)
                    {
                        bytes = GetChunkSizeBytes(0, true);
                        headers.Position = headers.Length;
                        headers.Write(bytes, 0, bytes.Length);
                    }

                    WriteInternally(headers.GetBuffer(), (int)start, (int)(headers.Length - start)).Wait();
                }

                _trailerSent = true;
            }
            else if (chunked && !_trailerSent)
            {
                bytes = GetChunkSizeBytes(0, true);
                WriteInternally(bytes, 0, bytes.Length).Wait();
                _trailerSent = true;
            }

            _response.Close();
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            throw new NotSupportedException();
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
