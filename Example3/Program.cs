using System;
using System.Configuration;
using System.Security.Cryptography.X509Certificates;
using WebSocketSharp;
using WebSocketSharp.Net;
using WebSocketSharp.Server;

namespace Example3
{
  public class Program
  {
    private static HttpServer _httpsv;

    public static void Main (string [] args)
    {
      _httpsv = new HttpServer (4649);
      //_httpsv = new HttpServer (4649, true); // For Secure Connection
#if DEBUG
      // Changing the logging level
      _httpsv.Log.Level = LogLevel.Trace;
#endif
      /* For Secure Connection
      var cert = ConfigurationManager.AppSettings ["ServerCertFile"];
      var password = ConfigurationManager.AppSettings ["CertFilePassword"];
      _httpsv.Certificate = new X509Certificate2 (cert, password);
       */

      /* For HTTP Authentication (Basic/Digest)
      _httpsv.AuthenticationSchemes = AuthenticationSchemes.Basic;
      _httpsv.Realm = "WebSocket Test";
      _httpsv.UserCredentialsFinder = identity => {
        var expected = "nobita";
        return identity.Name == expected
               ? new NetworkCredential (expected, "password", "gunfighter")
               : null;
      };
       */

      // Not to remove inactive clients in WebSocket services periodically
      //_httpsv.KeepClean = false;

      // Setting the document root path
      _httpsv.RootPath = ConfigurationManager.AppSettings ["RootPath"];

      // Setting HTTP method events
      _httpsv.OnGet += (sender, e) => onGet (e);

      // Adding WebSocket services
      _httpsv.AddWebSocketService<Echo> ("/Echo");
      _httpsv.AddWebSocketService<Chat> ("/Chat");

      /* With initializing
      _httpsv.AddWebSocketService<Chat> (
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

      _httpsv.Start ();
      if (_httpsv.IsListening) {
        Console.WriteLine (
          "An HTTP server listening on port: {0}, providing WebSocket services:", _httpsv.Port);

        foreach (var path in _httpsv.WebSocketServices.Paths)
          Console.WriteLine ("- {0}", path);
      }

      Console.WriteLine ("\nPress Enter key to stop the server...");
      Console.ReadLine ();

      _httpsv.Stop ();
    }

    private static byte [] getContent (string path)
    {
      if (path == "/")
        path += "index.html";

      return _httpsv.GetFile (path);
    }

    private static void onGet (HttpRequestEventArgs e)
    {
      var req = e.Request;
      var res = e.Response;
      var content = getContent (req.RawUrl);
      if (content != null) {
        res.WriteContent (content);
        return;
      }

      res.StatusCode = (int) HttpStatusCode.NotFound;
    }
  }
}
