#region License
/*
 * IWebSocketServiceHost.cs
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
  public interface IWebSocketServiceHost
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
    /// Sends Pings to all clients of the WebSocket service host.
    /// </summary>
    /// <returns>
    /// A Dictionary&lt;string, bool&gt; that contains the collection of pairs of session ID and value
    /// indicating whether the WebSocket service host received a Pong from each client in a time.
    /// </returns>
    Dictionary<string, bool> Broadping ();

    /// <summary>
    /// Sends Pings with the specified <paramref name="data"/> to all clients of the WebSocket service host.
    /// </summary>
    /// <returns>
    /// A Dictionary&lt;string, bool&gt; that contains the collection of pairs of session ID and value
    /// indicating whether the WebSocket service host received a Pong from each client in a time.
    /// </returns>
    /// <param name="data">
    /// An array of <see cref="byte"/> that contains a message data to send.
    /// </param>
    Dictionary<string, bool> Broadping (byte [] data);

    /// <summary>
    /// Close the WebSocket session with the specified <paramref name="id"/>.
    /// </summary>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID to find.
    /// </param>
    void CloseSession (string id);

    /// <summary>
    /// Close the WebSocket session with the specified <paramref name="code"/>, <paramref name="reason"/>
    /// and <paramref name="id"/>.
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
    void CloseSession (ushort code, string reason, string id);

    /// <summary>
    /// Close the WebSocket session with the specified <paramref name="code"/>, <paramref name="reason"/>
    /// and <paramref name="id"/>.
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
    void CloseSession (CloseStatusCode code, string reason, string id);

    /// <summary>
    /// Sends a Ping to the client associated with the specified <paramref name="id"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the WebSocket service host receives a Pong from the client in a time;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID that represents the destination for the Ping.
    /// </param>
    bool PingTo (string id);

    /// <summary>
    /// Sends a Ping with the specified <paramref name="message"/> to the client associated with
    /// the specified <paramref name="id"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the WebSocket service host receives a Pong from the client in a time;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to send.
    /// </param>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID that represents the destination for the Ping.
    /// </param>
    bool PingTo (string message, string id);

    /// <summary>
    /// Sends a binary data to the client associated with the specified <paramref name="id"/>.
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
    bool SendTo (byte [] data, string id);

    /// <summary>
    /// Sends a text data to the client associated with the specified <paramref name="id"/>.
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
    bool SendTo (string data, string id);

    /// <summary>
    /// Stops the WebSocket service host.
    /// </summary>
    void Stop ();

    /// <summary>
    /// Stops the WebSocket service host with the specified array of <see cref="byte"/>.
    /// </summary>
    /// <param name="data">
    /// An array of <see cref="byte"/> that contains the reason for stop.
    /// </param>
    void Stop (byte [] data);
  }
}
