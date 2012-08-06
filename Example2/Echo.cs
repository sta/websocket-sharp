using System;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Example2
{
  public class Echo : WebSocketService
  {
    protected override void onMessage(object sender, MessageEventArgs e)
    {
      Send(e.Data);
    }
  }
}
