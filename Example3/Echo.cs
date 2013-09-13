using System;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Example3
{
  public class Echo : WebSocketService
  {
    protected override void OnMessage (MessageEventArgs e)
    {
      var name = Context.QueryString ["name"];
      var msg = name != null
              ? String.Format ("Returns '{0}' to {1}", e.Data, name)
              : e.Data;

      Send (msg);
    }
  }
}
