using System;
using System.Threading;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Example3
{
  public class Chat : WebSocketService
  {
    private static int _num = 0;

    private string _name;
    private string _prefix;

    public Chat ()
      : this (null)
    {
    }

    public Chat (string prefix)
    {
      _prefix = prefix ?? "anon#";
    }

    private string getName ()
    {
      return Context.QueryString ["name"] ?? (_prefix + getNum ());
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
