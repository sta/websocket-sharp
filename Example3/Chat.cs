using System;
using System.Threading;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Example3 {

  public class Chat : WebSocketService
  {
    private static int _num = 0;

    private string _name;

    private string getName()
    {
      return QueryString.Exists("name")
             ? QueryString["name"]
             : "anon#" + getNum();
    }

    private int getNum()
    {
      return Interlocked.Increment(ref _num);
    }

    protected override void OnOpen(object sender, EventArgs e)
    {
      _name = getName();
    }

    protected override void OnMessage(object sender, MessageEventArgs e)
    {
      
      var msg = String.Format("{0}: {1}", _name, e.Data);
      Publish(msg);
    }

    protected override void OnClose(object sender, CloseEventArgs e)
    {
      var msg = String.Format("{0} got logged off...", _name);
      Publish(msg);
    }
  }
}
