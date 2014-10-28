namespace WebSocketSharp.Tests
{
	using System;
	using System.IO;
	using System.Linq;

	using NUnit.Framework;

	public class WebSocketStreamReaderTests
	{
		private WebSocketStreamReaderTests()
		{
		}

		[TestFixture]
		public class GivenAWebSocketStreamReader
		{
			private WebSocketStreamReader _sut;

			[SetUp]
			public void Setup()
			{
				var data1 = Enumerable.Repeat((byte)1, 1000).ToArray();
				var frame1 = new WebSocketFrame(Fin.More, Opcode.Binary, data1, false, true);
				var data2 = Enumerable.Repeat((byte)2, 1000).ToArray();
				var frame2 = new WebSocketFrame(Fin.Final, Opcode.Cont, data2, false, true);
				var stream = new MemoryStream(frame1.ToByteArray().Concat(frame2.ToByteArray()).ToArray());
				_sut = new WebSocketStreamReader(stream);
			}

			[Test]
			public void WhenReadingMessageThenGetsAllFrames()
			{
				var msg = _sut.Read().First();

				var buffer = new byte[2000];
				var bytesRead = msg.RawData.Read(buffer, 0, 2000);

				Assert.False(buffer.Any(x => x == 0));
			}
		}
	}
}