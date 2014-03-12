using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using WebSocketSharp;

namespace Example1
{
  internal class AudioStreamer : IDisposable
  {
    private Dictionary<uint, Queue> _audioBox;
    private Timer                   _heartbeatTimer;
    private uint?                   _id;
    private string                  _name;
    private Notifier                _notifier;
    private WebSocket               _websocket;

    public AudioStreamer (string url)
    {
      _websocket = new WebSocket (url);

      _audioBox = new Dictionary<uint, Queue> ();
      _heartbeatTimer = new Timer (sendHeartbeat, null, -1, -1);
      _id = null;
      _notifier = new Notifier ();

      configure ();
    }

    private AudioMessage acceptBinaryMessage (byte [] value)
    {
      var id = value.SubArray (0, 4).To<uint> (ByteOrder.Big);
      var chNum = value.SubArray (4, 1) [0];
      var bufferLength = value.SubArray (5, 4).To<uint> (ByteOrder.Big);
      var bufferArray = new float [chNum, bufferLength];

      var offset = 9;
      ((int) chNum).Times (
        i => bufferLength.Times (
          j => {
            bufferArray [i, j] = value.SubArray (offset, 4).To<float> (ByteOrder.Big);
            offset += 4;
          }));

      return new AudioMessage {
        user_id = id,
        ch_num = chNum,
        buffer_length = bufferLength,
        buffer_array = bufferArray
      };
    }

    private NotificationMessage acceptTextMessage (string value)
    {
      var json = JObject.Parse (value);
      var id = (uint) json ["user_id"];
      var name = (string) json ["name"];
      var type = (string) json ["type"];

      string message;
      if (type == "connection") {
        var users = (JArray) json ["message"];
        var msg = new StringBuilder ("Now keeping connection:");
        foreach (JToken user in users)
          msg.AppendFormat (
            "\n- user_id: {0} name: {1}", (uint) user ["user_id"], (string) user ["name"]);

        message = msg.ToString ();
      }
      else if (type == "connected") {
        _heartbeatTimer.Change (30000, 30000);
        _id = id;
        message = String.Format ("user_id: {0} name: {1}", id, name);
      }
      else if (type == "message")
        message = String.Format ("{0}: {1}", name, (string) json ["message"]);
      else if (type == "start_music")
        message = String.Format ("{0}: Started playing music!", name);
      else
        message = "Received unknown type message.";

      return new NotificationMessage {
        Summary = String.Format ("AudioStreamer Message ({0})", type),
        Body = message,
        Icon = "notification-message-im"
      };
    }

    private void configure ()
    {
#if DEBUG
      _websocket.Log.Level = LogLevel.Trace;
#endif
      _websocket.OnOpen += (sender, e) =>
        _websocket.Send (createTextMessage ("connection", String.Empty));

      _websocket.OnMessage += (sender, e) => {
        if (e.Type == Opcode.Text)
          _notifier.Notify (acceptTextMessage (e.Data));
        else {
          var msg = acceptBinaryMessage (e.RawData);
          if (msg.user_id == _id)
            return;

          if (_audioBox.ContainsKey (msg.user_id)) {
            _audioBox [msg.user_id].Enqueue (msg.buffer_array);
            return;
          }

          var queue = Queue.Synchronized (new Queue ());
          queue.Enqueue (msg.buffer_array);
          _audioBox.Add (msg.user_id, queue);
        }
      };

      _websocket.OnError += (sender, e) =>
        _notifier.Notify (
          new NotificationMessage {
            Summary = "AudioStreamer Error",
            Body = e.Message,
            Icon = "notification-message-im"
          });

      _websocket.OnClose += (sender, e) =>
        _notifier.Notify (
          new NotificationMessage {
            Summary = String.Format ("AudioStreamer Disconnect ({0})", e.Code),
            Body = e.Reason,
            Icon = "notification-message-im"
          });
    }

    private byte [] createAudioMessage (float [,] bufferArray)
    {
      var msg = new List<byte> ();

      var id = (uint) _id;
      var chNum = bufferArray.GetLength (0);
      var bufferLength = bufferArray.GetLength (1);

      msg.AddRange (id.ToByteArray (ByteOrder.Big));
      msg.Add ((byte) chNum);
      msg.AddRange (((uint) bufferLength).ToByteArray (ByteOrder.Big));

      chNum.Times (
        i => bufferLength.Times (
          j => msg.AddRange (bufferArray [i, j].ToByteArray (ByteOrder.Big))));

      return msg.ToArray ();
    }

    private string createTextMessage (string type, string message)
    {
      return JsonConvert.SerializeObject (
        new TextMessage {
          user_id = _id,
          name = _name,
          type = type,
          message = message
        });
    }

    private void sendHeartbeat (object state)
    {
      _websocket.Send (createTextMessage ("heartbeat", String.Empty));
    }

    public void Connect ()
    {
      do {
        Console.Write ("Input your name> ");
        _name = Console.ReadLine ();
      }
      while (_name.Length == 0);

      _websocket.Connect ();
    }

    public void Disconnect ()
    {
      var wait = new ManualResetEvent (false);
      _heartbeatTimer.Dispose (wait);
      wait.WaitOne ();

      _websocket.Close ();
      _notifier.Close ();
    }

    public void Write (string message)
    {
      _websocket.Send (createTextMessage ("message", message));
    }

    void IDisposable.Dispose ()
    {
      Disconnect ();
    }
  }
}
