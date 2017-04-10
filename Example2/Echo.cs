using System;
using Semisweet;
using Semisweet.Server;

namespace Example2
{
  public class Echo : WebSocketBehavior
  {
    protected override void OnMessage (MessageEventArgs e)
    {
      var name = Context.QueryString["name"];
      Send (!name.IsNullOrEmpty () ? String.Format ("\"{0}\" to {1}", e.Data, name) : e.Data);
    }
  }
}
