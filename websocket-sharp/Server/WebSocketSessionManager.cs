#region License
/*
 * WebSocketSessionManager.cs
 *
 * The MIT License
 *
 * Copyright (c) 2012-2014 sta.blockhead
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
using System.Text;
using System.Threading;
using System.Timers;

namespace WebSocketSharp.Server
{
  /// <summary>
  /// Manages the sessions in a Websocket service.
  /// </summary>
  public class WebSocketSessionManager
  {
    #region Private Static Fields

    private static readonly Dictionary<string, IWebSocketSession> _emptySessions;

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
      _emptySessions = new Dictionary<string, IWebSocketSession> ();
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
      _state = ServerState.Ready;
      _sync = new object ();

      setSweepTimer (60000);
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
    /// Gets the collection of every ID of the active sessions in the Websocket service.
    /// </summary>
    /// <value>
    /// An IEnumerable&lt;string&gt; that contains the collection of every ID of the active
    /// sessions.
    /// </value>
    public IEnumerable<string> ActiveIDs {
      get {
        foreach (var result in Broadping (WsFrame.EmptyUnmaskPingData, 1000))
          if (result.Value)
            yield return result.Key;
      }
    }

    /// <summary>
    /// Gets the number of the sessions in the Websocket service.
    /// </summary>
    /// <value>
    /// An <see cref="int"/> that represents the number of the sessions.
    /// </value>
    public int Count {
      get {
        lock (_sync) {
          return _sessions.Count;
        }
      }
    }

    /// <summary>
    /// Gets the collection of every ID of the sessions in the Websocket service.
    /// </summary>
    /// <value>
    /// An IEnumerable&lt;string&gt; that contains the collection of every ID of the sessions.
    /// </value>
    public IEnumerable<string> IDs {
      get {
        if (_state == ServerState.ShuttingDown)
          return _emptySessions.Keys;

        lock (_sync) {
          return _sessions.Keys.ToList ();
        }
      }
    }

    /// <summary>
    /// Gets the collection of every ID of the inactive sessions in the Websocket service.
    /// </summary>
    /// <value>
    /// An IEnumerable&lt;string&gt; that contains the collection of every ID of the inactive
    /// sessions.
    /// </value>
    public IEnumerable<string> InactiveIDs {
      get {
        foreach (var result in Broadping (WsFrame.EmptyUnmaskPingData, 1000))
          if (!result.Value)
            yield return result.Key;
      }
    }

    /// <summary>
    /// Gets the information in a session with the specified <paramref name="id"/> in the WebSocket
    /// service.
    /// </summary>
    /// <value>
    /// A <see cref="IWebSocketSession"/> instance that provides the access to the session if it's
    /// successfully found; otherwise, <see langword="null"/>.
    /// </value>
    /// <param name="id">
    /// A <see cref="string"/> that represents the ID of the session to find.
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
    /// <c>true</c> if the manager cleans up the inactive sessions every 60 seconds; otherwise,
    /// <c>false</c>.
    /// </value>
    public bool KeepClean {
      get {
        return _keepClean;
      }

      internal set {
        if (!(value ^ _keepClean))
          return;

        _keepClean = value;
        if (_state == ServerState.Start)
          _sweepTimer.Enabled = value;
      }
    }

    /// <summary>
    /// Gets the collection of every information in the sessions in the Websocket service.
    /// </summary>
    /// <value>
    /// An IEnumerable&lt;IWebSocketSession&gt; that contains the collection of every information
    /// in the sessions.
    /// </value>
    public IEnumerable<IWebSocketSession> Sessions {
      get {
        if (_state == ServerState.ShuttingDown)
          return _emptySessions.Values;

        lock (_sync) {
          return _sessions.Values.ToList ();
        }
      }
    }

    #endregion

    #region Private Methods

    private void broadcast (Opcode opcode, byte [] data, Action completed)
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

    private void broadcast (Opcode opcode, Stream stream, Action completed)
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

    private void broadcastAsync (Opcode opcode, byte [] data, Action completed)
    {
      ThreadPool.QueueUserWorkItem (
        state => broadcast (opcode, data, completed));
    }

    private void broadcastAsync (Opcode opcode, Stream stream, Action completed)
    {
      ThreadPool.QueueUserWorkItem (
        state => broadcast (opcode, stream, completed));
    }

    private static string createID ()
    {
      return Guid.NewGuid ().ToString ("N");
    }

    private void setSweepTimer (double interval)
    {
      _sweepTimer = new System.Timers.Timer (interval);
      _sweepTimer.Elapsed += (sender, e) => Sweep ();
    }

    private bool tryGetSession (string id, out IWebSocketSession session)
    {
      bool result;
      lock (_sync) {
        result = _sessions.TryGetValue (id, out session);
      }

      if (!result)
        _logger.Error ("A session with the specified ID not found.\nID: " + id);

      return result;
    }

    #endregion

    #region Internal Methods

    internal string Add (IWebSocketSession session)
    {
      lock (_sync) {
        if (_state != ServerState.Start)
          return null;

        var id = createID ();
        _sessions.Add (id, session);

        return id;
      }
    }

    internal void Broadcast (
      Opcode opcode, byte [] data, Dictionary<CompressionMethod, byte []> cache)
    {
      foreach (var session in Sessions) {
        if (_state != ServerState.Start)
          break;

        session.Context.WebSocket.Send (opcode, data, cache);
      }
    }

    internal void Broadcast (
      Opcode opcode, Stream stream, Dictionary <CompressionMethod, Stream> cache)
    {
      foreach (var session in Sessions) {
        if (_state != ServerState.Start)
          break;

        session.Context.WebSocket.Send (opcode, stream, cache);
      }
    }

    internal Dictionary<string, bool> Broadping (byte [] frame, int millisecondsTimeout)
    {
      var result = new Dictionary<string, bool> ();
      foreach (var session in Sessions) {
        if (_state != ServerState.Start)
          break;

        result.Add (
          session.ID, session.Context.WebSocket.Ping (frame, millisecondsTimeout));
      }

      return result;
    }

    internal bool Remove (string id)
    {
      lock (_sync) {
        return _sessions.Remove (id);
      }
    }

    internal void Start ()
    {
      _sweepTimer.Enabled = _keepClean;
      _state = ServerState.Start;
    }

    internal void Stop (byte [] data, bool send)
    {
      var payload = new PayloadData (data);
      var args = new CloseEventArgs (payload);
      var frameAsBytes = send
                       ? WsFrame.CreateCloseFrame (Mask.Unmask, payload).ToByteArray ()
                       : null;

      Stop (args, frameAsBytes);
    }

    internal void Stop (CloseEventArgs args, byte [] frame)
    {
      lock (_sync) {
        _state = ServerState.ShuttingDown;

        _sweepTimer.Enabled = false;
        foreach (var session in _sessions.Values.ToList ())
          session.Context.WebSocket.Close (args, frame, 1000);

        _state = ServerState.Stop;
      }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Broadcasts a binary <paramref name="data"/> to every client in the WebSocket service.
    /// </summary>
    /// <param name="data">
    /// An array of <see cref="byte"/> that represents the binary data to broadcast.
    /// </param>
    public void Broadcast (byte [] data)
    {
      var msg = _state.CheckIfStart () ?? data.CheckIfValidSendData ();
      if (msg != null) {
        _logger.Error (msg);
        return;
      }

      if (data.LongLength <= WebSocket.FragmentLength)
        broadcast (Opcode.Binary, data, null);
      else
        broadcast (Opcode.Binary, new MemoryStream (data), null);
    }

    /// <summary>
    /// Broadcasts a text <paramref name="data"/> to every client in the WebSocket service.
    /// </summary>
    /// <param name="data">
    /// A <see cref="string"/> that represents the text data to broadcast.
    /// </param>
    public void Broadcast (string data)
    {
      var msg = _state.CheckIfStart () ?? data.CheckIfValidSendData ();
      if (msg != null) {
        _logger.Error (msg);
        return;
      }

      var rawData = Encoding.UTF8.GetBytes (data);
      if (rawData.LongLength <= WebSocket.FragmentLength)
        broadcast (Opcode.Text, rawData, null);
      else
        broadcast (Opcode.Text, new MemoryStream (rawData), null);
    }

    /// <summary>
    /// Broadcasts a binary <paramref name="data"/> asynchronously to every client in the WebSocket
    /// service.
    /// </summary>
    /// <remarks>
    /// This method doesn't wait for the broadcast to be complete.
    /// </remarks>
    /// <param name="data">
    /// An array of <see cref="byte"/> that represents the binary data to broadcast.
    /// </param>
    /// <param name="completed">
    /// A <see cref="Action"/> delegate that references the method(s) called when the broadcast is
    /// complete.
    /// </param>
    public void BroadcastAsync (byte [] data, Action completed)
    {
      var msg = _state.CheckIfStart () ?? data.CheckIfValidSendData ();
      if (msg != null) {
        _logger.Error (msg);
        return;
      }

      if (data.LongLength <= WebSocket.FragmentLength)
        broadcastAsync (Opcode.Binary, data, completed);
      else
        broadcastAsync (Opcode.Binary, new MemoryStream (data), completed);
    }

    /// <summary>
    /// Broadcasts a text <paramref name="data"/> asynchronously to every client in the WebSocket
    /// service.
    /// </summary>
    /// <remarks>
    /// This method doesn't wait for the broadcast to be complete.
    /// </remarks>
    /// <param name="data">
    /// A <see cref="string"/> that represents the text data to broadcast.
    /// </param>
    /// <param name="completed">
    /// A <see cref="Action"/> delegate that references the method(s) called when the broadcast is
    /// complete.
    /// </param>
    public void BroadcastAsync (string data, Action completed)
    {
      var msg = _state.CheckIfStart () ?? data.CheckIfValidSendData ();
      if (msg != null) {
        _logger.Error (msg);
        return;
      }

      var rawData = Encoding.UTF8.GetBytes (data);
      if (rawData.LongLength <= WebSocket.FragmentLength)
        broadcastAsync (Opcode.Text, rawData, completed);
      else
        broadcastAsync (Opcode.Text, new MemoryStream (rawData), completed);
    }

    /// <summary>
    /// Broadcasts a binary data from the specified <see cref="Stream"/> asynchronously to every
    /// client in the WebSocket service.
    /// </summary>
    /// <remarks>
    /// This method doesn't wait for the broadcast to be complete.
    /// </remarks>
    /// <param name="stream">
    /// A <see cref="Stream"/> from which contains the binary data to broadcast.
    /// </param>
    /// <param name="length">
    /// An <see cref="int"/> that represents the number of bytes to broadcast.
    /// </param>
    /// <param name="completed">
    /// A <see cref="Action"/> delegate that references the method(s) called when the broadcast is
    /// complete.
    /// </param>
    public void BroadcastAsync (Stream stream, int length, Action completed)
    {
      var msg = _state.CheckIfStart () ??
                stream.CheckIfCanRead () ??
                (length < 1 ? "'length' must be greater than 0." : null);

      if (msg != null) {
        _logger.Error (msg);
        return;
      }

      stream.ReadBytesAsync (
        length,
        data => {
          var len = data.Length;
          if (len == 0) {
            _logger.Error ("A data cannot be read from 'stream'.");
            return;
          }

          if (len < length)
            _logger.Warn (
              String.Format (
                "A data with 'length' cannot be read from 'stream'.\nexpected: {0} actual: {1}",
                length,
                len));

          if (len <= WebSocket.FragmentLength)
            broadcast (Opcode.Binary, data, completed);
          else
            broadcast (Opcode.Binary, new MemoryStream (data), completed);
        },
        ex => _logger.Fatal (ex.ToString ()));
    }

    /// <summary>
    /// Sends a Ping to every client in the WebSocket service.
    /// </summary>
    /// <returns>
    /// A Dictionary&lt;string, bool&gt; that contains the collection of pairs of session ID and
    /// value indicating whether the manager received a Pong from every client in a time.
    /// </returns>
    public Dictionary<string, bool> Broadping ()
    {
      var msg = _state.CheckIfStart ();
      if (msg != null) {
        _logger.Error (msg);
        return null;
      }

      return Broadping (WsFrame.EmptyUnmaskPingData, 1000);
    }

    /// <summary>
    /// Sends a Ping with the specified <paramref name="message"/> to every client in the WebSocket
    /// service.
    /// </summary>
    /// <returns>
    /// A Dictionary&lt;string, bool&gt; that contains the collection of pairs of session ID and
    /// value indicating whether the manager received a Pong from every client in a time.
    /// </returns>
    /// <param name="message">
    /// A <see cref="string"/> that represents the message to send.
    /// </param>
    public Dictionary<string, bool> Broadping (string message)
    {
      if (message == null || message.Length == 0)
        return Broadping ();

      byte [] data = null;
      var msg = _state.CheckIfStart () ??
                (data = Encoding.UTF8.GetBytes (message)).CheckIfValidControlData ("message");

      if (msg != null) {
        _logger.Error (msg);
        return null;
      }

      return Broadping (
        WsFrame.CreatePingFrame (Mask.Unmask, data).ToByteArray (), 1000);
    }

    /// <summary>
    /// Closes the session with the specified <paramref name="id"/>.
    /// </summary>
    /// <param name="id">
    /// A <see cref="string"/> that represents the ID of the session to close.
    /// </param>
    public void CloseSession (string id)
    {
      IWebSocketSession session;
      if (TryGetSession (id, out session))
        session.Context.WebSocket.Close ();
    }

    /// <summary>
    /// Closes the session with the specified <paramref name="id"/>, <paramref name="code"/>, and
    /// <paramref name="reason"/>.
    /// </summary>
    /// <param name="id">
    /// A <see cref="string"/> that represents the ID of the session to close.
    /// </param>
    /// <param name="code">
    /// A <see cref="ushort"/> that represents the status code indicating the reason for closure.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that represents the reason for closure.
    /// </param>
    public void CloseSession (string id, ushort code, string reason)
    {
      IWebSocketSession session;
      if (TryGetSession (id, out session))
        session.Context.WebSocket.Close (code, reason);
    }

    /// <summary>
    /// Closes the session with the specified <paramref name="id"/>, <paramref name="code"/>, and
    /// <paramref name="reason"/>.
    /// </summary>
    /// <param name="id">
    /// A <see cref="string"/> that represents the ID of the session to close.
    /// </param>
    /// <param name="code">
    /// One of the <see cref="CloseStatusCode"/> enum values, represents the status code indicating
    /// the reason for closure.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that represents the reason for closure.
    /// </param>
    public void CloseSession (string id, CloseStatusCode code, string reason)
    {
      IWebSocketSession session;
      if (TryGetSession (id, out session))
        session.Context.WebSocket.Close (code, reason);
    }

    /// <summary>
    /// Sends a Ping to the client on the session with the specified <paramref name="id"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the manager receives a Pong from the client in a time; otherwise,
    /// <c>false</c>.
    /// </returns>
    /// <param name="id">
    /// A <see cref="string"/> that represents the ID of the session to find.
    /// </param>
    public bool PingTo (string id)
    {
      IWebSocketSession session;
      return TryGetSession (id, out session) &&
             session.Context.WebSocket.Ping ();
    }

    /// <summary>
    /// Sends a Ping with the specified <paramref name="message"/> to the client on the session
    /// with the specified <paramref name="id"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the manager receives a Pong from the client in a time; otherwise,
    /// <c>false</c>.
    /// </returns>
    /// <param name="id">
    /// A <see cref="string"/> that represents the ID of the session to find.
    /// </param>
    /// <param name="message">
    /// A <see cref="string"/> that represents the message to send.
    /// </param>
    public bool PingTo (string id, string message)
    {
      IWebSocketSession session;
      return TryGetSession (id, out session) &&
             session.Context.WebSocket.Ping (message);
    }

    /// <summary>
    /// Sends a binary <paramref name="data"/> to the client on the session with the specified
    /// <paramref name="id"/>.
    /// </summary>
    /// <param name="id">
    /// A <see cref="string"/> that represents the ID of the session to find.
    /// </param>
    /// <param name="data">
    /// An array of <see cref="byte"/> that represents the binary data to send.
    /// </param>
    public void SendTo (string id, byte [] data)
    {
      IWebSocketSession session;
      if (TryGetSession (id, out session))
        session.Context.WebSocket.Send (data);
    }

    /// <summary>
    /// Sends a text <paramref name="data"/> to the client on the session with the specified
    /// <paramref name="id"/>.
    /// </summary>
    /// <param name="id">
    /// A <see cref="string"/> that represents the ID of the session to find.
    /// </param>
    /// <param name="data">
    /// A <see cref="string"/> that represents the text data to send.
    /// </param>
    public void SendTo (string id, string data)
    {
      IWebSocketSession session;
      if (TryGetSession (id, out session))
        session.Context.WebSocket.Send (data);
    }

    /// <summary>
    /// Sends a binary <paramref name="data"/> asynchronously to the client on the session with the
    /// specified <paramref name="id"/>.
    /// </summary>
    /// <remarks>
    /// This method doesn't wait for the send to be complete.
    /// </remarks>
    /// <param name="id">
    /// A <see cref="string"/> that represents the ID of the session to find.
    /// </param>
    /// <param name="data">
    /// An array of <see cref="byte"/> that represents the binary data to send.
    /// </param>
    /// <param name="completed">
    /// An Action&lt;bool&gt; delegate that references the method(s) called when the send is
    /// complete. A <see cref="bool"/> passed to this delegate is <c>true</c> if the send is
    /// complete successfully; otherwise, <c>false</c>.
    /// </param>
    public void SendToAsync (string id, byte [] data, Action<bool> completed)
    {
      IWebSocketSession session;
      if (TryGetSession (id, out session))
        session.Context.WebSocket.SendAsync (data, completed);
    }

    /// <summary>
    /// Sends a text <paramref name="data"/> asynchronously to the client on the session with the
    /// specified <paramref name="id"/>.
    /// </summary>
    /// <remarks>
    /// This method doesn't wait for the send to be complete.
    /// </remarks>
    /// <param name="id">
    /// A <see cref="string"/> that represents the ID of the session to find.
    /// </param>
    /// <param name="data">
    /// A <see cref="string"/> that represents the text data to send.
    /// </param>
    /// <param name="completed">
    /// An Action&lt;bool&gt; delegate that references the method(s) called when the send is
    /// complete. A <see cref="bool"/> passed to this delegate is <c>true</c> if the send is
    /// complete successfully; otherwise, <c>false</c>.
    /// </param>
    public void SendToAsync (string id, string data, Action<bool> completed)
    {
      IWebSocketSession session;
      if (TryGetSession (id, out session))
        session.Context.WebSocket.SendAsync (data, completed);
    }

    /// <summary>
    /// Sends a binary data from the specified <see cref="Stream"/> asynchronously to the client on
    /// the session with the specified <paramref name="id"/>.
    /// </summary>
    /// <remarks>
    /// This method doesn't wait for the send to be complete.
    /// </remarks>
    /// <param name="id">
    /// A <see cref="string"/> that represents the ID of the session to find.
    /// </param>
    /// <param name="stream">
    /// A <see cref="Stream"/> from which contains the binary data to send.
    /// </param>
    /// <param name="length">
    /// An <see cref="int"/> that represents the number of bytes to send.
    /// </param>
    /// <param name="completed">
    /// An Action&lt;bool&gt; delegate that references the method(s) called when the send is
    /// complete. A <see cref="bool"/> passed to this delegate is <c>true</c> if the send is
    /// complete successfully; otherwise, <c>false</c>.
    /// </param>
    public void SendToAsync (
      string id, Stream stream, int length, Action<bool> completed)
    {
      IWebSocketSession session;
      if (TryGetSession (id, out session))
        session.Context.WebSocket.SendAsync (stream, length, completed);
    }

    /// <summary>
    /// Cleans up the inactive sessions in the WebSocket service.
    /// </summary>
    public void Sweep ()
    {
      if (_state != ServerState.Start || _sweeping || Count == 0)
        return;

      lock (_forSweep) {
        _sweeping = true;
        foreach (var id in InactiveIDs) {
          if (_state != ServerState.Start)
            break;

          lock (_sync) {
            IWebSocketSession session;
            if (_sessions.TryGetValue (id, out session)) {
              var state = session.State;
              if (state == WebSocketState.Open)
                session.Context.WebSocket.Close (CloseStatusCode.Abnormal);
              else if (state == WebSocketState.Closing)
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
    /// Tries to get the information in a session with the specified <paramref name="id"/> in the
    /// WebSocket service.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the session is successfully found; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="id">
    /// A <see cref="string"/> that represents the ID of the session to find.
    /// </param>
    /// <param name="session">
    /// When this method returns, a <see cref="IWebSocketSession"/> instance that provides the
    /// access to the session if it's successfully found; otherwise, <see langword="null"/>. This
    /// parameter is passed uninitialized.
    /// </param>
    public bool TryGetSession (string id, out IWebSocketSession session)
    {
      var msg = _state.CheckIfStart () ?? id.CheckIfValidSessionID ();
      if (msg != null) {
        _logger.Error (msg);
        session = null;

        return false;
      }

      return tryGetSession (id, out session);
    }

    #endregion
  }
}
