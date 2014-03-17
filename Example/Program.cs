using System;
using System.Threading;
using WebSocketSharp;
using WebSocketSharp.Net;

namespace Example
{
  public class Program
  {
    public static void Main (string [] args)
    {
      using (var nf = new Notifier ())
      using (var ws = new WebSocket ("ws://echo.websocket.org"))
      //using (var ws = new WebSocket ("wss://echo.websocket.org")) // For Secure Connection
      //using (var ws = new WebSocket ("ws://localhost:4649/Echo"))
      //using (var ws = new WebSocket ("wss://localhost:4649/Echo"))
      //using (var ws = new WebSocket ("ws://localhost:4649/Echo?name=nobita"))
      //using (var ws = new WebSocket ("ws://localhost:4649/エコー?name=のび太"))
      //using (var ws = new WebSocket ("ws://localhost:4649/Chat"))
      //using (var ws = new WebSocket ("wss://localhost:4649/Chat"))
      //using (var ws = new WebSocket ("ws://localhost:4649/Chat?name=nobita"))
      //using (var ws = new WebSocket ("ws://localhost:4649/チャット?name=のび太"))
      {
        /* Setting WebSocket events */
        ws.OnOpen += (sender, e) => ws.Send ("Hi, there!");

        ws.OnMessage += (sender, e) =>
          nf.Notify (
            new NotificationMessage {
              Summary = "WebSocket Message",
              Body = e.Data,
              Icon = "notification-message-im"
            });

        ws.OnError += (sender, e) =>
          nf.Notify (
            new NotificationMessage {
              Summary = "WebSocket Error",
              Body = e.Message,
              Icon = "notification-message-im"
            });

        ws.OnClose += (sender, e) =>
          nf.Notify (
            new NotificationMessage {
              Summary = String.Format ("WebSocket Close ({0})", e.Code),
              Body = e.Reason,
              Icon = "notification-message-im"
            });
         
#if DEBUG
        // Changing logging level
        ws.Log.Level = LogLevel.Trace;
#endif
        // Setting Per-message Compression
        //ws.Compression = CompressionMethod.Deflate;

        /* For Secure Connection
        ws.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => {
          ws.Log.Debug (String.Format ("\n{0}\n{1}", certificate.Issuer, certificate.Subject));
          return true; // If the server cert is valid
        };
         */

        // For HTTP Authentication (Basic/Digest)
        //ws.SetCredentials ("nobita", "password", false);

        // Setting Origin header
        //ws.Origin = "http://echo.websocket.org";

        // Setting Cookies
        //ws.SetCookie (new Cookie ("nobita", "\"idiot, gunfighter\""));
        //ws.SetCookie (new Cookie ("dora", "tanuki"));

        ws.Connect ();
        //ws.ConnectAsync ();

        Console.WriteLine ("\nType \"exit\" to exit.\n");
        while (true) {
          Thread.Sleep (1000);
          Console.Write ("> ");
          var msg = Console.ReadLine ();
          if (msg == "exit")
            break;

          ws.Send (msg);
        }
      }
    }
  }
}
