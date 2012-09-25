using System;
using WebSocketSharp.Server;

namespace Example2
{
  public class Program
  {
    public static void Main(string[] args)
    {
      // Single service server
      var wssv = new WebSocketServer<Echo>("ws://localhost:4649");
      //var wssv = new WebSocketServer<Echo>(4649);
      //var wssv = new WebSocketServer<Chat>("ws://localhost:4649");
      //var wssv = new WebSocketServer<Chat>(4649);

      wssv.Start();
      Console.WriteLine(
        "WebSocket Server (url: {0})\n  listening on address: {1} port: {2}\n",
        wssv.Uri, wssv.Address, wssv.Port);
       

      /* Multi services server
      var wssv = new WebSocketServer(4649);
      wssv.AddService<Echo>("/Echo");
      wssv.AddService<Chat>("/Chat");

      wssv.Start();
      Console.WriteLine(
        "WebSocket Server listening on port: {0}\n", wssv.Port);
       */

      Console.WriteLine("Press any key to stop server...");
      Console.ReadLine();

      wssv.Stop();
    }
  }
}
