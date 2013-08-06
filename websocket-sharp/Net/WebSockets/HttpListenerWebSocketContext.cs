#region License
/*
 * HttpListenerWebSocketContext.cs
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
  /// Provides access to the WebSocket connection request objects received by the <see cref="HttpListener"/>.
  /// </summary>
  /// <remarks>
  /// </remarks>
  public class HttpListenerWebSocketContext : WebSocketContext
  {
    #region Private Fields

    private HttpListenerContext _context;
    private WebSocket           _websocket;
    private WsStream            _stream;

    #endregion

    #region Internal Constructors

    internal HttpListenerWebSocketContext (HttpListenerContext context)
    {
      _context = context;
      _stream = WsStream.CreateServerStream (context);
      _websocket = new WebSocket (this);
    }

    #endregion

    #region Internal Properties

    internal WsStream Stream {
      get {
        return _stream;
      }
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the cookies used in the WebSocket opening handshake.
    /// </summary>
    /// <value>
    /// A <see cref="WebSocketSharp.Net.CookieCollection"/> that contains the cookies.
    /// </value>
    public override CookieCollection CookieCollection {
      get {
        return _context.Request.Cookies;
      }
    }

    /// <summary>
    /// Gets the HTTP headers used in the WebSocket opening handshake.
    /// </summary>
    /// <value>
    /// A <see cref="System.Collections.Specialized.NameValueCollection"/> that contains the HTTP headers.
    /// </value>
    public override NameValueCollection Headers {
      get {
        return _context.Request.Headers;
      }
    }

    /// <summary>
    /// Gets the value of the Host header field used in the WebSocket opening handshake.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that contains the value of the Host header field.
    /// </value>
    public override string Host {
      get {
        return _context.Request.Headers ["Host"];
      }
    }

    /// <summary>
    /// Gets a value indicating whether the client is authenticated.
    /// </summary>
    /// <value>
    /// <c>true</c> if the client is authenticated; otherwise, <c>false</c>.
    /// </value>
    public override bool IsAuthenticated {
      get {
        return _context.Request.IsAuthenticated;
      }
    }

    /// <summary>
    /// Gets a value indicating whether the client connected from the local computer.
    /// </summary>
    /// <value>
    /// <c>true</c> if the client connected from the local computer; otherwise, <c>false</c>.
    /// </value>
    public override bool IsLocal {
      get {
        return _context.Request.IsLocal;
      }
    }

    /// <summary>
    /// Gets a value indicating whether the WebSocket connection is secured.
    /// </summary>
    /// <value>
    /// <c>true</c> if the WebSocket connection is secured; otherwise, <c>false</c>.
    /// </value>
    public override bool IsSecureConnection {
      get {
        return _context.Request.IsSecureConnection;
      }
    }

    /// <summary>
    /// Gets a value indicating whether the request is a WebSocket connection request.
    /// </summary>
    /// <value>
    /// <c>true</c> if the request is a WebSocket connection request; otherwise, <c>false</c>.
    /// </value>
    public override bool IsWebSocketRequest {
      get {
        return _context.Request.IsWebSocketRequest;
      }
    }

    /// <summary>
    /// Gets the value of the Origin header field used in the WebSocket opening handshake.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that contains the value of the Origin header field.
    /// </value>
    public override string Origin {
      get {
        return _context.Request.Headers ["Origin"];
      }
    }

    /// <summary>
    /// Gets the absolute path of the requested WebSocket URI.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that contains the absolute path of the requested WebSocket URI.
    /// </value>
    public override string Path {
      get {
        return RequestUri.GetAbsolutePath ();
      }
    }

    /// <summary>
    /// Gets the collection of query string variables used in the WebSocket opening handshake.
    /// </summary>
    /// <value>
    /// A <see cref="NameValueCollection"/> that contains the collection of query string variables.
    /// </value>
    public override NameValueCollection QueryString {
      get {
        return _context.Request.QueryString;
      }
    }

    /// <summary>
    /// Gets the WebSocket URI requested by the client.
    /// </summary>
    /// <value>
    /// A <see cref="Uri"/> that contains the WebSocket URI.
    /// </value>
    public override Uri RequestUri {
      get {
        return _context.Request.RawUrl.ToUri ();
      }
    }

    /// <summary>
    /// Gets the value of the Sec-WebSocket-Key header field used in the WebSocket opening handshake.
    /// </summary>
    /// <remarks>
    /// The SecWebSocketKey property provides a part of the information used by the server to prove that it received a valid WebSocket opening handshake.
    /// </remarks>
    /// <value>
    /// A <see cref="string"/> that contains the value of the Sec-WebSocket-Key header field.
    /// </value>
    public override string SecWebSocketKey {
      get {
        return _context.Request.Headers ["Sec-WebSocket-Key"];
      }
    }

    /// <summary>
    /// Gets the values of the Sec-WebSocket-Protocol header field used in the WebSocket opening handshake.
    /// </summary>
    /// <remarks>
    /// The SecWebSocketProtocols property indicates the subprotocols of the WebSocket connection.
    /// </remarks>
    /// <value>
    /// An IEnumerable&lt;string&gt; that contains the values of the Sec-WebSocket-Protocol header field.
    /// </value>
    public override IEnumerable<string> SecWebSocketProtocols {
      get {
        return _context.Request.Headers.GetValues ("Sec-WebSocket-Protocol");
      }
    }

    /// <summary>
    /// Gets the value of the Sec-WebSocket-Version header field used in the WebSocket opening handshake.
    /// </summary>
    /// <remarks>
    /// The SecWebSocketVersion property indicates the WebSocket protocol version of the connection.
    /// </remarks>
    /// <value>
    /// A <see cref="string"/> that contains the value of the Sec-WebSocket-Version header field.
    /// </value>
    public override string SecWebSocketVersion {
      get {
        return _context.Request.Headers ["Sec-WebSocket-Version"];
      }
    }

    /// <summary>
    /// Gets the server endpoint as an IP address and a port number.
    /// </summary>
    /// <value>
    /// A <see cref="System.Net.IPEndPoint"/> that contains the server endpoint.
    /// </value>
    public override System.Net.IPEndPoint ServerEndPoint {
      get {
        return _context.Connection.LocalEndPoint;
      }
    }

    /// <summary>
    /// Gets the client information (identity, authentication information and security roles).
    /// </summary>
    /// <value>
    /// A <see cref="IPrincipal"/> that contains the client information.
    /// </value>
    public override IPrincipal User {
      get {
        return _context.User;
      }
    }

    /// <summary>
    /// Gets the client endpoint as an IP address and a port number.
    /// </summary>
    /// <value>
    /// A <see cref="System.Net.IPEndPoint"/> that contains the client endpoint.
    /// </value>
    public override System.Net.IPEndPoint UserEndPoint {
      get {
        return _context.Connection.RemoteEndPoint;
      }
    }

    /// <summary>
    /// Gets the WebSocket instance used for two-way communication between client and server.
    /// </summary>
    /// <value>
    /// A <see cref="WebSocketSharp.WebSocket"/>.
    /// </value>
    public override WebSocket WebSocket {
      get {
        return _websocket;
      }
    }

    #endregion

    #region Internal Methods

    internal void Close ()
    {
      _context.Connection.Close (true);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Returns a <see cref="string"/> that represents the current <see cref="HttpListenerWebSocketContext"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that represents the current <see cref="HttpListenerWebSocketContext"/>.
    /// </returns>
    public override string ToString ()
    {
      return _context.Request.ToString ();
    }

    #endregion
  }
}
