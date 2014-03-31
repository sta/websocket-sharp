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
    public static void Main (string [] args)
    {
      var wssv = new WebSocketServer (4649);
      //var wssv = new WebSocketServer (4649, true); // For Secure Connection
      //var wssv = new WebSocketServer ("ws://localhost:4649");
      //var wssv = new WebSocketServer ("wss://localhost:4649"); // For Secure Connection
#if DEBUG
      // Changing the logging level
      wssv.Log.Level = LogLevel.Trace;
#endif
      /* For Secure Connection
      var cert = ConfigurationManager.AppSettings ["ServerCertFile"];
      var password = ConfigurationManager.AppSettings ["CertFilePassword"];
      wssv.Certificate = new X509Certificate2 (cert, password);
       */

      /* For HTTP Authentication (Basic/Digest)
      wssv.AuthenticationSchemes = AuthenticationSchemes.Basic;
      wssv.Realm = "WebSocket Test";
      wssv.UserCredentialsFinder = identity => {
        var expected = "nobita";
        return identity.Name == expected
               ? new NetworkCredential (expected, "password", "gunfighter")
               : null;
      };
       */

      // Not to remove inactive clients periodically
      //wssv.KeepClean = false;

      // Adding WebSocket services
      wssv.AddWebSocketService<Echo> ("/Echo");
      wssv.AddWebSocketService<Chat> ("/Chat");

      /* With initializing
      wssv.AddWebSocketService<Chat> (
        "/Chat",
        () => new Chat ("Anon#") {
          Protocol = "chat",
          // Checking Origin header
          OriginValidator = value => {
            Uri origin;
            return !value.IsNullOrEmpty () &&
                   Uri.TryCreate (value, UriKind.Absolute, out origin) &&
                   origin.Host == "localhost";
          },
          // Checking Cookies
          CookiesValidator = (req, res) => {
            foreach (Cookie cookie in req) {
              cookie.Expired = true;
              res.Add (cookie);
            }

            return true;
          }
        });
       */

      wssv.Start ();
      if (wssv.IsListening) {
        Console.WriteLine (
          "A WebSocket server listening on port: {0}, providing services:", wssv.Port);

        foreach (var path in wssv.WebSocketServices.Paths)
          Console.WriteLine ("- {0}", path);
      }

      Console.WriteLine ("\nPress Enter key to stop the server...");
      Console.ReadLine ();

      wssv.Stop ();
    }
  }
}
