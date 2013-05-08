using System;
using System.Threading;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Example2 {

  public class Chat : WebSocketService
  {
    private static int _num = 0;

    private string _name;

    private string getName()
    {
      return QueryString.Contains("name")
             ? QueryString["name"]
             : "anon#" + getNum();
    }

    private int getNum()
    {
      return Interlocked.Increment(ref _num);
    }

    protected override void OnOpen()
    {
      _name = getName();
    }

    protected override void OnMessage(MessageEventArgs e)
    {
      
      var msg = String.Format("{0}: {1}", _name, e.Data);
      Broadcast(msg);
    }

    protected override void OnClose(CloseEventArgs e)
    {
      var msg = String.Format("{0} got logged off...", _name);
      Broadcast(msg);
    }
  }
}
