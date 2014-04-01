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
      var msg = !name.IsNullOrEmpty ()
                ? String.Format ("'{0}' to {1}", e.Data, name)
                : e.Data;

      Send (msg);
    }
  }
}
