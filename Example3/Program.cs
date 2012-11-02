using System;
using WebSocketSharp;
using WebSocketSharp.Net;
using WebSocketSharp.Server;

namespace Example3
{
  public class Program
  {
    private static HttpServer _httpsv;

    public static void Main(string[] args)
    {
      _httpsv = new HttpServer(4649);
      //_httpsv.Sweeped = false; // Stop the Sweep inactive session Timer.
      _httpsv.AddService<Echo>("/Echo");
      _httpsv.AddService<Chat>("/Chat");

      _httpsv.OnGet += (sender, e) =>
      {
        onGet(e.Request, e.Response);
      };

      _httpsv.OnError += (sender, e) =>
      {
        Console.WriteLine(e.Message);
      };

      _httpsv.Start();
      Console.WriteLine("HTTP Server listening on port: {0} service path:", _httpsv.Port);
      foreach (var path in _httpsv.ServicePath)
        Console.WriteLine("  {0}", path);
      Console.WriteLine();

      Console.WriteLine("Press any key to stop server...");
      Console.ReadLine();

      _httpsv.Stop();       
    }

    private static byte[] getContent(string path)
    {
      if (path == "/")
        path += "index.html";

      return _httpsv.GetFile(path);
    }

    private static void onGet(HttpListenerRequest request, HttpListenerResponse response)
    {
      var content = getContent(request.RawUrl);
      if (content != null)
      {
        response.WriteContent(content);
        return;
      }

      response.StatusCode = (int)HttpStatusCode.NotFound;
    }
  }
}
