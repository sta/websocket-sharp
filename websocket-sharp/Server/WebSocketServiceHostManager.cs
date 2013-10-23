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

    private volatile bool                            _keepClean;
    private Logger                                   _logger;
    private Dictionary<string, WebSocketServiceHost> _serviceHosts;
    private volatile ServerState                     _state;
    private object                                   _sync;

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
      _serviceHosts = new Dictionary<string, WebSocketServiceHost> ();
      _state = ServerState.READY;
      _sync = new object ();
    }

    #endregion

    #region Public Properties

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
    /// Gets a WebSocket service host with the specified <paramref name="servicePath"/>.
    /// </summary>
    /// <value>
    /// A <see cref="WebSocketServiceHost"/> instance that represents the service host
    /// if the service is successfully found; otherwise, <see langword="null"/>.
    /// </value>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the service to find.
    /// </param>
    public WebSocketServiceHost this [string servicePath] {
      get {
        WebSocketServiceHost host;
        TryGetServiceHost (servicePath, out host);

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
    /// An IEnumerable&lt;WebSocketServiceHost&gt; that contains the collection of the WebSocket
    /// service hosts.
    /// </value>
    public IEnumerable<WebSocketServiceHost> ServiceHosts {
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

    /// <summary>
    /// Gets the number of the sessions to the every WebSocket service
    /// provided by the WebSocket server.
    /// </summary>
    /// <value>
    /// An <see cref="int"/> that contains the session count of the WebSocket server.
    /// </value>
    public int SessionCount {
      get {
        var count = 0;
        foreach (var host in ServiceHosts)
        {
          if (_state != ServerState.START)
            break;

          count += host.SessionCount;
        }

        return count;
      }
    }

    #endregion

    #region Private Methods

    private void broadcast (Opcode opcode, byte [] data, Action completed)
    {
      var cache = new Dictionary<CompressionMethod, byte []> ();
      try {
        foreach (var host in ServiceHosts)
        {
          if (_state != ServerState.START)
            break;

          host.Sessions.Broadcast (opcode, data, cache);
        }

        if (completed != null)
          completed ();
      }
      catch (Exception ex) {
        _logger.Fatal (ex.ToString ());
      }
      finally {
        cache.Clear ();
      }
    }

    private void broadcast (Opcode opcode, Stream stream, Action completed)
    {
      var cache = new Dictionary<CompressionMethod, Stream> ();
      try {
        foreach (var host in ServiceHosts)
        {
          if (_state != ServerState.START)
            break;

          host.Sessions.Broadcast (opcode, stream, cache);
        }

        if (completed != null)
          completed ();
      }
      catch (Exception ex) {
        _logger.Fatal (ex.ToString ());
      }
      finally {
        foreach (var cached in cache.Values)
          cached.Dispose ();

        cache.Clear ();
      }
    }

    private void broadcastAsync (Opcode opcode, byte [] data, Action completed)
    {
      WaitCallback callback = state =>
      {
        broadcast (opcode, data, completed);
      };

      ThreadPool.QueueUserWorkItem (callback);
    }

    private void broadcastAsync (Opcode opcode, Stream stream, Action completed)
    {
      WaitCallback callback = state =>
      {
        broadcast (opcode, stream, completed);
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

        result.Add (host.ServicePath, host.Sessions.Broadping (frameAsBytes, timeOut));
      }

      return result;
    }

    #endregion

    #region Internal Methods

    internal void Add (string servicePath, WebSocketServiceHost serviceHost)
    {
      lock (_sync)
      {
        WebSocketServiceHost host;
        if (_serviceHosts.TryGetValue (servicePath, out host))
        {
          _logger.Error (
            "A WebSocket service with the specified path already exists.\npath: " + servicePath);
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
      WebSocketServiceHost host;
      lock (_sync)
      {
        if (!_serviceHosts.TryGetValue (servicePath, out host))
        {
          _logger.Error (
            "A WebSocket service with the specified path not found.\npath: " + servicePath);
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

    internal bool TryGetServiceHostInternally (string servicePath, out WebSocketServiceHost serviceHost)
    {
      servicePath = HttpUtility.UrlDecode (servicePath).TrimEndSlash ();
      bool result;
      lock (_sync)
      {
        result = _serviceHosts.TryGetValue (servicePath, out serviceHost);
      }

      if (!result)
        _logger.Error ("A WebSocket service with the specified path not found.\npath: " + servicePath);

      return result;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Broadcasts a binary <paramref name="data"/> to all clients of the WebSocket services
    /// provided by a WebSocket server.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the broadcast to be complete.
    /// </remarks>
    /// <param name="data">
    /// An array of <see cref="byte"/> that contains a binary data to broadcast.
    /// </param>
    public void Broadcast (byte [] data)
    {
      Broadcast (data, null);
    }

    /// <summary>
    /// Broadcasts a text <paramref name="data"/> to all clients of the WebSocket services
    /// provided by a WebSocket server.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the broadcast to be complete.
    /// </remarks>
    /// <param name="data">
    /// A <see cref="string"/> that contains a text data to broadcast.
    /// </param>
    public void Broadcast (string data)
    {
      Broadcast (data, null);
    }

    /// <summary>
    /// Broadcasts a binary <paramref name="data"/> to all clients of the WebSocket services
    /// provided by a WebSocket server.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the broadcast to be complete.
    /// </remarks>
    /// <param name="data">
    /// An array of <see cref="byte"/> that contains a binary data to broadcast.
    /// </param>
    /// <param name="completed">
    /// A <see cref="Action"/> delegate that references the method(s) called when
    /// the broadcast is complete.
    /// </param>
    public void Broadcast (byte [] data, Action completed)
    {
      var msg = _state.CheckIfStarted () ?? data.CheckIfValidSendData ();
      if (msg != null)
      {
        _logger.Error (msg);
        return;
      }

      if (data.LongLength <= WebSocket.FragmentLength)
        broadcastAsync (Opcode.BINARY, data, completed);
      else
        broadcastAsync (Opcode.BINARY, new MemoryStream (data), completed);
    }

    /// <summary>
    /// Broadcasts a text <paramref name="data"/> to all clients of the WebSocket services
    /// provided by a WebSocket server.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the broadcast to be complete.
    /// </remarks>
    /// <param name="data">
    /// A <see cref="string"/> that contains a text data to broadcast.
    /// </param>
    /// <param name="completed">
    /// A <see cref="Action"/> delegate that references the method(s) called when
    /// the broadcast is complete.
    /// </param>
    public void Broadcast (string data, Action completed)
    {
      var msg = _state.CheckIfStarted () ?? data.CheckIfValidSendData ();
      if (msg != null)
      {
        _logger.Error (msg);
        return;
      }

      var rawData = Encoding.UTF8.GetBytes (data);
      if (rawData.LongLength <= WebSocket.FragmentLength)
        broadcastAsync (Opcode.TEXT, rawData, completed);
      else
        broadcastAsync (Opcode.TEXT, new MemoryStream (rawData), completed);
    }

    /// <summary>
    /// Broadcasts a binary data from the specified <see cref="Stream"/> to all clients
    /// of the WebSocket services provided by a WebSocket server.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the broadcast to be complete.
    /// </remarks>
    /// <param name="stream">
    /// A <see cref="Stream"/> object from which contains a binary data to broadcast.
    /// </param>
    /// <param name="length">
    /// An <see cref="int"/> that contains the number of bytes to broadcast.
    /// </param>
    public void Broadcast (Stream stream, int length)
    {
      Broadcast (stream, length, null);
    }

    /// <summary>
    /// Broadcasts a binary data from the specified <see cref="Stream"/> to all clients
    /// of the WebSocket services provided by a WebSocket server.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the broadcast to be complete.
    /// </remarks>
    /// <param name="stream">
    /// A <see cref="Stream"/> object from which contains a binary data to broadcast.
    /// </param>
    /// <param name="length">
    /// An <see cref="int"/> that contains the number of bytes to broadcast.
    /// </param>
    /// <param name="completed">
    /// A <see cref="Action"/> delegate that references the method(s) called when
    /// the broadcast is complete.
    /// </param>
    public void Broadcast (Stream stream, int length, Action completed)
    {
      var msg = _state.CheckIfStarted () ??
                stream.CheckIfCanRead () ??
                (length < 1 ? "'length' must be greater than 0." : null);

      if (msg != null)
      {
        _logger.Error (msg);
        return;
      }

      stream.ReadBytesAsync (
        length,
        data =>
        {
          var len = data.Length;
          if (len == 0)
          {
            _logger.Error ("A data cannot be read from 'stream'.");
            return;
          }

          if (len < length)
            _logger.Warn (String.Format (
              "A data with 'length' cannot be read from 'stream'.\nexpected: {0} actual: {1}",
              length,
              len));

          if (len <= WebSocket.FragmentLength)
            broadcast (Opcode.BINARY, data, completed);
          else
            broadcast (Opcode.BINARY, new MemoryStream (data), completed);
        },
        ex =>
        {
          _logger.Fatal (ex.ToString ());
        });
    }

    /// <summary>
    /// Broadcasts a binary <paramref name="data"/> to all clients of a WebSocket service
    /// with the specified <paramref name="servicePath"/>.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the broadcast to be complete.
    /// </remarks>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    /// <param name="data">
    /// An array of <see cref="byte"/> that contains a binary data to broadcast.
    /// </param>
    public void BroadcastTo (string servicePath, byte [] data)
    {
      BroadcastTo (servicePath, data, null);
    }

    /// <summary>
    /// Broadcasts a text <paramref name="data"/> to all clients of a WebSocket service
    /// with the specified <paramref name="servicePath"/>.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the broadcast to be complete.
    /// </remarks>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    /// <param name="data">
    /// A <see cref="string"/> that contains a text data to broadcast.
    /// </param>
    public void BroadcastTo (string servicePath, string data)
    {
      BroadcastTo (servicePath, data, null);
    }

    /// <summary>
    /// Broadcasts a binary <paramref name="data"/> to all clients of a WebSocket service
    /// with the specified <paramref name="servicePath"/>.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the broadcast to be complete.
    /// </remarks>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    /// <param name="data">
    /// An array of <see cref="byte"/> that contains a binary data to broadcast.
    /// </param>
    /// <param name="completed">
    /// A <see cref="Action"/> delegate that references the method(s) called when
    /// the broadcast is complete.
    /// </param>
    public void BroadcastTo (string servicePath, byte [] data, Action completed)
    {
      WebSocketServiceHost host;
      if (TryGetServiceHost (servicePath, out host))
        host.Sessions.Broadcast (data, completed);
    }

    /// <summary>
    /// Broadcasts a text <paramref name="data"/> to all clients of a WebSocket service
    /// with the specified <paramref name="servicePath"/>.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the broadcast to be complete.
    /// </remarks>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    /// <param name="data">
    /// A <see cref="string"/> that contains a text data to broadcast.
    /// </param>
    /// <param name="completed">
    /// A <see cref="Action"/> delegate that references the method(s) called when
    /// the broadcast is complete.
    /// </param>
    public void BroadcastTo (string servicePath, string data, Action completed)
    {
      WebSocketServiceHost host;
      if (TryGetServiceHost (servicePath, out host))
        host.Sessions.Broadcast (data, completed);
    }

    /// <summary>
    /// Broadcasts a binary data from the specified <see cref="Stream"/> to all clients
    /// of a WebSocket service with the specified <paramref name="servicePath"/>.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the broadcast to be complete.
    /// </remarks>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    /// <param name="stream">
    /// A <see cref="Stream"/> object from which contains a binary data to broadcast.
    /// </param>
    /// <param name="length">
    /// An <see cref="int"/> that contains the number of bytes to broadcast.
    /// </param>
    public void BroadcastTo (string servicePath, Stream stream, int length)
    {
      BroadcastTo (servicePath, stream, length, null);
    }

    /// <summary>
    /// Broadcasts a binary data from the specified <see cref="Stream"/> to all clients
    /// of a WebSocket service with the specified <paramref name="servicePath"/>.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the broadcast to be complete.
    /// </remarks>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    /// <param name="stream">
    /// A <see cref="Stream"/> object from which contains a binary data to broadcast.
    /// </param>
    /// <param name="length">
    /// An <see cref="int"/> that contains the number of bytes to broadcast.
    /// </param>
    /// <param name="completed">
    /// A <see cref="Action"/> delegate that references the method(s) called when
    /// the broadcast is complete.
    /// </param>
    public void BroadcastTo (
      string servicePath, Stream stream, int length, Action completed)
    {
      WebSocketServiceHost host;
      if (TryGetServiceHost (servicePath, out host))
        host.Sessions.Broadcast (stream, length, completed);
    }

    /// <summary>
    /// Sends Pings to all clients of the WebSocket services provided by a WebSocket server.
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

      return broadping (WsFrame.EmptyUnmaskPingData, 1000);
    }

    /// <summary>
    /// Sends Pings with the specified <paramref name="message"/> to all clients of the WebSocket
    /// services provided by a WebSocket server.
    /// </summary>
    /// <returns>
    /// A Dictionary&lt;string, Dictionary&lt;string, bool&gt;&gt; that contains the collection of
    /// service paths and pairs of session ID and value indicating whether each WebSocket service
    /// received a Pong from each client in a time.
    /// If <paramref name="message"/> is invalid, returns <see langword="null"/>.
    /// </returns>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to broadcast.
    /// </param>
    public Dictionary<string, Dictionary<string, bool>> Broadping (string message)
    {
      if (message == null || message.Length == 0)
        return Broadping ();

      byte [] data = null;
      var msg = _state.CheckIfStarted () ??
                (data = Encoding.UTF8.GetBytes (message)).CheckIfValidPingData ();

      if (msg != null)
      {
        _logger.Error (msg);
        return null;
      }

      return broadping (WsFrame.CreatePingFrame (Mask.UNMASK, data).ToByteArray (), 1000);
    }

    /// <summary>
    /// Sends Pings to all clients of a WebSocket service with
    /// the specified <paramref name="servicePath"/>.
    /// </summary>
    /// <returns>
    /// A Dictionary&lt;string, bool&gt; that contains the collection of pairs of session ID and
    /// value indicating whether the WebSocket service received a Pong from each client in a time.
    /// If the WebSocket service not found, returns <see langword="null"/>.
    /// </returns>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    public Dictionary<string, bool> BroadpingTo (string servicePath)
    {
      WebSocketServiceHost host;
      return TryGetServiceHost (servicePath, out host)
             ? host.Sessions.Broadping ()
             : null;
    }

    /// <summary>
    /// Sends Pings with the specified <paramref name="message"/> to all clients
    /// of a WebSocket service with the specified <paramref name="servicePath"/>.
    /// </summary>
    /// <returns>
    /// A Dictionary&lt;string, bool&gt; that contains the collection of pairs of session ID and
    /// value indicating whether the WebSocket service received a Pong from each client in a time.
    /// If the WebSocket service not found, returns <see langword="null"/>.
    /// </returns>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to send.
    /// </param>
    public Dictionary<string, bool> BroadpingTo (string servicePath, string message)
    {
      WebSocketServiceHost host;
      return TryGetServiceHost (servicePath, out host)
             ? host.Sessions.Broadping (message)
             : null;
    }

    /// <summary>
    /// Closes the session with the specified <paramref name="servicePath"/> and
    /// <paramref name="id"/>.
    /// </summary>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to a WebSocket service to find.
    /// </param>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID to find.
    /// </param>
    public void CloseSession (string servicePath, string id)
    {
      WebSocketServiceHost host;
      if (TryGetServiceHost (servicePath, out host))
        host.Sessions.CloseSession (id);
    }

    /// <summary>
    /// Closes the session with the specified <paramref name="servicePath"/>, <paramref name="id"/>,
    /// <paramref name="code"/> and <paramref name="reason"/>.
    /// </summary>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to a WebSocket service to find.
    /// </param>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID to find.
    /// </param>
    /// <param name="code">
    /// A <see cref="ushort"/> that contains a status code indicating the reason for closure.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that contains the reason for closure.
    /// </param>
    public void CloseSession (string servicePath, string id, ushort code, string reason)
    {
      WebSocketServiceHost host;
      if (TryGetServiceHost (servicePath, out host))
        host.Sessions.CloseSession (id, code, reason);
    }

    /// <summary>
    /// Closes the session with the specified <paramref name="servicePath"/>, <paramref name="id"/>,
    /// <paramref name="code"/> and <paramref name="reason"/>.
    /// </summary>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to a WebSocket service to find.
    /// </param>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID to find.
    /// </param>
    /// <param name="code">
    /// One of the <see cref="CloseStatusCode"/> values that indicate the status codes for closure.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that contains the reason for closure.
    /// </param>
    public void CloseSession (string servicePath, string id, CloseStatusCode code, string reason)
    {
      WebSocketServiceHost host;
      if (TryGetServiceHost (servicePath, out host))
        host.Sessions.CloseSession (id, code, reason);
    }

    /// <summary>
    /// Sends a Ping to the client associated with the specified <paramref name="servicePath"/>
    /// and <paramref name="id"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the WebSocket service with <paramref name="servicePath"/> receives
    /// a Pong from the client in a time; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID that represents the destination
    /// for the Ping.
    /// </param>
    public bool PingTo (string servicePath, string id)
    {
      WebSocketServiceHost host;
      return TryGetServiceHost (servicePath, out host) && host.Sessions.PingTo (id);
    }

    /// <summary>
    /// Sends a Ping with the specified <paramref name="message"/> to the client associated
    /// with the specified <paramref name="servicePath"/> and <paramref name="id"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the WebSocket service with <paramref name="servicePath"/> receives
    /// a Pong from the client in a time; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID that represents the destination
    /// for the Ping.
    /// </param>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to send.
    /// </param>
    public bool PingTo (string servicePath, string id, string message)
    {
      WebSocketServiceHost host;
      return TryGetServiceHost (servicePath, out host) && host.Sessions.PingTo (id, message);
    }

    /// <summary>
    /// Sends a binary <paramref name="data"/> to the client associated with the specified
    /// <paramref name="servicePath"/> and <paramref name="id"/>.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the send to be complete.
    /// </remarks>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID that represents the destination
    /// for the data.
    /// </param>
    /// <param name="data">
    /// An array of <see cref="byte"/> that contains a binary data to send.
    /// </param>
    public void SendTo (string servicePath, string id, byte [] data)
    {
      SendTo (servicePath, id, data, null);
    }

    /// <summary>
    /// Sends a text <paramref name="data"/> to the client associated with the specified
    /// <paramref name="servicePath"/> and <paramref name="id"/>.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the send to be complete.
    /// </remarks>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID that represents the destination
    /// for the data.
    /// </param>
    /// <param name="data">
    /// A <see cref="string"/> that contains a text data to send.
    /// </param>
    public void SendTo (string servicePath, string id, string data)
    {
      SendTo (servicePath, id, data, null);
    }

    /// <summary>
    /// Sends a binary <paramref name="data"/> to the client associated with the specified
    /// <paramref name="servicePath"/> and <paramref name="id"/>.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the send to be complete.
    /// </remarks>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID that represents the destination
    /// for the data.
    /// </param>
    /// <param name="data">
    /// An array of <see cref="byte"/> that contains a binary data to send.
    /// </param>
    /// <param name="completed">
    /// An Action&lt;bool&gt; delegate that references the method(s) called when
    /// the send is complete.
    /// A <see cref="bool"/> passed to this delegate is <c>true</c> if the send is complete
    /// successfully; otherwise, <c>false</c>.
    /// </param>
    public void SendTo (string servicePath, string id, byte [] data, Action<bool> completed)
    {
      WebSocketServiceHost host;
      if (TryGetServiceHost (servicePath, out host))
        host.Sessions.SendTo (id, data, completed);
    }

    /// <summary>
    /// Sends a text <paramref name="data"/> to the client associated with the specified
    /// <paramref name="servicePath"/> and <paramref name="id"/>.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the send to be complete.
    /// </remarks>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID that represents the destination
    /// for the data.
    /// </param>
    /// <param name="data">
    /// A <see cref="string"/> that contains a text data to send.
    /// </param>
    /// <param name="completed">
    /// An Action&lt;bool&gt; delegate that references the method(s) called when
    /// the send is complete.
    /// A <see cref="bool"/> passed to this delegate is <c>true</c> if the send is complete
    /// successfully; otherwise, <c>false</c>.
    /// </param>
    public void SendTo (string servicePath, string id, string data, Action<bool> completed)
    {
      WebSocketServiceHost host;
      if (TryGetServiceHost (servicePath, out host))
        host.Sessions.SendTo (id, data, completed);
    }

    /// <summary>
    /// Sends a binary data from the specified <see cref="Stream"/> to the client associated with
    /// the specified <paramref name="servicePath"/> and <paramref name="id"/>.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the send to be complete.
    /// </remarks>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID that represents the destination
    /// for the data.
    /// </param>
    /// <param name="stream">
    /// A <see cref="Stream"/> object from which contains a binary data to send.
    /// </param>
    /// <param name="length">
    /// An <see cref="int"/> that contains the number of bytes to send.
    /// </param>
    public void SendTo (string servicePath, string id, Stream stream, int length)
    {
      SendTo (servicePath, id, stream, length, null);
    }

    /// <summary>
    /// Sends a binary data from the specified <see cref="Stream"/> to the client associated with
    /// the specified <paramref name="servicePath"/> and <paramref name="id"/>.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the send to be complete.
    /// </remarks>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the WebSocket service to find.
    /// </param>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID that represents the destination
    /// for the data.
    /// </param>
    /// <param name="stream">
    /// A <see cref="Stream"/> object from which contains a binary data to send.
    /// </param>
    /// <param name="length">
    /// An <see cref="int"/> that contains the number of bytes to send.
    /// </param>
    /// <param name="completed">
    /// An Action&lt;bool&gt; delegate that references the method(s) called when
    /// the send is complete.
    /// A <see cref="bool"/> passed to this delegate is <c>true</c> if the send is complete
    /// successfully; otherwise, <c>false</c>.
    /// </param>
    public void SendTo (
      string servicePath, string id, Stream stream, int length, Action<bool> completed)
    {
      WebSocketServiceHost host;
      if (TryGetServiceHost (servicePath, out host))
        host.Sessions.SendTo (id, stream, length, completed);
    }

    /// <summary>
    /// Tries to get a WebSocket service host with the specified <paramref name="servicePath"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the service is successfully found; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="servicePath">
    /// A <see cref="string"/> that contains an absolute path to the service to find.
    /// </param>
    /// <param name="serviceHost">
    /// When this method returns, a <see cref="WebSocketServiceHost"/> instance that represents
    /// the service host if the service is successfully found; otherwise, <see langword="null"/>.
    /// This parameter is passed uninitialized.
    /// </param>
    public bool TryGetServiceHost (string servicePath, out WebSocketServiceHost serviceHost)
    {
      var msg = _state.CheckIfStarted () ?? servicePath.CheckIfValidServicePath ();
      if (msg != null)
      {
        _logger.Error (msg);
        serviceHost = null;

        return false;
      }

      return TryGetServiceHostInternally (servicePath, out serviceHost);
    }

    #endregion
  }
}
