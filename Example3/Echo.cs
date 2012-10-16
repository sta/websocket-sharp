using System;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Example3
{
  public class Echo : WebSocketService
  {
    protected override void onMessage(object sender, MessageEventArgs e)
    {
      Send(e.Data);
    }

    protected override void onClose(object sender, CloseEventArgs e)
    {
      Console.WriteLine("[Echo] Close({0})", e.Code);
    }
  }
}
