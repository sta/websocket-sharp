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
        return from result in Broadping (new byte [] {})
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
        return from result in Broadping (new byte [] {})
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
    /// A <see cref="string"/> that contains a session ID to find.
    /// </param>
    public IWebSocketSession this [string id] {
      get {
        var msg = id.CheckIfValidSessionID ();
        if (msg != null)
        {
          _logger.Error (msg);
          return null;
        }

        lock (_sync)
        {
          try {
            return _sessions [id];
          }
          catch {
            _logger.Error ("'id' not found.\nid: " + id);
            return null;
          }
        }
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

    internal void Broadcast (byte [] data)
    {
      if (_stopped)
        broadcast (data);
      else
        broadcastAsync (data);
    }

    internal void Broadcast (string data)
    {
      if (_stopped)
        broadcast (data);
      else
        broadcastAsync (data);
    }

    internal Dictionary<string, bool> Broadping (byte [] data)
    {
      var result = new Dictionary<string, bool> ();
      foreach (var service in ServiceInstances)
        result.Add (service.ID, service.Ping (data));

      return result;
    }

    internal bool PingTo (string id)
    {
      WebSocketService service;
      if (!TryGetServiceInstance (id, out service))
      {
        _logger.Error (
          "The WebSocket session with the specified ID not found.\nID: " + id);
        return false;
      }

      return service.Ping ();
    }

    internal bool PingTo (string message, string id)
    {
      WebSocketService service;
      if (!TryGetServiceInstance (id, out service))
      {
        _logger.Error (
          "The WebSocket session with the specified ID not found.\nID: " + id);
        return false;
      }

      return service.Ping (message);
    }

    internal bool Remove (string id)
    {
      lock (_sync)
      {
        return _sessions.Remove (id);
      }
    }

    internal bool SendTo (byte [] data, string id)
    {
      WebSocketService service;
      if (!TryGetServiceInstance (id, out service))
      {
        _logger.Error (
          "The WebSocket session with the specified ID not found.\nID: " + id);
        return false;
      }

      service.Send (data);
      return true;
    }

    internal bool SendTo (string data, string id)
    {
      WebSocketService service;
      if (!TryGetServiceInstance (id, out service))
      {
        _logger.Error (
          "The WebSocket session with the specified ID not found.\nID: " + id);
        return false;
      }

      service.Send (data);
      return true;
    }

    internal void Stop ()
    {
      stopSweepTimer ();
      lock (_sync)
      {
        if (_stopped)
          return;

        _stopped = true;
        foreach (var service in ServiceInstances)
          service.Stop ();
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
        foreach (var service in ServiceInstances)
          service.Stop (data);
      }
    }

    internal void StopServiceInstance (string id)
    {
      WebSocketService service;
      if (!TryGetServiceInstance (id, out service))
      {
        _logger.Error (
          "The WebSocket session with the specified ID not found.\nID: " + id);
        return;
      }

      service.Stop ();
    }

    internal void StopServiceInstance (ushort code, string reason, string id)
    {
      WebSocketService service;
      if (!TryGetServiceInstance (id, out service))
      {
        _logger.Error (
          "The WebSocket session with the specified ID not found.\nID: " + id);
        return;
      }

      service.Stop (code, reason);
    }

    internal void StopServiceInstance (CloseStatusCode code, string reason, string id)
    {
      WebSocketService service;
      if (!TryGetServiceInstance (id, out service))
      {
        _logger.Error (
          "The WebSocket session with the specified ID not found.\nID: " + id);
        return;
      }

      service.Stop (code, reason);
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

            WebSocketService service;
            if (_sessions.TryGetValue (id, out service))
            {
              var state = service.State;
              if (state == WebSocketState.OPEN)
                service.Stop (((ushort) CloseStatusCode.ABNORMAL).ToByteArray (ByteOrder.BIG));
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
    /// A <see cref="string"/> that contains a session ID to find.
    /// </param>
    /// <param name="session">
    /// When this method returns, a <see cref="IWebSocketSession"/> instance that contains the session
    /// information if it is successfully found; otherwise, <see langword="null"/>.
    /// </param>
    public bool TryGetSession (string id, out IWebSocketSession session)
    {
      WebSocketService service;
      var result = TryGetServiceInstance (id, out service);
      session = service;

      return result;
    }

    #endregion
  }
}
