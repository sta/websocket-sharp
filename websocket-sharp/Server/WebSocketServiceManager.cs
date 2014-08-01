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

		private Dictionary<string, WebSocketServiceHost> _hosts;
		private volatile bool _keepClean;
		private Logger _logger;
		private volatile ServerState _state;
		private object _sync;

		#endregion

		#region Internal Constructors

		internal WebSocketServiceManager()
			: this(new Logger())
		{
		}

		internal WebSocketServiceManager(Logger logger)
		{
			_logger = logger;

			_hosts = new Dictionary<string, WebSocketServiceHost>();
			_keepClean = true;
			_state = ServerState.Ready;
			_sync = new object();
		}

		#endregion

		#region Public Properties

		/// <summary>
		/// Gets the number of the WebSocket services provided by the server.
		/// </summary>
		/// <value>
		/// An <see cref="int"/> that represents the number of the WebSocket services.
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
		/// Gets the collection of every information in the Websocket services provided by the server.
		/// </summary>
		/// <value>
		/// An IEnumerable&lt;WebSocketServiceHost&gt; that contains the collection of every
		/// information in the Websocket services.
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
		/// Gets the information in a WebSocket service with the specified <paramref name="path"/>.
		/// </summary>
		/// <value>
		/// A <see cref="WebSocketServiceHost"/> instance that provides the access to the WebSocket
		/// service if it's successfully found; otherwise, <see langword="null"/>.
		/// </value>
		/// <param name="path">
		/// A <see cref="string"/> that represents the absolute path to the WebSocket service to find.
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
		/// Gets a value indicating whether the manager cleans up the inactive sessions in the
		/// WebSocket services provided by the server periodically.
		/// </summary>
		/// <value>
		/// <c>true</c> if the manager cleans up the inactive sessions every 60 seconds; otherwise,
		/// <c>false</c>.
		/// </value>
		public bool KeepClean
		{
			get
			{
				return _keepClean;
			}

			internal set
			{
				lock (_sync)
				{
					if (!(value ^ _keepClean))
						return;

					_keepClean = value;
					foreach (var host in _hosts.Values)
						host.KeepClean = value;
				}
			}
		}

		/// <summary>
		/// Gets the collection of every path to the WebSocket services provided by the server.
		/// </summary>
		/// <value>
		/// An IEnumerable&lt;string&gt; that contains the collection of every path to the WebSocket
		/// services.
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
		/// Gets the number of the sessions in the WebSocket services provided by the server.
		/// </summary>
		/// <value>
		/// An <see cref="int"/> that represents the number of the sessions.
		/// </value>
		public int SessionCount
		{
			get
			{
				var count = 0;
				foreach (var host in Hosts)
				{
					if (_state != ServerState.Start)
						break;

					count += host.Sessions.Count;
				}

				return count;
			}
		}

		#endregion

		#region Private Methods

		private void broadcast(Opcode opcode, byte[] data)
		{
			var cache = new Dictionary<CompressionMethod, byte[]>();
			try
			{
				foreach (var host in this.Hosts.TakeWhile(host => _state == ServerState.Start))
				{
					host.Sessions.Broadcast(opcode, data, cache);
				}
			}
			catch (Exception ex)
			{
				_logger.Fatal(ex.ToString());
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
				foreach (var host in this.Hosts.TakeWhile(host => _state == ServerState.Start))
				{
					host.Sessions.Broadcast(opcode, stream, cache);
				}
			}
			catch (Exception ex)
			{
				_logger.Fatal(ex.ToString());
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
		  byte[] frame, int millisecondsTimeout)
		{
			return this.Hosts.TakeWhile(host => _state == ServerState.Start)
				.ToDictionary(host => host.Path, host => host.Sessions.Broadping(frame, millisecondsTimeout));
		}

		#endregion

		#region Internal Methods

		internal void Add(string path, WebSocketServiceHost host)
		{
			lock (_sync)
			{
				WebSocketServiceHost find;
				if (_hosts.TryGetValue(path, out find))
				{
					_logger.Error(
					  "A WebSocket service with the specified path already exists.\npath: " + path);

					return;
				}

				if (_state == ServerState.Start)
					host.Sessions.Start();

				_hosts.Add(path, host);
			}
		}

		internal bool Remove(string path)
		{
			path = HttpUtility.UrlDecode(path).TrimEndSlash();

			WebSocketServiceHost host;
			lock (_sync)
			{
				if (!_hosts.TryGetValue(path, out host))
				{
					_logger.Error("A WebSocket service with the specified path not found.\npath: " + path);
					return false;
				}

				_hosts.Remove(path);
			}

			if (host.Sessions.State == ServerState.Start)
				host.Sessions.Stop(
				  ((ushort)CloseStatusCode.Away).ToByteArrayInternally(ByteOrder.Big), true);

			return true;
		}

		internal void Start()
		{
			lock (_sync)
			{
				foreach (var host in _hosts.Values)
					host.Sessions.Start();

				_state = ServerState.Start;
			}
		}

		internal void Stop(byte[] data, bool send)
		{
			lock (_sync)
			{
				_state = ServerState.ShuttingDown;

				var payload = new PayloadData(data);
				var args = new CloseEventArgs(payload);
				var frameAsBytes =
				  send ? WebSocketFrame.CreateCloseFrame(Mask.Unmask, payload).ToByteArray() : null;

				foreach (var host in _hosts.Values)
					host.Sessions.Stop(args, frameAsBytes);

				_hosts.Clear();
				_state = ServerState.Stop;
			}
		}

		internal bool TryGetServiceHostInternally(string path, out WebSocketServiceHost host)
		{
			path = HttpUtility.UrlDecode(path).TrimEndSlash();

			bool result;
			lock (_sync)
			{
				result = _hosts.TryGetValue(path, out host);
			}

			if (!result)
				_logger.Error("A WebSocket service with the specified path not found.\npath: " + path);

			return result;
		}

		#endregion

		#region Public Methods

		/// <summary>
		/// Broadcasts a binary <paramref name="data"/> to every client in the WebSocket services
		/// provided by the server.
		/// </summary>
		/// <param name="data">
		/// An array of <see cref="byte"/> that represents the binary data to broadcast.
		/// </param>
		public void Broadcast(byte[] data)
		{
			var msg = _state.CheckIfStart() ?? data.CheckIfValidSendData();
			if (msg != null)
			{
				_logger.Error(msg);
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
		/// Broadcasts a text <paramref name="data"/> to every client in the WebSocket services
		/// provided by the server.
		/// </summary>
		/// <param name="data">
		/// A <see cref="string"/> that represents the text data to broadcast.
		/// </param>
		public void Broadcast(string data)
		{
			var msg = _state.CheckIfStart() ?? data.CheckIfValidSendData();
			if (msg != null)
			{
				_logger.Error(msg);
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
		/// Broadcasts a binary <paramref name="data"/> asynchronously to every client in the WebSocket
		/// services provided by the server.
		/// </summary>
		/// <remarks>
		/// This method doesn't wait for the broadcast to be complete.
		/// </remarks>
		/// <param name="data">
		/// An array of <see cref="byte"/> that represents the binary data to broadcast.
		/// </param>
		/// <param name="completed">
		/// A <see cref="Action"/> delegate that references the method(s) called when the broadcast is
		/// complete.
		/// </param>
		public Task BroadcastAsync(byte[] data)
		{
			var msg = _state.CheckIfStart() ?? data.CheckIfValidSendData();
			if (msg != null)
			{
				_logger.Error(msg);
				return Task.FromResult(false);
			}

			if (data.LongLength <= WebSocket.FragmentLength)
			{
				return broadcastAsync(Opcode.Binary, data);
			}

			return broadcastAsync(Opcode.Binary, new MemoryStream(data));
		}

		/// <summary>
		/// Broadcasts a text <paramref name="data"/> asynchronously to every client in the WebSocket
		/// services provided by the server.
		/// </summary>
		/// <remarks>
		/// This method doesn't wait for the broadcast to be complete.
		/// </remarks>
		/// <param name="data">
		/// A <see cref="string"/> that represents the text data to broadcast.
		/// </param>
		public Task BroadcastAsync(string data)
		{
			var msg = _state.CheckIfStart() ?? data.CheckIfValidSendData();
			if (msg != null)
			{
				_logger.Error(msg);
				return Task.FromResult(false);
			}

			var rawData = Encoding.UTF8.GetBytes(data);
			if (rawData.LongLength <= WebSocket.FragmentLength)
			{
				return broadcastAsync(Opcode.Text, rawData);
			}

			return broadcastAsync(Opcode.Text, new MemoryStream(rawData));
		}

		/// <summary>
		/// Broadcasts a binary data from the specified <see cref="Stream"/> asynchronously to every
		/// client in the WebSocket services provided by the server.
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
		public async Task BroadcastAsync(Stream stream, int length)
		{
			try
			{
				var msg = _state.CheckIfStart() ?? stream.CheckIfCanRead() ?? (length < 1 ? "'length' must be greater than 0." : null);

				if (msg != null)
				{
					_logger.Error(msg);
					return;
				}

				var data = await stream.ReadBytesAsync(length);

				var len = data.Length;
				if (len == 0)
				{
					_logger.Error("A data cannot be read from 'stream'.");
					return;
				}

				if (len < length)
				{
					_logger.Warn(String.Format("A data with 'length' cannot be read from 'stream'.\nexpected: {0} actual: {1}", length, len));
				}

				if (len <= WebSocket.FragmentLength)
				{
					broadcast(Opcode.Binary, data);
				}
				else
				{
					broadcast(Opcode.Binary, new MemoryStream(data));
				}
			}
			catch (Exception ex)
			{
				_logger.Fatal(ex.ToString());
			}
		}

		/// <summary>
		/// Sends a Ping to every client in the WebSocket services provided by the server.
		/// </summary>
		/// <returns>
		/// A Dictionary&lt;string, Dictionary&lt;string, bool&gt;&gt; that contains the collection of
		/// pairs of service path and collection of pairs of session ID and value indicating whether
		/// the manager received a Pong from every client in a time. If this method isn't available,
		/// returns <see langword="null"/>.
		/// </returns>
		public Dictionary<string, Dictionary<string, bool>> Broadping()
		{
			var msg = _state.CheckIfStart();
			if (msg != null)
			{
				_logger.Error(msg);
				return null;
			}

			return broadping(WebSocketFrame.EmptyUnmaskPingData, 1000);
		}

		/// <summary>
		/// Sends a Ping with the specified <paramref name="message"/> to every client in the WebSocket
		/// services provided by the server.
		/// </summary>
		/// <returns>
		/// A Dictionary&lt;string, Dictionary&lt;string, bool&gt;&gt; that contains the collection of
		/// pairs of service path and collection of pairs of session ID and value indicating whether
		/// the manager received a Pong from every client in a time. If this method isn't available or
		/// <paramref name="message"/> is invalid, returns <see langword="null"/>.
		/// </returns>
		/// <param name="message">
		/// A <see cref="string"/> that represents the message to send.
		/// </param>
		public Dictionary<string, Dictionary<string, bool>> Broadping(string message)
		{
			if (message == null || message.Length == 0)
				return Broadping();

			byte[] data = null;
			var msg = _state.CheckIfStart() ??
					  (data = Encoding.UTF8.GetBytes(message)).CheckIfValidControlData("message");

			if (msg != null)
			{
				_logger.Error(msg);
				return null;
			}

			return broadping(WebSocketFrame.CreatePingFrame(Mask.Unmask, data).ToByteArray(), 1000);
		}

		/// <summary>
		/// Tries to get the information in a WebSocket service with the specified
		/// <paramref name="path"/>.
		/// </summary>
		/// <returns>
		/// <c>true</c> if the WebSocket service is successfully found; otherwise, <c>false</c>.
		/// </returns>
		/// <param name="path">
		/// A <see cref="string"/> that represents the absolute path to the WebSocket service to find.
		/// </param>
		/// <param name="host">
		/// When this method returns, a <see cref="WebSocketServiceHost"/> instance that
		/// provides the access to the WebSocket service if it's successfully found;
		/// otherwise, <see langword="null"/>. This parameter is passed uninitialized.
		/// </param>
		public bool TryGetServiceHost(string path, out WebSocketServiceHost host)
		{
			var msg = _state.CheckIfStart() ?? path.CheckIfValidServicePath();
			if (msg != null)
			{
				_logger.Error(msg);
				host = null;

				return false;
			}

			return TryGetServiceHostInternally(path, out host);
		}

		#endregion
	}
}
