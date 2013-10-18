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
  /// Manages the sessions to a Websocket service.
  /// </summary>
  public class WebSocketSessionManager
  {
    #region Private Static Fields

    private static readonly List<IWebSocketSession> _emptySessions;

    #endregion

    #region Private Fields

    private object                                _forSweep;
    private volatile bool                         _keepClean;
    private Logger                                _logger;
    private Dictionary<string, IWebSocketSession> _sessions;
    private volatile ServerState                  _state;
    private volatile bool                         _sweeping;
    private System.Timers.Timer                   _sweepTimer;
    private object                                _sync;

    #endregion

    #region Static Constructor

    static WebSocketSessionManager ()
    {
      _emptySessions = new List<IWebSocketSession> ();
    }

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
      _sessions = new Dictionary<string, IWebSocketSession> ();
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
        return from result in Broadping (WsFrame.EmptyUnmaskPingData, 1000)
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
        return from result in Broadping (WsFrame.EmptyUnmaskPingData, 1000)
               where !result.Value
               select result.Key;
      }
    }

    /// <summary>
    /// Gets a WebSocket session information with the specified <paramref name="id"/>.
    /// </summary>
    /// <value>
    /// A <see cref="IWebSocketSession"/> instance that contains the session information
    /// if the session is successfully found; otherwise, <see langword="null"/>.
    /// </value>
    /// <param name="id">
    /// A <see cref="string"/> that contains an ID of the session to find.
    /// </param>
    public IWebSocketSession this [string id] {
      get {
        IWebSocketSession session;
        TryGetSession (id, out session);

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
        if (_state == ServerState.SHUTDOWN)
          return _emptySessions;

        lock (_sync)
        {
          return _sessions.Values.ToList ();
        }
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

    private bool tryGetSession (string id, out IWebSocketSession session)
    {
      bool result;
      lock (_sync)
      {
        result = _sessions.TryGetValue (id, out session);
      }

      if (!result)
        _logger.Error ("A WebSocket session with the specified ID not found.\nID: " + id);

      return result;
    }

    #endregion

    #region Internal Methods

    internal string Add (IWebSocketSession session)
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

    internal void Broadcast (Opcode opcode, byte [] data, Action completed)
    {
      var cache = new Dictionary<CompressionMethod, byte []> ();
      try {
        Broadcast (opcode, data, cache);
        if (completed != null)
          completed ();
      }
      catch (Exception ex) {
        _logger.Fatal (ex.ToString ());
      }
      finally {
        cache.Clear ();
      }
    }

    internal void Broadcast (Opcode opcode, Stream stream, Action completed)
    {
      var cache = new Dictionary <CompressionMethod, Stream> ();
      try {
        Broadcast (opcode, stream, cache);
        if (completed != null)
          completed ();
      }
      catch (Exception ex) {
        _logger.Fatal (ex.ToString ());
      }
      finally {
        foreach (var cached in cache.Values)
          cached.Dispose ();

        cache.Clear ();
      }
    }

    internal void Broadcast (
      Opcode opcode, byte [] data, Dictionary<CompressionMethod, byte []> cache)
    {
      foreach (var session in Sessions)
      {
        if (_state != ServerState.START)
          break;

        session.Context.WebSocket.Send (opcode, data, cache);
      }
    }

    internal void Broadcast (
      Opcode opcode, Stream stream, Dictionary <CompressionMethod, Stream> cache)
    {
      foreach (var session in Sessions)
      {
        if (_state != ServerState.START)
          break;

        session.Context.WebSocket.Send (opcode, stream, cache);
      }
    }

    internal void BroadcastAsync (Opcode opcode, byte [] data, Action completed)
    {
      WaitCallback callback = state =>
      {
        Broadcast (opcode, data, completed);
      };

      ThreadPool.QueueUserWorkItem (callback);
    }

    internal void BroadcastAsync (Opcode opcode, Stream stream, Action completed)
    {
      WaitCallback callback = state =>
      {
        Broadcast (opcode, stream, completed);
      };

      ThreadPool.QueueUserWorkItem (callback);
    }

    internal Dictionary<string, bool> Broadping (byte [] frameAsBytes, int timeOut)
    {
      var result = new Dictionary<string, bool> ();
      foreach (var session in Sessions)
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

    #endregion

    #region Public Methods

    /// <summary>
    /// Broadcasts a binary <paramref name="data"/> to all clients of a WebSocket service.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the broadcast to be complete.
    /// </remarks>
    /// <param name="data">
    /// An array of <see cref="byte"/> that contains a binary data to broadcast.
    /// </param>
    public void Broadcast (byte [] data)
    {
      Broadcast (data, null);
    }

    /// <summary>
    /// Broadcasts a text <paramref name="data"/> to all clients of a WebSocket service.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the broadcast to be complete.
    /// </remarks>
    /// <param name="data">
    /// A <see cref="string"/> that contains a text data to broadcast.
    /// </param>
    public void Broadcast (string data)
    {
      Broadcast (data, null);
    }

    /// <summary>
    /// Broadcasts a binary <paramref name="data"/> to all clients of a WebSocket service.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the broadcast to be complete.
    /// </remarks>
    /// <param name="data">
    /// An array of <see cref="byte"/> that contains a binary data to broadcast.
    /// </param>
    /// <param name="completed">
    /// A <see cref="Action"/> delegate that references the method(s) called when
    /// the broadcast is complete.
    /// </param>
    public void Broadcast (byte [] data, Action completed)
    {
      var msg = _state.CheckIfStarted () ?? data.CheckIfValidSendData ();
      if (msg != null)
      {
        _logger.Error (msg);
        return;
      }

      if (data.LongLength <= WebSocket.FragmentLength)
        BroadcastAsync (Opcode.BINARY, data, completed);
      else
        BroadcastAsync (Opcode.BINARY, new MemoryStream (data), completed);
    }

    /// <summary>
    /// Broadcasts a text <paramref name="data"/> to all clients of a WebSocket service.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the broadcast to be complete.
    /// </remarks>
    /// <param name="data">
    /// A <see cref="string"/> that contains a text data to broadcast.
    /// </param>
    /// <param name="completed">
    /// A <see cref="Action"/> delegate that references the method(s) called when
    /// the broadcast is complete.
    /// </param>
    public void Broadcast (string data, Action completed)
    {
      var msg = _state.CheckIfStarted () ?? data.CheckIfValidSendData ();
      if (msg != null)
      {
        _logger.Error (msg);
        return;
      }

      var rawData = Encoding.UTF8.GetBytes (data);
      if (rawData.LongLength <= WebSocket.FragmentLength)
        BroadcastAsync (Opcode.TEXT, rawData, completed);
      else
        BroadcastAsync (Opcode.TEXT, new MemoryStream (rawData), completed);
    }

    /// <summary>
    /// Broadcasts a binary data from the specified <see cref="Stream"/>
    /// to all clients of a WebSocket service.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the broadcast to be complete.
    /// </remarks>
    /// <param name="stream">
    /// A <see cref="Stream"/> object from which contains a binary data to broadcast.
    /// </param>
    /// <param name="length">
    /// An <see cref="int"/> that contains the number of bytes to broadcast.
    /// </param>
    public void Broadcast (Stream stream, int length)
    {
      Broadcast (stream, length, null);
    }

    /// <summary>
    /// Broadcasts a binary data from the specified <see cref="Stream"/>
    /// to all clients of a WebSocket service.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the broadcast to be complete.
    /// </remarks>
    /// <param name="stream">
    /// A <see cref="Stream"/> object from which contains a binary data to broadcast.
    /// </param>
    /// <param name="length">
    /// An <see cref="int"/> that contains the number of bytes to broadcast.
    /// </param>
    /// <param name="completed">
    /// A <see cref="Action"/> delegate that references the method(s) called when
    /// the broadcast is complete.
    /// </param>
    public void Broadcast (Stream stream, int length, Action completed)
    {
      var msg = _state.CheckIfStarted () ??
                stream.CheckIfCanRead () ??
                (length < 1 ? "'length' must be greater than 0." : null);

      if (msg != null)
      {
        _logger.Error (msg);
        return;
      }

      stream.ReadBytesAsync (
        length,
        data =>
        {
          var len = data.Length;
          if (len == 0)
          {
            _logger.Error ("A data cannot be read from 'stream'.");
            return;
          }

          if (len < length)
            _logger.Warn (String.Format (
              "A data with 'length' cannot be read from 'stream'.\nexpected: {0} actual: {1}",
              length,
              len));

          if (len <= WebSocket.FragmentLength)
            Broadcast (Opcode.BINARY, data, completed);
          else
            Broadcast (Opcode.BINARY, new MemoryStream (data), completed);
        },
        ex =>
        {
          _logger.Fatal (ex.ToString ());
        });
    }

    /// <summary>
    /// Sends Pings to all clients of a WebSocket service.
    /// </summary>
    /// <returns>
    /// A Dictionary&lt;string, bool&gt; that contains the collection of pairs of session ID and
    /// value indicating whether the WebSocket service received a Pong from each client in a time.
    /// </returns>
    public Dictionary<string, bool> Broadping ()
    {
      var msg = _state.CheckIfStarted ();
      if (msg != null)
      {
        _logger.Error (msg);
        return null;
      }

      return Broadping (WsFrame.EmptyUnmaskPingData, 1000);
    }

    /// <summary>
    /// Sends Pings with the specified <paramref name="message"/> to all clients
    /// of a WebSocket service.
    /// </summary>
    /// <returns>
    /// A Dictionary&lt;string, bool&gt; that contains the collection of pairs of session ID and
    /// value indicating whether the WebSocket service received a Pong from each client in a time.
    /// </returns>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to send.
    /// </param>
    public Dictionary<string, bool> Broadping (string message)
    {
      if (message == null || message.Length == 0)
        return Broadping ();

      byte [] data = null;
      var msg = _state.CheckIfStarted () ??
                (data = Encoding.UTF8.GetBytes (message)).CheckIfValidPingData ();

      if (msg != null)
      {
        _logger.Error (msg);
        return null;
      }

      return Broadping (WsFrame.CreatePingFrame (Mask.UNMASK, data).ToByteArray (), 1000);
    }

    /// <summary>
    /// Closes the session with the specified <paramref name="id"/>.
    /// </summary>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID to find.
    /// </param>
    public void CloseSession (string id)
    {
      IWebSocketSession session;
      if (TryGetSession (id, out session))
        session.Context.WebSocket.Close ();
    }

    /// <summary>
    /// Closes the session with the specified <paramref name="id"/>, <paramref name="code"/>
    /// and <paramref name="reason"/>.
    /// </summary>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID to find.
    /// </param>
    /// <param name="code">
    /// A <see cref="ushort"/> that contains a status code indicating the reason for closure.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that contains the reason for closure.
    /// </param>
    public void CloseSession (string id, ushort code, string reason)
    {
      IWebSocketSession session;
      if (TryGetSession (id, out session))
        session.Context.WebSocket.Close (code, reason);
    }

    /// <summary>
    /// Closes the session with the specified <paramref name="id"/>, <paramref name="code"/>
    /// and <paramref name="reason"/>.
    /// </summary>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID to find.
    /// </param>
    /// <param name="code">
    /// One of the <see cref="CloseStatusCode"/> values that indicate the status codes for closure.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that contains the reason for closure.
    /// </param>
    public void CloseSession (string id, CloseStatusCode code, string reason)
    {
      IWebSocketSession session;
      if (TryGetSession (id, out session))
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
    /// A <see cref="string"/> that contains a session ID that represents the destination
    /// for the Ping.
    /// </param>
    public bool PingTo (string id)
    {
      IWebSocketSession session;
      return TryGetSession (id, out session) && session.Context.WebSocket.Ping ();
    }

    /// <summary>
    /// Sends a Ping with the specified <paramref name="message"/> to the client
    /// associated with the specified <paramref name="id"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the WebSocket service receives a Pong from the client in a time;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID that represents the destination
    /// for the Ping.
    /// </param>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to send.
    /// </param>
    public bool PingTo (string id, string message)
    {
      IWebSocketSession session;
      return TryGetSession (id, out session) && session.Context.WebSocket.Ping (message);
    }

    /// <summary>
    /// Sends a binary <paramref name="data"/> to the client associated with the specified
    /// <paramref name="id"/>.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the send to be complete.
    /// </remarks>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID that represents the destination
    /// for the data.
    /// </param>
    /// <param name="data">
    /// An array of <see cref="byte"/> that contains a binary data to send.
    /// </param>
    public void SendTo (string id, byte [] data)
    {
      SendTo (id, data, null);
    }

    /// <summary>
    /// Sends a text <paramref name="data"/> to the client associated with the specified
    /// <paramref name="id"/>.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the send to be complete.
    /// </remarks>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID that represents the destination
    /// for the data.
    /// </param>
    /// <param name="data">
    /// A <see cref="string"/> that contains a text data to send.
    /// </param>
    public void SendTo (string id, string data)
    {
      SendTo (id, data, null);
    }

    /// <summary>
    /// Sends a binary <paramref name="data"/> to the client associated with the specified
    /// <paramref name="id"/>.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the send to be complete.
    /// </remarks>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID that represents the destination
    /// for the data.
    /// </param>
    /// <param name="data">
    /// An array of <see cref="byte"/> that contains a binary data to send.
    /// </param>
    /// <param name="completed">
    /// An Action&lt;bool&gt; delegate that references the method(s) called when
    /// the send is complete.
    /// A <see cref="bool"/> passed to this delegate is <c>true</c> if the send is complete
    /// successfully; otherwise, <c>false</c>.
    /// </param>
    public void SendTo (string id, byte [] data, Action<bool> completed)
    {
      IWebSocketSession session;
      if (TryGetSession (id, out session))
        session.Context.WebSocket.Send (data, completed);
    }

    /// <summary>
    /// Sends a text <paramref name="data"/> to the client associated with the specified
    /// <paramref name="id"/>.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the send to be complete.
    /// </remarks>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID that represents the destination
    /// for the data.
    /// </param>
    /// <param name="data">
    /// A <see cref="string"/> that contains a text data to send.
    /// </param>
    /// <param name="completed">
    /// An Action&lt;bool&gt; delegate that references the method(s) called when
    /// the send is complete.
    /// A <see cref="bool"/> passed to this delegate is <c>true</c> if the send is complete
    /// successfully; otherwise, <c>false</c>.
    /// </param>
    public void SendTo (string id, string data, Action<bool> completed)
    {
      IWebSocketSession session;
      if (TryGetSession (id, out session))
        session.Context.WebSocket.Send (data, completed);
    }

    /// <summary>
    /// Sends a binary data from the specified <see cref="Stream"/> to the client
    /// associated with the specified <paramref name="id"/>.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the send to be complete.
    /// </remarks>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID that represents the destination
    /// for the data.
    /// </param>
    /// <param name="stream">
    /// A <see cref="Stream"/> object from which contains a binary data to send.
    /// </param>
    /// <param name="length">
    /// An <see cref="int"/> that contains the number of bytes to send.
    /// </param>
    public void SendTo (string id, Stream stream, int length)
    {
      SendTo (id, stream, length, null);
    }

    /// <summary>
    /// Sends a binary data from the specified <see cref="Stream"/> to the client
    /// associated with the specified <paramref name="id"/>.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the send to be complete.
    /// </remarks>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID that represents the destination
    /// for the data.
    /// </param>
    /// <param name="stream">
    /// A <see cref="Stream"/> object from which contains a binary data to send.
    /// </param>
    /// <param name="length">
    /// An <see cref="int"/> that contains the number of bytes to send.
    /// </param>
    /// <param name="completed">
    /// An Action&lt;bool&gt; delegate that references the method(s) called when
    /// the send is complete.
    /// A <see cref="bool"/> passed to this delegate is <c>true</c> if the send is complete
    /// successfully; otherwise, <c>false</c>.
    /// </param>
    public void SendTo (
      string id, Stream stream, int length, Action<bool> completed)
    {
      IWebSocketSession session;
      if (TryGetSession (id, out session))
        session.Context.WebSocket.Send (stream, length, completed);
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
            IWebSocketSession session;
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
    /// Tries to get a WebSocket session information with the specified <paramref name="id"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the session is successfully found; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="id">
    /// A <see cref="string"/> that contains an ID of the session to find.
    /// </param>
    /// <param name="session">
    /// When this method returns, a <see cref="IWebSocketSession"/> instance that contains
    /// the session information if the session is successfully found; otherwise, <see langword="null"/>.
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

      return tryGetSession (id, out session);
    }

    #endregion
  }
}
