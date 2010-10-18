using Notifications;
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
        ws.OnMessage += (o, e) =>
        {
#if LINUX
  #if NOTIFY
          ws.MsgNf.Summary = "[WebSocket] Message";
          ws.MsgNf.Body = e;
          ws.MsgNf.IconName = "notification-message-im";
          ws.MsgNf.Show();
  #else
          Notification nf = new Notification("[WebSocket] Message",
                                             e,
                                             "notification-message-im");
          nf.Show();
  #endif
#else
          Console.WriteLine(e);
#endif
        };

        ws.OnError += (o, e) =>
        {
#if LINUX
          Notification nf = new Notification("[WebSocket] Error",
                                             e,
                                             "notification-network-disconnected");
          nf.Show();
#else
          Console.WriteLine("Error: ", e);
#endif
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
