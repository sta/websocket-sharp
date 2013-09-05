#region License
/*
 * WebSocketServiceManager.cs
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
  /// Manages the collection of <see cref="WebSocketService"/> instances.
  /// </summary>
  public class WebSocketServiceManager
  {
    #region Private Fields

    private object                               _forSweep;
    private Logger                               _logger;
    private Dictionary<string, WebSocketService> _services;
    private volatile bool                        _stopped;
    private volatile bool                        _sweeping;
    private Timer                                _sweepTimer;
    private object                               _sync;

    #endregion

    #region Internal Constructors

    internal WebSocketServiceManager ()
      : this (new Logger ())
    {
    }

    internal WebSocketServiceManager (Logger logger)
    {
      _logger = logger;
      _forSweep = new object ();
      _services = new Dictionary<string, WebSocketService> ();
      _stopped = false;
      _sweeping = false;
      _sync = new object ();

      setSweepTimer ();
      startSweepTimer ();
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the collection of IDs of active <see cref="WebSocketService"/> instances
    /// managed by the <see cref="WebSocketServiceManager"/>.
    /// </summary>
    /// <value>
    /// An IEnumerable&lt;string&gt; that contains the collection of IDs
    /// of active <see cref="WebSocketService"/> instances.
    /// </value>
    public IEnumerable<string> ActiveIDs {
      get {
        return from result in Broadping ()
               where result.Value
               select result.Key;
      }
    }

    /// <summary>
    /// Gets the number of <see cref="WebSocketService"/> instances
    /// managed by the <see cref="WebSocketServiceManager"/>.
    /// </summary>
    /// <value>
    /// An <see cref="int"/> that contains the number of <see cref="WebSocketService"/> instances
    /// managed by the <see cref="WebSocketServiceManager"/>.
    /// </value>
    public int Count {
      get {
        lock (_sync)
        {
          return _services.Count;
        }
      }
    }

    /// <summary>
    /// Gets the collection of IDs of <see cref="WebSocketService"/> instances
    /// managed by the <see cref="WebSocketServiceManager"/>.
    /// </summary>
    /// <value>
    /// An IEnumerable&lt;string&gt; that contains the collection of IDs
    /// of <see cref="WebSocketService"/> instances.
    /// </value>
    public IEnumerable<string> IDs {
      get {
        lock (_sync)
        {
          return _services.Keys;
        }
      }
    }

    /// <summary>
    /// Gets the collection of IDs of inactive <see cref="WebSocketService"/> instances
    /// managed by the <see cref="WebSocketServiceManager"/>.
    /// </summary>
    /// <value>
    /// An IEnumerable&lt;string&gt; that contains the collection of IDs
    /// of inactive <see cref="WebSocketService"/> instances.
    /// </value>
    public IEnumerable<string> InactiveIDs {
      get {
        return from result in Broadping ()
               where !result.Value
               select result.Key;
      }
    }

    /// <summary>
    /// Gets the <see cref="WebSocketService"/> instance with the specified <paramref name="id"/>
    /// from the <see cref="WebSocketServiceManager"/>.
    /// </summary>
    /// <value>
    /// A <see cref="WebSocketService"/> instance with <paramref name="id"/> if it is successfully found;
    /// otherwise, <see langword="null"/>.
    /// </value>
    /// <param name="id">
    /// A <see cref="string"/> that contains an ID to find.
    /// </param>
    public WebSocketService this [string id] {
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
            return _services [id];
          }
          catch {
            _logger.Error ("'id' not found.\nid: " + id);
            return null;
          }
        }
      }
    }

    /// <summary>
    /// Gets a value indicating whether the <see cref="WebSocketServiceManager"/> cleans up
    /// the inactive <see cref="WebSocketService"/> instances periodically.
    /// </summary>
    /// <value>
    /// <c>true</c> if the <see cref="WebSocketServiceManager"/> cleans up the inactive
    /// <see cref="WebSocketService"/> instances every 60 seconds; otherwise, <c>false</c>.
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
    /// Gets the collection of the <see cref="WebSocketService"/> instances
    /// managed by the <see cref="WebSocketServiceManager"/>.
    /// </summary>
    /// <value>
    /// An IEnumerable&lt;WebSocketService&gt; that contains the collection of
    /// the <see cref="WebSocketService"/> instances.
    /// </value>
    public IEnumerable<WebSocketService> ServiceInstances {
      get {
        lock (_sync)
        {
          return _services.Values;
        }
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
      var copied = copy ();
      var services = copied.Values.GetEnumerator ();

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
      var copied = copy ();
      var services = copied.Values.GetEnumerator ();

      Action completed = null;
      completed = () =>
      {
        if (services.MoveNext ())
          services.Current.SendAsync (data, completed);
      };

      if (services.MoveNext ())
        services.Current.SendAsync (data, completed);
    }

    private Dictionary<string, WebSocketService> copy ()
    {
      lock (_sync)
      {
        return new Dictionary<string, WebSocketService> (_services);
      }
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

    internal string Add (WebSocketService service)
    {
      lock (_sync)
      {
        if (_stopped)
          return null;

        var id = createID ();
        _services.Add (id, service);

        return id;
      }
    }

    /// <summary>
    /// Broadcasts the specified array of <see cref="byte"/> to the clients of every <see cref="WebSocketService"/>
    /// instances managed by the <see cref="WebSocketServiceManager"/>.
    /// </summary>
    /// <param name="data">
    /// An array of <see cref="byte"/> to broadcast.
    /// </param>
    internal void Broadcast (byte [] data)
    {
      if (_stopped)
        broadcast (data);
      else
        broadcastAsync (data);
    }

    /// <summary>
    /// Broadcasts the specified <see cref="string"/> to the clients of every <see cref="WebSocketService"/>
    /// instances managed by the <see cref="WebSocketServiceManager"/>.
    /// </summary>
    /// <param name="data">
    /// A <see cref="string"/> to broadcast.
    /// </param>
    internal void Broadcast (string data)
    {
      if (_stopped)
        broadcast (data);
      else
        broadcastAsync (data);
    }

    /// <summary>
    /// Sends Pings to the clients of every <see cref="WebSocketService"/> instances managed by
    /// the <see cref="WebSocketServiceManager"/>.
    /// </summary>
    /// <returns>
    /// A Dictionary&lt;string, bool&gt; that contains the collection of pairs of ID and value indicating
    /// whether each <see cref="WebSocketService"/> instance received a Pong from the client in a time.
    /// </returns>
    internal Dictionary<string, bool> Broadping ()
    {
      var result = new Dictionary<string, bool> ();
      foreach (var session in copy ())
        result.Add (session.Key, session.Value.Ping ());

      return result;
    }

    /// <summary>
    /// Sends Pings with the specified <paramref name="message"/> to the clients of every <see cref="WebSocketService"/>
    /// instances managed by the <see cref="WebSocketServiceManager"/>.
    /// </summary>
    /// <returns>
    /// A Dictionary&lt;string, bool&gt; that contains the collection of pairs of ID and value indicating
    /// whether each <see cref="WebSocketService"/> instance received a Pong from the client in a time.
    /// </returns>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to send.
    /// </param>
    internal Dictionary<string, bool> Broadping (string message)
    {
      var result = new Dictionary<string, bool> ();
      foreach (var session in copy ())
        result.Add (session.Key, session.Value.Ping (message));

      return result;
    }

    /// <summary>
    /// Sends a Ping to the client of the <see cref="WebSocketService"/> instance
    /// with the specified <paramref name="id"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the <see cref="WebSocketService"/> instance receives a Pong from the client
    /// in a time; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="id">
    /// A <see cref="string"/> that contains an ID that represents the destination for the Ping.
    /// </param>
    internal bool PingTo (string id)
    {
      WebSocketService service;
      if (!TryGetServiceInstance (id, out service))
      {
        _logger.Error (
          "The WebSocket service instance with the specified ID not found.\nID: " + id);
        return false;
      }

      return service.Ping ();
    }

    /// <summary>
    /// Sends a Ping with the specified <paramref name="message"/> to the client of the <see cref="WebSocketService"/>
    /// instance with the specified <paramref name="id"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the <see cref="WebSocketService"/> instance receives a Pong from the client
    /// in a time; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to send.
    /// </param>
    /// <param name="id">
    /// A <see cref="string"/> that contains an ID that represents the destination for the Ping.
    /// </param>
    internal bool PingTo (string message, string id)
    {
      WebSocketService service;
      if (!TryGetServiceInstance (id, out service))
      {
        _logger.Error (
          "The WebSocket service instance with the specified ID not found.\nID: " + id);
        return false;
      }

      return service.Ping (message);
    }

    internal bool Remove (string id)
    {
      lock (_sync)
      {
        return _services.Remove (id);
      }
    }

    /// <summary>
    /// Sends a binary data to the client of the <see cref="WebSocketService"/> instance
    /// with the specified <paramref name="id"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="data"/> is successfully sent; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="data">
    /// An array of <see cref="byte"/> that contains a binary data to send.
    /// </param>
    /// <param name="id">
    /// A <see cref="string"/> that contains an ID that represents the destination for the data.
    /// </param>
    internal bool SendTo (byte [] data, string id)
    {
      WebSocketService service;
      if (!TryGetServiceInstance (id, out service))
      {
        _logger.Error (
          "The WebSocket service instance with the specified ID not found.\nID: " + id);
        return false;
      }

      service.Send (data);
      return true;
    }

    /// <summary>
    /// Sends a text data to the client of the <see cref="WebSocketService"/> instance
    /// with the specified <paramref name="id"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="data"/> is successfully sent; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="data">
    /// A <see cref="string"/> that contains a text data to send.
    /// </param>
    /// <param name="id">
    /// A <see cref="string"/> that contains an ID that represents the destination for the data.
    /// </param>
    internal bool SendTo (string data, string id)
    {
      WebSocketService service;
      if (!TryGetServiceInstance (id, out service))
      {
        _logger.Error (
          "The WebSocket service instance with the specified ID not found.\nID: " + id);
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
        foreach (var service in copy ().Values)
          service.Stop ();
      }
    }

    internal void Stop (ushort code, string reason)
    {
      stopSweepTimer ();
      lock (_sync)
      {
        if (_stopped)
          return;

        _stopped = true;
        foreach (var service in copy ().Values)
          service.Stop (code, reason);
      }
    }

    internal void StopServiceInstance (string id)
    {
      WebSocketService service;
      if (!TryGetServiceInstance (id, out service))
      {
        _logger.Error (
          "The WebSocket service instance with the specified ID not found.\nID: " + id);
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
          "The WebSocket service instance with the specified ID not found.\nID: " + id);
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
          "The WebSocket service instance with the specified ID not found.\nID: " + id);
        return;
      }

      service.Stop (code, reason);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Cleans up the inactive <see cref="WebSocketService"/> instances.
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
            if (_services.TryGetValue (id, out service))
            {
              var state = service.WebSocket.ReadyState;
              if (state == WsState.OPEN)
                service.Stop (CloseStatusCode.ABNORMAL, String.Empty);
              else if (state == WsState.CLOSING)
                continue;
              else
                _services.Remove (id);
            }
          }
        }

        _sweeping = false;
      }
    }

    /// <summary>
    /// Tries to get the <see cref="WebSocketService"/> instance with the specified <paramref name="id"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the <see cref="WebSocketService"/> instance with <paramref name="id"/>
    /// is successfully found; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="id">
    /// A <see cref="string"/> that contains an ID to find.
    /// </param>
    /// <param name="service">
    /// When this method returns, contains a <see cref="WebSocketService"/> instance with <param name="id"/>
    /// if it is successfully found; otherwise, <see langword="null"/>.
    /// </param>
    public bool TryGetServiceInstance (string id, out WebSocketService service)
    {
      lock (_sync)
      {
        return _services.TryGetValue (id, out service);
      }
    }

    #endregion
  }
}
