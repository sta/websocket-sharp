#if NOTIFY
using Notifications;
#endif
using System;
using System.Threading;
using WebSocketSharp;

namespace Example
{
  public class Program
  {
    public static void Main(string[] args)
    {
      //using (WebSocket ws = new WebSocket("ws://localhost:8000/"))
      using (WebSocket ws = new WebSocket("ws://localhost:8000/", "chat"))
      {
        /*ws.OnOpen += (o, e) =>
        {
          //Do something.
        };
         */
        ws.OnMessage += (o, s) =>
        {
#if NOTIFY
          Notification nf = new Notification("[WebSocket] Message",
                                             s,
                                             "notification-message-im");
          nf.AddHint("append", "allowed");
          nf.Show();
#else
          Console.WriteLine("[WebSocket] Message: {0}", s);
#endif
        };

        ws.OnError += (o, s) =>
        {
          Console.WriteLine("[WebSocket] Error  : {0}", s);
        };

        /*ws.OnClose += (o, e) =>
        {
          //Do something.
        };
         */
        ws.Connect();

        Thread.Sleep(500);
        Console.WriteLine("\nType \"exit\" to exit.\n");

        string data;
        while (true)
        {
          Thread.Sleep(500);

          Console.Write("> ");
          data = Console.ReadLine();
          if (data == "exit")
          {
            break;
          }

          ws.Send(data);
        }
      }
    }
  }
}
