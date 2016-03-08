#if (DNXCORE50 || UAP10_0 || DOTNET5_4)
namespace System.Net.Sockets
{
    public static class TcpClientExtensions
    {
        public static void Close(this TcpClient client)
        {
            client.Dispose();
        }
    }
}
#endif