#region MIT License
/**
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
using WebSocketSharp.Frame;

namespace WebSocketSharp.Server {

  public class SessionManager {

    #region Private Fields

    private bool                                 _isStopped;
    private Dictionary<string, WebSocketService> _sessions;
    private object                               _syncRoot;

    #endregion

    #region Public Constructor

    public SessionManager()
    {
      _isStopped = false;
      _sessions  = new Dictionary<string, WebSocketService>();
      _syncRoot  = new object();
    }

    #endregion

    #region Properties

    public int Count {
      get {
        lock (_syncRoot)
        {
          return _sessions.Count;
        }
      }
    } 

    public object SyncRoot {
      get {
        return _syncRoot;
      }
    }

    #endregion

    #region Private Method

    private Dictionary<string, WebSocketService> copySessions()
    {
      lock (_syncRoot)
      {
        return new Dictionary<string, WebSocketService>(_sessions);
      }
    }

    private string getNewID()
    {
      return Guid.NewGuid().ToString("N");
    }

    #endregion

    #region Public Methods

    public string Add(WebSocketService service)
    {
      lock (_syncRoot)
      {
        if (_isStopped)
          return null;

        var id = getNewID();
        _sessions.Add(id, service);

        return id;
      }
    }

    public void Broadcast(byte[] data)
    {
      lock (_syncRoot)
      {
        foreach (var service in _sessions.Values)
          service.SendAsync(data);
      }
    }

    public void Broadcast(string data)
    {
      lock (_syncRoot)
      {
        foreach (var service in _sessions.Values)
          service.SendAsync(data);
      }
    }

    public Dictionary<string, bool> Broadping(string message)
    {
      var result = new Dictionary<string, bool>();
      foreach (var session in copySessions())
        result.Add(session.Key, session.Value.Ping(message));

      return result;
    }

    public IEnumerable<string> GetIDs()
    {
      lock (_syncRoot)
      {
        return _sessions.Keys;
      }
    }

    public bool Remove(string id)
    {
      lock (_syncRoot)
      {
        if (_isStopped)
          return false;

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
      lock (_syncRoot)
      {
        if (_isStopped)
          return;

        _isStopped = true;
        foreach (var service in _sessions.Values)
          service.Stop(code, reason);

        _sessions.Clear();
      }
    }

    #endregion
  }
}
