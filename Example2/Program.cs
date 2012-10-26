using System;
using WebSocketSharp.Server;

namespace Example2
{
  public class Program
  {
    public static void Main(string[] args)
    {
      /* Single service server
      //var wssv = new WebSocketServiceHost<Echo>("ws://localhost:4649");
      var wssv = new WebSocketServiceHost<Echo>("ws://localhost:4649/Echo");
      //var wssv = new WebSocketServiceHost<Echo>("ws://localhost:4649/エコー");
      //var wssv = new WebSocketServiceHost<Echo>(4649);
      //var wssv = new WebSocketServiceHost<Echo>(4649, "/Echo");
      //var wssv = new WebSocketServiceHost<Echo>(4649, "/エコー");
      //var wssv = new WebSocketServiceHost<Chat>("ws://localhost:4649");
      //var wssv = new WebSocketServiceHost<Chat>("ws://localhost:4649/Chat");
      //var wssv = new WebSocketServiceHost<Chat>("ws://localhost:4649/チャット");
      //var wssv = new WebSocketServiceHost<Chat>(4649);
      //var wssv = new WebSocketServiceHost<Chat>(4649, "/Chat");
      //var wssv = new WebSocketServiceHost<Chat>(4649, "/チャット");
      //wssv.Sweeped = false; // Stop the Sweep inactive session Timer.

      wssv.Start();
      Console.WriteLine(
        "WebSocket Service Host (url: {0})\n  listening on address: {1} port: {2}\n",
        wssv.Uri, wssv.Address, wssv.Port);
       */

      /// Multi services server
      var wssv = new WebSocketServer(4649);
      wssv.AddService<Echo>("/Echo");
      wssv.AddService<Echo>("/エコー");
      wssv.AddService<Chat>("/Chat");
      wssv.AddService<Chat>("/チャット");
      //wssv.Sweeped = false; // Must be set after any AddService methods done.

      wssv.Start();
      Console.WriteLine(
        "WebSocket Server listening on port: {0}\n", wssv.Port);
       

      Console.WriteLine("Press any key to stop server...");
      Console.ReadLine();

      wssv.Stop();
    }
  }
}
