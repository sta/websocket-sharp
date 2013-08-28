#region License
/*
 * WebSocketServiceHostManager.cs
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
using System.Text;

namespace WebSocketSharp.Server
{
  /// <summary>
  /// Manages the collection of the WebSocket service hosts.
  /// </summary>
  public class WebSocketServiceHostManager
  {
    #region Private Fields

    private volatile bool                    _keepClean;
    private Logger                           _logger;
    private Dictionary<string, IServiceHost> _serviceHosts;
    private object                           _sync;

    #endregion

    #region Internal Constructors

    internal WebSocketServiceHostManager ()
      : this (new Logger ())
    {
    }

    internal WebSocketServiceHostManager (Logger logger)
    {
      _logger = logger;
      _keepClean = true;
      _serviceHosts = new Dictionary<string, IServiceHost> ();
      _sync = new object ();
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the connection count to the WebSocket services managed by the <see cref="WebSocketServiceHostManager"/>.
    /// </summary>
    /// <value>
    /// An <see cref="int"/> that contains the connection count.
    /// </value>
    public int ConnectionCount {
      get {
        var count = 0;
        foreach (var host in ServiceHosts)
          count += host.ConnectionCount;

        return count;
      }
    }

    /// <summary>
    /// Gets the number of the WebSocket services managed by the <see cref="WebSocketServiceHostManager"/>.
    /// </summary>
    /// <value>
    /// An <see cref="int"/> that contains the number of the WebSocket services.
    /// </value>
    public int ServiceCount {
      get {
        lock (_sync)
        {
          return _serviceHosts.Count;
        }
      }
    }

    /// <summary>
    /// Gets the collection of paths to the WebSocket services managed by the <see cref="WebSocketServiceHostManager"/>.
    /// </summary>
    /// <value>
    /// An IEnumerable&lt;string&gt; that contains the collection of paths.
    /// </value>
    public IEnumerable<string> ServicePaths {
      get {
        lock (_sync)
        {
          return _serviceHosts.Keys;
        }
      }
    }

    #endregion

    #region Internal Properties

    internal bool KeepClean {
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

    internal IEnumerable<IServiceHost> ServiceHosts {
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

    #region Internal Methods

    internal void Add (string servicePath, IServiceHost serviceHost)
    {
      lock (_sync)
      {
        IServiceHost host;
        if (_serviceHosts.TryGetValue (servicePath, out host))
        {
          _logger.Error (
            "The WebSocket service host with the specified path already exists.\npath: " + servicePath);
          return;
        }

        _serviceHosts.Add (servicePath.UrlDecode (), serviceHost);
      }
    }

    internal bool Remove (string servicePath)
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

    internal void Stop ()
    {
      lock (_sync)
      {
        foreach (var host in _serviceHosts.Values)
          host.Stop ();

        _serviceHosts.Clear ();
      }
    }

    internal void Stop (ushort code, string reason)
    {
      lock (_sync)
      {
        foreach (var host in _serviceHosts.Values)
          host.Stop (code, reason);

        _serviceHosts.Clear ();
      }
    }

    internal bool TryGetServiceHost (string servicePath, out IServiceHost serviceHost)
    {
      lock (_sync)
      {
        return _serviceHosts.TryGetValue (servicePath, out serviceHost);
      }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Broadcasts the specified array of <see cref="byte"/> to all clients of the WebSocket services.
    /// </summary>
    /// <param name="data">
    /// An array of <see cref="byte"/> to broadcast.
    /// </param>
    public void Broadcast (byte [] data)
    {
      if (data == null)
      {
        _logger.Error ("'data' must not be null.");
        return;
      }

      foreach (var host in ServiceHosts)
        host.Broadcast (data);
    }

    /// <summary>
    /// Broadcasts the specified <see cref="string"/> to all clients of the WebSocket services.
    /// </summary>
    /// <param name="data">
    /// A <see cref="string"/> to broadcast.
    /// </param>
    public void Broadcast (string data)
    {
      if (data == null)
      {
        _logger.Error ("'data' must not be null.");
        return;
      }

      foreach (var host in ServiceHosts)
        host.Broadcast (data);
    }

    /// <summary>
    /// Broadcasts the specified array of <see cref="byte"/> to all clients of the WebSocket service
    /// with the specified <paramref name="servicePath"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="data"/> is broadcasted; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="data">
    /// An array of <see cref="byte"/> to broadcast.
    /// </param>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    public bool BroadcastTo (byte [] data, string servicePath)
    {
      var msg = data == null
              ? "'data' must not be null."
              : servicePath.IsNullOrEmpty ()
                ? "'servicePath' must not be null or empty."
                : null;

      if (msg != null)
      {
        _logger.Error (msg);
        return false;
      }

      IServiceHost host;
      if (!TryGetServiceHost (servicePath, out host))
      {
        _logger.Error ("The WebSocket service with the specified path not found.\npath: " + servicePath);
        return false;
      }

      host.Broadcast (data);
      return true;
    }

    /// <summary>
    /// Broadcasts the specified <see cref="string"/> to all clients of the WebSocket service
    /// with the specified <paramref name="servicePath"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="data"/> is broadcasted; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="data">
    /// A <see cref="string"/> to broadcast.
    /// </param>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    public bool BroadcastTo (string data, string servicePath)
    {
      var msg = data == null
              ? "'data' must not be null."
              : servicePath.IsNullOrEmpty ()
                ? "'servicePath' must not be null or empty."
                : null;

      if (msg != null)
      {
        _logger.Error (msg);
        return false;
      }

      IServiceHost host;
      if (!TryGetServiceHost (servicePath, out host))
      {
        _logger.Error ("The WebSocket service with the specified path not found.\npath: " + servicePath);
        return false;
      }

      host.Broadcast (data);
      return true;
    }

    /// <summary>
    /// Sends Pings with the specified <paramref name="message"/> to all clients of the WebSocket services.
    /// </summary>
    /// <returns>
    /// A Dictionary&lt;string, Dictionary&lt;string, bool&gt;&gt; that contains the collection of
    /// service paths and pairs of session ID and value indicating whether each WebSocket service
    /// received the Pong from each client in a time.
    /// </returns>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to send.
    /// </param>
    public Dictionary<string, Dictionary<string, bool>> Broadping (string message)
    {
      if (!message.IsNullOrEmpty ())
      {
        var len = Encoding.UTF8.GetBytes (message).Length;
        if (len > 125)
        {
          _logger.Error ("The payload length of a Ping frame must be 125 bytes or less.");
          return null;
        }
      }

      var result = new Dictionary<string, Dictionary<string, bool>> ();
      foreach (var service in copy ())
        result.Add (service.Key, service.Value.Broadping (message));

      return result;
    }

    /// <summary>
    /// Sends Pings with the specified <paramref name="message"/> to all clients of the WebSocket service
    /// with the specified <paramref name="servicePath"/>.
    /// </summary>
    /// <returns>
    /// A Dictionary&lt;string, bool&gt; that contains the collection of pairs of session ID and value
    /// indicating whether the WebSocket service received the Pong from each client in a time.
    /// If the WebSocket service is not found, returns <see langword="null"/>.
    /// </returns>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to send.
    /// </param>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    public Dictionary<string, bool> BroadpingTo (string message, string servicePath)
    {
      if (message == null)
        message = String.Empty;

      var msg = Encoding.UTF8.GetBytes (message).Length > 125
              ? "The payload length of a Ping frame must be 125 bytes or less."
              : servicePath.IsNullOrEmpty ()
                ? "'servicePath' must not be null or empty."
                : null;

      if (msg != null)
      {
        _logger.Error (msg);
        return null;
      }

      IServiceHost host;
      if (!TryGetServiceHost (servicePath, out host))
      {
        _logger.Error ("The WebSocket service with the specified path not found.\npath: " + servicePath);
        return null;
      }

      return host.Broadping (message);
    }

    /// <summary>
    /// Gets the connection count to the WebSocket service with the specified <paramref name="servicePath"/>.
    /// </summary>
    /// <returns>
    /// An <see cref="int"/> that contains the connection count if the WebSocket service is successfully found;
    /// otherwise, <c>-1</c>.
    /// </returns>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    public int GetConnectionCount (string servicePath)
    {
      if (servicePath.IsNullOrEmpty ())
      {
        _logger.Error ("'servicePath' must not be null or empty.");
        return -1;
      }

      IServiceHost host;
      if (!TryGetServiceHost (servicePath, out host))
      {
        _logger.Error ("The WebSocket service with the specified path not found.\npath: " + servicePath);
        return -1;
      }

      return host.ConnectionCount;
    }

    /// <summary>
    /// Sends a Ping with the specified <paramref name="message"/> to the client associated with
    /// the specified <paramref name="id"/> and <paramref name="servicePath"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the WebSocket service with <paramref name="servicePath"/> receives a Pong
    /// from the client in a time; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to send.
    /// </param>
    /// <param name="id">
    /// A <see cref="string"/> that contains an ID that represents the destination for the Ping.
    /// </param>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    public bool PingTo (string message, string id, string servicePath)
    {
      if (message == null)
        message = String.Empty;

      var msg = Encoding.UTF8.GetBytes (message).Length > 125
              ? "The payload length of a Ping frame must be 125 bytes or less."
              : id.IsNullOrEmpty ()
                ? "'id' must not be null or empty."
                : servicePath.IsNullOrEmpty ()
                  ? "'servicePath' must not be null or empty."
                  : null;

      if (msg != null)
      {
        _logger.Error (msg);
        return false;
      }

      IServiceHost host;
      if (!TryGetServiceHost (servicePath, out host))
      {
        _logger.Error ("The WebSocket service with the specified path not found.\npath: " + servicePath);
        return false;
      }

      return host.PingTo (id, message);
    }

    /// <summary>
    /// Sends a binary data to the client associated with the specified <paramref name="id"/> and
    /// <paramref name="servicePath"/>.
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
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    public bool SendTo (byte [] data, string id, string servicePath)
    {
      var msg = data == null
              ? "'data' must not be null."
              : id.IsNullOrEmpty ()
                ? "'id' must not be null or empty."
                : servicePath.IsNullOrEmpty ()
                  ? "'servicePath' must not be null or empty."
                  : null;

      if (msg != null)
      {
        _logger.Error (msg);
        return false;
      }

      IServiceHost host;
      if (!TryGetServiceHost (servicePath, out host))
      {
        _logger.Error ("The WebSocket service with the specified path not found.\npath: " + servicePath);
        return false;
      }

      return host.SendTo (id, data);
    }

    /// <summary>
    /// Sends a text data to the client associated with the specified <paramref name="id"/> and
    /// <paramref name="servicePath"/>.
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
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    public bool SendTo (string data, string id, string servicePath)
    {
      var msg = data == null
              ? "'data' must not be null."
              : id.IsNullOrEmpty ()
                ? "'id' must not be null or empty."
                : servicePath.IsNullOrEmpty ()
                  ? "'servicePath' must not be null or empty."
                  : null;

      if (msg != null)
      {
        _logger.Error (msg);
        return false;
      }

      IServiceHost host;
      if (!TryGetServiceHost (servicePath, out host))
      {
        _logger.Error ("The WebSocket service with the specified path not found.\npath: " + servicePath);
        return false;
      }

      return host.SendTo (id, data);
    }

    #endregion
  }
}
