using System;
using System.Configuration;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using WebSocketSharp;
using WebSocketSharp.Net;
using WebSocketSharp.Server;

namespace Example3
{
  public class Program
  {
    public static void Main (string[] args)
    {
      /* Create a new instance of the HttpServer class.
       *
       * If you would like to provide the secure connection, you should create the instance
       * with the 'secure' parameter set to true.
       */
      var httpsv = new HttpServer (4649);
      //httpsv = new HttpServer (4649, true);
#if DEBUG
      // To change the logging level.
      httpsv.Log.Level = LogLevel.Trace;

      // To change the wait time for the response to the WebSocket Ping or Close.
      httpsv.WaitTime = TimeSpan.FromSeconds (2);
#endif
      /* To provide the secure connection.
      var cert = ConfigurationManager.AppSettings["ServerCertFile"];
      var passwd = ConfigurationManager.AppSettings["CertFilePassword"];
      httpsv.SslConfiguration.ServerCertificate = new X509Certificate2 (cert, passwd);
       */

      /* To provide the HTTP Authentication (Basic/Digest).
      httpsv.AuthenticationSchemes = AuthenticationSchemes.Basic;
      httpsv.Realm = "WebSocket Test";
      httpsv.UserCredentialsFinder = id => {
        var name = id.Name;

        // Return user name, password, and roles.
        return name == "nobita"
               ? new NetworkCredential (name, "password", "gunfighter")
               : null; // If the user credentials aren't found.
      };
       */

      // To set the document root path.
      httpsv.RootPath = ConfigurationManager.AppSettings["RootPath"];

      // To set the HTTP GET method event.
      httpsv.OnGet += (sender, e) => {
        var req = e.Request;
        var res = e.Response;

        var path = req.RawUrl;
        if (path == "/")
          path += "index.html";

        var content = httpsv.GetFile (path);
        if (content == null) {
          res.StatusCode = (int) HttpStatusCode.NotFound;
          return;
        }

        if (path.EndsWith (".html")) {
          res.ContentType = "text/html";
          res.ContentEncoding = Encoding.UTF8;
        }

        res.WriteContent (content);
      };

      // Not to remove the inactive WebSocket sessions periodically.
      //httpsv.KeepClean = false;

      // To resolve to wait for socket in TIME_WAIT state.
      //httpsv.ReuseAddress = true;

      // Add the WebSocket services.
      httpsv.AddWebSocketService<Echo> ("/Echo");
      httpsv.AddWebSocketService<Chat> ("/Chat");

      /* Add the WebSocket service with initializing.
      httpsv.AddWebSocketService<Chat> (
        "/Chat",
        () => new Chat ("Anon#") {
          Protocol = "chat",
          // To ignore the Sec-WebSocket-Extensions header.
          IgnoreExtensions = true,
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

      httpsv.Start ();
      if (httpsv.IsListening) {
        Console.WriteLine ("Listening on port {0}, and providing WebSocket services:", httpsv.Port);
        foreach (var path in httpsv.WebSocketServices.Paths)
          Console.WriteLine ("- {0}", path);
      }

      Console.WriteLine ("\nPress Enter key to stop the server...");
      Console.ReadLine ();

      httpsv.Stop ();
    }
  }
}
