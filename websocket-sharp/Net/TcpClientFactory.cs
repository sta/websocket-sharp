namespace System.Net.Sockets
{
    public static class TcpClientFactory
    {
        public static TcpClient CreateAndConnect(string host, int port)
        {
#if (DNXCORE50 || UAP10_0 || DOTNET5_4)
            var client = new TcpClient();
            client.ConnectAsync(host, port).Wait(TimeSpan.FromSeconds(1)); // TODO : ok to wait max 1 seconds ?

            return client;
#else
            return new TcpClient(host, port);
#endif
        }
    }
}