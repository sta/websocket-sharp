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
using System.Collections;
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
    #region Private Fields

    private volatile bool                         _clean;
    private object                                _forSweep;
    private Logger                                _logger;
    private Dictionary<string, IWebSocketSession> _sessions;
    private volatile ServerState                  _state;
    private volatile bool                         _sweeping;
    private System.Timers.Timer                   _sweepTimer;
    private object                                _sync;
    private TimeSpan                              _waitTime;

    #endregion

    #region Internal Constructors

    internal WebSocketSessionManager ()
      : this (new Logger ())
    {
    }

    internal WebSocketSessionManager (Logger logger)
    {
      _logger = logger;

      _clean = true;
      _forSweep = new object ();
      _sessions = new Dictionary<string, IWebSocketSession> ();
      _state = ServerState.Ready;
      _sync = ((ICollection) _sessions).SyncRoot;
      _waitTime = TimeSpan.FromSeconds (1);

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
    /// Gets the IDs for the active sessions in the Websocket service.
    /// </summary>
    /// <value>
    /// An <c>IEnumerable&lt;string&gt;</c> instance that provides an enumerator which
    /// supports the iteration over the collection of the IDs for the active sessions.
    /// </value>
    public IEnumerable<string> ActiveIDs {
      get {
        foreach (var res in Broadping (WebSocketFrame.EmptyUnmaskPingBytes, _waitTime))
          if (res.Value)
            yield return res.Key;
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
        lock (_sync)
          return _sessions.Count;
      }
    }

    /// <summary>
    /// Gets the IDs for the sessions in the Websocket service.
    /// </summary>
    /// <value>
    /// An <c>IEnumerable&lt;string&gt;</c> instance that provides an enumerator which
    /// supports the iteration over the collection of the IDs for the sessions.
    /// </value>
    public IEnumerable<string> IDs {
      get {
        if (_state == ServerState.ShuttingDown)
          return new string[0];

        lock (_sync)
          return _sessions.Keys.ToList ();
      }
    }

    /// <summary>
    /// Gets the IDs for the inactive sessions in the Websocket service.
    /// </summary>
    /// <value>
    /// An <c>IEnumerable&lt;string&gt;</c> instance that provides an enumerator which
    /// supports the iteration over the collection of the IDs for the inactive sessions.
    /// </value>
    public IEnumerable<string> InactiveIDs {
      get {
        foreach (var res in Broadping (WebSocketFrame.EmptyUnmaskPingBytes, _waitTime))
          if (!res.Value)
            yield return res.Key;
      }
    }

    /// <summary>
    /// Gets the session with the specified <paramref name="id"/>.
    /// </summary>
    /// <value>
    /// A <see cref="IWebSocketSession"/> instance that provides the access to
    /// the information in the session, or <see langword="null"/> if it's not found.
    /// </value>
    /// <param name="id">
    /// A <see cref="string"/> that represents the ID of the session to find.
    /// </param>
    public IWebSocketSession this[string id] {
      get {
        IWebSocketSession session;
        TryGetSession (id, out session);

        return session;
      }
    }

    /// <summary>
    /// Gets a value indicating whether the manager cleans up the inactive sessions
    /// in the WebSocket service periodically.
    /// </summary>
    /// <value>
    /// <c>true</c> if the manager cleans up the inactive sessions every 60 seconds;
    /// otherwise, <c>false</c>.
    /// </value>
    public bool KeepClean {
      get {
        return _clean;
      }

      internal set {
        if (!(value ^ _clean))
          return;

        _clean = value;
        if (_state == ServerState.Start)
          _sweepTimer.Enabled = value;
      }
    }

    /// <summary>
    /// Gets the sessions in the Websocket service.
    /// </summary>
    /// <value>
    /// An <c>IEnumerable&lt;IWebSocketSession&gt;</c> instance that provides an enumerator
    /// which supports the iteration over the collection of the sessions in the service.
    /// </value>
    public IEnumerable<IWebSocketSession> Sessions {
      get {
        if (_state == ServerState.ShuttingDown)
          return new IWebSocketSession[0];

        lock (_sync)
          return _sessions.Values.ToList ();
      }
    }

    /// <summary>
    /// Gets the wait time for the response to the WebSocket Ping or Close.
    /// </summary>
    /// <value>
    /// A <see cref="TimeSpan"/> that represents the wait time.
    /// </value>
    public TimeSpan WaitTime {
      get {
        return _waitTime;
      }

      internal set {
        if (value == _waitTime)
          return;

        _waitTime = value;
        foreach (var session in Sessions)
          session.Context.WebSocket.WaitTime = value;
      }
    }

    #endregion

    #region Private Methods

    private void broadcast (Opcode opcode, byte[] data, Action completed)
    {
      var cache = new Dictionary<CompressionMethod, byte[]> ();
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

    private void broadcastAsync (Opcode opcode, byte[] data, Action completed)
    {
      ThreadPool.QueueUserWorkItem (state => broadcast (opcode, data, completed));
    }

    private void broadcastAsync (Opcode opcode, Stream stream, Action completed)
    {
      ThreadPool.QueueUserWorkItem (state => broadcast (opcode, stream, completed));
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
      bool res;
      lock (_sync)
        res = _sessions.TryGetValue (id, out session);

      if (!res)
        _logger.Error ("A session with the specified ID isn't found.\nID: " + id);

      return res;
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
      Opcode opcode, byte[] data, Dictionary<CompressionMethod, byte[]> cache)
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

    internal Dictionary<string, bool> Broadping (byte[] frameAsBytes, TimeSpan timeout)
    {
      var res = new Dictionary<string, bool> ();
      foreach (var session in Sessions) {
        if (_state != ServerState.Start)
          break;

        res.Add (session.ID, session.Context.WebSocket.Ping (frameAsBytes, timeout));
      }

      return res;
    }

    internal bool Remove (string id)
    {
      lock (_sync)
        return _sessions.Remove (id);
    }

    internal void Start ()
    {
      lock (_sync) {
        _sweepTimer.Enabled = _clean;
        _state = ServerState.Start;
      }
    }

    internal void Stop (CloseEventArgs e, byte[] frameAsBytes, TimeSpan timeout)
    {
      lock (_sync) {
        _state = ServerState.ShuttingDown;

        _sweepTimer.Enabled = false;
        foreach (var session in _sessions.Values.ToList ())
          session.Context.WebSocket.Close (e, frameAsBytes, timeout);

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
    public void Broadcast (byte[] data)
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
    /// Broadcasts a binary <paramref name="data"/> asynchronously to every client
    /// in the WebSocket service.
    /// </summary>
    /// <remarks>
    /// This method doesn't wait for the broadcast to be complete.
    /// </remarks>
    /// <param name="data">
    /// An array of <see cref="byte"/> that represents the binary data to broadcast.
    /// </param>
    /// <param name="completed">
    /// An <see cref="Action"/> delegate that references the method(s) called when
    /// the broadcast is complete.
    /// </param>
    public void BroadcastAsync (byte[] data, Action completed)
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
    /// Broadcasts a text <paramref name="data"/> asynchronously to every client
    /// in the WebSocket service.
    /// </summary>
    /// <remarks>
    /// This method doesn't wait for the broadcast to be complete.
    /// </remarks>
    /// <param name="data">
    /// A <see cref="string"/> that represents the text data to broadcast.
    /// </param>
    /// <param name="completed">
    /// An <see cref="Action"/> delegate that references the method(s) called when
    /// the broadcast is complete.
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
    /// Broadcasts a binary data from the specified <see cref="Stream"/> asynchronously
    /// to every client in the WebSocket service.
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
    /// An <see cref="Action"/> delegate that references the method(s) called when
    /// the broadcast is complete.
    /// </param>
    public void BroadcastAsync (Stream stream, int length, Action completed)
    {
      var msg = _state.CheckIfStart () ??
                stream.CheckIfCanRead () ??
                (length < 1 ? "'length' is less than 1." : null);

      if (msg != null) {
        _logger.Error (msg);
        return;
      }

      stream.ReadBytesAsync (
        length,
        data => {
          var len = data.Length;
          if (len == 0) {
            _logger.Error ("The data cannot be read from 'stream'.");
            return;
          }

          if (len < length)
            _logger.Warn (
              String.Format (
                "The data with 'length' cannot be read from 'stream'.\nexpected: {0} actual: {1}",
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
    /// A <c>Dictionary&lt;string, bool&gt;</c> that contains a collection of pairs of
    /// a session ID and a value indicating whether the manager received a Pong from
    /// each client in a time.
    /// </returns>
    public Dictionary<string, bool> Broadping ()
    {
      var msg = _state.CheckIfStart ();
      if (msg != null) {
        _logger.Error (msg);
        return null;
      }

      return Broadping (WebSocketFrame.EmptyUnmaskPingBytes, _waitTime);
    }

    /// <summary>
    /// Sends a Ping with the specified <paramref name="message"/> to every client
    /// in the WebSocket service.
    /// </summary>
    /// <returns>
    /// A <c>Dictionary&lt;string, bool&gt;</c> that contains a collection of pairs of
    /// a session ID and a value indicating whether the manager received a Pong from
    /// each client in a time.
    /// </returns>
    /// <param name="message">
    /// A <see cref="string"/> that represents the message to send.
    /// </param>
    public Dictionary<string, bool> Broadping (string message)
    {
      if (message == null || message.Length == 0)
        return Broadping ();

      byte[] data = null;
      var msg = _state.CheckIfStart () ??
                (data = Encoding.UTF8.GetBytes (message)).CheckIfValidControlData ("message");

      if (msg != null) {
        _logger.Error (msg);
        return null;
      }

      return Broadping (WebSocketFrame.CreatePingFrame (data, false).ToByteArray (), _waitTime);
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
    /// Closes the session with the specified <paramref name="id"/>, <paramref name="code"/>,
    /// and <paramref name="reason"/>.
    /// </summary>
    /// <param name="id">
    /// A <see cref="string"/> that represents the ID of the session to close.
    /// </param>
    /// <param name="code">
    /// A <see cref="ushort"/> that represents the status code indicating the reason for the close.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that represents the reason for the close.
    /// </param>
    public void CloseSession (string id, ushort code, string reason)
    {
      IWebSocketSession session;
      if (TryGetSession (id, out session))
        session.Context.WebSocket.Close (code, reason);
    }

    /// <summary>
    /// Closes the session with the specified <paramref name="id"/>, <paramref name="code"/>,
    /// and <paramref name="reason"/>.
    /// </summary>
    /// <param name="id">
    /// A <see cref="string"/> that represents the ID of the session to close.
    /// </param>
    /// <param name="code">
    /// One of the <see cref="CloseStatusCode"/> enum values, represents the status code
    /// indicating the reason for the close.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that represents the reason for the close.
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
    /// <c>true</c> if the manager receives a Pong from the client in a time;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <param name="id">
    /// A <see cref="string"/> that represents the ID of the session to find.
    /// </param>
    public bool PingTo (string id)
    {
      IWebSocketSession session;
      return TryGetSession (id, out session) && session.Context.WebSocket.Ping ();
    }

    /// <summary>
    /// Sends a Ping with the specified <paramref name="message"/> to the client
    /// on the session with the specified <paramref name="id"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the manager receives a Pong from the client in a time;
    /// otherwise, <c>false</c>.
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
      return TryGetSession (id, out session) && session.Context.WebSocket.Ping (message);
    }

    /// <summary>
    /// Sends a binary <paramref name="data"/> to the client on the session
    /// with the specified <paramref name="id"/>.
    /// </summary>
    /// <param name="id">
    /// A <see cref="string"/> that represents the ID of the session to find.
    /// </param>
    /// <param name="data">
    /// An array of <see cref="byte"/> that represents the binary data to send.
    /// </param>
    public void SendTo (string id, byte[] data)
    {
      IWebSocketSession session;
      if (TryGetSession (id, out session))
        session.Context.WebSocket.Send (data);
    }

    /// <summary>
    /// Sends a text <paramref name="data"/> to the client on the session
    /// with the specified <paramref name="id"/>.
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
    /// Sends a binary <paramref name="data"/> asynchronously to the client on the session
    /// with the specified <paramref name="id"/>.
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
    /// An <c>Action&lt;bool&gt;</c> delegate that references the method(s) called when
    /// the send is complete. A <see cref="bool"/> passed to this delegate is <c>true</c>
    /// if the send is complete successfully.
    /// </param>
    public void SendToAsync (string id, byte[] data, Action<bool> completed)
    {
      IWebSocketSession session;
      if (TryGetSession (id, out session))
        session.Context.WebSocket.SendAsync (data, completed);
    }

    /// <summary>
    /// Sends a text <paramref name="data"/> asynchronously to the client on the session
    /// with the specified <paramref name="id"/>.
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
    /// An <c>Action&lt;bool&gt;</c> delegate that references the method(s) called when
    /// the send is complete. A <see cref="bool"/> passed to this delegate is <c>true</c>
    /// if the send is complete successfully.
    /// </param>
    public void SendToAsync (string id, string data, Action<bool> completed)
    {
      IWebSocketSession session;
      if (TryGetSession (id, out session))
        session.Context.WebSocket.SendAsync (data, completed);
    }

    /// <summary>
    /// Sends a binary data from the specified <see cref="Stream"/> asynchronously
    /// to the client on the session with the specified <paramref name="id"/>.
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
    /// An <c>Action&lt;bool&gt;</c> delegate that references the method(s) called when
    /// the send is complete. A <see cref="bool"/> passed to this delegate is <c>true</c>
    /// if the send is complete successfully.
    /// </param>
    public void SendToAsync (string id, Stream stream, int length, Action<bool> completed)
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
                session.Context.WebSocket.Close (CloseStatusCode.ProtocolError);
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
    /// Tries to get the session with the specified <paramref name="id"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the session is successfully found; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="id">
    /// A <see cref="string"/> that represents the ID of the session to find.
    /// </param>
    /// <param name="session">
    /// When this method returns, a <see cref="IWebSocketSession"/> instance that
    /// provides the access to the information in the session, or <see langword="null"/>
    /// if it's not found. This parameter is passed uninitialized.
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
