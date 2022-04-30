using System;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Example2
{
  public class Echo : WebSocketBehavior
  {
    protected override void OnMessage (MessageEventArgs e)
    {
      Send (e.Data);
    }
  }
}
