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
      EventHandler onOpen = (o, e) =>
      {
        Console.WriteLine("[WebSocket] Opened.");
      };

      MessageEventHandler onMessage = (o, s) =>
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

      MessageEventHandler onError = (o, s) =>
      {
        Console.WriteLine("[WebSocket] Error  : {0}", s);
      };

      EventHandler onClose = (o, e) =>
      {
        Console.WriteLine("[WebSocket] Closed.");
      };

      //using (WebSocket ws = new WebSocket("ws://localhost:8000/", onOpen, onMessage, onError, onClose))
      using (WebSocket ws = new WebSocket("ws://localhost:8000/", "chat", onOpen, onMessage, onError, onClose))
      {
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
