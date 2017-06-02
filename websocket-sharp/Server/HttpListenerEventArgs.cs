using System;
using System.Collections.Generic;
using System.Text;
using WebSocketSharp.Net;

namespace WebSocketSharp.Server
{
    public class HttpListenerEventArgs : EventArgs
    {
        public HttpListenerContext Context;

        public HttpListenerEventArgs(HttpListenerContext context)
        {
            // TODO: Complete member initialization
            this.Context = context;
        }
    }
}
