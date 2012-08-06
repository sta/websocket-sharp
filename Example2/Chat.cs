using System;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Example2
{
  public class Chat : WebSocketService
  {
    protected override void onMessage(object sender, MessageEventArgs e)
    {
      Server.Send(e.Data);
    }
  }
}
