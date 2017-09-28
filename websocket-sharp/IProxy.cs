using System;
using System.Collections.Generic;
using System.Linq;
using WebSocketSharp.Net;
using System.Text;

namespace WebSocketSharp
{
    public interface IProxy
    {
        System.Net.Sockets.TcpClient ConnectThroughProxy(Uri DestinationUri);
    }
}
