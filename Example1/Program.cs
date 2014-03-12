using System;
using System.Threading;

namespace Example1
{
  public class Program
  {
    public static void Main (string [] args)
    {
      using (var streamer = new AudioStreamer ("ws://agektmr.node-ninja.com:3000/socket"))
      //using (var streamer = new AudioStreamer ("ws://localhost:3000/socket"))
      {
        streamer.Connect ();

        Console.WriteLine ("\nType \"exit\" to exit.\n");
        while (true) {
          Thread.Sleep (500);
          Console.Write ("> ");
          var msg = Console.ReadLine ();
          if (msg == "exit")
            break;

          streamer.Write (msg);
        }
      }
    }
  }
}
