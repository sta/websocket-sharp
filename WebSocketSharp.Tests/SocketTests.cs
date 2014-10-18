namespace WebSocketSharp.Tests
{
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
			}

			[TearDown]
			public void Teardown()
			{
				_sut.Close();
			}

			[Test]
			public void WhenConnectingToAddressThenConnects()
			{
				_sut.Connect();

				Assert.IsTrue(_sut.ReadyState == WebSocketState.Open);
			}

			[Test]
			public async Task WhenConnectingAsyncToAddressThenConnects()
			{
				await _sut.ConnectAsync();

				Assert.IsTrue(_sut.ReadyState == WebSocketState.Open);
			}
		}
	}
}
