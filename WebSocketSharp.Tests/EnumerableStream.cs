namespace WebSocketSharp.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    internal class EnumerableStream : Stream
	{
		private readonly IEnumerator<byte> _enumerator;

		public EnumerableStream(IEnumerable<byte> bytes)
		{
			_enumerator = bytes.GetEnumerator();
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
			for (int i = 0; i < count; i++)
			{
				Position += 1;

				if (_enumerator.MoveNext())
				{
					buffer[offset + i] = _enumerator.Current;
				}
				else
				{
					return i;
				}
			}

			return count;
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

		public override long Position { get; set; }
	}
}