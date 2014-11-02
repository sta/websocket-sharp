// --------------------------------------------------------------------------------------------------------------------
// <copyright file="SecureWebSocketServerTests.cs" company="Reimers.dk">
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
//   Defines the SecureWebSocketServerTests type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace WebSocketSharp.Tests
{
	using System;
	using System.Diagnostics;
	using System.Security.Cryptography.X509Certificates;
	using System.Threading;
	using System.Threading.Tasks;

	using NUnit.Framework;

	using WebSocketSharp.Net;
	using WebSocketSharp.Server;

	public sealed class SecureWebSocketServerTests
	{
		//[Ignore("Must create test certificate.")]
		public class GivenASecureWebSocketServer
		{
			private WebSocketServer _sut;

			[SetUp]
			public void Setup()
			{
				var cert = GetRandomCertificate();
				_sut = new WebSocketServer(443, new ServerSslAuthConfiguration(cert));
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
				Assert.AreEqual(443, _sut.Port);
			}

			[Test]
			public void ClientCanConnectToServer()
			{
				var client = new WebSocket("wss://localhost:443/echo");

				client.Connect();

				Assert.AreEqual(WebSocketState.Open, client.ReadyState);

				client.Close();
			}

			[Test]
			public void WhenClientSendsTextMessageThenResponds()
			{
				const string Message = "Message";
				var waitHandle = new ManualResetEventSlim(false);
				using (var client = new WebSocket("wss://localhost:443/echo"))
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
				using (var client = new WebSocket("wss://localhost:443/echo"))
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

			[Test]
			public async Task WhenClientSendsMultipleAsyncTextMessageThenResponds([Random(1, 100, 10)]int multiplicity)
			{
				int count = 0;
				const string Message = "Message";
				var waitHandle = new ManualResetEventSlim(false);
				using (var client = new WebSocket("wss://localhost:443/echo"))
				{
					EventHandler<MessageEventArgs> onMessage = (s, e) =>
						{
							if (e.Data == Message)
							{
								if (Interlocked.Increment(ref count) == multiplicity)
								{
									waitHandle.Set();
								}
							}
						};
					client.OnMessage += onMessage;

					client.Connect();
					for (int i = 0; i < multiplicity; i++)
					{
						await client.SendAsync(Message);
					}

					var result = waitHandle.Wait(Debugger.IsAttached ? 30000 : 5000);

					Assert.True(result);

					client.OnMessage -= onMessage;
					client.Close();
				}
			}

			[Test]
			public void WhenClientSendsMultipleTextMessageThenResponds([Random(1, 100, 10)]int multiplicity)
			{
				int count = 0;
				const string Message = "Message";
				var waitHandle = new ManualResetEventSlim(false);
				using (var client = new WebSocket("wss://localhost:443/echo"))
				{
					EventHandler<MessageEventArgs> onMessage = (s, e) =>
						{
							if (e.Data == Message)
							{
								if (Interlocked.Increment(ref count) == multiplicity)
								{
									waitHandle.Set();
								}
							}
						};
					client.OnMessage += onMessage;

					client.Connect();
					for (int i = 0; i < multiplicity; i++)
					{
						client.Send(Message);
					}

					var result = waitHandle.Wait(Debugger.IsAttached ? 30000 : 5000);

					Assert.True(result);

					client.OnMessage -= onMessage;
					client.Close();
				}
			}

			private static X509Certificate2 GetRandomCertificate()
			{
				var st = new X509Store(StoreName.My, StoreLocation.LocalMachine);
				st.Open(OpenFlags.ReadOnly);
				try
				{
					var certCollection = st.Certificates;

					return certCollection.Count == 0 ? null : certCollection[0];
				}
				finally
				{
					st.Close();
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
						this.Send(e.Data);
						break;
					case Opcode.Binary:
						this.Send(e.RawData);
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