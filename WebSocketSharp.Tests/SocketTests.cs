namespace WebSocketSharp.Tests
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	using NUnit.Framework;

	public class SocketTests
	{
		public class GivenASocket
		{
			private WebSocket _sut;

			[SetUp]
			public void Setup()
			{
				_sut = new WebSocket("ws://echo.websocket.org");
				_sut.OnError += PrintError;
			}

			[TearDown]
			public void Teardown()
			{
				_sut.OnError -= PrintError;
				_sut.Close();
			}

			[Test]
			public void WhenConnectingToAddressThenConnects()
			{
				_sut.Connect();

				Assert.IsTrue(_sut.ReadyState == WebSocketState.Open);
			}

			[Test]
			public void WhenSendingMessageThenReceivesEcho()
			{
				var waitHandle = new ManualResetEventSlim(false);
				const string Message = "Test Ping";
				var echoReceived = false;
				EventHandler<MessageEventArgs> onMessage = (s, e) =>
					{
						echoReceived = e.Data == Message;
						waitHandle.Set();
					};
				_sut.OnMessage += onMessage;

				_sut.Connect();
				_sut.Send(Message);

				var result = waitHandle.Wait(2000);

				_sut.OnMessage -= onMessage;

				Assert.True(result && echoReceived);
			}

			[Test]
			public async Task WhenSendingMessageAsyncThenReceivesEcho()
			{
				var waitHandle = new ManualResetEventSlim(false);
				const string Message = "Test Ping";
				var echoReceived = false;
				EventHandler<MessageEventArgs> onMessage = (s, e) =>
					{
						echoReceived = e.Data == Message;
						waitHandle.Set();
					};
				_sut.OnMessage += onMessage;

				_sut.Connect();
				await _sut.SendAsync(Message);

				var result = waitHandle.Wait(2000);

				_sut.OnMessage -= onMessage;

				Assert.True(result && echoReceived);
			}

			[Test]
			public async Task WhenConnectingAsyncToAddressThenConnects()
			{
				await _sut.ConnectAsync();

				Assert.IsTrue(_sut.ReadyState == WebSocketState.Open);
			}

			private void PrintError(object sender, ErrorEventArgs e)
			{
				Console.WriteLine(e.Message);
			}
		}
	}
}
