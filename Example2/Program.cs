using System;
using System.Configuration;
using System.Security.Cryptography.X509Certificates;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace Example2 {

  public class Program {

    public static void Main (string [] args)
    {
      /* Single service server
      var wssv = new WebSocketServiceHost<Echo> ("ws://localhost:4649");
      //var wssv = new WebSocketServiceHost<Echo> ("ws://localhost:4649/Echo");
      //var wssv = new WebSocketServiceHost<Echo> ("ws://localhost:4649/エコー");
      //var wssv = new WebSocketServiceHost<Echo> (4649);
      //var wssv = new WebSocketServiceHost<Echo> (4649, "/Echo");
      //var wssv = new WebSocketServiceHost<Echo> (4649, "/エコー");
      //var wssv = new WebSocketServiceHost<Chat> ("ws://localhost:4649");
      //var wssv = new WebSocketServiceHost<Chat> ("ws://localhost:4649/Chat");
      //var wssv = new WebSocketServiceHost<Chat> ("ws://localhost:4649/チャット");
      //var wssv = new WebSocketServiceHost<Chat> (4649);
      //var wssv = new WebSocketServiceHost<Chat> (4649, "/Chat");
      //var wssv = new WebSocketServiceHost<Chat> (4649, "/チャット");
      #if DEBUG
      wssv.Log.Level = LogLevel.TRACE;
      #endif
      //wssv.KeepClean = false;

      wssv.Start ();
      Console.WriteLine (
        "WebSocket Service Host (url: {0})\n  listening on address: {1} port: {2}\n",
        wssv.Uri, wssv.Address, wssv.Port);
       */

      /* Multi services server */
      var wssv = new WebSocketServer (4649);
      //var wssv = new WebSocketServer (4649, true);
      //var wssv = new WebSocketServer ("ws://localhost:4649");
      //var wssv = new WebSocketServer ("wss://localhost:4649");
      #if DEBUG
      wssv.Log.Level = LogLevel.TRACE;
      #endif
      //var file = ConfigurationManager.AppSettings ["ServerCertFile"];
      //var password = ConfigurationManager.AppSettings ["CertFilePassword"];
      //wssv.Certificate = new X509Certificate2 (file, password);
      //wssv.KeepClean = false;
      wssv.AddWebSocketService<Echo> ("/Echo");
      wssv.AddWebSocketService<Chat> ("/Chat");
      //wssv.AddWebSocketService<Echo> ("/エコー");
      //wssv.AddWebSocketService<Chat> ("/チャット");

      wssv.Start ();
      Console.WriteLine (
        "WebSocket Server listening on port: {0} service path:", wssv.Port);
      foreach (var path in wssv.ServicePaths)
        Console.WriteLine ("  {0}", path);

      Console.WriteLine ();
      Console.WriteLine ("Press enter key to stop server...");
      Console.ReadLine ();

      wssv.Stop ();
    }
  }
}
