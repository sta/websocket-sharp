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
    private uint?                   _id;
    private string                  _name;
    private Notifier                _notifier;
    private Timer                   _timer;
    private WebSocket               _websocket;

    public AudioStreamer (string url)
    {
      _websocket = new WebSocket (url);

      _audioBox = new Dictionary<uint, Queue> ();
      _id = null;
      _notifier = new Notifier ();
      _timer = new Timer (sendHeartbeat, null, -1, -1);

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
            _notifier.Notify (processTextMessage (e.Data));
            return;
          }

          if (e.IsBinary) {
            processBinaryMessage (e.RawData);
            return;
          }
        };

      _websocket.OnError += (sender, e) =>
          _notifier.Notify (
            new NotificationMessage {
              Summary = "AudioStreamer (error)",
              Body = e.Message,
              Icon = "notification-message-im"
            }
          );

      _websocket.OnClose += (sender, e) =>
          _notifier.Notify (
            new NotificationMessage {
              Summary = "AudioStreamer (disconnect)",
              Body = String.Format ("code: {0} reason: {1}", e.Code, e.Reason),
              Icon = "notification-message-im"
            }
          );
    }

    private byte[] createBinaryMessage (float[,] bufferArray)
    {
      return new BinaryMessage {
               UserID = (uint) _id,
               ChannelNumber = (byte) bufferArray.GetLength (0),
               BufferLength = (uint) bufferArray.GetLength (1),
               BufferArray = bufferArray
             }
             .ToArray ();
    }

    private string createTextMessage (string type, string message)
    {
      return new TextMessage {
               UserID = _id,
               Name = _name,
               Type = type,
               Message = message
             }
             .ToString ();
    }

    private void processBinaryMessage (byte[] data)
    {
      var msg = BinaryMessage.Parse (data);

      var id = msg.UserID;
      if (id == _id)
        return;

      Queue queue;
      if (_audioBox.TryGetValue (id, out queue)) {
        queue.Enqueue (msg.BufferArray);
        return;
      }

      queue = Queue.Synchronized (new Queue ());
      queue.Enqueue (msg.BufferArray);
      _audioBox.Add (id, queue);
    }

    private NotificationMessage processTextMessage (string data)
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
        foreach (JToken user in users) {
          buff.AppendFormat (
            "\n- user_id: {0} name: {1}", (uint) user["user_id"], (string) user["name"]
          );
        }

        body = buff.ToString ();
      }
      else if (type == "connected") {
        _id = id;
        _timer.Change (30000, 30000);

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

    private void sendHeartbeat (object state)
    {
      _websocket.Send (createTextMessage ("heartbeat", String.Empty));
    }

    public void Close ()
    {
      Disconnect ();
      _timer.Dispose ();
      _notifier.Close ();
    }

    public void Connect (string username)
    {
      _name = username;
      _websocket.Connect ();
    }

    public void Disconnect ()
    {
      _timer.Change (-1, -1);
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
      Close ();
    }
  }
}
