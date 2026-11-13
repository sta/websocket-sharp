// Measures how long AltWebSocketSharp takes to close a connection when the
// peer answers the close frame promptly. A healthy close takes a few ms; the
// bug this harness was written for made every close wait the full 5 s timeout.
//
// Usage: dotnet run -c Release --project tools/CloseTiming [iterations]
// Exit code 1 if any close takes 1 s or longer.

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using AltWebSocketSharp;
using AltWebSocketSharp.Server;
using NetClientWebSocket = System.Net.WebSockets.ClientWebSocket;
using NetWebSocketCloseStatus = System.Net.WebSockets.WebSocketCloseStatus;
using NetWebSocketMessageType = System.Net.WebSockets.WebSocketMessageType;

static class Program
{
    const long LimitMs = 1000;

    static int Main(string[] args)
    {
        var iterations = args.Length > 0 ? int.Parse(args[0]) : 3;
        var failed = false;

        for (var i = 1; i <= iterations; i++)
        {
            var (clientMs, clean) = ClientCloseAgainstSystemNetServer();
            var serverMs = ServerCloseAgainstSystemNetClient();
            Console.WriteLine($"run {i}: ClientWebSocket.Close() = {clientMs} ms (WasClean={clean}), server-side session close = {serverMs} ms");
            failed |= clientMs >= LimitMs || serverMs >= LimitMs || !clean;
        }

        Console.WriteLine(failed ? "FAIL: a close took >= 1000 ms or the client close was not clean" : "OK: every close took < 1000 ms and the client close was clean");
        return failed ? 1 : 0;
    }

    static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    // AltWebSocketSharp.ClientWebSocket closes against a System.Net.WebSockets
    // server that echoes the close frame as soon as it receives it.
    static (long ms, bool wasClean) ClientCloseAgainstSystemNetServer()
    {
        var port = FreePort();
        var prefix = $"http://127.0.0.1:{port}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();

        var serverTask = Task.Run(async () =>
        {
            var ctx = await listener.GetContextAsync();
            var wsCtx = await ctx.AcceptWebSocketAsync(null);
            var ws = wsCtx.WebSocket;
            var buffer = new byte[1024];
            while (true)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == NetWebSocketMessageType.Close)
                {
                    await ws.CloseOutputAsync(NetWebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                    break;
                }
            }
        });

        var client = new ClientWebSocket($"ws://127.0.0.1:{port}/");
        client.Log.Level = LogLevel.Fatal;
        var wasClean = false;
        client.OnClose += (_, e) => wasClean = e.WasClean;
        client.Connect();
        if (client.ReadyState != WebSocketState.Open)
            throw new Exception("client did not connect");

        var sw = Stopwatch.StartNew();
        client.Close();
        sw.Stop();

        try { serverTask.Wait(TimeSpan.FromSeconds(10)); } catch (AggregateException) { }
        listener.Stop();
        return (sw.ElapsedMilliseconds, wasClean);
    }

    class Holder : WebSocketBehavior
    {
        public static TaskCompletionSource<Holder> Opened;
        public void CloseFromServer() => Sessions.CloseSession(ID);
        protected override void OnOpen() => Opened.TrySetResult(this);
    }

    // AltWebSocketSharp.Server closes a session against a System.Net.WebSockets
    // client that answers the close frame as soon as it receives it.
    static long ServerCloseAgainstSystemNetClient()
    {
        var port = FreePort();
        var server = new WebSocketServer($"ws://127.0.0.1:{port}");
        server.Log.Level = LogLevel.Fatal;
        Holder.Opened = new TaskCompletionSource<Holder>(TaskCreationOptions.RunContinuationsAsynchronously);
        server.AddWebSocketService<Holder>("/");
        server.Start();

        using var client = new NetClientWebSocket();
        client.ConnectAsync(new Uri($"ws://127.0.0.1:{port}/"), CancellationToken.None).Wait();
        var clientTask = Task.Run(async () =>
        {
            var buffer = new byte[1024];
            while (client.State == System.Net.WebSockets.WebSocketState.Open)
            {
                var result = await client.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                if (result.MessageType == NetWebSocketMessageType.Close)
                    break; // System.Net answers the close frame automatically
            }
        });

        var holder = Holder.Opened.Task.Result;
        var sw = Stopwatch.StartNew();
        holder.CloseFromServer();
        sw.Stop();

        // The server drops TCP after its close handshake; an EOF here is fine.
        try { clientTask.Wait(TimeSpan.FromSeconds(10)); } catch (AggregateException) { }
        server.Stop();
        return sw.ElapsedMilliseconds;
    }
}
