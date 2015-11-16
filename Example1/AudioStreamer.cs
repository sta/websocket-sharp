using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
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

    private void configure ()
    {
#if DEBUG
      _websocket.Log.Level = LogLevel.Trace;
#endif
      _websocket.OnOpen += (sender, e) =>
        _websocket.Send (createTextMessage ("connection", String.Empty));

      _websocket.OnMessage += (sender, e) => {
        if (e.IsText) {
          _notifier.Notify (convertTextMessage (e.Data));
        }
        else {
          var msg = convertBinaryMessage (e.RawData);
          if (msg.user_id == _id)
            return;

          if (_audioBox.ContainsKey (msg.user_id)) {
            _audioBox[msg.user_id].Enqueue (msg.buffer_array);
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
            Summary = "AudioStreamer (error)",
            Body = e.Message,
            Icon = "notification-message-im"
          });

      _websocket.OnClose += (sender, e) =>
        _notifier.Notify (
          new NotificationMessage {
            Summary = "AudioStreamer (disconnect)",
            Body = String.Format ("code: {0} reason: {1}", e.Code, e.Reason),
            Icon = "notification-message-im"
          });
    }

    private AudioMessage convertBinaryMessage (byte[] data)
    {
      var id = data.SubArray (0, 4).To<uint> (ByteOrder.Big);
      var chNum = data.SubArray (4, 1)[0];
      var buffLen = data.SubArray (5, 4).To<uint> (ByteOrder.Big);
      var buffArr = new float[chNum, buffLen];

      var offset = 9;
      ((int) chNum).Times (
        i => buffLen.Times (
          j => {
            buffArr[i, j] = data.SubArray (offset, 4).To<float> (ByteOrder.Big);
            offset += 4;
          }));

      return new AudioMessage {
        user_id = id,
        ch_num = chNum,
        buffer_length = buffLen,
        buffer_array = buffArr
      };
    }

    private NotificationMessage convertTextMessage (string data)
    {
      var json = JObject.Parse (data);
      var id = (uint) json["user_id"];
      var name = (string) json["name"];
      var type = (string) json["type"];

      string body;
      if (type == "message") {
        body = String.Format ("{0}: {1}", name, (string) json["message"]);
      }
      else if (type == "start_music") {
        body = String.Format ("{0}: Started playing music!", name);
      }
      else if (type == "connection") {
        var users = (JArray) json["message"];
        var buff = new StringBuilder ("Now keeping connections:");
        foreach (JToken user in users)
          buff.AppendFormat (
            "\n- user_id: {0} name: {1}", (uint) user["user_id"], (string) user["name"]);

        body = buff.ToString ();
      }
      else if (type == "connected") {
        _id = id;
        _heartbeatTimer.Change (30000, 30000);
        body = String.Format ("user_id: {0} name: {1}", id, name);
      }
      else {
        body = "Received unknown type message.";
      }

      return new NotificationMessage {
        Summary = String.Format ("AudioStreamer ({0})", type),
        Body = body,
        Icon = "notification-message-im"
      };
    }

    private byte[] createBinaryMessage (float[,] bufferArray)
    {
      var msg = new List<byte> ();

      var id = (uint) _id;
      var chNum = bufferArray.GetLength (0);
      var buffLen = bufferArray.GetLength (1);

      msg.AddRange (id.ToByteArray (ByteOrder.Big));
      msg.Add ((byte) chNum);
      msg.AddRange (((uint) buffLen).ToByteArray (ByteOrder.Big));

      chNum.Times (
        i => buffLen.Times (
          j => msg.AddRange (bufferArray[i, j].ToByteArray (ByteOrder.Big))));

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

    public void Connect (string username)
    {
      _name = username;
      _websocket.Connect ();
    }

    public void Disconnect ()
    {
      _heartbeatTimer.Change (-1, -1);
      _websocket.Close (CloseStatusCode.Away);
      _audioBox.Clear ();
      _id = null;
      _name = null;
    }

    public void Write (string message)
    {
      _websocket.Send (createTextMessage ("message", message));
    }

    void IDisposable.Dispose ()
    {
      Disconnect ();

      _heartbeatTimer.Dispose ();
      _notifier.Close ();
    }
  }
}
