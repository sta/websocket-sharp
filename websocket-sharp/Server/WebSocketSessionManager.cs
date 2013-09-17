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
using System.Linq;
using System.Text;
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
    private Logger                               _logger;
    private Dictionary<string, WebSocketService> _sessions;
    private volatile bool                        _stopped;
    private volatile bool                        _sweeping;
    private Timer                                _sweepTimer;
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
      _sessions = new Dictionary<string, WebSocketService> ();
      _stopped = false;
      _sweeping = false;
      _sync = new object ();

      setSweepTimer ();
      startSweepTimer ();
    }

    #endregion

    #region Internal Properties

    internal IEnumerable<WebSocketService> ServiceInstances {
      get {
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
        return from result in BroadpingInternally (new byte [] {})
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
        return from result in BroadpingInternally (new byte [] {})
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
        var msg = id.CheckIfValidSessionID ();
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
        return _sweepTimer.Enabled;
      }

      internal set {
        if (value)
        {
          if (!_stopped)
            startSweepTimer ();
        }
        else
          stopSweepTimer ();
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

    private void broadcast (byte [] data)
    {
      foreach (var service in ServiceInstances)
        service.Send (data);
    }

    private void broadcast (string data)
    {
      foreach (var service in ServiceInstances)
        service.Send (data);
    }

    private void broadcastAsync (byte [] data)
    {
      var services = ServiceInstances.GetEnumerator ();
      Action completed = null;
      completed = () =>
      {
        if (services.MoveNext ())
          services.Current.SendAsync (data, completed);
      };

      if (services.MoveNext ())
        services.Current.SendAsync (data, completed);
    }

    private void broadcastAsync (string data)
    {
      var services = ServiceInstances.GetEnumerator ();
      Action completed = null;
      completed = () =>
      {
        if (services.MoveNext ())
          services.Current.SendAsync (data, completed);
      };

      if (services.MoveNext ())
        services.Current.SendAsync (data, completed);
    }

    private static string createID ()
    {
      return Guid.NewGuid ().ToString ("N");
    }

    private void setSweepTimer ()
    {
      _sweepTimer = new Timer (60 * 1000);
      _sweepTimer.Elapsed += (sender, e) =>
      {
        Sweep ();
      };
    }

    private void startSweepTimer ()
    {
      if (!_sweepTimer.Enabled)
        _sweepTimer.Start ();
    }

    private void stopSweepTimer ()
    {
      if (_sweepTimer.Enabled)
        _sweepTimer.Stop ();
    }

    #endregion

    #region Internal Methods

    internal string Add (WebSocketService session)
    {
      lock (_sync)
      {
        if (_stopped)
          return null;

        var id = createID ();
        _sessions.Add (id, session);

        return id;
      }
    }

    internal void BroadcastInternally (byte [] data)
    {
      if (_stopped)
        broadcast (data);
      else
        broadcastAsync (data);
    }

    internal void BroadcastInternally (string data)
    {
      if (_stopped)
        broadcast (data);
      else
        broadcastAsync (data);
    }

    internal Dictionary<string, bool> BroadpingInternally (byte [] data)
    {
      var result = new Dictionary<string, bool> ();
      foreach (var session in ServiceInstances)
        result.Add (session.ID, session.Context.WebSocket.Ping (data));

      return result;
    }

    internal bool Remove (string id)
    {
      lock (_sync)
      {
        return _sessions.Remove (id);
      }
    }

    internal void Stop ()
    {
      stopSweepTimer ();
      lock (_sync)
      {
        if (_stopped)
          return;

        _stopped = true;
        foreach (var session in ServiceInstances)
          session.Context.WebSocket.Close ();
      }
    }

    internal void Stop (byte [] data)
    {
      stopSweepTimer ();
      lock (_sync)
      {
        if (_stopped)
          return;

        _stopped = true;
        foreach (var session in ServiceInstances)
          session.Context.WebSocket.Close (data);
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
      var msg = data.CheckIfValidSendData ();
      if (msg != null)
      {
        _logger.Error (msg);
        return;
      }

      BroadcastInternally (data);
    }

    /// <summary>
    /// Broadcasts the specified <see cref="string"/> to all clients of the WebSocket service.
    /// </summary>
    /// <param name="data">
    /// A <see cref="string"/> to broadcast.
    /// </param>
    public void Broadcast (string data)
    {
      var msg = data.CheckIfValidSendData ();
      if (msg != null)
      {
        _logger.Error (msg);
        return;
      }

      BroadcastInternally (data);
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
      return BroadpingInternally (new byte [] {});
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
        return BroadpingInternally (new byte [] {});

      var data = Encoding.UTF8.GetBytes (message);
      var msg = data.CheckIfValidPingData ();
      if (msg != null)
      {
        _logger.Error (msg);
        return null;
      }

      return BroadpingInternally (data);
    }

    /// <summary>
    /// Closes the session with the specified <paramref name="id"/>.
    /// </summary>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID to find.
    /// </param>
    public void CloseSession (string id)
    {
      var msg = id.CheckIfValidSessionID ();
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
      var msg = id.CheckIfValidSessionID ();
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
      var msg = id.CheckIfValidSessionID ();
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
      var msg = id.CheckIfValidSessionID ();
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
      var msg = id.CheckIfValidSessionID ();
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
    /// <returns>
    /// <c>true</c> if <paramref name="data"/> is successfully sent; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="data">
    /// An array of <see cref="byte"/> that contains a binary data to send.
    /// </param>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID that represents the destination for the data.
    /// </param>
    public bool SendTo (byte [] data, string id)
    {
      var msg = id.CheckIfValidSessionID ();
      if (msg != null)
      {
        _logger.Error (msg);
        return false;
      }

      WebSocketService service;
      if (!TryGetServiceInstance (id, out service))
      {
        _logger.Error ("The WebSocket session with the specified ID not found.\nID: " + id);
        return false;
      }

      service.Send (data);
      return true;
    }

    /// <summary>
    /// Sends a text <paramref name="data"/> to the client associated with the specified
    /// <paramref name="id"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="data"/> is successfully sent; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="data">
    /// A <see cref="string"/> that contains a text data to send.
    /// </param>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID that represents the destination for the data.
    /// </param>
    public bool SendTo (string data, string id)
    {
      var msg = id.CheckIfValidSessionID ();
      if (msg != null)
      {
        _logger.Error (msg);
        return false;
      }

      WebSocketService service;
      if (!TryGetServiceInstance (id, out service))
      {
        _logger.Error ("The WebSocket session with the specified ID not found.\nID: " + id);
        return false;
      }

      service.Send (data);
      return true;
    }

    /// <summary>
    /// Cleans up the inactive sessions.
    /// </summary>
    public void Sweep ()
    {
      if (_stopped || _sweeping || Count == 0)
        return;

      lock (_forSweep)
      {
        _sweeping = true;
        foreach (var id in InactiveIDs)
        {
          lock (_sync)
          {
            if (_stopped)
              break;

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
      var msg = id.CheckIfValidSessionID ();
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
