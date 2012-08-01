using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
#if NOTIFY
using Notifications;
#endif
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using WebSocketSharp;
using WebSocketSharp.Frame;

namespace Example
{
  public struct NfMessage
  {
    public string Summary;
    public string Body;
    public string Icon;
  }

  public class AudioMessage
  {
    public uint     user_id;
    public byte     ch_num;
    public uint     buffer_length;
    public float[,] buffer_array;
  }

  public class TextMessage
  {
    public uint?  user_id;
    public string name;
    public string type;
    public string message;
  }

  public class ThreadState
  {
    public bool           Enabled      { get; set; }
    public AutoResetEvent Notification { get; private set; }

    public ThreadState()
    {
      Enabled      = true;
      Notification = new AutoResetEvent(false);
    }
  }

  public class AudioStreamer : IDisposable
  {
    private Dictionary<uint, Queue> _audioBox;
    private Queue                   _msgQ;
    private string                  _name;
    private WaitCallback            _notifyMsg;
    private ThreadState             _notifyMsgState;
    private TimerCallback           _sendHeartbeat;
    private Timer                   _heartbeatTimer;
    private uint?                   _user_id;
    private WebSocket               _ws;

    public AudioStreamer(string url)
    {
      _ws       = new WebSocket(url);
      _msgQ     = Queue.Synchronized(new Queue());
      _audioBox = new Dictionary<uint, Queue>();
      _user_id  = null;

      configure();
    }

    private void configure()
    {
      _ws.OnOpen += (sender, e) =>
      {
        var msg = createTextMessage("connection", String.Empty);
        _ws.Send(msg);
      };

      _ws.OnMessage += (sender, e) =>
      {
        switch (e.Type)
        {
          case Opcode.TEXT:
            var msg = parseTextMessage(e.Data);
            _msgQ.Enqueue(msg);
            break;
          case Opcode.BINARY:
            var audioMsg = parseAudioMessage(e.RawData);
            if (audioMsg.user_id == _user_id) goto default;
            if (_audioBox.ContainsKey(audioMsg.user_id))
            {
              _audioBox[audioMsg.user_id].Enqueue(audioMsg.buffer_array);
            }
            else
            {
              var q = Queue.Synchronized(new Queue());
              q.Enqueue(audioMsg.buffer_array);
              _audioBox.Add(audioMsg.user_id, q);
            }
            break;
          default:
            break;
        }
      };

      _ws.OnError += (sender, e) =>
      {
        enNfMessage("[AudioStreamer] error", "WS: Error: " + e.Data, "notification-message-im");
      };

      _ws.OnClose += (sender, e) =>
      {
        enNfMessage
        (
          "[AudioStreamer] disconnect",
          String.Format("WS: Close({0}:{1}): {2}", (ushort)e.Code, e.Code, e.Reason),
          "notification-message-im"
        );
      };

      _notifyMsgState = new ThreadState();
      _notifyMsg = (state) =>
      {
        while (_notifyMsgState.Enabled)
        {
          Thread.Sleep(500);

          if (_msgQ.Count > 0)
          {
            NfMessage msg = (NfMessage)_msgQ.Dequeue();
            #if NOTIFY
            Notification nf = new Notification(msg.Summary,
                                               msg.Body,
                                               msg.Icon);
            nf.AddHint("append", "allowed");
            nf.Show();
            #else
            Console.WriteLine("{0}: {1}", msg.Summary, msg.Body);
            #endif
          }
        }

        _notifyMsgState.Notification.Set();
      };

      _sendHeartbeat = (state) =>
      {
        var msg = createTextMessage("heartbeat", String.Empty);
        _ws.Send(msg);
      };
    }

    private byte[] createAudioMessage(float[,] buffer_array)
    {
      List<byte> msg = new List<byte>();

      uint user_id       = (uint)_user_id;
      int  ch_num        = buffer_array.GetLength(0);
      int  buffer_length = buffer_array.GetLength(1);

      msg.AddRange(user_id.ToBytes(ByteOrder.BIG));
      msg.Add((byte)ch_num);
      msg.AddRange(((uint)buffer_length).ToBytes(ByteOrder.BIG));

      ch_num.Times(i =>
      {
        buffer_length.Times(j =>
        {
          msg.AddRange(buffer_array[i, j].ToBytes(ByteOrder.BIG));
        });
      });

      return msg.ToArray();
    }

    private string createTextMessage(string type, string message)
    {
      var msg = new TextMessage
      {
        user_id = _user_id,
        name    = _name,
        type    = type,
        message = message
      };

      return JsonConvert.SerializeObject(msg);
    }

    private AudioMessage parseAudioMessage(byte[] data)
    {
      uint     user_id       = data.SubArray(0, 4).To<uint>(ByteOrder.BIG);
      byte     ch_num        = data.SubArray(4, 1)[0];
      uint     buffer_length = data.SubArray(5, 4).To<uint>(ByteOrder.BIG);
      float[,] buffer_array  = new float[ch_num, buffer_length];

      int offset = 9;
      ch_num.Times(i =>
      {
        buffer_length.Times(j =>
        {
          buffer_array[i, j] = data.SubArray(offset, 4).To<float>(ByteOrder.BIG);
          offset += 4;
        });
      });

      return new AudioMessage
             {
               user_id       = user_id,
               ch_num        = ch_num,
               buffer_length = buffer_length,
               buffer_array  = buffer_array
             };
    }

    private NfMessage parseTextMessage(string data)
    {
      JObject msg     = JObject.Parse(data);
      uint    user_id = (uint)msg["user_id"];
      string  name    = (string)msg["name"];
      string  type    = (string)msg["type"];

      string message;
      switch (type)
      {
        case "connection":
          JArray users = (JArray)msg["message"];
          StringBuilder sb = new StringBuilder("Now keeping connection\n");
          foreach (JToken user in users)
          {
            sb.AppendFormat("user_id: {0} name: {1}\n", (uint)user["user_id"], (string)user["name"]);
          }
          message = sb.ToString().TrimEnd('\n');
          break;
        case "connected":
          _user_id = user_id;
          message = String.Format("user_id: {0} name: {1}", user_id, name);
          break;
        case "message":
          message = String.Format("{0}: {1}", name, (string)msg["message"]);
          break;
        case "start_music":
          message = String.Format("{0}: Started playing music!", name);
          break;
        default:
          message = "Received unknown type message: " + type;
          break;
      }

      return new NfMessage
             {
               Summary = String.Format("[AudioStreamer] {0}", type),
               Body    = message,
               Icon    = "notification-message-im"
             };
    }

    private void enNfMessage(string summary, string body, string icon)
    {
      var msg = new NfMessage
      {
        Summary = summary,
        Body    = body,
        Icon    = icon
      };

      _msgQ.Enqueue(msg);
    }

    public void Connect()
    {
      string name;
      do
      {
        Console.Write("Your name > ");
        name = Console.ReadLine();
      }
      while (name == String.Empty);

      _name = name;
      
      _ws.Connect();

      ThreadPool.QueueUserWorkItem(_notifyMsg);
      _heartbeatTimer = new Timer(_sendHeartbeat, null, 30 * 1000, 30 * 1000);
    }

    public void Disconnect()
    {
      var wait = new AutoResetEvent(false);
      _heartbeatTimer.Dispose(wait);
      wait.WaitOne();

      _ws.Close();

      _notifyMsgState.Enabled = false;
      _notifyMsgState.Notification.WaitOne();
    }

    public void Dispose()
    {
      Disconnect();
    }

    public void Write(string data)
    {
      var msg = createTextMessage("message", data);
      _ws.Send(msg);
    }

    public void Write(FileInfo file)
    {
      throw new NotImplementedException();
    }
  }
}
