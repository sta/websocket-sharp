using System;
using System.Threading;
using WebSocketSharp;

namespace Example
{
  public class Program
  {
    public static void Main (string[] args)
    {
      //WebSocketServer wssv = new WebSocketServer("ws://localhost");
      WebSocketServer wssv = new WebSocketServer("ws://localhost:4649");

      wssv.OnConnection += (sender, e) =>
      {
        WebSocket ws = e.Socket;
        ws.OnMessage += (sender_, e_) =>
        {
          // Echo
          ws.Send(e_.Data);
          // Chat
          //wssv.Send(e_.Data);
        };
      };

      wssv.Start();
      Console.WriteLine(
        "WebSocket Server ({0}) listening on address: {1} port: {2}\n", wssv.Url, wssv.Address, wssv.Port);

      Console.WriteLine("Press any key to stop server...");
      Console.ReadLine();

      wssv.Stop();
    }
  }
}
