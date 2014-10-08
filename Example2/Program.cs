using System;
using System.Configuration;
using System.Security.Cryptography.X509Certificates;
using WebSocketSharp;
using WebSocketSharp.Net;
using WebSocketSharp.Server;

namespace Example2
{
  public class Program
  {
    public static void Main (string[] args)
    {
      /* Create a new instance of the WebSocketServer class.
       *
       * If you would like to provide the secure connection, you should create the instance
       * with the 'secure' parameter set to true, or the wss scheme WebSocket URL.
       */
      var wssv = new WebSocketServer (4649);
      //var wssv = new WebSocketServer (4649, true);
      //var wssv = new WebSocketServer ("ws://localhost:4649");
      //var wssv = new WebSocketServer ("wss://localhost:4649");
#if DEBUG
      // To change the logging level.
      wssv.Log.Level = LogLevel.Trace;

      // To change the wait time for the response to the WebSocket Ping or Close.
      wssv.WaitTime = TimeSpan.FromSeconds (2);
#endif
      /* To provide the secure connection.
      var cert = ConfigurationManager.AppSettings["ServerCertFile"];
      var password = ConfigurationManager.AppSettings["CertFilePassword"];
      wssv.Certificate = new X509Certificate2 (cert, password);
       */

      /* To provide the HTTP Authentication (Basic/Digest).
      wssv.AuthenticationSchemes = AuthenticationSchemes.Basic;
      wssv.Realm = "WebSocket Test";
      wssv.UserCredentialsFinder = id => {
        var expected = "nobita";
        return id.Name == expected
               ? new NetworkCredential (expected, "password", "gunfighter")
               : null; // If the user credentials aren't found.
      };
       */

      // Not to remove the inactive sessions periodically.
      //wssv.KeepClean = false;

      // To resolve to wait for socket in TIME_WAIT state.
      //wssv.ReuseAddress = true;

      // Add the WebSocket services.
      wssv.AddWebSocketService<Echo> ("/Echo");
      wssv.AddWebSocketService<Chat> ("/Chat");

      /* Add the WebSocket service with initializing.
      wssv.AddWebSocketService<Chat> (
        "/Chat",
        () => new Chat ("Anon#") {
          Protocol = "chat",
          // To validate the Origin header.
          OriginValidator = val => {
            // Check the value of the Origin header, and return true if valid.
            Uri origin;
            return !val.IsNullOrEmpty () &&
                   Uri.TryCreate (val, UriKind.Absolute, out origin) &&
                   origin.Host == "localhost";
          },
          // To validate the Cookies.
          CookiesValidator = (req, res) => {
            // Check the Cookies in 'req', and set the Cookies to send to the client with 'res'
            // if necessary.
            foreach (Cookie cookie in req) {
              cookie.Expired = true;
              res.Add (cookie);
            }

            return true; // If valid.
          }
        });
       */

      wssv.Start ();
      if (wssv.IsListening) {
        Console.WriteLine ("Listening on port {0}, and providing WebSocket services:", wssv.Port);
        foreach (var path in wssv.WebSocketServices.Paths)
          Console.WriteLine ("- {0}", path);
      }

      Console.WriteLine ("\nPress Enter key to stop the server...");
      Console.ReadLine ();

      wssv.Stop ();
    }
  }
}
