using System;
using System.Threading;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Example2
{
  public class Chat : WebSocketService
  {
    private static int _num = 0;

    private string _name;

    private string getName ()
    {
      return Context.QueryString ["name"] ?? ("anon#" + getNum ());
    }

    private int getNum ()
    {
      return Interlocked.Increment (ref _num);
    }

    protected override void OnOpen ()
    {
      _name = getName ();
    }

    protected override void OnMessage (MessageEventArgs e)
    {
      Sessions.Broadcast (String.Format ("{0}: {1}", _name, e.Data));
    }

    protected override void OnClose (CloseEventArgs e)
    {
      Sessions.Broadcast (String.Format ("{0} got logged off...", _name));
    }
  }
}
