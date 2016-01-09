// --------------------------------------------------------------------------------------------------------------------
// <copyright file="WebSocketServerTests.cs" company="Reimers.dk">
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
//   Defines the WebSocketServerTests type.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace WebSocketSharp.Tests
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using NUnit.Framework;
    using NUnit.Framework.Constraints;

    using WebSocketSharp.Net;
    using WebSocketSharp.Server;

    public sealed class WebSocketServerTests
    {
        public abstract class GivenAWebSocketServer
        {
            protected const string WsLocalhostRadio = "ws://localhost:8080/radio";
            protected const string WsLocalhostEcho = "ws://localhost:8080/echo";
            private const string Message = "Message";
            protected WebSocketServer Sut;

            protected abstract void SetCredentials(WebSocket ws);

            [Test]
            public void CanGetDefinedPort()
            {
                Assert.AreEqual(8080, Sut.Port);
            }

            [Test]
            public async Task ClientCanConnectToServer()
            {
                using (var client = new WebSocket(WsLocalhostEcho))
                {
                    SetCredentials(client);
                    await client.Connect().ConfigureAwait(false);

                    Assert.AreEqual(WebSocketState.Open, client.ReadyState);
                }
            }

            [Test]
            public async Task WhenClientSendsTextMessageThenResponds()
            {
                var waitHandle = new ManualResetEventSlim(false);
                Func<MessageEventArgs, Task> onMessage = e =>
                                        {
                                            if (e.Text.ReadToEnd() == Message)
                                            {
                                                waitHandle.Set();
                                            }
                                            return Task.FromResult(true);
                                        };
                using (var client = new WebSocket(WsLocalhostEcho, onMessage: onMessage))
                {
                    SetCredentials(client);

                    await client.Connect().ConfigureAwait(false);
                    await client.Send(Message).ConfigureAwait(false);

                    var result = waitHandle.Wait(Debugger.IsAttached ? 30000 : 2000);

                    Assert.True(result);
                    await client.Close().ConfigureAwait(false);
                }
            }

            [Test]
            public async Task WhenClientConnectsToNonExistingPathThenStateIsClosed()
            {
                using (var client = new WebSocket("ws://localhost:8080/fjgkdfjhgld"))
                {
                    SetCredentials(client);
                    await client.Connect().ConfigureAwait(false);

                    Assert.True(client.ReadyState == WebSocketState.Closed);
                    await client.Close().ConfigureAwait(false);
                }
            }

            [Test]
            public Task WhenClientConnectsToNonExistingPathThenDoesNotThrow()
            {
                using (var client = new WebSocket("ws://localhost:8080/fjgkdfjhgld"))
                {
                    SetCredentials(client);
                    Assert.That(async () => await client.Connect().ConfigureAwait(false), new ThrowsNothingConstraint());
                }
                return Task.FromResult(true);
            }

            [Test]
            public async Task WhenClientSendsMultipleAsyncTextMessageThenResponds([Random(1, 100, 10)] int multiplicity)
            {
                int count = 0;
                var waitHandle = new ManualResetEventSlim(false);
                Func<MessageEventArgs, Task> onMessage = e =>
                {
                    if (e.Text.ReadToEnd() == Message)
                    {
                        if (Interlocked.Increment(ref count) == multiplicity)
                        {
                            waitHandle.Set();
                        }
                    }
                    return Task.FromResult(true);
                };
                using (var client = new WebSocket(WsLocalhostEcho, onMessage: onMessage))
                {
                    SetCredentials(client);

                    await client.Connect().ConfigureAwait(false);
                    for (int i = 0; i < multiplicity; i++)
                    {
                        await client.Send(Message).ConfigureAwait(false);
                    }

                    var result = waitHandle.Wait(Debugger.IsAttached ? 30000 : 5000);

                    Assert.True(result);
                }
            }

            [Test]
            public async Task WhenStreamVeryLargeStreamToServerThenResponds([Random(750000, 1500000, 5)] int length)
            {
                var responseLength = 0;

                var stream = new EnumerableStream(Enumerable.Repeat((byte)123, length));
                var waitHandle = new ManualResetEventSlim(false);
                Func<MessageEventArgs, Task> onMessage = async e =>
                {
                    var bytesRead = 0;
                    var readLength = 10240000;
                    do
                    {
                        var buffer = new byte[readLength];
                        bytesRead = await e.Data.ReadAsync(buffer, 0, readLength).ConfigureAwait(false);
                        responseLength += buffer.Count(x => x == 123);
                    }
                    while (bytesRead == readLength);

                    waitHandle.Set();
                };

                using (var client = new WebSocket(WsLocalhostEcho, onMessage: onMessage))
                {
                    SetCredentials(client);

                    await client.Connect().ConfigureAwait(false);
                    await client.Send(stream).ConfigureAwait(false);

                    var result = waitHandle.Wait(Debugger.IsAttached ? -1 : 20000);

                    Assert.AreEqual(length, responseLength);
                }
            }

            [Test]
            public async Task WhenWritingFromMultipleThreadsThenMessagesDoNotInterleave([Random(2, 50, 5)]int writerCount, [Random((int)1E6, (int)1E7, 5)] int messageLength)
            {
                var waitHandle = new ManualResetEventSlim(false);
                var cleanResponses = 0;
                Func<MessageEventArgs, Task> onMessage = e =>
                {
                    var c = e.Text.ReadToEnd().Distinct().Select(x => (int)x).ToArray();
                    if (c.Length == 1)
                    {
                        if (Interlocked.Increment(ref cleanResponses) == writerCount)
                        {
                            waitHandle.Set();
                        }
                    }
                    return Task.FromResult(true);
                };
                using (var client = new WebSocket(WsLocalhostEcho, onMessage: onMessage))
                {
                    SetCredentials(client);

                    await client.Connect().ConfigureAwait(false);

                    var writes =
                        Enumerable.Range(0, writerCount)
                            .AsParallel()
                            .Select(_ => client.Send(new EnumerableStream(Enumerable.Repeat((byte)_, messageLength))))
                            .ToArray();
                    await Task.WhenAll(writes).ConfigureAwait(false);

                    var result = waitHandle.Wait(Debugger.IsAttached ? 30000 : 10000);

                    Assert.True(result);
                }
            }

            [Test]
            [Timeout(50000)]
            [Ignore("Performance")]
            public async Task CanSendTwentyThousandSynchronousRequestsPerSecond()
            {
                var stopwatch = new Stopwatch();
                int count = 0;
                var stream = Encoding.UTF8.GetBytes(Message);
                const int Multiplicity = 20000;
                var waitHandle = new ManualResetEventSlim(false);
                Func<MessageEventArgs, Task> onMessage = e =>
                    {
                        if (Interlocked.Increment(ref count) == Multiplicity)
                        {
                            waitHandle.Set();
                        }
                        return Task.FromResult(true);
                    };

                using (var client = new WebSocket(WsLocalhostEcho, onMessage: onMessage))
                {
                    SetCredentials(client);

                    await client.Connect().ConfigureAwait(false);
                    stopwatch.Start();
                    var tasks = Enumerable.Repeat(false, Multiplicity).Select(_ => client.Send(stream));
                    await Task.WhenAll(tasks).ConfigureAwait(false);

                    stopwatch.Stop();

                    waitHandle.Wait();

                    Console.WriteLine(stopwatch.Elapsed);

                    Assert.LessOrEqual(stopwatch.Elapsed, TimeSpan.FromSeconds(1));
                }
            }

            [Test]
            [Timeout(10000)]
            [Ignore("Performance")]
            public async Task CanReceiveTwentyFiveThousandSynchronousRequestsInSixSeconds()
            {
                var responseWatch = new Stopwatch();
                int count = 0;
                var stream = Encoding.UTF8.GetBytes(Message);
                var waitHandle = new ManualResetEventSlim(false);
                const int Multiplicity = 25000;
                Func<MessageEventArgs, Task> onMessage = async e =>
                    {
                        if (await e.Text.ReadToEndAsync().ConfigureAwait(false) == Message)
                        {
                            if (Interlocked.Increment(ref count) == Multiplicity)
                            {
                                responseWatch.Stop();
                                waitHandle.Set();
                            }
                        }
                    };

                using (var client = new WebSocket(WsLocalhostEcho, onMessage: onMessage))
                {
                    SetCredentials(client);

                    await client.Connect().ConfigureAwait(false);
                    responseWatch.Start();
                    for (int i = 0; i < Multiplicity; i++)
                    {
                        await client.Send(stream).ConfigureAwait(false);
                    }

                    waitHandle.Wait();

                    Console.WriteLine(responseWatch.Elapsed);
                    Assert.LessOrEqual(responseWatch.Elapsed, TimeSpan.FromSeconds(6));
                }
            }

            [Test]
            [Ignore("Performance")]
            public async Task CanSendOneMillionAsynchronousRequestsPerSecond()
            {
                var stopwatch = new Stopwatch();

                var stream = new MemoryStream(Encoding.UTF8.GetBytes(Message));
                var length = (int)stream.Length;
                var waitHandle = new ManualResetEventSlim(false);
                using (var client = new WebSocket(WsLocalhostEcho))
                {
                    SetCredentials(client);
                    const int Multiplicity = (int)1E3;

                    await client.Connect().ConfigureAwait(false);
                    stopwatch.Start();

                    var tasks = Enumerable.Range(0, Multiplicity).Select(x => client.Send(stream, length));

                    await Task.WhenAll(tasks).ConfigureAwait(false);
                    stopwatch.Stop();

                    waitHandle.Wait(Debugger.IsAttached ? 30000 : 5000);

                    Assert.LessOrEqual(
                        stopwatch.Elapsed,
                        TimeSpan.FromSeconds(1),
                        "Total time taken: " + stopwatch.Elapsed);
                }
            }

            [Test]
            [Timeout(10000)]
            [Ignore("Performance")]
            public async Task CanReceiveOneMillionAsynchronousResponsesInTenSecond()
            {
                var responseWatch = new Stopwatch();

                int count = 0;
                var stream = new MemoryStream(Encoding.UTF8.GetBytes(Message));
                var length = (int)stream.Length;
                var waitHandle = new ManualResetEventSlim(false);
                const int Multiplicity = 1000000;
                Func<MessageEventArgs, Task> onMessage = async e =>
                    {
                        if (await e.Text.ReadToEndAsync().ConfigureAwait(false) == Message
                            && Interlocked.Increment(ref count) == Multiplicity)
                        {
                            responseWatch.Stop();
                            waitHandle.Set();
                        }
                    };
                using (var client = new WebSocket(WsLocalhostEcho, onMessage: onMessage))
                {
                    SetCredentials(client);

                    await client.Connect().ConfigureAwait(false);
                    responseWatch.Start();

                    var tasks = Enumerable.Range(0, Multiplicity).Select(x => client.Send(stream, length));

                    await Task.WhenAll(tasks).ConfigureAwait(false);

                    waitHandle.Wait();

                    Console.WriteLine(responseWatch.Elapsed);

                    Assert.LessOrEqual(responseWatch.Elapsed, TimeSpan.FromSeconds(10));
                }
            }
        }

        [TestFixture]
        public class GivenAWebSocketServerAllowingAnonymousConnections : GivenAWebSocketServer
        {
            [SetUp]
            public void Setup()
            {
                Debug.Listeners.Add(new ConsoleTraceListener());
                Sut = new WebSocketServer(port: 8080);
                Sut.AddWebSocketService<TestEchoService>("/echo");
                Sut.AddWebSocketService<TestRadioService>("/radio");
                Sut.Start();
            }

            [TearDown]
            public async Task Teardown()
            {
                await Sut.Stop().ConfigureAwait(false);
                Debug.Listeners.Clear();
            }

            protected override void SetCredentials(WebSocket ws)
            {
            }
        }

        [TestFixture]
        public class GivenAWebSocketServerUsingBasicAuthentication : GivenAWebSocketServer
        {
            [SetUp]
            public void Setup()
            {
                Debug.Listeners.Add(new ConsoleTraceListener());
                Sut = new WebSocketServer(port: 8080, authenticationSchemes: AuthenticationSchemes.Basic)
                {
                    UserCredentialsFinder = id => new NetworkCredential(id.Name, "password")
                };
                Sut.AddWebSocketService<TestEchoService>("/echo");
                Sut.AddWebSocketService<TestRadioService>("/radio");
                Sut.Start();
            }

            [TearDown]
            public async Task Teardown()
            {
                await Sut.Stop().ConfigureAwait(false);
                Debug.Listeners.Clear();
            }

            [Test]
            public async Task WhenCredentialsAreNotSetThenCannotConnect()
            {
                using (var client = new WebSocket(WsLocalhostEcho))
                {
                    var connected = await client.Connect().ConfigureAwait(false);

                    Assert.False(connected);
                }
            }

            [Test]
            [Timeout(60000)]
            public async Task WhenStreamVeryLargeStreamToServerThenBroadcasts([Random(750000, 1500000, 5)] int length)
            {
                var responseLength = 0;

                var stream = new EnumerableStream(Enumerable.Repeat((byte)123, length));
                var waitHandle = new ManualResetEventSlim(false);

                Func<MessageEventArgs, Task> onMessage = async e =>
                {
                    var bytes = await e.Data.ReadBytes(length).ConfigureAwait(false);
                    responseLength = bytes.Count(x => x == 123);
                    waitHandle.Set();
                };

                using (var sender = new WebSocket(WsLocalhostRadio))
                {
                    SetCredentials(sender);
                    using (var client = new WebSocket(WsLocalhostRadio, onMessage: onMessage))
                    {
                        SetCredentials(client);

                        await sender.Connect().ConfigureAwait(false);
                        await client.Connect().ConfigureAwait(false);
                        await sender.Send(stream).ConfigureAwait(false);

                        waitHandle.Wait();

                        Assert.AreEqual(length, responseLength);
                    }
                }
            }

            protected override void SetCredentials(WebSocket ws)
            {
                ws.SetCredentials("username", "password", true);
            }
        }

        [TestFixture]
        public class GivenAWebSocketServerUsingDigestAuthentication : GivenAWebSocketServer
        {
            [SetUp]
            public void Setup()
            {
                Debug.Listeners.Add(new ConsoleTraceListener());
                Sut = new WebSocketServer(port: 8080, authenticationSchemes: AuthenticationSchemes.Digest);
                Sut.UserCredentialsFinder = id => new NetworkCredential(id.Name, "password", "Test");
                Sut.Realm = "Test";
                Sut.AddWebSocketService<TestEchoService>("/echo");
                Sut.AddWebSocketService<TestRadioService>("/radio");
                Sut.Start();
            }

            [TearDown]
            public async Task Teardown()
            {
                await Sut.Stop().ConfigureAwait(false);
                Debug.Listeners.Clear();
            }

            [Test]
            public async Task WhenCredentialsAreNotSetThenCannotConnect()
            {
                using (var client = new WebSocket(WsLocalhostEcho))
                {
                    var connected = await client.Connect().ConfigureAwait(false);

                    Assert.False(connected);
                }
            }

            protected override void SetCredentials(WebSocket ws)
            {
                ws.SetCredentials("username", "password", true);
            }
        }
    }
}