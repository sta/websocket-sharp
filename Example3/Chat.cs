using System;
using System.Threading;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Example3
{
  public class Chat : WebSocketBehavior
  {
    private string     _name;
    private static int _number = 0;
    private string     _prefix;

    public Chat ()
    {
      _prefix = "anon#";
    }

    public string Prefix {
      get {
        return _prefix;
      }

      set {
        _prefix = !value.IsNullOrEmpty () ? value : "anon#";
      }
    }

    private string getName ()
    {
      var name = Context.QueryString["name"];
      return !name.IsNullOrEmpty () ? name : _prefix + getNumber ();
    }

    private static int getNumber ()
    {
      return Interlocked.Increment (ref _number);
    }

    protected override void OnClose (CloseEventArgs e)
    {
      Sessions.Broadcast (String.Format ("{0} got logged off...", _name));
    }

    protected override void OnMessage (MessageEventArgs e)
    {
      Sessions.Broadcast (String.Format ("{0}: {1}", _name, e.Data));
    }

    protected override void OnOpen ()
    {
      _name = getName ();
    }
  }
}
