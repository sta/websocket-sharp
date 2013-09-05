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
using WebSocketSharp.Net;

namespace WebSocketSharp.Server
{
  /// <summary>
  /// Manages the collection of the WebSocket service hosts.
  /// </summary>
  public class WebSocketServiceHostManager
  {
    #region Private Fields

    private volatile bool                             _keepClean;
    private Logger                                    _logger;
    private Dictionary<string, IWebSocketServiceHost> _serviceHosts;
    private object                                    _sync;

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
      _serviceHosts = new Dictionary<string, IWebSocketServiceHost> ();
      _sync = new object ();
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

    internal IEnumerable<IWebSocketServiceHost> ServiceHosts {
      get {
        lock (_sync)
        {
          return _serviceHosts.Values;
        }
      }
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
    public int Count {
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

    #region Private Methods

    private Dictionary<string, IWebSocketServiceHost> copy ()
    {
      lock (_sync)
      {
        return new Dictionary<string, IWebSocketServiceHost> (_serviceHosts);
      }
    }

    #endregion

    #region Internal Methods

    internal void Add (string servicePath, IWebSocketServiceHost serviceHost)
    {
      servicePath = HttpUtility.UrlDecode (servicePath).TrimEndSlash ();
      lock (_sync)
      {
        IWebSocketServiceHost host;
        if (_serviceHosts.TryGetValue (servicePath, out host))
        {
          _logger.Error (
            "The WebSocket service with the specified path already exists.\npath: " + servicePath);
          return;
        }

        _serviceHosts.Add (servicePath, serviceHost);
      }
    }

    internal bool Remove (string servicePath)
    {
      servicePath = HttpUtility.UrlDecode (servicePath).TrimEndSlash ();

      IWebSocketServiceHost host;
      lock (_sync)
      {
        if (!_serviceHosts.TryGetValue (servicePath, out host))
        {
          _logger.Error (
            "The WebSocket service with the specified path not found.\npath: " + servicePath);
          return false;
        }

        _serviceHosts.Remove (servicePath);
      }

      host.Stop (((ushort) CloseStatusCode.AWAY).ToByteArray (ByteOrder.BIG));
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

    internal void Stop (byte [] data)
    {
      lock (_sync)
      {
        foreach (var host in _serviceHosts.Values)
          host.Stop (data);

        _serviceHosts.Clear ();
      }
    }

    internal bool TryGetServiceHost (string servicePath, out IWebSocketServiceHost serviceHost)
    {
      servicePath = HttpUtility.UrlDecode (servicePath).TrimEndSlash ();
      lock (_sync)
      {
        return _serviceHosts.TryGetValue (servicePath, out serviceHost);
      }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Broadcasts the specified array of <see cref="byte"/> to all clients of the WebSocket services
    /// managed by the <see cref="WebSocketServiceHostManager"/>.
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

      foreach (var host in ServiceHosts)
        host.Broadcast (data);
    }

    /// <summary>
    /// Broadcasts the specified <see cref="string"/> to all clients of the WebSocket services
    /// managed by the <see cref="WebSocketServiceHostManager"/>.
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
      var msg = data.CheckIfValidSendData () ?? servicePath.CheckIfValidServicePath ();
      if (msg != null)
      {
        _logger.Error (msg);
        return false;
      }

      IWebSocketServiceHost host;
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
      var msg = data.CheckIfValidSendData () ?? servicePath.CheckIfValidServicePath ();
      if (msg != null)
      {
        _logger.Error (msg);
        return false;
      }

      IWebSocketServiceHost host;
      if (!TryGetServiceHost (servicePath, out host))
      {
        _logger.Error ("The WebSocket service with the specified path not found.\npath: " + servicePath);
        return false;
      }

      host.Broadcast (data);
      return true;
    }

    /// <summary>
    /// Sends Pings to all clients of the WebSocket services managed by the <see cref="WebSocketServiceHostManager"/>.
    /// </summary>
    /// <returns>
    /// A Dictionary&lt;string, Dictionary&lt;string, bool&gt;&gt; that contains the collection of
    /// service paths and pairs of session ID and value indicating whether each WebSocket service
    /// received a Pong from each client in a time.
    /// </returns>
    public Dictionary<string, Dictionary<string, bool>> Broadping ()
    {
      var result = new Dictionary<string, Dictionary<string, bool>> ();
      foreach (var service in copy ())
        result.Add (service.Key, service.Value.Broadping ());

      return result;
    }

    /// <summary>
    /// Sends Pings with the specified <paramref name="message"/> to all clients of the WebSocket services
    /// managed by the <see cref="WebSocketServiceHostManager"/>.
    /// </summary>
    /// <returns>
    /// A Dictionary&lt;string, Dictionary&lt;string, bool&gt;&gt; that contains the collection of
    /// service paths and pairs of session ID and value indicating whether each WebSocket service
    /// received a Pong from each client in a time.
    /// </returns>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to send.
    /// </param>
    public Dictionary<string, Dictionary<string, bool>> Broadping (string message)
    {
      var msg = message.CheckIfValidPingMessage ();
      if (msg != null)
      {
        _logger.Error (msg);
        return null;
      }

      var result = new Dictionary<string, Dictionary<string, bool>> ();
      foreach (var service in copy ())
        result.Add (service.Key, service.Value.Broadping (message));

      return result;
    }

    /// <summary>
    /// Sends Pings to all clients of the WebSocket service with the specified <paramref name="servicePath"/>.
    /// </summary>
    /// <returns>
    /// A Dictionary&lt;string, bool&gt; that contains the collection of pairs of session ID and value
    /// indicating whether the WebSocket service received a Pong from each client in a time.
    /// If the WebSocket service is not found, returns <see langword="null"/>.
    /// </returns>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    public Dictionary<string, bool> BroadpingTo (string servicePath)
    {
      var msg = servicePath.CheckIfValidServicePath ();
      if (msg != null)
      {
        _logger.Error (msg);
        return null;
      }

      IWebSocketServiceHost host;
      if (!TryGetServiceHost (servicePath, out host))
      {
        _logger.Error ("The WebSocket service with the specified path not found.\npath: " + servicePath);
        return null;
      }

      return host.Broadping ();
    }

    /// <summary>
    /// Sends Pings with the specified <paramref name="message"/> to all clients of the WebSocket service
    /// with the specified <paramref name="servicePath"/>.
    /// </summary>
    /// <returns>
    /// A Dictionary&lt;string, bool&gt; that contains the collection of pairs of session ID and value
    /// indicating whether the WebSocket service received a Pong from each client in a time.
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
      var msg = message.CheckIfValidPingMessage () ?? servicePath.CheckIfValidServicePath ();
      if (msg != null)
      {
        _logger.Error (msg);
        return null;
      }

      IWebSocketServiceHost host;
      if (!TryGetServiceHost (servicePath, out host))
      {
        _logger.Error ("The WebSocket service with the specified path not found.\npath: " + servicePath);
        return null;
      }

      return host.Broadping (message);
    }

    /// <summary>
    /// Close the WebSocket session with the specified <paramref name="id"/> and
    /// <paramref name="servicePath"/>.
    /// </summary>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID to find.
    /// </param>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    public void CloseSession (string id, string servicePath)
    {
      var msg = id.CheckIfValidSessionID () ?? servicePath.CheckIfValidServicePath ();
      if (msg != null)
      {
        _logger.Error (msg);
        return;
      }

      IWebSocketServiceHost host;
      if (!TryGetServiceHost (servicePath, out host))
      {
        _logger.Error ("The WebSocket service with the specified path not found.\npath: " + servicePath);
        return;
      }

      host.CloseSession (id);
    }

    /// <summary>
    /// Close the WebSocket session with the specified <paramref name="code"/>, <paramref name="reason"/>,
    /// <paramref name="id"/> and <paramref name="servicePath"/>.
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
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    public void CloseSession (ushort code, string reason, string id, string servicePath)
    {
      var msg = id.CheckIfValidSessionID () ?? servicePath.CheckIfValidServicePath ();
      if (msg != null)
      {
        _logger.Error (msg);
        return;
      }

      IWebSocketServiceHost host;
      if (!TryGetServiceHost (servicePath, out host))
      {
        _logger.Error ("The WebSocket service with the specified path not found.\npath: " + servicePath);
        return;
      }

      host.CloseSession (code, reason, id);
    }

    /// <summary>
    /// Close the WebSocket session with the specified <paramref name="code"/>, <paramref name="reason"/>,
    /// <paramref name="id"/> and <paramref name="servicePath"/>.
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
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    public void CloseSession (CloseStatusCode code, string reason, string id, string servicePath)
    {
      var msg = id.CheckIfValidSessionID () ?? servicePath.CheckIfValidServicePath ();
      if (msg != null)
      {
        _logger.Error (msg);
        return;
      }

      IWebSocketServiceHost host;
      if (!TryGetServiceHost (servicePath, out host))
      {
        _logger.Error ("The WebSocket service with the specified path not found.\npath: " + servicePath);
        return;
      }

      host.CloseSession (code, reason, id);
    }

    /// <summary>
    /// Gets the connection count to the WebSocket service with the specified <paramref name="servicePath"/>.
    /// </summary>
    /// <returns>
    /// An <see cref="int"/> that contains the connection count if the WebSocket service is successfully
    /// found; otherwise, <c>-1</c>.
    /// </returns>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    public int GetConnectionCount (string servicePath)
    {
      var msg = servicePath.CheckIfValidServicePath ();
      if (msg != null)
      {
        _logger.Error (msg);
        return -1;
      }

      IWebSocketServiceHost host;
      if (!TryGetServiceHost (servicePath, out host))
      {
        _logger.Error ("The WebSocket service with the specified path not found.\npath: " + servicePath);
        return -1;
      }

      return host.ConnectionCount;
    }

    /// <summary>
    /// Sends a Ping to the client associated with the specified <paramref name="id"/> and
    /// <paramref name="servicePath"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the WebSocket service with <paramref name="servicePath"/> receives a Pong
    /// from the client in a time; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID that represents the destination for the Ping.
    /// </param>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    public bool PingTo (string id, string servicePath)
    {
      var msg = id.CheckIfValidSessionID () ?? servicePath.CheckIfValidServicePath ();
      if (msg != null)
      {
        _logger.Error (msg);
        return false;
      }

      IWebSocketServiceHost host;
      if (!TryGetServiceHost (servicePath, out host))
      {
        _logger.Error ("The WebSocket service with the specified path not found.\npath: " + servicePath);
        return false;
      }

      return host.PingTo (id);
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
    /// A <see cref="string"/> that contains a session ID that represents the destination for the Ping.
    /// </param>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    public bool PingTo (string message, string id, string servicePath)
    {
      var msg = (message.CheckIfValidPingMessage () ?? id.CheckIfValidSessionID ()) ??
                servicePath.CheckIfValidServicePath ();

      if (msg != null)
      {
        _logger.Error (msg);
        return false;
      }

      IWebSocketServiceHost host;
      if (!TryGetServiceHost (servicePath, out host))
      {
        _logger.Error ("The WebSocket service with the specified path not found.\npath: " + servicePath);
        return false;
      }

      return host.PingTo (message, id);
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
    /// A <see cref="string"/> that contains a session ID that represents the destination for the data.
    /// </param>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    public bool SendTo (byte [] data, string id, string servicePath)
    {
      var msg = (data.CheckIfValidSendData () ?? id.CheckIfValidSessionID ()) ??
                servicePath.CheckIfValidServicePath ();

      if (msg != null)
      {
        _logger.Error (msg);
        return false;
      }

      IWebSocketServiceHost host;
      if (!TryGetServiceHost (servicePath, out host))
      {
        _logger.Error ("The WebSocket service with the specified path not found.\npath: " + servicePath);
        return false;
      }

      return host.SendTo (data, id);
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
    /// A <see cref="string"/> that contains a session ID that represents the destination for the data.
    /// </param>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    public bool SendTo (string data, string id, string servicePath)
    {
      var msg = (data.CheckIfValidSendData () ?? id.CheckIfValidSessionID ()) ??
                servicePath.CheckIfValidServicePath ();

      if (msg != null)
      {
        _logger.Error (msg);
        return false;
      }

      IWebSocketServiceHost host;
      if (!TryGetServiceHost (servicePath, out host))
      {
        _logger.Error ("The WebSocket service with the specified path not found.\npath: " + servicePath);
        return false;
      }

      return host.SendTo (data, id);
    }

    #endregion
  }
}
