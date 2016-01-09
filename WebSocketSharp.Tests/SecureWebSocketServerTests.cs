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
    using System.Linq;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;

    using NUnit.Framework;

    using WebSocketSharp.Net;
    using WebSocketSharp.Server;

    public sealed class SecureWebSocketServerTests
    {
        public class GivenASecureWebSocketServer
        {
            private WebSocketServer _sut;

            [SetUp]
            public void Setup()
            {
                var cert = GetRandomCertificate();
                var serverSslConfiguration = new ServerSslConfiguration(
                    cert,
                    true,
                    SslProtocols.Tls,
                    clientCertificateValidationCallback: (sender, certificate, chain, errors) => true);
                _sut = new WebSocketServer(port: 443, certificate: serverSslConfiguration);
                _sut.AddWebSocketService<TestEchoService>("/echo");
                _sut.AddWebSocketService<TestRadioService>("/radio");
                _sut.Start();
            }

            [TearDown]
            public Task Teardown()
            {
                return _sut.Stop();
            }

            [Test]
            public void CanGetDefinedPort()
            {
                Assert.AreEqual(443, _sut.Port);
            }

            [Test]
            public async Task ClientCanConnectToServer()
            {
                var clientSslConfiguration = new ClientSslConfiguration(
                    "localhost",
                    clientCertificates: new X509Certificate2Collection(GetRandomCertificate()),
                    enabledSslProtocols: SslProtocols.Tls,
                    certificateValidationCallback: (sender, certificate, chain, errors) => true);

                using (var client = new WebSocket("wss://localhost:443/echo", sslAuthConfiguration: clientSslConfiguration))
                {
                    await client.Connect().ConfigureAwait(false);

                    Assert.AreEqual(WebSocketState.Open, client.ReadyState);
                }
            }

            [Test]
            public async Task WhenClientSendsTextMessageThenResponds()
            {
                const string Message = "Message";
                var waitHandle = new ManualResetEventSlim(false);
                var clientSslConfiguration = new ClientSslConfiguration("localhost", certificateValidationCallback: (sender, certificate, chain, errors) => true);
                Func<MessageEventArgs, Task> onMessage = e =>
                {
                    if (e.Text.ReadToEnd() == Message)
                    {
                        waitHandle.Set();
                    }
                    return AsyncEx.Completed();
                };
                using (var client = new WebSocket("wss://localhost:443/echo", sslAuthConfiguration: clientSslConfiguration, onMessage: onMessage))
                {
                    await client.Connect().ConfigureAwait(false);
                    await client.Send(Message).ConfigureAwait(false);

                    var result = waitHandle.Wait(Debugger.IsAttached ? 30000 : 2000);

                    Assert.True(result);
                }
            }

            [Test]
            public async Task WhenClientSendsMultipleTextMessageThenResponds([Random(1, 100, 10)]int multiplicity)
            {
                int count = 0;
                const string Message = "Message";
                var waitHandle = new ManualResetEventSlim(false);
                var clientSslConfiguration = new ClientSslConfiguration("localhost", certificateValidationCallback: (sender, certificate, chain, errors) => true);
                Func<MessageEventArgs, Task> onMessage = async e =>
                                        {
                                            if (await e.Text.ReadToEndAsync().ConfigureAwait(false) == Message)
                                            {
                                                if (Interlocked.Increment(ref count) == multiplicity)
                                                {
                                                    waitHandle.Set();
                                                }
                                            }
                                        };
                using (var client = new WebSocket("wss://localhost:443/echo", sslAuthConfiguration: clientSslConfiguration, onMessage: onMessage))
                {
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
            public async Task WhenStreamVeryLargeStreamToServerThenResponds()
            {
                var responseLength = 0;
                const int Length = 1000000;

                var stream = new EnumerableStream(Enumerable.Repeat((byte)123, Length));
                var waitHandle = new ManualResetEventSlim(false);
                var clientSslConfiguration = new ClientSslConfiguration("localhost", certificateValidationCallback: (sender, certificate, chain, errors) => true);

                Func<MessageEventArgs, Task> onMessage = async e =>
                    {
                        var bytes = await e.Data.ReadBytes(Length).ConfigureAwait(false);
                        responseLength = bytes.Count(x => x == 123);

                        waitHandle.Set();
                    };

                using (var client = new WebSocket("wss://localhost:443/echo", sslAuthConfiguration: clientSslConfiguration, onMessage: onMessage))
                {
                    await client.Connect().ConfigureAwait(false);
                    await client.Send(stream).ConfigureAwait(false);

                    var result = waitHandle.Wait(Debugger.IsAttached ? -1 : 30000);

                    Assert.AreEqual(Length, responseLength);
                }
            }

            [Test]
            [Timeout(30000)]
            public async Task WhenStreamVeryLargeStreamToServerThenBroadcasts()
            {
                var responseLength = 0;
                const int Length = 1000000;

                var stream = new EnumerableStream(Enumerable.Repeat((byte)123, Length));
                var waitHandle = new ManualResetEventSlim(false);

                var clientSslConfiguration = new ClientSslConfiguration("localhost", certificateValidationCallback: (sender, certificate, chain, errors) => true);

                Func<MessageEventArgs, Task> onMessage = async e =>
                    {
                        var bytes = await e.Data.ReadBytes(Length).ConfigureAwait(false);
                        responseLength = bytes.Count(x => x == 123);

                        waitHandle.Set();
                    };

                using (var sender = new WebSocket("wss://localhost:443/radio", sslAuthConfiguration: clientSslConfiguration))
                {
                    using (var client = new WebSocket("wss://localhost:443/radio", sslAuthConfiguration: clientSslConfiguration, onMessage: onMessage))
                    {
                        await sender.Connect().ConfigureAwait(false);
                        await client.Connect().ConfigureAwait(false);
                        await sender.Send(stream).ConfigureAwait(false);

                        waitHandle.Wait();

                        Assert.AreEqual(Length, responseLength);
                    }
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
    }
}