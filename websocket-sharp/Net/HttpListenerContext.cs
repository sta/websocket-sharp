#region License
/*
 * HttpListenerContext.cs
 *
 * This code is derived from System.Net.HttpListenerContext.cs of Mono
 * (http://www.mono-project.com).
 *
 * The MIT License
 *
 * Copyright (c) 2005 Novell, Inc. (http://www.novell.com)
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

#region Authors
/*
 * Authors:
 * - Gonzalo Paniagua Javier <gonzalo@novell.com>
 */
#endregion

using System;
using System.Security.Principal;
using WebSocketSharp.Net.WebSockets;

namespace WebSocketSharp.Net
{
  /// <summary>
  /// Provides the access to the HTTP request and response information
  /// used by the <see cref="HttpListener"/>.
  /// </summary>
  /// <remarks>
  /// The HttpListenerContext class cannot be inherited.
  /// </remarks>
  public sealed class HttpListenerContext
  {
    #region Private Fields

    private HttpConnection       _connection;
    private string               _error;
    private int                  _errorStatus;
    private HttpListener         _listener;
    private HttpListenerRequest  _request;
    private HttpListenerResponse _response;
    private IPrincipal           _user;

    #endregion

    #region Internal Constructors

    internal HttpListenerContext (HttpConnection connection)
    {
      _connection = connection;
      _errorStatus = 400;
      _request = new HttpListenerRequest (this);
      _response = new HttpListenerResponse (this);
    }

    #endregion

    #region Internal Properties

    internal HttpConnection Connection {
      get {
        return _connection;
      }
    }

    internal string ErrorMessage {
      get {
        return _error;
      }

      set {
        _error = value;
      }
    }

    internal int ErrorStatus {
      get {
        return _errorStatus;
      }

      set {
        _errorStatus = value;
      }
    }

    internal bool HasError {
      get {
        return _error != null;
      }
    }

    internal HttpListener Listener {
      get {
        return _listener;
      }

      set {
        _listener = value;
      }
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the HTTP request information from a client.
    /// </summary>
    /// <value>
    /// A <see cref="HttpListenerRequest"/> that represents the HTTP request.
    /// </value>
    public HttpListenerRequest Request {
      get {
        return _request;
      }
    }

    /// <summary>
    /// Gets the HTTP response information used to send to the client.
    /// </summary>
    /// <value>
    /// A <see cref="HttpListenerResponse"/> that represents the HTTP response to send.
    /// </value>
    public HttpListenerResponse Response {
      get {
        return _response;
      }
    }

    /// <summary>
    /// Gets the client information (identity, authentication, and security roles).
    /// </summary>
    /// <value>
    /// A <see cref="IPrincipal"/> instance that represents the client information.
    /// </value>
    public IPrincipal User {
      get {
        return _user;
      }

      internal set {
        _user = value;
      }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Accepts a WebSocket connection request.
    /// </summary>
    /// <returns>
    /// A <see cref="HttpListenerWebSocketContext"/> that represents the WebSocket connection
    /// request.
    /// </returns>
    /// <param name="protocol">
    /// A <see cref="string"/> that represents the subprotocol used in the WebSocket connection.
    /// </param>
    /// <param name="logger">
    /// A <see cref="Logger"/> that provides the logging functions used in the WebSocket attempts.
    /// </param>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="protocol"/> is empty.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="protocol"/> contains an invalid character.
    ///   </para>
    /// </exception>
    public HttpListenerWebSocketContext AcceptWebSocket (string protocol, Logger logger)
    {
      if (protocol != null) {
        if (protocol.Length == 0)
          throw new ArgumentException ("An empty string.", "protocol");

        if (!protocol.IsToken ())
          throw new ArgumentException ("Contains an invalid character.", "protocol");
      }

      return new HttpListenerWebSocketContext (this, protocol, logger ?? new Logger ());
    }

    #endregion
  }
}
