namespace WebSocketSharp.Tests
{
	using System;
	using System.Diagnostics;
	using System.Threading;
	using System.Threading.Tasks;

	using NUnit.Framework;

	using WebSocketSharp.Server;

	public sealed class WebSocketServerTests
	{
		public class GivenAWebSocketServer
		{
			private WebSocketServer _sut;

			[SetUp]
			public void Setup()
			{
				_sut = new WebSocketServer(8080);
				_sut.AddWebSocketService<TestEchoService>("/echo");
				_sut.Start();
			}

			[TearDown]
			public void Teardown()
			{
				_sut.Stop();
			}

			[Test]
			public void CanGetDefinedPort()
			{
				Assert.AreEqual(8080, _sut.Port);
			}

			[Test]
			public void ClientCanConnectToServer()
			{
				var client = new WebSocket("ws://localhost:8080/echo");

				client.Connect();

				Assert.AreEqual(WebSocketState.Open, client.ReadyState);

				client.Close();
			}

			[Test]
			public void WhenClientSendsTextMessageThenResponds()
			{
				const string Message = "Message";
				var waitHandle = new ManualResetEventSlim(false);
				using (var client = new WebSocket("ws://localhost:8080/echo"))
				{
					EventHandler<MessageEventArgs> onMessage = (s, e) =>
						{
							if (e.Data == Message)
							{
								waitHandle.Set();
							}
						};
					client.OnMessage += onMessage;

					client.Connect();
					client.Send(Message);

					var result = waitHandle.Wait(Debugger.IsAttached ? 30000 : 2000);

					Assert.True(result);

					client.OnMessage -= onMessage;
					client.Close();
				}
			}

			[Test]
			public async Task WhenClientSendsAsyncTextMessageThenResponds()
			{
				const string Message = "Message";
				var waitHandle = new ManualResetEventSlim(false);
				using (var client = new WebSocket("ws://localhost:8080/echo"))
				{
					EventHandler<MessageEventArgs> onMessage = (s, e) =>
						{
							if (e.Data == Message)
							{
								waitHandle.Set();
							}
						};
					client.OnMessage += onMessage;

					client.Connect();
					await client.SendAsync(Message);

					var result = waitHandle.Wait(Debugger.IsAttached ? 30000 : 2000);

					Assert.True(result);

					client.OnMessage -= onMessage;
					client.Close();
				}
			}
		}

		private class TestEchoService : WebSocketBehavior
		{
			protected override void OnMessage(MessageEventArgs e)
			{
				switch (e.Type)
				{
					case Opcode.Text:
						Send(e.Data);
						break;
					case Opcode.Binary:
						Send(e.RawData);
						break;
					case Opcode.Cont:
					case Opcode.Close:
					case Opcode.Ping:
					case Opcode.Pong:
					default:
						throw new ArgumentOutOfRangeException();
				}
				base.OnMessage(e);
			}
		}
	}
}