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
      //_httpsv = new HttpServer (4649, true) // Secure;

#if DEBUG
      _httpsv.Log.Level = LogLevel.TRACE;
#endif

      /* Secure Connection
      var cert = ConfigurationManager.AppSettings ["ServerCertFile"];
      var password = ConfigurationManager.AppSettings ["CertFilePassword"];
      _httpsv.Certificate = new X509Certificate2 (cert, password);
       */

      /* HTTP Authentication (Basic/Digest)
      _httpsv.AuthenticationSchemes = AuthenticationSchemes.Basic;
      _httpsv.Realm = "WebSocket Test";
      _httpsv.UserCredentialsFinder = identity => {
        var expected = "nobita";
        return identity.Name == expected
               ? new NetworkCredential (expected, "password", "gunfighter")
               : null;
      };
       */

      //_httpsv.KeepClean = false;

      _httpsv.RootPath = ConfigurationManager.AppSettings ["RootPath"];

      _httpsv.OnGet += (sender, e) => onGet (e);

      _httpsv.AddWebSocketService<Echo> ("/Echo");
      _httpsv.AddWebSocketService<Chat> ("/Chat");
      //_httpsv.AddWebSocketService<Chat> ("/Chat", () => new Chat ("Anon#"));

      _httpsv.Start ();
      if (_httpsv.IsListening) {
        Console.WriteLine (
          "An HTTP server listening on port: {0} WebSocket service paths:",
          _httpsv.Port);

        foreach (var path in _httpsv.WebSocketServices.ServicePaths)
          Console.WriteLine ("  {0}", path);
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

    private static void onGet (HttpRequestEventArgs eventArgs)
    {
      var req = eventArgs.Request;
      var res = eventArgs.Response;
      var content = getContent (req.RawUrl);
      if (content != null) {
        res.WriteContent (content);
        return;
      }

      res.StatusCode = (int) HttpStatusCode.NotFound;
    }
  }
}
