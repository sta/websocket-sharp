#region License
/*
 * WebSocketServiceManager.cs
 *
 * The MIT License
 *
 * Copyright (c) 2012-2014 sta.blockhead
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

namespace WebSocketSharp.Server
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    using WebSocketSharp.Net;

    using System.Linq;
    using System.Threading.Tasks;

    /// <summary>
    /// Manages the WebSocket services provided by the <see cref="WebSocketServer"/> or
    /// <see cref="WebSocketServer"/>.
    /// </summary>
    public class WebSocketServiceManager
    {
        private readonly int _fragmentSize;

        private volatile bool _clean;
        private readonly Dictionary<string, WebSocketServiceHost> _hosts;
        private volatile ServerState _state;
        private readonly object _sync;
        private TimeSpan _waitTime;

        internal WebSocketServiceManager(int fragmentSize, bool keepClean = true)
        {
            _fragmentSize = fragmentSize;
            _clean = keepClean;
            _hosts = new Dictionary<string, WebSocketServiceHost>();
            _state = ServerState.Ready;
            _sync = ((ICollection)_hosts).SyncRoot;
            _waitTime = TimeSpan.FromSeconds(1);
        }

        /// <summary>
        /// Gets the number of the WebSocket services.
        /// </summary>
        /// <value>
        /// An <see cref="int"/> that represents the number of the services.
        /// </value>
        public int Count
        {
            get
            {
                lock (_sync)
                {
                    return _hosts.Count;
                }
            }
        }

        /// <summary>
        /// Gets the host instances for the Websocket services.
        /// </summary>
        /// <value>
        /// An <c>IEnumerable&lt;WebSocketServiceHost&gt;</c> instance that provides an enumerator
        /// which supports the iteration over the collection of the host instances for the services.
        /// </value>
        public IEnumerable<WebSocketServiceHost> Hosts
        {
            get
            {
                lock (_sync)
                {
                    return _hosts.Values.ToList();
                }
            }
        }

        /// <summary>
        /// Gets the WebSocket service host with the specified <paramref name="path"/>.
        /// </summary>
        /// <value>
        /// A <see cref="WebSocketServiceHost"/> instance that provides the access to
        /// the information in the service, or <see langword="null"/> if it's not found.
        /// </value>
        /// <param name="path">
        /// A <see cref="string"/> that represents the absolute path to the service to find.
        /// </param>
        public WebSocketServiceHost this[string path]
        {
            get
            {
                WebSocketServiceHost host;
                TryGetServiceHost(path, out host);

                return host;
            }
        }

        /// <summary>
        /// Gets the paths for the WebSocket services.
        /// </summary>
        /// <value>
        /// An <c>IEnumerable&lt;string&gt;</c> instance that provides an enumerator which supports
        /// the iteration over the collection of the paths for the services.
        /// </value>
        public IEnumerable<string> Paths
        {
            get
            {
                lock (_sync)
                {
                    return _hosts.Keys.ToList();
                }
            }
        }

        /// <summary>
        /// Gets the total number of the sessions in the WebSocket services.
        /// </summary>
        /// <value>
        /// An <see cref="int"/> that represents the total number of the sessions in the services.
        /// </value>
        public int SessionCount
        {
            get
            {
                return Hosts.TakeWhile(host => _state == ServerState.Start).Sum(host => host.Sessions.Count);
            }
        }

        /// <summary>
        /// Gets the wait time for the response to the WebSocket Ping or Close.
        /// </summary>
        /// <value>
        /// A <see cref="TimeSpan"/> that represents the wait time.
        /// </value>
        public TimeSpan WaitTime
        {
            get
            {
                return _waitTime;
            }

            internal set
            {
                lock (_sync)
                {
                    if (value == _waitTime)
                        return;

                    _waitTime = value;
                    foreach (var host in _hosts.Values)
                        host.WaitTime = value;
                }
            }
        }

        /// <summary>
        /// Broadcasts a binary <paramref name="data"/> to every client in the WebSocket services.
        /// </summary>
        /// <param name="data">
        /// An array of <see cref="byte"/> that represents the binary data to broadcast.
        /// </param>
        public Task Broadcast(byte[] data)
        {
            var msg = _state.CheckIfStart() ?? data.CheckIfValidSendData();
            if (msg != null)
            {
                return Task.FromResult(false);
            }

            return data.LongLength <= _fragmentSize
                ? InnerBroadcast(Opcode.Binary, data)
                : InnerBroadcast(Opcode.Binary, new MemoryStream(data));
        }

        /// <summary>
        /// Broadcasts a text <paramref name="data"/> to every client in the WebSocket services.
        /// </summary>
        /// <param name="data">
        /// A <see cref="string"/> that represents the text data to broadcast.
        /// </param>
        public void Broadcast(string data)
        {
            var msg = _state.CheckIfStart() ?? data.CheckIfValidSendData();
            if (msg != null)
            {
                return;
            }

            var rawData = Encoding.UTF8.GetBytes(data);
            if (rawData.LongLength <= _fragmentSize)
            {
                InnerBroadcast(Opcode.Text, rawData);
            }
            else
            {
                InnerBroadcast(Opcode.Text, new MemoryStream(rawData));
            }
        }

        /// <summary>
        /// Broadcasts a text <paramref name="data"/> to every client in the WebSocket services.
        /// </summary>
        /// <param name="data">
        /// A <see cref="string"/> that represents the text data to broadcast.
        /// </param>
        public void Broadcast(Stream data)
        {
            var msg = _state.CheckIfStart() ?? data.CheckIfValidSendData();
            if (msg != null)
            {
                return;
            }

            InnerBroadcast(Opcode.Binary, data);
        }

        /// <summary>
        /// Broadcasts a binary <paramref name="data"/> asynchronously to every client
        /// in the WebSocket services.
        /// </summary>
        /// <remarks>
        /// This method doesn't wait for the broadcast to be complete.
        /// </remarks>
        /// <param name="data">
        /// An array of <see cref="byte"/> that represents the binary data to broadcast.
        /// </param>
        public Task BroadcastAsync(byte[] data)
        {
            return Task.Run(() => Broadcast(data));
        }

        /// <summary>
        /// Broadcasts a text <paramref name="data"/> asynchronously to every client
        /// in the WebSocket services.
        /// </summary>
        /// <remarks>
        /// This method doesn't wait for the broadcast to be complete.
        /// </remarks>
        /// <param name="data">
        /// A <see cref="string"/> that represents the text data to broadcast.
        /// </param>
        public Task BroadcastAsync(string data)
        {
            return Task.Run(() => Broadcast(data));
        }

        /// <summary>
        /// Broadcasts a binary data from the specified <see cref="Stream"/> asynchronously
        /// to every client in the WebSocket services.
        /// </summary>
        /// <remarks>
        /// This method doesn't wait for the broadcast to be complete.
        /// </remarks>
        /// <param name="stream">
        /// A <see cref="Stream"/> from which contains the binary data to broadcast.
        /// </param>
        /// <param name="length">
        /// An <see cref="int"/> that represents the number of bytes to broadcast.
        /// </param>
        public Task BroadcastAsync(Stream stream)
        {
            return Task.Run(() => Broadcast(stream));
        }

        /// <summary>
        /// Sends a Ping to every client in the WebSocket services.
        /// </summary>
        /// <returns>
        /// A <c>Dictionary&lt;string, Dictionary&lt;string, bool&gt;&gt;</c> that contains
        /// a collection of pairs of a service path and a collection of pairs of a session ID
        /// and a value indicating whether the manager received a Pong from each client in a time,
        /// or <see langword="null"/> if this method isn't available.
        /// </returns>
        public Task<IDictionary<string, IDictionary<string, bool>>> Broadping()
        {
            var msg = _state.CheckIfStart();
            if (msg != null)
            {
                return null;
            }

            return Broadping(WebSocketFrame.EmptyUnmaskPingBytes, _waitTime);
        }

        /// <summary>
        /// Sends a Ping with the specified <paramref name="message"/> to every client
        /// in the WebSocket services.
        /// </summary>
        /// <returns>
        /// A <c>Dictionary&lt;string, Dictionary&lt;string, bool&gt;&gt;</c> that contains
        /// a collection of pairs of a service path and a collection of pairs of a session ID
        /// and a value indicating whether the manager received a Pong from each client in a time,
        /// or <see langword="null"/> if this method isn't available or <paramref name="message"/>
        /// is invalid.
        /// </returns>
        /// <param name="message">
        /// A <see cref="string"/> that represents the message to send.
        /// </param>
        public Task<IDictionary<string, IDictionary<string, bool>>> Broadping(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return Broadping();
            }

            byte[] data = null;
            var msg = _state.CheckIfStart() ??
                      (data = Encoding.UTF8.GetBytes(message)).CheckIfValidControlData("message");

            if (msg != null)
            {
                return null;
            }

            return Broadping(WebSocketFrame.CreatePingFrame(data, false).ToByteArray(), _waitTime);
        }

        /// <summary>
        /// Tries to get the WebSocket service host with the specified <paramref name="path"/>.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the service is successfully found; otherwise, <c>false</c>.
        /// </returns>
        /// <param name="path">
        /// A <see cref="string"/> that represents the absolute path to the service to find.
        /// </param>
        /// <param name="host">
        /// When this method returns, a <see cref="WebSocketServiceHost"/> instance that provides
        /// the access to the information in the service, or <see langword="null"/> if it's not found.
        /// This parameter is passed uninitialized.
        /// </param>
        public bool TryGetServiceHost(string path, out WebSocketServiceHost host)
        {
            var msg = _state.CheckIfStart() ?? path.CheckIfValidServicePath();
            if (msg != null)
            {
                host = null;

                return false;
            }

            return InternalTryGetServiceHost(path, out host);
        }

        internal void Add<TBehavior>(string path, Func<TBehavior> initializer)
          where TBehavior : WebSocketBehavior
        {
            lock (_sync)
            {
                path = HttpUtility.UrlDecode(path).TrimEndSlash();

                WebSocketServiceHost host;
                if (_hosts.TryGetValue(path, out host))
                {
                    return;
                }

                host = new WebSocketServiceHost<TBehavior>(path, _fragmentSize, initializer);
                if (!_clean)
                    host.KeepClean = false;

                if (_waitTime != host.WaitTime)
                    host.WaitTime = _waitTime;

                if (_state == ServerState.Start)
                    host.Start();

                _hosts.Add(path, host);
            }
        }

        internal bool InternalTryGetServiceHost(string path, out WebSocketServiceHost host)
        {
            bool res;
            lock (_sync)
            {
                path = HttpUtility.UrlDecode(path).TrimEndSlash();
                res = _hosts.TryGetValue(path, out host);
            }

            return res;
        }

        internal bool Remove(string path)
        {
            WebSocketServiceHost host;
            lock (_sync)
            {
                path = HttpUtility.UrlDecode(path).TrimEndSlash();
                if (!_hosts.TryGetValue(path, out host))
                {
                    return false;
                }

                _hosts.Remove(path);
            }

            if (host.State == ServerState.Start)
                host.Stop((ushort)CloseStatusCode.Away, null);

            return true;
        }

        internal void Start()
        {
            lock (_sync)
            {
                foreach (var host in _hosts.Values)
                    host.Start();

                _state = ServerState.Start;
            }
        }

        internal void Stop(CloseEventArgs e, bool send, bool wait)
        {
            lock (_sync)
            {
                _state = ServerState.ShuttingDown;

                var bytes =
                  send ? WebSocketFrame.CreateCloseFrame(e.PayloadData, false).ToByteArray() : null;

                var timeout = wait ? _waitTime : TimeSpan.Zero;
                foreach (var host in _hosts.Values)
                    host.Sessions.Stop(e, bytes, timeout);

                _hosts.Clear();
                _state = ServerState.Stop;
            }
        }

        private async Task<bool> InnerBroadcast(Opcode opcode, byte[] data)
        {
            var results =
                Hosts
                .TakeWhile(host => _state == ServerState.Start)
                .Select(host => host.Sessions.InnerBroadcast(opcode, data))
                .ToArray();
            await Task.WhenAll(results).ConfigureAwait(false);
            return results.All(x => x.Result);
        }

        private async Task<bool> InnerBroadcast(Opcode opcode, Stream stream)
        {
            var tasks =
                Hosts.TakeWhile(host => _state == ServerState.Start)
                    .Select(host => host.Sessions.InnerBroadcast(opcode, stream))
                    .ToArray();

            await Task.WhenAll(tasks).ConfigureAwait(false);

            return tasks.All(x => x.Result);
        }

        private async Task<IDictionary<string, IDictionary<string, bool>>> Broadping(byte[] frameAsBytes, TimeSpan timeout)
        {
            var tasks = Hosts.TakeWhile(host => _state == ServerState.Start)
                .ToDictionary(host => host.Path, host => host.Sessions.InnerBroadping(frameAsBytes, timeout));
            await Task.WhenAll(tasks.Values).ConfigureAwait(false);

            return tasks.ToDictionary(x => x.Key, x => x.Value.Result);
        }
    }
}
