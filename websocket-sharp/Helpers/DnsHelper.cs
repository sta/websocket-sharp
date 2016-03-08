using System.Net;

namespace WebSocketSharp
{
    public static class DnsHelper
    {
        public static IPAddress[] GetHostAddresses(string hostNameOrAddress)
        {
#if (DNXCORE50 || UAP10_0 || DOTNET5_4)
            return Dns.GetHostAddressesAsync(hostNameOrAddress).Result;
#else
            return Dns.GetHostAddresses(hostNameOrAddress);
#endif
        }

        public static IPHostEntry GetHostEntry(string hostNameOrAddress)
        {
#if (DNXCORE50 || UAP10_0 || DOTNET5_4)
            return Dns.GetHostEntryAsync(hostNameOrAddress).Result;
#else
            return Dns.GetHostEntry(hostNameOrAddress);
#endif
        }
    }
}
