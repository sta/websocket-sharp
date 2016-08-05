using System;
using System.Threading;
using WebSocketSharp;
using WebSocketSharp.Net;

namespace Example
{
  public class Program
  {
    public static void Main (string[] args)
    {
      // Create a new instance of the WebSocket class.
      //
      // The WebSocket class inherits the System.IDisposable interface, so you can
      // use the using statement. And the WebSocket connection will be closed with
      // close status 1001 (going away) when the control leaves the using block.
      //
      // If you would like to connect to the server with the secure connection,
      // you should create a new instance with a wss scheme WebSocket URL.

      using (var nf = new Notifier ())
      using (var ws = new WebSocket ("ws://echo.websocket.org"))
      //using (var ws = new WebSocket ("wss://echo.websocket.org"))
      //using (var ws = new WebSocket ("ws://localhost:4649/Echo"))
      //using (var ws = new WebSocket ("wss://localhost:5963/Echo"))
      //using (var ws = new WebSocket ("ws://localhost:4649/Echo?name=nobita"))
      //using (var ws = new WebSocket ("wss://localhost:5963/Echo?name=nobita"))
      //using (var ws = new WebSocket ("ws://localhost:4649/Chat"))
      //using (var ws = new WebSocket ("wss://localhost:5963/Chat"))
      //using (var ws = new WebSocket ("ws://localhost:4649/Chat?name=nobita"))
      //using (var ws = new WebSocket ("wss://localhost:5963/Chat?name=nobita"))
      {
        // Set the WebSocket events.

        ws.OnOpen += (sender, e) => ws.Send ("Hi, there!");

        ws.OnMessage += (sender, e) =>
            nf.Notify (
              new NotificationMessage {
                Summary = "WebSocket Message",
                Body = !e.IsPing ? e.Data : "Received a ping.",
                Icon = "notification-message-im"
              }
            );

        ws.OnError += (sender, e) =>
            nf.Notify (
              new NotificationMessage {
                Summary = "WebSocket Error",
                Body = e.Message,
                Icon = "notification-message-im"
              }
            );

        ws.OnClose += (sender, e) =>
            nf.Notify (
              new NotificationMessage {
                Summary = String.Format ("WebSocket Close ({0})", e.Code),
                Body = e.Reason,
                Icon = "notification-message-im"
              }
            );
#if DEBUG
        // To change the logging level.
        ws.Log.Level = LogLevel.Trace;

        // To change the wait time for the response to the Ping or Close.
        //ws.WaitTime = TimeSpan.FromSeconds (10);

        // To emit a WebSocket.OnMessage event when receives a ping.
        //ws.EmitOnPing = true;
#endif
        // To enable the Per-message Compression extension.
        //ws.Compression = CompressionMethod.Deflate;

        // To validate the server certificate.
        /*
        ws.SslConfiguration.ServerCertificateValidationCallback =
          (sender, certificate, chain, sslPolicyErrors) => {
            ws.Log.Debug (
              String.Format (
                "Certificate:\n- Issuer: {0}\n- Subject: {1}",
                certificate.Issuer,
                certificate.Subject
              )
            );

            return true; // If the server certificate is valid.
          };
         */

        // To send the credentials for the HTTP Authentication (Basic/Digest).
        //ws.SetCredentials ("nobita", "password", false);

        // To send the Origin header.
        //ws.Origin = "http://localhost:4649";

        // To send the cookies.
        //ws.SetCookie (new Cookie ("name", "nobita"));
        //ws.SetCookie (new Cookie ("roles", "\"idiot, gunfighter\""));

        // To connect through the HTTP Proxy server.
        //ws.SetProxy ("http://localhost:3128", "nobita", "password");

        // To enable the redirection.
        //ws.EnableRedirection = true;

        // Connect to the server.
        ws.Connect ();

        // Connect to the server asynchronously.
        //ws.ConnectAsync ();

        Console.WriteLine ("\nType 'exit' to exit.\n");
        while (true) {
          Thread.Sleep (1000);
          Console.Write ("> ");
          var msg = Console.ReadLine ();
          if (msg == "exit")
            break;

          // Send a text message.
          ws.Send (msg);
        }
      }
    }
  }
}
