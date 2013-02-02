#region MIT License
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

namespace WebSocketSharp.Server {

  /// <summary>
  /// Exposes the methods and property for the WebSocket service host.
  /// </summary>
  /// <remarks>
  /// </remarks>
  public interface IServiceHost {

    /// <summary>
    /// Gets or sets a value indicating whether the WebSocket service host cleans up the inactive service clients periodically.
    /// </summary>
    /// <value>
    /// <c>true</c> if the WebSocket service host cleans up the inactive service clients periodically; otherwise, <c>false</c>.
    /// </value>
    bool Sweeped { get; set; }

    /// <summary>
    /// Binds the specified <see cref="WebSocket"/> to the WebSocket service instance.
    /// </summary>
    /// <param name="socket">
    /// A <see cref="WebSocketSharp.WebSocket"/> to bind.
    /// </param>
    void BindWebSocket(WebSocket socket);

    /// <summary>
    /// Broadcasts the specified <see cref="string"/> to all service clients.
    /// </summary>
    /// <param name="data">
    /// A <see cref="string"/> to broadcast.
    /// </param>
    void Broadcast(string data);

    /// <summary>
    /// Starts the WebSocket service host.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops the WebSocket service host.
    /// </summary>
    void Stop();
  }
}
