// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SocketTests.cs" company="Reimers.dk">
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
//   Defines the SocketTests type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

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
