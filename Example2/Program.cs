using System;
using WebSocketSharp.Server;

namespace Example2
{
  public class Program
  {
    public static void Main(string[] args)
    {
      var wssv = new WebSocketServer<Echo>("ws://localhost:4649");
      //var wssv = new WebSocketServer<Chat>("ws://localhost:4649");

      wssv.Start();
      Console.WriteLine(
        "WebSocket Server (url: {0})\n  listening on address: {1} port: {2}\n",
        wssv.Url, wssv.Address, wssv.Port);

      Console.WriteLine("Press any key to stop server...");
      Console.ReadLine();

      wssv.Stop();
    }
  }
}
