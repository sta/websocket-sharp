#region License
/*
 * WebSocketContext.cs
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
using System.Collections.Specialized;
using System.Security.Principal;

namespace WebSocketSharp.Net.WebSockets
{
  /// <summary>
  /// Provides access to the WebSocket connection request objects.
  /// </summary>
  /// <remarks>
  /// The WebSocketContext class is an abstract class.
  /// </remarks>
  public abstract class WebSocketContext
  {
    #region Protected Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketContext"/> class.
    /// </summary>
    protected WebSocketContext ()
    {
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the cookies used in the WebSocket opening handshake.
    /// </summary>
    /// <value>
    /// A <see cref="WebSocketSharp.Net.CookieCollection"/> that contains the cookies.
    /// </value>
    public abstract CookieCollection CookieCollection { get; }

    /// <summary>
    /// Gets the HTTP headers used in the WebSocket opening handshake.
    /// </summary>
    /// <value>
    /// A <see cref="System.Collections.Specialized.NameValueCollection"/> that contains the HTTP headers.
    /// </value>
    public abstract NameValueCollection Headers { get; }

    /// <summary>
    /// Gets the value of the Host header field used in the WebSocket opening handshake.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that contains the value of the Host header field.
    /// </value>
    public abstract string Host { get; }

    /// <summary>
    /// Gets a value indicating whether the client is authenticated.
    /// </summary>
    /// <value>
    /// <c>true</c> if the client is authenticated; otherwise, <c>false</c>.
    /// </value>
    public abstract bool IsAuthenticated { get; }

    /// <summary>
    /// Gets a value indicating whether the client connected from the local computer.
    /// </summary>
    /// <value>
    /// <c>true</c> if the client connected from the local computer; otherwise, <c>false</c>.
    /// </value>
    public abstract bool IsLocal { get; }

    /// <summary>
    /// Gets a value indicating whether the WebSocket connection is secured.
    /// </summary>
    /// <value>
    /// <c>true</c> if the WebSocket connection is secured; otherwise, <c>false</c>.
    /// </value>
    public abstract bool IsSecureConnection { get; }

    /// <summary>
    /// Gets a value indicating whether the request is a WebSocket connection request.
    /// </summary>
    /// <value>
    /// <c>true</c> if the request is a WebSocket connection request; otherwise, <c>false</c>.
    /// </value>
    public abstract bool IsWebSocketRequest { get; }

    /// <summary>
    /// Gets the value of the Origin header field used in the WebSocket opening handshake.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that contains the value of the Origin header field.
    /// </value>
    public abstract string Origin { get; }

    /// <summary>
    /// Gets the absolute path of the requested WebSocket URI.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that contains the absolute path of the requested WebSocket URI.
    /// </value>
    public abstract string Path { get; }

    /// <summary>
    /// Gets the collection of query string variables used in the WebSocket opening handshake.
    /// </summary>
    /// <value>
    /// A <see cref="NameValueCollection"/> that contains the collection of query string variables.
    /// </value>
    public abstract NameValueCollection QueryString { get; }

    /// <summary>
    /// Gets the WebSocket URI requested by the client.
    /// </summary>
    /// <value>
    /// A <see cref="Uri"/> that contains the WebSocket URI.
    /// </value>
    public abstract Uri RequestUri { get; }

    /// <summary>
    /// Gets the value of the Sec-WebSocket-Key header field used in the WebSocket opening handshake.
    /// </summary>
    /// <remarks>
    /// The SecWebSocketKey property provides a part of the information used by the server to prove that it received a valid WebSocket opening handshake.
    /// </remarks>
    /// <value>
    /// A <see cref="string"/> that contains the value of the Sec-WebSocket-Key header field.
    /// </value>
    public abstract string SecWebSocketKey { get; }

    /// <summary>
    /// Gets the values of the Sec-WebSocket-Protocol header field used in the WebSocket opening handshake.
    /// </summary>
    /// <remarks>
    /// The SecWebSocketProtocols property indicates the subprotocols of the WebSocket connection.
    /// </remarks>
    /// <value>
    /// An IEnumerable&lt;string&gt; that contains the values of the Sec-WebSocket-Protocol header field.
    /// </value>
    public abstract IEnumerable<string> SecWebSocketProtocols { get; }

    /// <summary>
    /// Gets the value of the Sec-WebSocket-Version header field used in the WebSocket opening handshake.
    /// </summary>
    /// <remarks>
    /// The SecWebSocketVersion property indicates the WebSocket protocol version of the connection.
    /// </remarks>
    /// <value>
    /// A <see cref="string"/> that contains the value of the Sec-WebSocket-Version header field.
    /// </value>
    public abstract string SecWebSocketVersion { get; }

    /// <summary>
    /// Gets the server endpoint as an IP address and a port number.
    /// </summary>
    /// <value>
    /// A <see cref="System.Net.IPEndPoint"/> that contains the server endpoint.
    /// </value>
    public abstract System.Net.IPEndPoint ServerEndPoint { get; }

    /// <summary>
    /// Gets the client information (identity, authentication information and security roles).
    /// </summary>
    /// <value>
    /// A <see cref="IPrincipal"/> that contains the client information.
    /// </value>
    public abstract IPrincipal User { get; }

    /// <summary>
    /// Gets the client endpoint as an IP address and a port number.
    /// </summary>
    /// <value>
    /// A <see cref="System.Net.IPEndPoint"/> that contains the client endpoint.
    /// </value>
    public abstract System.Net.IPEndPoint UserEndPoint { get; }

    /// <summary>
    /// Gets the WebSocket instance used for two-way communication between client and server.
    /// </summary>
    /// <value>
    /// A <see cref="WebSocketSharp.WebSocket"/>.
    /// </value>
    public abstract WebSocket WebSocket { get; }

    #endregion
  }
}
