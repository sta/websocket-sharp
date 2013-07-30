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
    private Dictionary<string, WebSocketService> _services;
    private volatile bool                        _stopped;
    private volatile bool                        _sweeping;
    private Timer                                _sweepTimer;
    private object                               _sync;

    #endregion

    #region Internal Constructors

    internal WebSocketServiceManager ()
    {
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
        return from result in Broadping (String.Empty)
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
        return from result in Broadping (String.Empty)
               where !result.Value
               select result.Key;
      }
    }

    /// <summary>
    /// Gets a value indicating whether the <see cref="WebSocketServiceManager"/> cleans up
    /// the inactive <see cref="WebSocketService"/> instances periodically.
    /// </summary>
    /// <value>
    /// <c>true</c> if the <see cref="WebSocketServiceManager"/> cleans up
    /// the inactive <see cref="WebSocketService"/> instances every 60 seconds; otherwise, <c>false</c>.
    /// </value>
    public bool Sweeping {
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

    #endregion

    #region Private Methods

    private void broadcast (byte [] data)
    {
      lock (_sync)
      {
        foreach (var service in _services.Values)
          service.Send (data);
      }
    }

    private void broadcast (string data)
    {
      lock (_sync)
      {
        foreach (var service in _services.Values)
          service.Send (data);
      }
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

    private void stop (ushort code, string reason, bool ignoreArgs)
    {
      stopSweepTimer ();
      lock (_sync)
      {
        if (_stopped)
          return;

        _stopped = true;
        foreach (var service in copy ().Values)
          if (ignoreArgs)
            service.Stop ();
          else
            service.Stop (code, reason);
      }
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

    internal bool Remove (string id)
    {
      lock (_sync)
      {
        return _services.Remove (id);
      }
    }

    internal void Stop ()
    {
      stop (0, null, true);
    }

    internal void Stop (ushort code, string reason)
    {
      stop (code, reason, false);
    }

    internal void Stop (CloseStatusCode code, string reason)
    {
      Stop ((ushort) code, reason);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Broadcasts the specified array of <see cref="byte"/> to the clients of every
    /// <see cref="WebSocketService"/> instances managed by the <see cref="WebSocketServiceManager"/>.
    /// </summary>
    /// <param name="data">
    /// An array of <see cref="byte"/> to broadcast.
    /// </param>
    public void Broadcast (byte [] data)
    {
      if (_stopped)
        broadcast (data);
      else
        broadcastAsync (data);
    }

    /// <summary>
    /// Broadcasts the specified <see cref="string"/> to the clients of every
    /// <see cref="WebSocketService"/> instances managed by the <see cref="WebSocketServiceManager"/>.
    /// </summary>
    /// <param name="data">
    /// A <see cref="string"/> to broadcast.
    /// </param>
    public void Broadcast (string data)
    {
      if (_stopped)
        broadcast (data);
      else
        broadcastAsync (data);
    }

    /// <summary>
    /// Sends Pings with the specified <see cref="string"/> to the clients of every
    /// <see cref="WebSocketService"/> instances managed by the <see cref="WebSocketServiceManager"/>.
    /// </summary>
    /// <returns>
    /// A Dictionary&lt;string, bool&gt; that contains the collection of IDs and values indicating
    /// whether each <see cref="WebSocketService"/> instances received a Pong in a time.
    /// </returns>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to send.
    /// </param>
    public Dictionary<string, bool> Broadping (string message)
    {
      var result = new Dictionary<string, bool> ();
      foreach (var session in copy ())
        result.Add (session.Key, session.Value.Ping (message));

      return result;
    }

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
            {
              _sweeping = false;
              return;
            }

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
    /// Tries to get the <see cref="WebSocketService"/> associated with the specified ID.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the <see cref="WebSocketServiceManager"/> manages the <see cref="WebSocketService"/>
    /// with <paramref name="id"/>; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="id">
    /// A <see cref="string"/> that contains an ID to find.
    /// </param>
    /// <param name="service">
    /// When this method returns, contains a <see cref="WebSocketService"/> with <paramref name="id"/>
    /// if it is found; otherwise, <see langword="null"/>.
    /// </param>
    public bool TryGetWebSocketService (string id, out WebSocketService service)
    {
      lock (_sync)
      {
        return _services.TryGetValue (id, out service);
      }
    }

    #endregion
  }
}
