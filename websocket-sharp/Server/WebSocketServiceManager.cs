#region License
/*
 * WebSocketServiceManager.cs
 *
 * The MIT License
 *
 * Copyright (c) 2012-2015 sta.blockhead
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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using WebSocketSharp.Net;

namespace WebSocketSharp.Server
{
  /// <summary>
  /// Provides the management function for the WebSocket services.
  /// </summary>
  /// <remarks>
  /// This class manages the WebSocket services provided by
  /// the <see cref="WebSocketServer"/> or <see cref="HttpServer"/>.
  /// </remarks>
  public class WebSocketServiceManager
  {
    #region Private Fields

    private volatile bool                            _clean;
    private Dictionary<string, WebSocketServiceHost> _hosts;
    private Logger                                   _log;
    private volatile ServerState                     _state;
    private object                                   _sync;
    private TimeSpan                                 _waitTime;

    #endregion

    #region Internal Constructors

    internal WebSocketServiceManager (Logger log)
    {
      _log = log;

      _clean = true;
      _hosts = new Dictionary<string, WebSocketServiceHost> ();
      _state = ServerState.Ready;
      _sync = ((ICollection) _hosts).SyncRoot;
      _waitTime = TimeSpan.FromSeconds (1);
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the number of the WebSocket services.
    /// </summary>
    /// <value>
    /// An <see cref="int"/> that represents the number of the services.
    /// </value>
    public int Count {
      get {
        lock (_sync)
          return _hosts.Count;
      }
    }

    /// <summary>
    /// Gets the host instances for the WebSocket services.
    /// </summary>
    /// <value>
    ///   <para>
    ///   An <c>IEnumerable&lt;WebSocketServiceHost&gt;</c> instance.
    ///   </para>
    ///   <para>
    ///   It provides an enumerator which supports the iteration over
    ///   the collection of the host instances.
    ///   </para>
    /// </value>
    public IEnumerable<WebSocketServiceHost> Hosts {
      get {
        lock (_sync)
          return _hosts.Values.ToList ();
      }
    }

    /// <summary>
    /// Gets the host instance for a WebSocket service with
    /// the specified <paramref name="path"/>.
    /// </summary>
    /// <remarks>
    /// <paramref name="path"/> is converted to a URL-decoded string and
    /// / is trimmed from the end of the converted string if any.
    /// </remarks>
    /// <value>
    ///   <para>
    ///   A <see cref="WebSocketServiceHost"/> instance or
    ///   <see langword="null"/> if not found.
    ///   </para>
    ///   <para>
    ///   That host instance provides the function to access
    ///   the information in the service.
    ///   </para>
    /// </value>
    /// <param name="path">
    /// A <see cref="string"/> that represents an absolute path to
    /// the service to find.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="path"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="path"/> is empty.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="path"/> is not an absolute path.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="path"/> includes either or both
    ///   query and fragment components.
    ///   </para>
    /// </exception>
    public WebSocketServiceHost this[string path] {
      get {
        if (path == null)
          throw new ArgumentNullException ("path");

        if (path.Length == 0)
          throw new ArgumentException ("An empty string.", "path");

        if (path[0] != '/')
          throw new ArgumentException ("Not an absolute path.", "path");

        if (path.IndexOfAny (new[] { '?', '#' }) > -1) {
          var msg = "It includes either or both query and fragment components.";
          throw new ArgumentException (msg, "path");
        }

        WebSocketServiceHost host;
        InternalTryGetServiceHost (path, out host);

        return host;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the inactive sessions in
    /// the WebSocket services are cleaned up periodically.
    /// </summary>
    /// <remarks>
    /// The set operation does nothing if the server has already started or
    /// it is shutting down.
    /// </remarks>
    /// <value>
    /// <c>true</c> if the inactive sessions are cleaned up every 60 seconds;
    /// otherwise, <c>false</c>.
    /// </value>
    public bool KeepClean {
      get {
        return _clean;
      }

      set {
        string msg;
        if (!canSet (out msg)) {
          _log.Warn (msg);
          return;
        }

        lock (_sync) {
          if (!canSet (out msg)) {
            _log.Warn (msg);
            return;
          }

          foreach (var host in _hosts.Values)
            host.KeepClean = value;

          _clean = value;
        }
      }
    }

    /// <summary>
    /// Gets the paths for the WebSocket services.
    /// </summary>
    /// <value>
    ///   <para>
    ///   An <c>IEnumerable&lt;string&gt;</c> instance.
    ///   </para>
    ///   <para>
    ///   It provides an enumerator which supports the iteration over
    ///   the collection of the paths.
    ///   </para>
    /// </value>
    public IEnumerable<string> Paths {
      get {
        lock (_sync)
          return _hosts.Keys.ToList ();
      }
    }

    /// <summary>
    /// Gets the total number of the sessions in the WebSocket services.
    /// </summary>
    /// <value>
    /// An <see cref="int"/> that represents the total number of
    /// the sessions in the services.
    /// </value>
    [Obsolete ("This property will be removed.")]
    public int SessionCount {
      get {
        var cnt = 0;
        foreach (var host in Hosts) {
          if (_state != ServerState.Start)
            break;

          cnt += host.Sessions.Count;
        }

        return cnt;
      }
    }

    /// <summary>
    /// Gets or sets the time to wait for the response to the WebSocket Ping or
    /// Close.
    /// </summary>
    /// <remarks>
    /// The set operation does nothing if the server has already started or
    /// it is shutting down.
    /// </remarks>
    /// <value>
    /// A <see cref="TimeSpan"/> to wait for the response.
    /// </value>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value specified for a set operation is zero or less.
    /// </exception>
    public TimeSpan WaitTime {
      get {
        return _waitTime;
      }

      set {
        if (value <= TimeSpan.Zero)
          throw new ArgumentOutOfRangeException ("value", "Zero or less.");

        string msg;
        if (!canSet (out msg)) {
          _log.Warn (msg);
          return;
        }

        lock (_sync) {
          if (!canSet (out msg)) {
            _log.Warn (msg);
            return;
          }

          foreach (var host in _hosts.Values)
            host.WaitTime = value;

          _waitTime = value;
        }
      }
    }

    #endregion

    #region Private Methods

    private void broadcast (Opcode opcode, byte[] data, Action completed)
    {
      var cache = new Dictionary<CompressionMethod, byte[]> ();

      try {
        foreach (var host in Hosts) {
          if (_state != ServerState.Start) {
            _log.Error ("The server is shutting down.");
            break;
          }

          host.Sessions.Broadcast (opcode, data, cache);
        }

        if (completed != null)
          completed ();
      }
      catch (Exception ex) {
        _log.Error (ex.Message);
        _log.Debug (ex.ToString ());
      }
      finally {
        cache.Clear ();
      }
    }

    private void broadcast (Opcode opcode, Stream stream, Action completed)
    {
      var cache = new Dictionary<CompressionMethod, Stream> ();

      try {
        foreach (var host in Hosts) {
          if (_state != ServerState.Start) {
            _log.Error ("The server is shutting down.");
            break;
          }

          host.Sessions.Broadcast (opcode, stream, cache);
        }

        if (completed != null)
          completed ();
      }
      catch (Exception ex) {
        _log.Error (ex.Message);
        _log.Debug (ex.ToString ());
      }
      finally {
        foreach (var cached in cache.Values)
          cached.Dispose ();

        cache.Clear ();
      }
    }

    private void broadcastAsync (Opcode opcode, byte[] data, Action completed)
    {
      ThreadPool.QueueUserWorkItem (
        state => broadcast (opcode, data, completed)
      );
    }

    private void broadcastAsync (Opcode opcode, Stream stream, Action completed)
    {
      ThreadPool.QueueUserWorkItem (
        state => broadcast (opcode, stream, completed)
      );
    }

    private Dictionary<string, Dictionary<string, bool>> broadping (
      byte[] frameAsBytes, TimeSpan timeout
    )
    {
      var ret = new Dictionary<string, Dictionary<string, bool>> ();

      foreach (var host in Hosts) {
        if (_state != ServerState.Start) {
          _log.Error ("The server is shutting down.");
          break;
        }

        var res = host.Sessions.Broadping (frameAsBytes, timeout);
        ret.Add (host.Path, res);
      }

      return ret;
    }

    private bool canSet (out string message)
    {
      message = null;

      if (_state == ServerState.Start) {
        message = "The server has already started.";
        return false;
      }

      if (_state == ServerState.ShuttingDown) {
        message = "The server is shutting down.";
        return false;
      }

      return true;
    }

    #endregion

    #region Internal Methods

    internal void Add<TBehavior> (string path, Func<TBehavior> creator)
      where TBehavior : WebSocketBehavior
    {
      path = HttpUtility.UrlDecode (path).TrimSlashFromEnd ();

      lock (_sync) {
        WebSocketServiceHost host;
        if (_hosts.TryGetValue (path, out host))
          throw new ArgumentException ("Already in use.", "path");

        host = new WebSocketServiceHost<TBehavior> (
                 path, creator, null, _log
               );

        if (!_clean)
          host.KeepClean = false;

        if (_waitTime != host.WaitTime)
          host.WaitTime = _waitTime;

        if (_state == ServerState.Start)
          host.Start ();

        _hosts.Add (path, host);
      }
    }

    internal bool InternalTryGetServiceHost (
      string path, out WebSocketServiceHost host
    )
    {
      path = HttpUtility.UrlDecode (path).TrimSlashFromEnd ();

      lock (_sync)
        return _hosts.TryGetValue (path, out host);
    }

    internal void Start ()
    {
      lock (_sync) {
        foreach (var host in _hosts.Values)
          host.Start ();

        _state = ServerState.Start;
      }
    }

    internal void Stop (ushort code, string reason)
    {
      lock (_sync) {
        _state = ServerState.ShuttingDown;

        foreach (var host in _hosts.Values)
          host.Stop (code, reason);

        _state = ServerState.Stop;
      }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Adds a WebSocket service with the specified behavior,
    /// <paramref name="path"/>, and <paramref name="initializer"/>.
    /// </summary>
    /// <remarks>
    /// <paramref name="path"/> is converted to a URL-decoded string and
    /// / is trimmed from the end of the converted string if any.
    /// </remarks>
    /// <param name="path">
    /// A <see cref="string"/> that represents an absolute path to
    /// the service to add.
    /// </param>
    /// <param name="initializer">
    ///   <para>
    ///   An <c>Action&lt;TBehavior&gt;</c> delegate or
    ///   <see langword="null"/> if not needed.
    ///   </para>
    ///   <para>
    ///   That delegate invokes the method called for initializing
    ///   a new session instance for the service.
    ///   </para>
    /// </param>
    /// <typeparam name="TBehavior">
    /// The type of the behavior for the service. It must inherit
    /// the <see cref="WebSocketBehavior"/> class and it must have
    /// a public parameterless constructor.
    /// </typeparam>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="path"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="path"/> is empty.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="path"/> is not an absolute path.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="path"/> includes either or both
    ///   query and fragment components.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="path"/> is already in use.
    ///   </para>
    /// </exception>
    public void AddService<TBehavior> (
      string path, Action<TBehavior> initializer
    )
      where TBehavior : WebSocketBehavior, new ()
    {
      if (path == null)
        throw new ArgumentNullException ("path");

      if (path.Length == 0)
        throw new ArgumentException ("An empty string.", "path");

      if (path[0] != '/')
        throw new ArgumentException ("Not an absolute path.", "path");

      if (path.IndexOfAny (new[] { '?', '#' }) > -1) {
        var msg = "It includes either or both query and fragment components.";
        throw new ArgumentException (msg, "path");
      }

      path = HttpUtility.UrlDecode (path).TrimSlashFromEnd ();

      lock (_sync) {
        WebSocketServiceHost host;
        if (_hosts.TryGetValue (path, out host))
          throw new ArgumentException ("Already in use.", "path");

        host = new WebSocketServiceHost<TBehavior> (
                 path, () => new TBehavior (), initializer, _log
               );

        if (!_clean)
          host.KeepClean = false;

        if (_waitTime != host.WaitTime)
          host.WaitTime = _waitTime;

        if (_state == ServerState.Start)
          host.Start ();

        _hosts.Add (path, host);
      }
    }

    /// <summary>
    /// Sends <paramref name="data"/> to every client in the WebSocket services.
    /// </summary>
    /// <param name="data">
    /// An array of <see cref="byte"/> that represents the binary data to send.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The current state of the manager is not Start.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="data"/> is <see langword="null"/>.
    /// </exception>
    [Obsolete ("This method will be removed.")]
    public void Broadcast (byte[] data)
    {
      if (_state != ServerState.Start) {
        var msg = "The current state of the manager is not Start.";
        throw new InvalidOperationException (msg);
      }

      if (data == null)
        throw new ArgumentNullException ("data");

      if (data.LongLength <= WebSocket.FragmentLength)
        broadcast (Opcode.Binary, data, null);
      else
        broadcast (Opcode.Binary, new MemoryStream (data), null);
    }

    /// <summary>
    /// Sends <paramref name="data"/> to every client in the WebSocket services.
    /// </summary>
    /// <param name="data">
    /// A <see cref="string"/> that represents the text data to send.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The current state of the manager is not Start.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="data"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="data"/> could not be UTF-8-encoded.
    /// </exception>
    [Obsolete ("This method will be removed.")]
    public void Broadcast (string data)
    {
      if (_state != ServerState.Start) {
        var msg = "The current state of the manager is not Start.";
        throw new InvalidOperationException (msg);
      }

      if (data == null)
        throw new ArgumentNullException ("data");

      byte[] bytes;
      if (!data.TryGetUTF8EncodedBytes (out bytes)) {
        var msg = "It could not be UTF-8-encoded.";
        throw new ArgumentException (msg, "data");
      }

      if (bytes.LongLength <= WebSocket.FragmentLength)
        broadcast (Opcode.Text, bytes, null);
      else
        broadcast (Opcode.Text, new MemoryStream (bytes), null);
    }

    /// <summary>
    /// Sends <paramref name="data"/> asynchronously to every client in
    /// the WebSocket services.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the send to be complete.
    /// </remarks>
    /// <param name="data">
    /// An array of <see cref="byte"/> that represents the binary data to send.
    /// </param>
    /// <param name="completed">
    ///   <para>
    ///   An <see cref="Action"/> delegate or <see langword="null"/>
    ///   if not needed.
    ///   </para>
    ///   <para>
    ///   The delegate invokes the method called when the send is complete.
    ///   </para>
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The current state of the manager is not Start.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="data"/> is <see langword="null"/>.
    /// </exception>
    [Obsolete ("This method will be removed.")]
    public void BroadcastAsync (byte[] data, Action completed)
    {
      if (_state != ServerState.Start) {
        var msg = "The current state of the manager is not Start.";
        throw new InvalidOperationException (msg);
      }

      if (data == null)
        throw new ArgumentNullException ("data");

      if (data.LongLength <= WebSocket.FragmentLength)
        broadcastAsync (Opcode.Binary, data, completed);
      else
        broadcastAsync (Opcode.Binary, new MemoryStream (data), completed);
    }

    /// <summary>
    /// Sends <paramref name="data"/> asynchronously to every client in
    /// the WebSocket services.
    /// </summary>
    /// <remarks>
    /// This method does not wait for the send to be complete.
    /// </remarks>
    /// <param name="data">
    /// A <see cref="string"/> that represents the text data to send.
    /// </param>
    /// <param name="completed">
    ///   <para>
    ///   An <see cref="Action"/> delegate or <see langword="null"/>
    ///   if not needed.
    ///   </para>
    ///   <para>
    ///   The delegate invokes the method called when the send is complete.
    ///   </para>
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The current state of the manager is not Start.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="data"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="data"/> could not be UTF-8-encoded.
    /// </exception>
    [Obsolete ("This method will be removed.")]
    public void BroadcastAsync (string data, Action completed)
    {
      if (_state != ServerState.Start) {
        var msg = "The current state of the manager is not Start.";
        throw new InvalidOperationException (msg);
      }

      if (data == null)
        throw new ArgumentNullException ("data");

      byte[] bytes;
      if (!data.TryGetUTF8EncodedBytes (out bytes)) {
        var msg = "It could not be UTF-8-encoded.";
        throw new ArgumentException (msg, "data");
      }

      if (bytes.LongLength <= WebSocket.FragmentLength)
        broadcastAsync (Opcode.Text, bytes, completed);
      else
        broadcastAsync (Opcode.Text, new MemoryStream (bytes), completed);
    }

    /// <summary>
    /// Sends the data from <paramref name="stream"/> asynchronously to
    /// every client in the WebSocket services.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   The data is sent as the binary data.
    ///   </para>
    ///   <para>
    ///   This method does not wait for the send to be complete.
    ///   </para>
    /// </remarks>
    /// <param name="stream">
    /// A <see cref="Stream"/> instance from which to read the data to send.
    /// </param>
    /// <param name="length">
    /// An <see cref="int"/> that specifies the number of bytes to send.
    /// </param>
    /// <param name="completed">
    ///   <para>
    ///   An <see cref="Action"/> delegate or <see langword="null"/>
    ///   if not needed.
    ///   </para>
    ///   <para>
    ///   The delegate invokes the method called when the send is complete.
    ///   </para>
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The current state of the manager is not Start.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="stream"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="stream"/> cannot be read.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="length"/> is less than 1.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   No data could be read from <paramref name="stream"/>.
    ///   </para>
    /// </exception>
    [Obsolete ("This method will be removed.")]
    public void BroadcastAsync (Stream stream, int length, Action completed)
    {
      if (_state != ServerState.Start) {
        var msg = "The current state of the manager is not Start.";
        throw new InvalidOperationException (msg);
      }

      if (stream == null)
        throw new ArgumentNullException ("stream");

      if (!stream.CanRead) {
        var msg = "It cannot be read.";
        throw new ArgumentException (msg, "stream");
      }

      if (length < 1) {
        var msg = "Less than 1.";
        throw new ArgumentException (msg, "length");
      }

      var bytes = stream.ReadBytes (length);

      var len = bytes.Length;
      if (len == 0) {
        var msg = "No data could be read from it.";
        throw new ArgumentException (msg, "stream");
      }

      if (len < length) {
        _log.Warn (
          String.Format (
            "Only {0} byte(s) of data could be read from the stream.",
            len
          )
        );
      }

      if (len <= WebSocket.FragmentLength)
        broadcastAsync (Opcode.Binary, bytes, completed);
      else
        broadcastAsync (Opcode.Binary, new MemoryStream (bytes), completed);
    }

    /// <summary>
    /// Sends a ping to every client in the WebSocket services.
    /// </summary>
    /// <returns>
    ///   <para>
    ///   A <c>Dictionary&lt;string, Dictionary&lt;string, bool&gt;&gt;</c>.
    ///   </para>
    ///   <para>
    ///   It represents a collection of pairs of a service path and another
    ///   collection of pairs of a session ID and a value indicating whether
    ///   a pong has been received from the client within a time.
    ///   </para>
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// The current state of the manager is not Start.
    /// </exception>
    [Obsolete ("This method will be removed.")]
    public Dictionary<string, Dictionary<string, bool>> Broadping ()
    {
      if (_state != ServerState.Start) {
        var msg = "The current state of the manager is not Start.";
        throw new InvalidOperationException (msg);
      }

      return broadping (WebSocketFrame.EmptyPingBytes, _waitTime);
    }

    /// <summary>
    /// Sends a ping with <paramref name="message"/> to every client in
    /// the WebSocket services.
    /// </summary>
    /// <returns>
    ///   <para>
    ///   A <c>Dictionary&lt;string, Dictionary&lt;string, bool&gt;&gt;</c>.
    ///   </para>
    ///   <para>
    ///   It represents a collection of pairs of a service path and another
    ///   collection of pairs of a session ID and a value indicating whether
    ///   a pong has been received from the client within a time.
    ///   </para>
    /// </returns>
    /// <param name="message">
    ///   <para>
    ///   A <see cref="string"/> that represents the message to send.
    ///   </para>
    ///   <para>
    ///   The size must be 125 bytes or less in UTF-8.
    ///   </para>
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The current state of the manager is not Start.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="message"/> could not be UTF-8-encoded.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The size of <paramref name="message"/> is greater than 125 bytes.
    /// </exception>
    [Obsolete ("This method will be removed.")]
    public Dictionary<string, Dictionary<string, bool>> Broadping (string message)
    {
      if (_state != ServerState.Start) {
        var msg = "The current state of the manager is not Start.";
        throw new InvalidOperationException (msg);
      }

      if (message.IsNullOrEmpty ())
        return broadping (WebSocketFrame.EmptyPingBytes, _waitTime);

      byte[] bytes;
      if (!message.TryGetUTF8EncodedBytes (out bytes)) {
        var msg = "It could not be UTF-8-encoded.";
        throw new ArgumentException (msg, "message");
      }

      if (bytes.Length > 125) {
        var msg = "Its size is greater than 125 bytes.";
        throw new ArgumentOutOfRangeException ("message", msg);
      }

      var frame = WebSocketFrame.CreatePingFrame (bytes, false);
      return broadping (frame.ToArray (), _waitTime);
    }

    /// <summary>
    /// Removes all WebSocket services managed by the manager.
    /// </summary>
    /// <remarks>
    /// A service is stopped with close status 1001 (going away)
    /// if it has already started.
    /// </remarks>
    public void Clear ()
    {
      List<WebSocketServiceHost> hosts = null;

      lock (_sync) {
        hosts = _hosts.Values.ToList ();
        _hosts.Clear ();
      }

      foreach (var host in hosts) {
        if (host.State == ServerState.Start)
          host.Stop (1001, String.Empty);
      }
    }

    /// <summary>
    /// Removes a WebSocket service with the specified <paramref name="path"/>.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   <paramref name="path"/> is converted to a URL-decoded string and
    ///   / is trimmed from the end of the converted string if any.
    ///   </para>
    ///   <para>
    ///   The service is stopped with close status 1001 (going away)
    ///   if it has already started.
    ///   </para>
    /// </remarks>
    /// <returns>
    /// <c>true</c> if the service is successfully found and removed;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <param name="path">
    /// A <see cref="string"/> that represents an absolute path to
    /// the service to remove.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="path"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="path"/> is empty.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="path"/> is not an absolute path.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="path"/> includes either or both
    ///   query and fragment components.
    ///   </para>
    /// </exception>
    public bool RemoveService (string path)
    {
      if (path == null)
        throw new ArgumentNullException ("path");

      if (path.Length == 0)
        throw new ArgumentException ("An empty string.", "path");

      if (path[0] != '/')
        throw new ArgumentException ("Not an absolute path.", "path");

      if (path.IndexOfAny (new[] { '?', '#' }) > -1) {
        var msg = "It includes either or both query and fragment components.";
        throw new ArgumentException (msg, "path");
      }

      path = HttpUtility.UrlDecode (path).TrimSlashFromEnd ();

      WebSocketServiceHost host;
      lock (_sync) {
        if (!_hosts.TryGetValue (path, out host))
          return false;

        _hosts.Remove (path);
      }

      if (host.State == ServerState.Start)
        host.Stop (1001, String.Empty);

      return true;
    }

    /// <summary>
    /// Tries to get the host instance for a WebSocket service with
    /// the specified <paramref name="path"/>.
    /// </summary>
    /// <remarks>
    /// <paramref name="path"/> is converted to a URL-decoded string and
    /// / is trimmed from the end of the converted string if any.
    /// </remarks>
    /// <returns>
    /// <c>true</c> if the service is successfully found;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <param name="path">
    /// A <see cref="string"/> that represents an absolute path to
    /// the service to find.
    /// </param>
    /// <param name="host">
    ///   <para>
    ///   When this method returns, a <see cref="WebSocketServiceHost"/>
    ///   instance or <see langword="null"/> if not found.
    ///   </para>
    ///   <para>
    ///   That host instance provides the function to access
    ///   the information in the service.
    ///   </para>
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="path"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="path"/> is empty.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="path"/> is not an absolute path.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="path"/> includes either or both
    ///   query and fragment components.
    ///   </para>
    /// </exception>
    public bool TryGetServiceHost (string path, out WebSocketServiceHost host)
    {
      if (path == null)
        throw new ArgumentNullException ("path");

      if (path.Length == 0)
        throw new ArgumentException ("An empty string.", "path");

      if (path[0] != '/')
        throw new ArgumentException ("Not an absolute path.", "path");

      if (path.IndexOfAny (new[] { '?', '#' }) > -1) {
        var msg = "It includes either or both query and fragment components.";
        throw new ArgumentException (msg, "path");
      }

      return InternalTryGetServiceHost (path, out host);
    }

    #endregion
  }
}
