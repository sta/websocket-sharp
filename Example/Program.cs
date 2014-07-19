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
      //using (var ws = new WebSocket ("ws://localhost:4649/Chat"))
      //using (var ws = new WebSocket ("wss://localhost:4649/Chat"))
      //using (var ws = new WebSocket ("ws://localhost:4649/Chat?name=nobita"))
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
        // Changing the logging level
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

        // For HTTP Proxy
        //ws.SetHttpProxy ("http://localhost:3128", "nobita", "password");

        // Setting Origin header
        //ws.Origin = "http://echo.websocket.org";
        //ws.Origin = "http://localhost:4649";

        // Setting Cookies
        //ws.SetCookie (new Cookie ("name", "nobita"));
        //ws.SetCookie (new Cookie ("roles", "\"idiot, gunfighter\""));

        // Connecting to the server
        ws.Connect ();
        //ws.ConnectAsync ();

        Console.WriteLine ("\nType 'exit' to exit.\n");
        while (true) {
          Thread.Sleep (1000);
          Console.Write ("> ");
          var msg = Console.ReadLine ();
          if (msg == "exit")
            break;

          // Sending a text message
          ws.Send (msg);
        }
      }
    }
  }
}
