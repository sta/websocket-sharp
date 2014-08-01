using System;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Example3
{
  public class Echo : WebSocketService
  {
	  /// <summary>
	  /// Called when the WebSocket connection used in the current session has been established.
	  /// </summary>
	  protected override void OnOpen()
	  {
		  base.OnOpen();
		  Console.WriteLine("Connection opened");
	  }

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
