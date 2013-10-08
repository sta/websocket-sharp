#region License
/*
 * WebSocketSessionManager.cs
 *
 * The MIT License
 *
 * Copyright (c) 2012-2013 sta.blockhead
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 */
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Timers;

namespace WebSocketSharp.Server
{
  /// <summary>
  /// Manages the sessions to the Websocket service.
  /// </summary>
  public class WebSocketSessionManager
  {
    #region Private Fields

    private object                               _forSweep;
    private volatile bool                        _keepClean;
    private Logger                               _logger;
    private Dictionary<string, WebSocketService> _sessions;
    private volatile ServerState                 _state;
    private volatile bool                        _sweeping;
    private System.Timers.Timer                  _sweepTimer;
    private object                               _sync;

    #endregion

    #region Internal Constructors

    internal WebSocketSessionManager ()
      : this (new Logger ())
    {
    }

    internal WebSocketSessionManager (Logger logger)
    {
      _logger = logger;
      _forSweep = new object ();
      _keepClean = true;
      _sessions = new Dictionary<string, WebSocketService> ();
      _state = ServerState.READY;
      _sweeping = false;
      _sync = new object ();

      setSweepTimer (60 * 1000);
    }

    #endregion

    #region Internal Properties

    internal ServerState State {
      get {
        return _state;
      }
    }

    internal IEnumerable<WebSocketService> ServiceInstances {
      get {
        if (_state != ServerState.START)
          return new List<WebSocketService> ();

        lock (_sync)
        {
          return _sessions.Values.ToList ();
        }
      }
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the collection of every ID of the active sessions to the Websocket service.
    /// </summary>
    /// <value>
    /// An IEnumerable&lt;string&gt; that contains the collection of every ID of the active sessions.
    /// </value>
    public IEnumerable<string> ActiveIDs {
      get {
        return from result in BroadpingInternally ()
               where result.Value
               select result.Key;
      }
    }

    /// <summary>
    /// Gets the number of the sessions to the Websocket service.
    /// </summary>
    /// <value>
    /// An <see cref="int"/> that contains the number of the sessions.
    /// </value>
    public int Count {
      get {
        lock (_sync)
        {
          return _sessions.Count;
        }
      }
    }

    /// <summary>
    /// Gets the collection of every ID of the sessions to the Websocket service.
    /// </summary>
    /// <value>
    /// An IEnumerable&lt;string&gt; that contains the collection of every ID of the sessions.
    /// </value>
    public IEnumerable<string> IDs {
      get {
        lock (_sync)
        {
          return _sessions.Keys.ToList ();
        }
      }
    }

    /// <summary>
    /// Gets the collection of every ID of the inactive sessions to the Websocket service.
    /// </summary>
    /// <value>
    /// An IEnumerable&lt;string&gt; that contains the collection of every ID of the inactive sessions.
    /// </value>
    public IEnumerable<string> InactiveIDs {
      get {
        return from result in BroadpingInternally ()
               where !result.Value
               select result.Key;
      }
    }

    /// <summary>
    /// Gets the session information with the specified <paramref name="id"/>.
    /// </summary>
    /// <value>
    /// A <see cref="IWebSocketSession"/> instance with <paramref name="id"/> if it is successfully found;
    /// otherwise, <see langword="null"/>.
    /// </value>
    /// <param name="id">
    /// A <see cref="string"/> that contains the ID of the session information to get.
    /// </param>
    public IWebSocketSession this [string id] {
      get {
        var msg = _state.CheckIfStarted () ?? id.CheckIfValidSessionID ();
        if (msg != null)
        {
          _logger.Error (msg);
          return null;
        }

        WebSocketService session;
        if (!TryGetServiceInstance (id, out session))
          _logger.Error ("The WebSocket session with the specified ID not found.\nID: " + id);

        return session;
      }
    }

    /// <summary>
    /// Gets a value indicating whether the manager cleans up the inactive sessions periodically.
    /// </summary>
    /// <value>
    /// <c>true</c> if the manager cleans up the inactive sessions every 60 seconds;
    /// otherwise, <c>false</c>.
    /// </value>
    public bool KeepClean {
      get {
        return _keepClean;
      }

      internal set {
        if (!(value ^ _keepClean))
          return;

        _keepClean = value;
        if (_state == ServerState.START)
          _sweepTimer.Enabled = value;
      }
    }

    /// <summary>
    /// Gets the collection of the session informations to the Websocket service.
    /// </summary>
    /// <value>
    /// An IEnumerable&lt;IWebSocketSession&gt; that contains the collection of the session informations.
    /// </value>
    public IEnumerable<IWebSocketSession> Sessions {
      get {
        return from IWebSocketSession session in ServiceInstances
               select session;
      }
    }

    #endregion

    #region Private Methods

    private static string createID ()
    {
      return Guid.NewGuid ().ToString ("N");
    }

    private void setSweepTimer (double interval)
    {
      _sweepTimer = new System.Timers.Timer (interval);
      _sweepTimer.Elapsed += (sender, e) =>
      {
        Sweep ();
      };
    }

    #endregion

    #region Internal Methods

    internal string Add (WebSocketService session)
    {
      lock (_sync)
      {
        if (_state != ServerState.START)
          return null;

        var id = createID ();
        _sessions.Add (id, session);

        return id;
      }
    }

    internal void BroadcastInternally (Opcode opcode, byte [] data)
    {
      WaitCallback callback = state =>
      {
        var cache = new Dictionary<CompressionMethod, byte []> ();
        try {
          BroadcastInternally (opcode, data, cache);
        }
        catch (Exception ex) {
          _logger.Fatal (ex.ToString ());
        }
        finally {
          cache.Clear ();
        }
      };

      ThreadPool.QueueUserWorkItem (callback);
    }

    internal void BroadcastInternally (Opcode opcode, Stream stream)
    {
      WaitCallback callback = state =>
      {
        var cache = new Dictionary <CompressionMethod, Stream> ();
        try {
          BroadcastInternally (opcode, stream, cache);
        }
        catch (Exception ex) {
          _logger.Fatal (ex.ToString ());
        }
        finally {
          foreach (var cached in cache.Values)
            cached.Dispose ();

          cache.Clear ();
        }
      };

      ThreadPool.QueueUserWorkItem (callback);
    }

    internal void BroadcastInternally (
      Opcode opcode, byte [] data, Dictionary<CompressionMethod, byte []> cache)
    {
      foreach (var session in ServiceInstances)
      {
        if (_state != ServerState.START)
          break;

        session.Context.WebSocket.Send (opcode, data, cache);
      }
    }

    internal void BroadcastInternally (
      Opcode opcode, Stream stream, Dictionary <CompressionMethod, Stream> cache)
    {
      foreach (var session in ServiceInstances)
      {
        if (_state != ServerState.START)
          break;

        session.Context.WebSocket.Send (opcode, stream, cache);
      }
    }

    internal Dictionary<string, bool> BroadpingInternally ()
    {
      return BroadpingInternally (WsFrame.CreatePingFrame (Mask.UNMASK).ToByteArray (), 1000);
    }

    internal Dictionary<string, bool> BroadpingInternally (byte [] frameAsBytes, int timeOut)
    {
      var result = new Dictionary<string, bool> ();
      foreach (var session in ServiceInstances)
      {
        if (_state != ServerState.START)
          break;

        result.Add (session.ID, session.Context.WebSocket.Ping (frameAsBytes, timeOut));
      }

      return result;
    }

    internal bool Remove (string id)
    {
      lock (_sync)
      {
        return _sessions.Remove (id);
      }
    }

    internal void Start ()
    {
      _sweepTimer.Enabled = _keepClean;
      _state = ServerState.START;
    }

    internal void Stop (byte [] data, bool send)
    {
      var payload = new PayloadData (data);
      var args = new CloseEventArgs (payload);
      var frameAsBytes = send
                       ? WsFrame.CreateCloseFrame (Mask.UNMASK, payload).ToByteArray ()
                       : null;

      Stop (args, frameAsBytes);
    }

    internal void Stop (CloseEventArgs args, byte [] frameAsBytes)
    {
      lock (_sync)
      {
        _state = ServerState.SHUTDOWN;

        _sweepTimer.Enabled = false;
        foreach (var session in _sessions.Values.ToList ())
          session.Context.WebSocket.Close (args, frameAsBytes, 1000);

        _state = ServerState.STOP;
      }
    }

    internal bool TryGetServiceInstance (string id, out WebSocketService service)
    {
      lock (_sync)
      {
        return _sessions.TryGetValue (id, out service);
      }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Broadcasts the specified array of <see cref="byte"/> to all clients of the WebSocket service.
    /// </summary>
    /// <param name="data">
    /// An array of <see cref="byte"/> to broadcast.
    /// </param>
    public void Broadcast (byte [] data)
    {
      var msg = _state.CheckIfStarted () ?? data.CheckIfValidSendData ();
      if (msg != null)
      {
        _logger.Error (msg);
        return;
      }

      if (data.LongLength <= WebSocket.FragmentLength)
        BroadcastInternally (Opcode.BINARY, data);
      else
        BroadcastInternally (Opcode.BINARY, new MemoryStream (data));
    }

    /// <summary>
    /// Broadcasts the specified <see cref="string"/> to all clients of the WebSocket service.
    /// </summary>
    /// <param name="data">
    /// A <see cref="string"/> to broadcast.
    /// </param>
    public void Broadcast (string data)
    {
      var msg = _state.CheckIfStarted () ?? data.CheckIfValidSendData ();
      if (msg != null)
      {
        _logger.Error (msg);
        return;
      }

      var rawData = Encoding.UTF8.GetBytes (data);
      if (rawData.LongLength <= WebSocket.FragmentLength)
        BroadcastInternally (Opcode.TEXT, rawData);
      else
        BroadcastInternally (Opcode.TEXT, new MemoryStream (rawData));
    }

    /// <summary>
    /// Sends Pings to all clients of the WebSocket service.
    /// </summary>
    /// <returns>
    /// A Dictionary&lt;string, bool&gt; that contains the collection of pairs of session ID and value
    /// indicating whether the WebSocket service received a Pong from each client in a time.
    /// </returns>
    public Dictionary<string, bool> Broadping ()
    {
      var msg = _state.CheckIfStarted ();
      if (msg != null)
      {
        _logger.Error (msg);
        return null;
      }

      return BroadpingInternally ();
    }

    /// <summary>
    /// Sends Pings with the specified <paramref name="message"/> to all clients of the WebSocket service.
    /// </summary>
    /// <returns>
    /// A Dictionary&lt;string, bool&gt; that contains the collection of pairs of session ID and value
    /// indicating whether the WebSocket service received a Pong from each client in a time.
    /// </returns>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to send.
    /// </param>
    public Dictionary<string, bool> Broadping (string message)
    {
      if (message == null || message.Length == 0)
        return Broadping ();

      var data = Encoding.UTF8.GetBytes (message);
      var msg = _state.CheckIfStarted () ?? data.CheckIfValidPingData ();
      if (msg != null)
      {
        _logger.Error (msg);
        return null;
      }

      return BroadpingInternally (WsFrame.CreatePingFrame (Mask.UNMASK, data).ToByteArray (), 1000);
    }

    /// <summary>
    /// Closes the session with the specified <paramref name="id"/>.
    /// </summary>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID to find.
    /// </param>
    public void CloseSession (string id)
    {
      var msg = _state.CheckIfStarted () ?? id.CheckIfValidSessionID ();
      if (msg != null)
      {
        _logger.Error (msg);
        return;
      }

      WebSocketService session;
      if (!TryGetServiceInstance (id, out session))
      {
        _logger.Error ("The WebSocket session with the specified ID not found.\nID: " + id);
        return;
      }

      session.Context.WebSocket.Close ();
    }

    /// <summary>
    /// Closes the session with the specified <paramref name="code"/>, <paramref name="reason"/>
    /// and <paramref name="id"/>.
    /// </summary>
    /// <param name="code">
    /// A <see cref="ushort"/> that contains a status code indicating the reason for closure.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that contains the reason for closure.
    /// </param>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID to find.
    /// </param>
    public void CloseSession (ushort code, string reason, string id)
    {
      var msg = _state.CheckIfStarted () ?? id.CheckIfValidSessionID ();
      if (msg != null)
      {
        _logger.Error (msg);
        return;
      }

      WebSocketService session;
      if (!TryGetServiceInstance (id, out session))
      {
        _logger.Error ("The WebSocket session with the specified ID not found.\nID: " + id);
        return;
      }

      session.Context.WebSocket.Close (code, reason);
    }

    /// <summary>
    /// Closes the session with the specified <paramref name="code"/>, <paramref name="reason"/>
    /// and <paramref name="id"/>.
    /// </summary>
    /// <param name="code">
    /// A <see cref="CloseStatusCode"/> that contains a status code indicating the reason for closure.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that contains the reason for closure.
    /// </param>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID to find.
    /// </param>
    public void CloseSession (CloseStatusCode code, string reason, string id)
    {
      var msg = _state.CheckIfStarted () ?? id.CheckIfValidSessionID ();
      if (msg != null)
      {
        _logger.Error (msg);
        return;
      }

      WebSocketService session;
      if (!TryGetServiceInstance (id, out session))
      {
        _logger.Error ("The WebSocket session with the specified ID not found.\nID: " + id);
        return;
      }

      session.Context.WebSocket.Close (code, reason);
    }

    /// <summary>
    /// Sends a Ping to the client associated with the specified <paramref name="id"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the WebSocket service receives a Pong from the client in a time;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID that represents the destination for the Ping.
    /// </param>
    public bool PingTo (string id)
    {
      var msg = _state.CheckIfStarted () ?? id.CheckIfValidSessionID ();
      if (msg != null)
      {
        _logger.Error (msg);
        return false;
      }

      WebSocketService session;
      if (!TryGetServiceInstance (id, out session))
      {
        _logger.Error ("The WebSocket session with the specified ID not found.\nID: " + id);
        return false;
      }

      return session.Context.WebSocket.Ping ();
    }

    /// <summary>
    /// Sends a Ping with the specified <paramref name="message"/> to the client associated with
    /// the specified <paramref name="id"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the WebSocket service receives a Pong from the client in a time;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to send.
    /// </param>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID that represents the destination for the Ping.
    /// </param>
    public bool PingTo (string message, string id)
    {
      var msg = _state.CheckIfStarted () ?? id.CheckIfValidSessionID ();
      if (msg != null)
      {
        _logger.Error (msg);
        return false;
      }

      WebSocketService session;
      if (!TryGetServiceInstance (id, out session))
      {
        _logger.Error ("The WebSocket session with the specified ID not found.\nID: " + id);
        return false;
      }

      return session.Context.WebSocket.Ping (message);
    }

    /// <summary>
    /// Sends a binary <paramref name="data"/> to the client associated with the specified
    /// <paramref name="id"/>.
    /// </summary>
    /// <param name="data">
    /// An array of <see cref="byte"/> that contains a binary data to send.
    /// </param>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID that represents the destination for the data.
    /// </param>
    public void SendTo (byte [] data, string id)
    {
      var msg = _state.CheckIfStarted () ?? id.CheckIfValidSessionID ();
      if (msg != null)
      {
        _logger.Error (msg);
        return;
      }

      WebSocketService session;
      if (!TryGetServiceInstance (id, out session))
      {
        _logger.Error ("The WebSocket session with the specified ID not found.\nID: " + id);
        return;
      }

      session.Context.WebSocket.Send (data, null);
    }

    /// <summary>
    /// Sends a text <paramref name="data"/> to the client associated with the specified
    /// <paramref name="id"/>.
    /// </summary>
    /// <param name="data">
    /// A <see cref="string"/> that contains a text data to send.
    /// </param>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID that represents the destination for the data.
    /// </param>
    public void SendTo (string data, string id)
    {
      var msg = _state.CheckIfStarted () ?? id.CheckIfValidSessionID ();
      if (msg != null)
      {
        _logger.Error (msg);
        return;
      }

      WebSocketService session;
      if (!TryGetServiceInstance (id, out session))
      {
        _logger.Error ("The WebSocket session with the specified ID not found.\nID: " + id);
        return;
      }

      session.Context.WebSocket.Send (data, null);
    }

    /// <summary>
    /// Cleans up the inactive sessions.
    /// </summary>
    public void Sweep ()
    {
      if (_state != ServerState.START || _sweeping || Count == 0)
        return;

      lock (_forSweep)
      {
        _sweeping = true;
        foreach (var id in InactiveIDs)
        {
          if (_state != ServerState.START)
            break;

          lock (_sync)
          {
            WebSocketService session;
            if (_sessions.TryGetValue (id, out session))
            {
              var state = session.State;
              if (state == WebSocketState.OPEN)
                session.Context.WebSocket.Close (CloseStatusCode.ABNORMAL);
              else if (state == WebSocketState.CLOSING)
                continue;
              else
                _sessions.Remove (id);
            }
          }
        }

        _sweeping = false;
      }
    }

    /// <summary>
    /// Tries to get the session information with the specified <paramref name="id"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the session information is successfully found;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <param name="id">
    /// A <see cref="string"/> that contains the ID of the session information to get.
    /// </param>
    /// <param name="session">
    /// When this method returns, a <see cref="IWebSocketSession"/> instance that contains the session
    /// information if it is successfully found; otherwise, <see langword="null"/>.
    /// This parameter is passed uninitialized.
    /// </param>
    public bool TryGetSession (string id, out IWebSocketSession session)
    {
      var msg = _state.CheckIfStarted () ?? id.CheckIfValidSessionID ();
      if (msg != null)
      {
        _logger.Error (msg);
        session = null;

        return false;
      }

      WebSocketService service;
      var result = TryGetServiceInstance (id, out service);
      if (!result)
        _logger.Error ("The WebSocket session with the specified ID not found.\nID: " + id);

      session = service;
      return result;
    }

    #endregion
  }
}
