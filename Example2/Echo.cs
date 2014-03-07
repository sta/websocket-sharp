using System;
using WebSocketSharp;
using WebSocketSharp.Net;
using WebSocketSharp.Server;

namespace Example2
{
  public class Echo : WebSocketService
  {
    protected override void OnMessage (MessageEventArgs e)
    {
      var name = Context.QueryString ["name"] ?? String.Empty;
      var msg = name.Length > 0
              ? String.Format ("'{0}' to {1}", e.Data, name)
              : e.Data;

      Send (msg);
    }
  }
}
