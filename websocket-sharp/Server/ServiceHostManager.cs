#region License
/*
 * ServiceHostManager.cs
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

namespace WebSocketSharp.Server
{
  internal class ServiceHostManager
  {
    #region Private Fields

    private volatile bool                    _keepClean;
    private Logger                           _logger;
    private Dictionary<string, IServiceHost> _serviceHosts;
    private object                           _sync;

    #endregion

    #region Public Constructors

    public ServiceHostManager ()
      : this (new Logger ())
    {
    }

    public ServiceHostManager (Logger logger)
    {
      _logger = logger;
      _keepClean = true;
      _serviceHosts = new Dictionary<string, IServiceHost> ();
      _sync = new object ();
    }

    #endregion

    #region Public Properties

    public int ConnectionCount {
      get {
        var count = 0;
        foreach (var host in ServiceHosts)
          count += host.ConnectionCount;

        return count;
      }
    }

    public int Count {
      get {
        lock (_sync)
        {
          return _serviceHosts.Count;
        }
      }
    }

    public bool KeepClean {
      get {
        return _keepClean;
      }

      set {
        lock (_sync)
        {
          if (_keepClean ^ value)
          {
            _keepClean = value;
            foreach (var host in _serviceHosts.Values)
              host.KeepClean = value;
          }
        }
      }
    }

    public IEnumerable<string> Paths {
      get {
        lock (_sync)
        {
          return _serviceHosts.Keys;
        }
      }
    }

    public IEnumerable<IServiceHost> ServiceHosts {
      get {
        lock (_sync)
        {
          return _serviceHosts.Values;
        }
      }
    }

    #endregion

    #region Private Methods

    private Dictionary<string, IServiceHost> copy ()
    {
      lock (_sync)
      {
        return new Dictionary<string, IServiceHost> (_serviceHosts);
      }
    }

    #endregion

    #region Public Methods

    public void Add (string servicePath, IServiceHost serviceHost)
    {
      lock (_sync)
      {
        IServiceHost host;
        if (_serviceHosts.TryGetValue (servicePath, out host))
        {
          _logger.Error (
            "The WebSocket service host with the specified path found.\npath: " + servicePath);
          return;
        }

        _serviceHosts.Add (servicePath.UrlDecode (), serviceHost);
      }
    }

    public void Broadcast (byte [] data)
    {
      foreach (var host in ServiceHosts)
        host.Broadcast (data);
    }

    public void Broadcast (string data)
    {
      foreach (var host in ServiceHosts)
        host.Broadcast (data);
    }

    public bool BroadcastTo (string servicePath, byte [] data)
    {
      IServiceHost host;
      if (TryGetServiceHost (servicePath, out host))
      {
        host.Broadcast (data);
        return true;
      }

      _logger.Error (
        "The WebSocket service host with the specified path not found.\npath: " + servicePath);
      return false;
    }

    public bool BroadcastTo (string servicePath, string data)
    {
      IServiceHost host;
      if (TryGetServiceHost (servicePath, out host))
      {
        host.Broadcast (data);
        return true;
      }

      _logger.Error (
        "The WebSocket service host with the specified path not found.\npath: " + servicePath);
      return false;
    }

    public Dictionary<string, Dictionary<string, bool>> Broadping (string message)
    {
      var result = new Dictionary<string, Dictionary<string, bool>> ();
      foreach (var service in copy ())
        result.Add (service.Key, service.Value.Broadping (message));

      return result;
    }

    public Dictionary<string, bool> BroadpingTo (string servicePath, string message)
    {
      IServiceHost host;
      if (TryGetServiceHost (servicePath, out host))
        return host.Broadping (message);

      _logger.Error (
        "The WebSocket service host with the specified path not found.\npath: " + servicePath);
      return null;
    }

    public int GetConnectionCount (string servicePath)
    {
      IServiceHost host;
      if (TryGetServiceHost (servicePath, out host))
        return host.ConnectionCount;

      _logger.Error (
        "The WebSocket service host with the specified path not found.\npath: " + servicePath);
      return -1;
    }

    public bool PingTo (string servicePath, string id, string message)
    {
      IServiceHost host;
      if (TryGetServiceHost (servicePath, out host))
        return host.PingTo (id, message);

      _logger.Error (
        "The WebSocket service host with the specified path not found.\npath: " + servicePath);
      return false;
    }

    public bool Remove (string servicePath)
    {
      IServiceHost host;
      lock (_sync)
      {
        if (!_serviceHosts.TryGetValue (servicePath, out host))
        {
          _logger.Error (
            "The WebSocket service host with the specified path not found.\npath: " + servicePath);
          return false;
        }

        _serviceHosts.Remove (servicePath);
      }

      host.Stop ((ushort) CloseStatusCode.AWAY, String.Empty);
      return true;
    }

    public bool SendTo (string servicePath, string id, byte [] data)
    {
      IServiceHost host;
      if (TryGetServiceHost (servicePath, out host))
        return host.SendTo (id, data);

      _logger.Error (
        "The WebSocket service host with the specified path not found.\npath: " + servicePath);
      return false;
    }

    public bool SendTo (string servicePath, string id, string data)
    {
      IServiceHost host;
      if (TryGetServiceHost (servicePath, out host))
        return host.SendTo (id, data);

      _logger.Error (
        "The WebSocket service host with the specified path not found.\npath: " + servicePath);
      return false;
    }

    public void Stop ()
    {
      lock (_sync)
      {
        foreach (var host in _serviceHosts.Values)
          host.Stop ();

        _serviceHosts.Clear ();
      }
    }

    public void Stop (ushort code, string reason)
    {
      lock (_sync)
      {
        foreach (var host in _serviceHosts.Values)
          host.Stop (code, reason);

        _serviceHosts.Clear ();
      }
    }

    public bool TryGetServiceHost (string servicePath, out IServiceHost serviceHost)
    {
      lock (_sync)
      {
        return _serviceHosts.TryGetValue (servicePath, out serviceHost);
      }
    }

    #endregion
  }
}
