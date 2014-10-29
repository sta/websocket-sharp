namespace WebSocketSharp.Tests
{
	using System;
	using System.IO;
	using System.Linq;
	using System.Threading;
	using System.Threading.Tasks;

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
				var frame3 = new WebSocketFrame(Fin.Final, Opcode.Ping, new byte[0], false, true);
				var stream = new MemoryStream(frame1.ToByteArray().Concat(frame2.ToByteArray()).Concat(frame3.ToByteArray()).ToArray());
				_sut = new WebSocketStreamReader(stream);
			}

			[Test]
			public void WhenReadingMessageThenGetsAllFrames()
			{
				var msg = _sut.Read().First();

				var buffer = new byte[2000];
				var bytesRead = msg.RawData.Read(buffer, 0, 2000);

				CollectionAssert.AreEqual(Enumerable.Repeat((byte)1, 1000).Concat(Enumerable.Repeat((byte)2, 1000)), buffer);
			}

			[Test]
			public void WhenMessageDataIsNotConsumedThenDoesNotGetSecondMessage()
			{
				var task = Task.Factory.StartNew(() => _sut.Read().ElementAt(1));
				var hasResult = task.Wait(TimeSpan.FromSeconds(2));

				Assert.False(hasResult);
			}

			[Test]
			public void WhenMessageDataIsConsumedThenGetsSecondMessage()
			{
				var count = 0;
				var messages = _sut.Read();
				foreach (var message in messages)
				{
					message.Consume();
					count++;
				}

				Assert.AreEqual(2, count);
			}

			[Test]
			public async Task WhenSecondReadCalledSecondTimeThenReturnsEmpty()
			{
				var first = await Task.Factory.StartNew(() => _sut.Read().First());
				var second = await Task.Factory.StartNew(() => _sut.Read().FirstOrDefault());

				Assert.IsNull(second);
			}
		}
	}
}