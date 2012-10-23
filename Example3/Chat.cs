using System;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Example3 {

  public class Chat : WebSocketService
  {
    private static object _forNum = new object();
    private static uint   _num    = 0;

    private string _name;

    private string getName()
    {
      return QueryString.Exists("name")
             ? QueryString["name"]
             : "anon#" + getNum();
    }

    private uint getNum()
    {
      lock (_forNum)
      {
        return ++_num;
      }
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
