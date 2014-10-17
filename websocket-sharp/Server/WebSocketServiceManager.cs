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

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using WebSocketSharp.Net;

namespace WebSocketSharp.Server
{
	using System.Linq;
	using System.Threading.Tasks;

	/// <summary>
	/// Manages the WebSocket services provided by the <see cref="HttpServer"/> or
	/// <see cref="WebSocketServer"/>.
	/// </summary>
	public class WebSocketServiceManager
	{
		#region Private Fields

		private volatile bool _clean;
		private Dictionary<string, WebSocketServiceHost> _hosts;
		private volatile ServerState _state;
		private object _sync;
		private TimeSpan _waitTime;

		#endregion

		#region Internal Constructors

		internal WebSocketServiceManager()
		{
			_clean = true;
			_hosts = new Dictionary<string, WebSocketServiceHost>();
			_state = ServerState.Ready;
			_sync = ((ICollection)_hosts).SyncRoot;
			_waitTime = TimeSpan.FromSeconds(1);
		}

		#endregion

		#region Public Properties

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
					return _hosts.Count;
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
					return _hosts.Values.ToList();
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
		/// Gets a value indicating whether the manager cleans up the inactive sessions
		/// in the WebSocket services periodically.
		/// </summary>
		/// <value>
		/// <c>true</c> if the manager cleans up the inactive sessions every 60 seconds;
		/// otherwise, <c>false</c>.
		/// </value>
		public bool KeepClean
		{
			get
			{
				return _clean;
			}

			internal set
			{
				lock (_sync)
				{
					if (!(value ^ _clean))
						return;

					_clean = value;
					foreach (var host in _hosts.Values)
						host.KeepClean = value;
				}
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
					return _hosts.Keys.ToList();
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
				var cnt = 0;
				foreach (var host in Hosts)
				{
					if (_state != ServerState.Start)
						break;

					cnt += host.Sessions.Count;
				}

				return cnt;
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

		#endregion

		#region Private Methods

		private void broadcast(Opcode opcode, byte[] data)
		{
			var cache = new Dictionary<CompressionMethod, byte[]>();
			try
			{
				foreach (var host in this.Hosts.TakeWhile(host => this._state == ServerState.Start))
				{
					host.Sessions.Broadcast(opcode, data, cache);
				}
			}
			finally
			{
				cache.Clear();
			}
		}

		private void broadcast(Opcode opcode, Stream stream)
		{
			var cache = new Dictionary<CompressionMethod, Stream>();
			try
			{
				foreach (var host in this.Hosts.TakeWhile(host => this._state == ServerState.Start))
				{
					host.Sessions.Broadcast(opcode, stream, cache);
				}
			}
			finally
			{
				foreach (var cached in cache.Values)
				{
					cached.Dispose();
				}

				cache.Clear();
			}
		}

		private Task broadcastAsync(Opcode opcode, byte[] data)
		{
			return Task.Factory.StartNew(() => broadcast(opcode, data));
		}

		private Task broadcastAsync(Opcode opcode, Stream stream)
		{
			return Task.Factory.StartNew(() => broadcast(opcode, stream));
		}

		private Dictionary<string, Dictionary<string, bool>> broadping(
		  byte[] frameAsBytes, TimeSpan timeout)
		{
			var res = new Dictionary<string, Dictionary<string, bool>>();
			foreach (var host in Hosts)
			{
				if (_state != ServerState.Start)
					break;

				res.Add(host.Path, host.Sessions.Broadping(frameAsBytes, timeout));
			}

			return res;
		}

		#endregion

		#region Internal Methods

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

				host = new WebSocketServiceHost<TBehavior>(path, initializer);
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

		#endregion

		#region Public Methods

		/// <summary>
		/// Broadcasts a binary <paramref name="data"/> to every client in the WebSocket services.
		/// </summary>
		/// <param name="data">
		/// An array of <see cref="byte"/> that represents the binary data to broadcast.
		/// </param>
		public void Broadcast(byte[] data)
		{
			var msg = _state.CheckIfStart() ?? data.CheckIfValidSendData();
			if (msg != null)
			{
				return;
			}

			if (data.LongLength <= WebSocket.FragmentLength)
			{
				broadcast(Opcode.Binary, data);
			}
			else
			{
				broadcast(Opcode.Binary, new MemoryStream(data));
			}
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
			if (rawData.LongLength <= WebSocket.FragmentLength)
			{
				broadcast(Opcode.Text, rawData);
			}
			else
			{
				broadcast(Opcode.Text, new MemoryStream(rawData));
			}
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
		/// <param name="completed">
		/// An <see cref="Action"/> delegate that references the method(s) called when
		/// the broadcast is complete.
		/// </param>
		public Task BroadcastAsync(byte[] data)
		{
			var msg = _state.CheckIfStart() ?? data.CheckIfValidSendData();
			if (msg != null)
			{
				return Task.Factory.StartNew(() => { });
			}

			return data.LongLength <= WebSocket.FragmentLength ? this.broadcastAsync(Opcode.Binary, data) : this.broadcastAsync(Opcode.Binary, new MemoryStream(data));
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
		/// <param name="completed">
		/// An <see cref="Action"/> delegate that references the method(s) called when
		/// the broadcast is complete.
		/// </param>
		public Task BroadcastAsync(string data, Action completed)
		{
			var msg = _state.CheckIfStart() ?? data.CheckIfValidSendData();
			if (msg != null)
			{
				return Task.Factory.StartNew(() => { });
			}

			var rawData = Encoding.UTF8.GetBytes(data);
			return rawData.LongLength <= WebSocket.FragmentLength ? this.broadcastAsync(Opcode.Text, rawData) : this.broadcastAsync(Opcode.Text, new MemoryStream(rawData));
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
		/// <param name="completed">
		/// An <see cref="Action"/> delegate that references the method(s) called when
		/// the broadcast is complete.
		/// </param>
		public async Task BroadcastAsync(Stream stream, int length)
		{
			var msg = _state.CheckIfStart() ??
					  stream.CheckIfCanRead() ??
					  (length < 1 ? "'length' is less than 1." : null);

			if (msg != null)
			{
				return;
			}

			var data = await stream.ReadBytesAsync(length);
			var len = data.Length;

			if (len <= WebSocket.FragmentLength)
				broadcast(Opcode.Binary, data);
			else
				broadcast(Opcode.Binary, new MemoryStream(data));
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
		public Dictionary<string, Dictionary<string, bool>> Broadping()
		{
			var msg = _state.CheckIfStart();
			if (msg != null)
			{
				return null;
			}

			return broadping(WebSocketFrame.EmptyUnmaskPingBytes, _waitTime);
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
		public Dictionary<string, Dictionary<string, bool>> Broadping(string message)
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

			return broadping(WebSocketFrame.CreatePingFrame(data, false).ToByteArray(), _waitTime);
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

		#endregion
	}
}
