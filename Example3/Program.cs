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
      //_httpsv = new HttpServer (4649, true);
      #if DEBUG
      _httpsv.Log.Level = LogLevel.TRACE;
      #endif
      _httpsv.RootPath = ConfigurationManager.AppSettings ["RootPath"];
      //var certFile = ConfigurationManager.AppSettings ["ServerCertFile"];
      //var password = ConfigurationManager.AppSettings ["CertFilePassword"];
      //_httpsv.Certificate = new X509Certificate2 (certFile, password);
      //_httpsv.KeepClean = false;
      _httpsv.AddWebSocketService<Echo> ("/Echo");
      _httpsv.AddWebSocketService<Chat> ("/Chat");

      _httpsv.OnGet += (sender, e) =>
      {
        onGet (e);
      };

      _httpsv.Start ();
      if (_httpsv.IsListening)
      {
        Console.WriteLine ("HTTP Server listening on port: {0} service path:", _httpsv.Port);
        foreach (var path in _httpsv.WebSocketServices.ServicePaths)
          Console.WriteLine ("  {0}", path);

        Console.WriteLine ();
      }

      Console.WriteLine ("Press Enter key to stop the server...");
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
      var request = eventArgs.Request;
      var response = eventArgs.Response;
      var content = getContent (request.RawUrl);
      if (content != null)
      {
        response.WriteContent (content);
        return;
      }

      response.StatusCode = (int) HttpStatusCode.NotFound;
    }
  }
}
