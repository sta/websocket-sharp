#region License
/*
 * IServiceHost.cs
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
using WebSocketSharp.Net.WebSockets;

namespace WebSocketSharp.Server
{
  /// <summary>
  /// Exposes the methods and properties for the WebSocket service host.
  /// </summary>
  /// <remarks>
  /// </remarks>
  public interface IServiceHost
  {
    /// <summary>
    /// Gets the connection count to the WebSocket service host.
    /// </summary>
    /// <value>
    /// An <see cref="int"/> that contains the connection count.
    /// </value>
    int ConnectionCount { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the WebSocket service host cleans up the inactive service
    /// instances periodically.
    /// </summary>
    /// <value>
    /// <c>true</c> if the WebSocket service host cleans up the inactive service instances periodically;
    /// otherwise, <c>false</c>.
    /// </value>
    bool KeepClean { get; set; }

    /// <summary>
    /// Binds the specified <see cref="WebSocketContext"/> to a <see cref="WebSocketService"/> instance.
    /// </summary>
    /// <param name="context">
    /// A <see cref="WebSocketContext"/> that contains the WebSocket connection request objects to bind.
    /// </param>
    void BindWebSocket (WebSocketContext context);

    /// <summary>
    /// Broadcasts the specified array of <see cref="byte"/> to all clients of the WebSocket service host.
    /// </summary>
    /// <param name="data">
    /// An array of <see cref="byte"/> to broadcast.
    /// </param>
    void Broadcast (byte [] data);

    /// <summary>
    /// Broadcasts the specified <see cref="string"/> to all clients of the WebSocket service host.
    /// </summary>
    /// <param name="data">
    /// A <see cref="string"/> to broadcast.
    /// </param>
    void Broadcast (string data);

    /// <summary>
    /// Sends Pings with the specified <see cref="string"/> to all clients of the WebSocket service host.
    /// </summary>
    /// <returns>
    /// A Dictionary&lt;string, bool&gt; that contains the collection of session IDs and values
    /// indicating whether the WebSocket service host received the Pongs from each clients in a time.
    /// </returns>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to send.
    /// </param>
    Dictionary<string, bool> Broadping (string message);

    /// <summary>
    /// Sends a Ping with the specified <see cref="string"/> to the client associated with
    /// the specified ID.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the WebSocket service host receives a Pong from the client in a time;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <param name="id">
    /// A <see cref="string"/> that contains an ID that represents the destination for the Ping.
    /// </param>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to send.
    /// </param>
    bool PingTo (string id, string message);

    /// <summary>
    /// Sends a binary data to the client associated with the specified ID.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the client associated with <paramref name="id"/> is successfully found;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <param name="id">
    /// A <see cref="string"/> that contains an ID that represents the destination for the data.
    /// </param>
    /// <param name="data">
    /// An array of <see cref="byte"/> that contains a binary data to send.
    /// </param>
    bool SendTo (string id, byte [] data);

    /// <summary>
    /// Sends a text data to the client associated with the specified ID.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the client associated with <paramref name="id"/> is successfully found;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <param name="id">
    /// A <see cref="string"/> that contains an ID that represents the destination for the data.
    /// </param>
    /// <param name="data">
    /// A <see cref="string"/> that contains a text data to send.
    /// </param>
    bool SendTo (string id, string data);

    /// <summary>
    /// Starts the WebSocket service host.
    /// </summary>
    void Start ();

    /// <summary>
    /// Stops the WebSocket service host.
    /// </summary>
    void Stop ();

    /// <summary>
    /// Stops the WebSocket service host with the specified <see cref="ushort"/> and <see cref="string"/>.
    /// </summary>
    /// <param name="code">
    /// A <see cref="ushort"/> that contains a status code indicating the reason for stop.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that contains the reason for stop.
    /// </param>
    void Stop (ushort code, string reason);
  }
}
