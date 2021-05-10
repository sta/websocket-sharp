#region License
/*
 * HttpListenerContext.cs
 *
 * This code is derived from HttpListenerContext.cs (System.Net) of Mono
 * (http://www.mono-project.com).
 *
 * The MIT License
 *
 * Copyright (c) 2005 Novell, Inc. (http://www.novell.com)
 * Copyright (c) 2012-2020 sta.blockhead
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
using System.Text;
using WebSocketSharp.Net.WebSockets;

namespace WebSocketSharp.Net
{
  /// <summary>
  /// Provides the access to the HTTP request and response objects used by
  /// the <see cref="HttpListener"/> class.
  /// </summary>
  /// <remarks>
  /// This class cannot be inherited.
  /// </remarks>
  public sealed class HttpListenerContext
  {
    #region Private Fields

    private HttpConnection               _connection;
    private string                       _errorMessage;
    private int                          _errorStatusCode;
    private HttpListener                 _listener;
    private HttpListenerRequest          _request;
    private HttpListenerResponse         _response;
    private IPrincipal                   _user;
    private HttpListenerWebSocketContext _websocketContext;

    #endregion

    #region Internal Constructors

    internal HttpListenerContext (HttpConnection connection)
    {
      _connection = connection;

      _errorStatusCode = 400;
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
        return _errorMessage;
      }

      set {
        _errorMessage = value;
      }
    }

    internal int ErrorStatusCode {
      get {
        return _errorStatusCode;
      }

      set {
        _errorStatusCode = value;
      }
    }

    internal bool HasErrorMessage {
      get {
        return _errorMessage != null;
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
    /// Gets the HTTP request object that represents a client request.
    /// </summary>
    /// <value>
    /// A <see cref="HttpListenerRequest"/> that represents the client request.
    /// </value>
    public HttpListenerRequest Request {
      get {
        return _request;
      }
    }

    /// <summary>
    /// Gets the HTTP response object used to send a response to the client.
    /// </summary>
    /// <value>
    /// A <see cref="HttpListenerResponse"/> that represents a response to
    /// the client request.
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
    ///   <para>
    ///   A <see cref="IPrincipal"/> instance or <see langword="null"/> if not
    ///   authenticated.
    ///   </para>
    ///   <para>
    ///   The instance describes the client.
    ///   </para>
    /// </value>
    public IPrincipal User {
      get {
        return _user;
      }
    }

    #endregion

    #region Private Methods

    private static string createErrorContent (
      int statusCode, string statusDescription, string message
    )
    {
      return message != null && message.Length > 0
             ? String.Format (
                 "<html><body><h1>{0} {1} ({2})</h1></body></html>",
                 statusCode,
                 statusDescription,
                 message
               )
             : String.Format (
                 "<html><body><h1>{0} {1}</h1></body></html>",
                 statusCode,
                 statusDescription
               );
    }

    private void sendAuthenticationChallenge (string challenge)
    {
      _response.StatusCode = 401;
      _response.Headers.InternalSet ("WWW-Authenticate", challenge, true);

      _response.Close ();
    }

    #endregion

    #region Internal Methods

    internal bool Authenticate ()
    {
      var schm = _listener.SelectAuthenticationScheme (_request);

      if (schm == AuthenticationSchemes.Anonymous)
        return true;

      if (schm == AuthenticationSchemes.None) {
        _errorStatusCode = 403;
        _errorMessage = "Authentication not allowed";
        SendError ();

        return false;
      }

      var realm = _listener.GetRealm ();
      var user = HttpUtility.CreateUser (
                   _request.Headers["Authorization"],
                   schm,
                   realm,
                   _request.HttpMethod,
                   _listener.GetUserCredentialsFinder ()
                 );

      if (user == null || !user.Identity.IsAuthenticated) {
        var chal = new AuthenticationChallenge (schm, realm).ToString ();
        sendAuthenticationChallenge (chal);

        return false;
      }

      _user = user;

      return true;
    }

    internal HttpListenerWebSocketContext GetWebSocketContext (string protocol)
    {
      _websocketContext = new HttpListenerWebSocketContext (this, protocol);

      return _websocketContext;
    }

    internal bool Register ()
    {
      return _listener.RegisterContext (this);
    }

    internal void SendError ()
    {
      try {
        _response.StatusCode = _errorStatusCode;
        _response.ContentType = "text/html";

        var content = createErrorContent (
                        _errorStatusCode,
                        _response.StatusDescription,
                        _errorMessage
                      );

        var enc = Encoding.UTF8;
        var entity = enc.GetBytes (content);
        _response.ContentEncoding = enc;
        _response.ContentLength64 = entity.LongLength;

        _response.Close (entity, true);
      }
      catch {
        _connection.Close (true);
      }
    }

    internal void Unregister ()
    {
      _listener.UnregisterContext (this);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Accepts a WebSocket handshake request.
    /// </summary>
    /// <returns>
    /// A <see cref="HttpListenerWebSocketContext"/> that represents
    /// the WebSocket handshake request.
    /// </returns>
    /// <param name="protocol">
    /// A <see cref="string"/> that specifies the subprotocol supported on
    /// the WebSocket connection.
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
    /// <exception cref="InvalidOperationException">
    /// This method has already been called.
    /// </exception>
    public HttpListenerWebSocketContext AcceptWebSocket (string protocol)
    {
      if (_websocketContext != null) {
        var msg = "The accepting is already in progress.";

        throw new InvalidOperationException (msg);
      }

      if (protocol != null) {
        if (protocol.Length == 0) {
          var msg = "An empty string.";

          throw new ArgumentException (msg, "protocol");
        }

        if (!protocol.IsToken ()) {
          var msg = "It contains an invalid character.";

          throw new ArgumentException (msg, "protocol");
        }
      }

      return GetWebSocketContext (protocol);
    }

    #endregion
  }
}
