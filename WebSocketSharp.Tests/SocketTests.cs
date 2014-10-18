namespace WebSocketSharp.Tests
{
	using NUnit.Framework;

	public class SocketTests
	{
		public class GivenASocket
		{
			[SetUp]
			public void Setup()
			{
				var sut = new WebSocket("ws://localhost:81");
			}
		}
	}
}
