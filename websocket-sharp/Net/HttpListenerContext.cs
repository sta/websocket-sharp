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

#region Authors
/*
 * Authors:
 *   Gonzalo Paniagua Javier <gonzalo@novell.com>
 */
#endregion

using System;
using System.Collections.Specialized;
using System.IO;
using System.Security.Principal;
using System.Text;
using WebSocketSharp.Net.WebSockets;

namespace WebSocketSharp.Net
{
  /// <summary>
  /// Provides access to the HTTP request and response information used by the
  /// <see cref="HttpListener"/>.
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
    private HttpListenerRequest  _request;
    private HttpListenerResponse _response;
    private IPrincipal           _user;

    #endregion

    #region Internal Fields

    internal HttpListener Listener;

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

    internal bool HaveError {
      get {
        return _error != null && _error.Length > 0;
      }
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the <see cref="HttpListenerRequest"/> that contains the HTTP
    /// request information from a client.
    /// </summary>
    /// <value>
    /// A <see cref="HttpListenerRequest"/> that contains the HTTP request
    /// information.
    /// </value>
    public HttpListenerRequest Request {
      get {
        return _request;
      }
    }

    /// <summary>
    /// Gets the <see cref="HttpListenerResponse"/> that contains the HTTP
    /// response information to send to the client in response to the client's
    /// request.
    /// </summary>
    /// <value>
    /// A <see cref="HttpListenerResponse"/> that contains the HTTP response
    /// information.
    /// </value>
    public HttpListenerResponse Response {
      get {
        return _response;
      }
    }

    /// <summary>
    /// Gets the client information (identity, authentication information, and
    /// security roles).
    /// </summary>
    /// <value>
    /// A <see cref="IPrincipal"/> contains the client information.
    /// </value>
    public IPrincipal User {
      get {
        return _user;
      }
    }

    #endregion

    #region Internal Methods

    internal void SetUser (
      AuthenticationSchemes expectedScheme,
      string realm,
      Func<IIdentity, NetworkCredential> credentialsFinder)
    {
      var authRes = AuthenticationResponse.Parse (_request.Headers ["Authorization"]);
      if (authRes == null)
        return;

      var identity = authRes.ToIdentity ();
      if (identity == null)
        return;

      NetworkCredential credentials = null;
      try {
        credentials = credentialsFinder (identity);
      }
      catch {
      }

      if (credentials == null)
        return;

      var valid = expectedScheme == AuthenticationSchemes.Basic
                ? ((HttpBasicIdentity) identity).Password == credentials.Password
                : expectedScheme == AuthenticationSchemes.Digest
                  ? ((HttpDigestIdentity) identity).IsValid (
                      credentials.Password, realm, _request.HttpMethod, null)
                  : false;

      if (valid)
        _user = new GenericPrincipal (identity, credentials.Roles);
    }

    #endregion

    #region Public Method

    /// <summary>
    /// Accepts a WebSocket connection request.
    /// </summary>
    /// <returns>
    /// A <see cref="HttpListenerWebSocketContext"/> that contains a WebSocket
    /// connection request information.
    /// </returns>
    /// <param name="logger">
    /// A <see cref="Logger"/> that provides the logging functions used in the
    /// WebSocket attempts.
    /// </param>
    public HttpListenerWebSocketContext AcceptWebSocket (Logger logger)
    {
      return new HttpListenerWebSocketContext (this, logger);
    }

    #endregion
  }
}
