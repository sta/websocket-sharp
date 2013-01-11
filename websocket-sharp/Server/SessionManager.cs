#region MIT License
/*
 * SessionManager.cs
 *
 * The MIT License
 *
 * Copyright (c) 2012 sta.blockhead
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
using WebSocketSharp.Frame;

namespace WebSocketSharp.Server {

  public class SessionManager {

    #region Private Fields

    private object                               _forSweep;
    private volatile bool                        _isStopped;
    private volatile bool                        _isSweeping;
    private Dictionary<string, WebSocketService> _sessions;
    private Timer                                _sweepTimer;
    private object                               _syncRoot;

    #endregion

    #region Public Constructor

    public SessionManager()
    {
      _forSweep   = new object();
      _isStopped  = false;
      _isSweeping = false;
      _sessions   = new Dictionary<string, WebSocketService>();
      _sweepTimer = new Timer(60 * 1000);
      _sweepTimer.Elapsed += (sender, e) =>
      {
        Sweep();
      };
      _syncRoot   = new object();

      startSweepTimer();
    }

    #endregion

    #region Properties

    public IEnumerable<string> ActiveID {
      get {
        return from result in Broadping(String.Empty)
               where result.Value
               select result.Key;
      }
    }

    public int Count {
      get {
        lock (_syncRoot)
        {
          return _sessions.Count;
        }
      }
    } 

    public IEnumerable<string> InactiveID {
      get {
        return from result in Broadping(String.Empty)
               where !result.Value
               select result.Key;
      }
    }

    public IEnumerable<string> ID {
      get {
        lock (_syncRoot)
        {
          return _sessions.Keys;
        }
      }
    }

    public bool Sweeped {
      get {
        return _sweepTimer.Enabled;
      }

      set {
        if (value && !_isStopped)
          startSweepTimer();

        if (!value)
          stopSweepTimer();
      }
    }

    public object SyncRoot {
      get {
        return _syncRoot;
      }
    }

    #endregion

    #region Private Methods

    private void broadcast(byte[] data)
    {
      lock (_syncRoot)
      {
        foreach (var service in _sessions.Values)
          service.Send(data);
      }
    }

    private void broadcast(string data)
    {
      lock (_syncRoot)
      {
        foreach (var service in _sessions.Values)
          service.Send(data);
      }
    }

    private void broadcastAsync(byte[] data)
    {
      var sessions = copySessions();
      var services = sessions.Values.GetEnumerator();

      Action completed = null;
      completed = () =>
      {
        if (services.MoveNext())
          services.Current.SendAsync(data, completed);
      };

      if (services.MoveNext())
        services.Current.SendAsync(data, completed);
    }

    private void broadcastAsync(string data)
    {
      var sessions = copySessions();
      var services = sessions.Values.GetEnumerator();

      Action completed = null;
      completed = () =>
      {
        if (services.MoveNext())
          services.Current.SendAsync(data, completed);
      };

      if (services.MoveNext())
        services.Current.SendAsync(data, completed);
    }

    private Dictionary<string, WebSocketService> copySessions()
    {
      lock (_syncRoot)
      {
        return new Dictionary<string, WebSocketService>(_sessions);
      }
    }

    private string createID()
    {
      return Guid.NewGuid().ToString("N");
    }

    private void startSweepTimer()
    {
      if (!Sweeped)
        _sweepTimer.Start();
    }

    private void stopSweepTimer()
    {
      if (Sweeped)
        _sweepTimer.Stop();
    }

    #endregion

    #region Public Methods

    public string Add(WebSocketService service)
    {
      lock (_syncRoot)
      {
        if (_isStopped)
          return null;

        var id = createID();
        _sessions.Add(id, service);

        return id;
      }
    }

    public void Broadcast(byte[] data)
    {
      if (_isStopped)
        broadcast(data);
      else
        broadcastAsync(data);
    }

    public void Broadcast(string data)
    {
      if (_isStopped)
        broadcast(data);
      else
        broadcastAsync(data);
    }

    public Dictionary<string, bool> Broadping(string message)
    {
      var result = new Dictionary<string, bool>();
      foreach (var session in copySessions())
        result.Add(session.Key, session.Value.Ping(message));

      return result;
    }

    public bool Remove(string id)
    {
      lock (_syncRoot)
      {
        return _sessions.Remove(id);
      }
    }

    public bool TryGetByID(string id, out WebSocketService service)
    {
      lock (_syncRoot)
      {
        return _sessions.TryGetValue(id, out service);
      }
    }

    public void Stop()
    {
      Stop(CloseStatusCode.NORMAL, String.Empty);
    }

    public void Stop(CloseStatusCode code, string reason)
    {
      stopSweepTimer();
      lock (_syncRoot)
      {
        if (_isStopped)
          return;

        _isStopped = true;
        foreach (var service in copySessions().Values)
          service.Stop(code, reason);
      }
    }

    public void Sweep()
    {
      if (_isStopped || _isSweeping || Count == 0)
        return;

      lock (_forSweep)
      {
        _isSweeping = true;
        foreach (var id in InactiveID)
        {
          lock (_syncRoot)
          {
            if (_isStopped)
            {
              _isSweeping = false;
              return;
            }

            WebSocketService service;
            if (TryGetByID(id, out service))
              service.Stop(CloseStatusCode.ABNORMAL, String.Empty);
          }
        }

        _isSweeping = false;
      }
    }

    #endregion
  }
}
