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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using WebSocketSharp.Net;

namespace WebSocketSharp.Server
{
  /// <summary>
  /// Manages the WebSocket services provided by the <see cref="HttpServer"/> and
  /// <see cref="WebSocketServer"/>.
  /// </summary>
  public class WebSocketServiceHostManager
  {
    #region Private Fields

    private volatile bool                             _keepClean;
    private Logger                                    _logger;
    private Dictionary<string, IWebSocketServiceHost> _serviceHosts;
    private volatile ServerState                      _state;
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
      _state = ServerState.READY;
      _sync = new object ();
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the connection count to the every WebSocket service provided by the WebSocket server.
    /// </summary>
    /// <value>
    /// An <see cref="int"/> that contains the connection count to the every WebSocket service.
    /// </value>
    public int ConnectionCount {
      get {
        var count = 0;
        foreach (var host in ServiceHosts)
        {
          if (_state != ServerState.START)
            break;

          count += host.ConnectionCount;
        }

        return count;
      }
    }

    /// <summary>
    /// Gets the number of the WebSocket services provided by the WebSocket server.
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
    /// Gets the WebSocket service host with the specified <paramref name="servicePath"/>.
    /// </summary>
    /// <value>
    /// A <see cref="IWebSocketServiceHost"/> instance that represents the WebSocket service host
    /// if it is successfully found; otherwise, <see langword="null"/>.
    /// </value>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service managed by
    /// the WebSocket service host to get.
    /// </param>
    public IWebSocketServiceHost this [string servicePath] {
      get {
        var msg = servicePath.CheckIfValidServicePath ();
        if (msg != null)
        {
          _logger.Error (msg);
          return null;
        }

        IWebSocketServiceHost host;
        if (!TryGetServiceHostInternally (servicePath, out host))
          _logger.Error ("The WebSocket service with the specified path not found.\npath: " + servicePath);

        return host;
      }
    }

    /// <summary>
    /// Gets a value indicating whether the manager cleans up periodically the every inactive session
    /// to the WebSocket services provided by the WebSocket server.
    /// </summary>
    /// <value>
    /// <c>true</c> if the manager cleans up periodically the every inactive session to the WebSocket
    /// services; otherwise, <c>false</c>.
    /// </value>
    public bool KeepClean {
      get {
        return _keepClean;
      }

      internal set {
        lock (_sync)
        {
          if (!(value ^ _keepClean))
            return;

          _keepClean = value;
          foreach (var host in _serviceHosts.Values)
            host.KeepClean = value;
        }
      }
    }

    /// <summary>
    /// Gets the collection of the WebSocket service hosts managed by the WebSocket server.
    /// </summary>
    /// <value>
    /// An IEnumerable&lt;IWebSocketServiceHost&gt; that contains the collection of the WebSocket
    /// service hosts.
    /// </value>
    public IEnumerable<IWebSocketServiceHost> ServiceHosts {
      get {
        lock (_sync)
        {
          return _serviceHosts.Values.ToList ();
        }
      }
    }

    /// <summary>
    /// Gets the collection of every path to the WebSocket services provided by the WebSocket server.
    /// </summary>
    /// <value>
    /// An IEnumerable&lt;string&gt; that contains the collection of every path to the WebSocket services.
    /// </value>
    public IEnumerable<string> ServicePaths {
      get {
        lock (_sync)
        {
          return _serviceHosts.Keys.ToList ();
        }
      }
    }

    #endregion

    #region Private Methods

    private void broadcast (Opcode opcode, byte [] data)
    {
      WaitCallback callback = state =>
      {
        var cache = new Dictionary<CompressionMethod, byte []> ();
        try {
          foreach (var host in ServiceHosts)
          {
            if (_state != ServerState.START)
              break;

            host.Sessions.BroadcastInternally (opcode, data, cache);
          }
        }
        catch (Exception ex) {
          _logger.Fatal (ex.ToString ());
        }
        finally {
          cache.Clear ();
        }
      };

      ThreadPool.QueueUserWorkItem (callback);
    }

    private void broadcast (Opcode opcode, Stream stream)
    {
      WaitCallback callback = state =>
      {
        var cache = new Dictionary<CompressionMethod, Stream> ();
        try {
          foreach (var host in ServiceHosts)
          {
            if (_state != ServerState.START)
              break;

            host.Sessions.BroadcastInternally (opcode, stream, cache);
          }
        }
        catch (Exception ex) {
          _logger.Fatal (ex.ToString ());
        }
        finally {
          foreach (var cached in cache.Values)
            cached.Dispose ();

          cache.Clear ();
        }
      };

      ThreadPool.QueueUserWorkItem (callback);
    }

    private Dictionary<string, Dictionary<string, bool>> broadping (byte [] frameAsBytes, int timeOut)
    {
      var result = new Dictionary<string, Dictionary<string, bool>> ();
      foreach (var host in ServiceHosts)
      {
        if (_state != ServerState.START)
          break;

        result.Add (host.ServicePath, host.Sessions.BroadpingInternally (frameAsBytes, timeOut));
      }

      return result;
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

        if (_state == ServerState.START)
          serviceHost.Sessions.Start ();

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

      if (host.Sessions.State == ServerState.START)
        host.Sessions.Stop (((ushort) CloseStatusCode.AWAY).ToByteArrayInternally (ByteOrder.BIG), true);

      return true;
    }

    internal void Start ()
    {
      lock (_sync)
      {
        foreach (var host in _serviceHosts.Values)
          host.Sessions.Start ();

        _state = ServerState.START;
      }
    }

    internal void Stop (byte [] data, bool send)
    {
      lock (_sync)
      {
        _state = ServerState.SHUTDOWN;

        var payload = new PayloadData (data);
        var args = new CloseEventArgs (payload);
        var frameAsBytes = send
                         ? WsFrame.CreateCloseFrame (Mask.UNMASK, payload).ToByteArray ()
                         : null;

        foreach (var host in _serviceHosts.Values)
          host.Sessions.Stop (args, frameAsBytes);

        _serviceHosts.Clear ();

        _state = ServerState.STOP;
      }
    }

    internal bool TryGetServiceHostInternally (string servicePath, out IWebSocketServiceHost serviceHost)
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
    /// provided by the WebSocket server.
    /// </summary>
    /// <param name="data">
    /// An array of <see cref="byte"/> to broadcast.
    /// </param>
    public void Broadcast (byte [] data)
    {
      var msg = _state.CheckIfStarted () ?? data.CheckIfValidSendData ();
      if (msg != null)
      {
        _logger.Error (msg);
        return;
      }

      if (data.LongLength <= WebSocket.FragmentLength)
        broadcast (Opcode.BINARY, data);
      else
        broadcast (Opcode.BINARY, new MemoryStream (data));
    }

    /// <summary>
    /// Broadcasts the specified <see cref="string"/> to all clients of the WebSocket services
    /// provided by the WebSocket server.
    /// </summary>
    /// <param name="data">
    /// A <see cref="string"/> to broadcast.
    /// </param>
    public void Broadcast (string data)
    {
      var msg = _state.CheckIfStarted () ?? data.CheckIfValidSendData ();
      if (msg != null)
      {
        _logger.Error (msg);
        return;
      }

      var rawData = Encoding.UTF8.GetBytes (data);
      if (rawData.LongLength <= WebSocket.FragmentLength)
        broadcast (Opcode.TEXT, rawData);
      else
        broadcast (Opcode.TEXT, new MemoryStream (rawData));
    }

    /// <summary>
    /// Broadcasts the specified array of <see cref="byte"/> to all clients of the WebSocket service
    /// with the specified <paramref name="servicePath"/>.
    /// </summary>
    /// <param name="data">
    /// An array of <see cref="byte"/> to broadcast.
    /// </param>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    public void BroadcastTo (byte [] data, string servicePath)
    {
      var msg = _state.CheckIfStarted () ?? servicePath.CheckIfValidServicePath ();
      if (msg != null)
      {
        _logger.Error (msg);
        return;
      }

      IWebSocketServiceHost host;
      if (!TryGetServiceHostInternally (servicePath, out host))
      {
        _logger.Error ("The WebSocket service with the specified path not found.\npath: " + servicePath);
        return;
      }

      host.Sessions.Broadcast (data);
    }

    /// <summary>
    /// Broadcasts the specified <see cref="string"/> to all clients of the WebSocket service
    /// with the specified <paramref name="servicePath"/>.
    /// </summary>
    /// <param name="data">
    /// A <see cref="string"/> to broadcast.
    /// </param>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    public void BroadcastTo (string data, string servicePath)
    {
      var msg = _state.CheckIfStarted () ?? servicePath.CheckIfValidServicePath ();
      if (msg != null)
      {
        _logger.Error (msg);
        return;
      }

      IWebSocketServiceHost host;
      if (!TryGetServiceHostInternally (servicePath, out host))
      {
        _logger.Error ("The WebSocket service with the specified path not found.\npath: " + servicePath);
        return;
      }

      host.Sessions.Broadcast (data);
    }

    /// <summary>
    /// Sends Pings to all clients of the WebSocket services provided by the WebSocket server.
    /// </summary>
    /// <returns>
    /// A Dictionary&lt;string, Dictionary&lt;string, bool&gt;&gt; that contains the collection of
    /// service paths and pairs of session ID and value indicating whether each WebSocket service
    /// received a Pong from each client in a time.
    /// </returns>
    public Dictionary<string, Dictionary<string, bool>> Broadping ()
    {
      var msg = _state.CheckIfStarted ();
      if (msg != null)
      {
        _logger.Error (msg);
        return null;
      }

      return broadping (WsFrame.CreatePingFrame (Mask.UNMASK).ToByteArray (), 1000);
    }

    /// <summary>
    /// Sends Pings with the specified <paramref name="message"/> to all clients of the WebSocket services
    /// provided by the WebSocket server.
    /// </summary>
    /// <returns>
    /// A Dictionary&lt;string, Dictionary&lt;string, bool&gt;&gt; that contains the collection of
    /// service paths and pairs of session ID and value indicating whether each WebSocket service
    /// received a Pong from each client in a time.
    /// If <paramref name="message"/> is invalid, returns <see langword="null"/>.
    /// </returns>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to send.
    /// </param>
    public Dictionary<string, Dictionary<string, bool>> Broadping (string message)
    {
      if (message == null || message.Length == 0)
        return Broadping ();

      var data = Encoding.UTF8.GetBytes (message);
      var msg = _state.CheckIfStarted () ?? data.CheckIfValidPingData ();
      if (msg != null)
      {
        _logger.Error (msg);
        return null;
      }

      return broadping (WsFrame.CreatePingFrame (Mask.UNMASK, data).ToByteArray (), 1000);
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
      var msg = _state.CheckIfStarted () ?? servicePath.CheckIfValidServicePath ();
      if (msg != null)
      {
        _logger.Error (msg);
        return null;
      }

      IWebSocketServiceHost host;
      if (!TryGetServiceHostInternally (servicePath, out host))
      {
        _logger.Error ("The WebSocket service with the specified path not found.\npath: " + servicePath);
        return null;
      }

      return host.Sessions.BroadpingInternally ();
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
      if (message == null || message.Length == 0)
        return BroadpingTo (servicePath);

      var data = Encoding.UTF8.GetBytes (message);
      var msg = _state.CheckIfStarted () ?? data.CheckIfValidPingData () ?? servicePath.CheckIfValidServicePath ();
      if (msg != null)
      {
        _logger.Error (msg);
        return null;
      }

      IWebSocketServiceHost host;
      if (!TryGetServiceHostInternally (servicePath, out host))
      {
        _logger.Error ("The WebSocket service with the specified path not found.\npath: " + servicePath);
        return null;
      }

      return host.Sessions.BroadpingInternally (
        WsFrame.CreatePingFrame (Mask.UNMASK, data).ToByteArray (), 1000);
    }

    /// <summary>
    /// Closes the session with the specified <paramref name="id"/> and
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
      var msg = _state.CheckIfStarted () ?? servicePath.CheckIfValidServicePath ();
      if (msg != null)
      {
        _logger.Error (msg);
        return;
      }

      IWebSocketServiceHost host;
      if (!TryGetServiceHostInternally (servicePath, out host))
      {
        _logger.Error ("The WebSocket service with the specified path not found.\npath: " + servicePath);
        return;
      }

      host.Sessions.CloseSession (id);
    }

    /// <summary>
    /// Closes the session with the specified <paramref name="code"/>, <paramref name="reason"/>,
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
      var msg = _state.CheckIfStarted () ?? servicePath.CheckIfValidServicePath ();
      if (msg != null)
      {
        _logger.Error (msg);
        return;
      }

      IWebSocketServiceHost host;
      if (!TryGetServiceHostInternally (servicePath, out host))
      {
        _logger.Error ("The WebSocket service with the specified path not found.\npath: " + servicePath);
        return;
      }

      host.Sessions.CloseSession (code, reason, id);
    }

    /// <summary>
    /// Closes the session with the specified <paramref name="code"/>, <paramref name="reason"/>,
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
      var msg = _state.CheckIfStarted () ?? servicePath.CheckIfValidServicePath ();
      if (msg != null)
      {
        _logger.Error (msg);
        return;
      }

      IWebSocketServiceHost host;
      if (!TryGetServiceHostInternally (servicePath, out host))
      {
        _logger.Error ("The WebSocket service with the specified path not found.\npath: " + servicePath);
        return;
      }

      host.Sessions.CloseSession (code, reason, id);
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
      var msg = _state.CheckIfStarted () ?? servicePath.CheckIfValidServicePath ();
      if (msg != null)
      {
        _logger.Error (msg);
        return false;
      }

      IWebSocketServiceHost host;
      if (!TryGetServiceHostInternally (servicePath, out host))
      {
        _logger.Error ("The WebSocket service with the specified path not found.\npath: " + servicePath);
        return false;
      }

      return host.Sessions.PingTo (id);
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
      var msg = _state.CheckIfStarted () ?? servicePath.CheckIfValidServicePath ();
      if (msg != null)
      {
        _logger.Error (msg);
        return false;
      }

      IWebSocketServiceHost host;
      if (!TryGetServiceHostInternally (servicePath, out host))
      {
        _logger.Error ("The WebSocket service with the specified path not found.\npath: " + servicePath);
        return false;
      }

      return host.Sessions.PingTo (message, id);
    }

    /// <summary>
    /// Sends a binary <paramref name="data"/> to the client associated with the specified
    /// <paramref name="id"/> and <paramref name="servicePath"/>.
    /// </summary>
    /// <param name="data">
    /// An array of <see cref="byte"/> that contains a binary data to send.
    /// </param>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID that represents the destination for the data.
    /// </param>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    public void SendTo (byte [] data, string id, string servicePath)
    {
      var msg = _state.CheckIfStarted () ?? servicePath.CheckIfValidServicePath ();
      if (msg != null)
      {
        _logger.Error (msg);
        return;
      }

      IWebSocketServiceHost host;
      if (!TryGetServiceHostInternally (servicePath, out host))
      {
        _logger.Error ("The WebSocket service with the specified path not found.\npath: " + servicePath);
        return;
      }

      host.Sessions.SendTo (data, id);
    }

    /// <summary>
    /// Sends a text <paramref name="data"/> to the client associated with the specified
    /// <paramref name="id"/> and <paramref name="servicePath"/>.
    /// </summary>
    /// <param name="data">
    /// A <see cref="string"/> that contains a text data to send.
    /// </param>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID that represents the destination for the data.
    /// </param>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    public void SendTo (string data, string id, string servicePath)
    {
      var msg = _state.CheckIfStarted () ?? servicePath.CheckIfValidServicePath ();
      if (msg != null)
      {
        _logger.Error (msg);
        return;
      }

      IWebSocketServiceHost host;
      if (!TryGetServiceHostInternally (servicePath, out host))
      {
        _logger.Error ("The WebSocket service with the specified path not found.\npath: " + servicePath);
        return;
      }

      host.Sessions.SendTo (data, id);
    }

    /// <summary>
    /// Tries to get the WebSocket service host with the specified <paramref name="servicePath"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the WebSocket service host is successfully found; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service managed by
    /// the WebSocket service host to get.
    /// </param>
    /// <param name="serviceHost">
    /// When this method returns, a <see cref="IWebSocketServiceHost"/> instance that represents
    /// the WebSocket service host if it is successfully found; otherwise, <see langword="null"/>.
    /// This parameter is passed uninitialized.
    /// </param>
    public bool TryGetServiceHost (string servicePath, out IWebSocketServiceHost serviceHost)
    {
      var msg = _state.CheckIfStarted () ?? servicePath.CheckIfValidServicePath ();
      if (msg != null)
      {
        _logger.Error (msg);
        serviceHost = null;

        return false;
      }

      var result = TryGetServiceHostInternally (servicePath, out serviceHost);
      if (!result)
        _logger.Error ("The WebSocket service with the specified path not found.\npath: " + servicePath);

      return result;
    }

    #endregion
  }
}
