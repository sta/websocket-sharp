#if (DNXCORE50 || UAP10_0 || DOTNET5_4)
namespace System.Net.Sockets
{
    public static class SocketExtensions
    {
        public static void Close(this Socket socket)
        {
            socket.Dispose();
        }
    }
}
#endif