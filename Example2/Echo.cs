using System;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Example2
{
  public class Echo : WebSocketBehavior
  {
    protected override void OnMessage (MessageEventArgs e)
    {
      var name = QueryString["name"];
      var msg = !name.IsNullOrEmpty ()
                ? String.Format ("\"{0}\" to {1}", e.Data, name)
                : e.Data;

      Send (msg);
    }
  }
}
