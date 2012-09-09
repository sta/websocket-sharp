using System;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Example3
{
  public class Chat : WebSocketService
  {
    protected override void onMessage(object sender, MessageEventArgs e)
    {
      Publish(e.Data);
    }
  }
}
