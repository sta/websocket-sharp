using System;
using System.Threading;

namespace Example
{
  public class Program
  {
    public static void Main(string[] args)
    {
      //using (AudioStreamer streamer = new AudioStreamer("ws://localhost:3000/socket"))
      using (AudioStreamer streamer = new AudioStreamer("ws://agektmr.node-ninja.com:3000/socket"))
      {
        streamer.Connect();

        Thread.Sleep(500);
        Console.WriteLine("\nType \"exit\" to exit.\n");

        string data;
        while (true)
        {
          Thread.Sleep(500);

          Console.Write("> ");
          data = Console.ReadLine();
          if (data == "exit")
          {
            break;
          }

          streamer.Write(data);
        }
      }
    }
  }
}
