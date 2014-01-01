#region License
/*
 * TcpListenerWebSocketContext.cs
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
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;

namespace WebSocketSharp.Net.WebSockets
{
  /// <summary>
  /// Provides access to the WebSocket connection request information received by
  /// the <see cref="TcpListener"/>.
  /// </summary>
  /// <remarks>
  /// </remarks>
  public class TcpListenerWebSocketContext : WebSocketContext
  {
    #region Private Fields

    private CookieCollection _cookies;
    private TcpClient        _client;
    private HandshakeRequest _request;
    private bool             _secure;
    private WsStream         _stream;
    private Uri              _uri;
    private IPrincipal       _user;
    private WebSocket        _websocket;

    #endregion

    #region Internal Constructors

    internal TcpListenerWebSocketContext (
      TcpClient client, X509Certificate cert, bool secure, Logger logger)
    {
      _client = client;
      _secure = secure;
      _stream = WsStream.CreateServerStream (client, cert, secure);
      _request = HandshakeRequest.Parse (_stream.ReadHandshake ());
      _websocket = new WebSocket (this, logger);
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
    /// Gets the cookies used in the WebSocket connection request.
    /// </summary>
    /// <value>
    /// A <see cref="WebSocketSharp.Net.CookieCollection"/> that contains the
    /// cookies.
    /// </value>
    public override CookieCollection CookieCollection {
      get {
        return _cookies ?? (_cookies = _request.Cookies);
      }
    }

    /// <summary>
    /// Gets the HTTP headers used in the WebSocket connection request.
    /// </summary>
    /// <value>
    /// A <see cref="NameValueCollection"/> that contains the HTTP headers.
    /// </value>
    public override NameValueCollection Headers {
      get {
        return _request.Headers;
      }
    }

    /// <summary>
    /// Gets the value of the Host header field used in the WebSocket connection
    /// request.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the value of the Host header field.
    /// </value>
    public override string Host {
      get {
        return _request.Headers ["Host"];
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
        return _user != null && _user.Identity.IsAuthenticated;
      }
    }

    /// <summary>
    /// Gets a value indicating whether the client connected from the local
    /// computer.
    /// </summary>
    /// <value>
    /// <c>true</c> if the client connected from the local computer; otherwise,
    /// <c>false</c>.
    /// </value>
    public override bool IsLocal {
      get {
        return UserEndPoint.Address.IsLocal ();
      }
    }

    /// <summary>
    /// Gets a value indicating whether the WebSocket connection is secured.
    /// </summary>
    /// <value>
    /// <c>true</c> if the WebSocket connection is secured; otherwise,
    /// <c>false</c>.
    /// </value>
    public override bool IsSecureConnection {
      get {
        return _secure;
      }
    }

    /// <summary>
    /// Gets a value indicating whether the request is a WebSocket connection
    /// request.
    /// </summary>
    /// <value>
    /// <c>true</c> if the request is a WebSocket connection request; otherwise,
    /// <c>false</c>.
    /// </value>
    public override bool IsWebSocketRequest {
      get {
        return _request.IsWebSocketRequest;
      }
    }

    /// <summary>
    /// Gets the value of the Origin header field used in the WebSocket
    /// connection request.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the value of the Origin header
    /// field.
    /// </value>
    public override string Origin {
      get {
        return _request.Headers ["Origin"];
      }
    }

    /// <summary>
    /// Gets the absolute path of the requested WebSocket URI.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the absolute path of the requested
    /// WebSocket URI.
    /// </value>
    public override string Path {
      get {
        return _request.RequestUri.GetAbsolutePath ();
      }
    }

    /// <summary>
    /// Gets the collection of query string variables used in the WebSocket
    /// connection request.
    /// </summary>
    /// <value>
    /// A <see cref="NameValueCollection"/> that contains the collection of query
    /// string variables.
    /// </value>
    public override NameValueCollection QueryString {
      get {
        return _request.QueryString;
      }
    }

    /// <summary>
    /// Gets the WebSocket URI requested by the client.
    /// </summary>
    /// <value>
    /// A <see cref="Uri"/> that represents the WebSocket URI requested by the
    /// client.
    /// </value>
    public override Uri RequestUri {
      get {
        return _uri ?? (_uri = createRequestUri ());
      }
    }

    /// <summary>
    /// Gets the value of the Sec-WebSocket-Key header field used in the
    /// WebSocket connection request.
    /// </summary>
    /// <remarks>
    /// This property provides a part of the information used by the server to
    /// prove that it received a valid WebSocket connection request.
    /// </remarks>
    /// <value>
    /// A <see cref="string"/> that represents the value of the Sec-WebSocket-Key
    /// header field.
    /// </value>
    public override string SecWebSocketKey {
      get {
        return _request.Headers ["Sec-WebSocket-Key"];
      }
    }

    /// <summary>
    /// Gets the values of the Sec-WebSocket-Protocol header field used in the
    /// WebSocket connection request.
    /// </summary>
    /// <remarks>
    /// This property represents the subprotocols of the WebSocket connection.
    /// </remarks>
    /// <value>
    /// An IEnumerable&lt;string&gt; that contains the values of the
    /// Sec-WebSocket-Protocol header field.
    /// </value>
    public override IEnumerable<string> SecWebSocketProtocols {
      get {
        return _request.Headers.GetValues ("Sec-WebSocket-Protocol");
      }
    }

    /// <summary>
    /// Gets the value of the Sec-WebSocket-Version header field used in the
    /// WebSocket connection request.
    /// </summary>
    /// <remarks>
    /// This property represents the WebSocket protocol version of the connection.
    /// </remarks>
    /// <value>
    /// A <see cref="string"/> that represents the value of the
    /// Sec-WebSocket-Version header field.
    /// </value>
    public override string SecWebSocketVersion {
      get {
        return _request.Headers ["Sec-WebSocket-Version"];
      }
    }

    /// <summary>
    /// Gets the server endpoint as an IP address and a port number.
    /// </summary>
    /// <value>
    /// A <see cref="System.Net.IPEndPoint"/> that represents the server endpoint.
    /// </value>
    public override System.Net.IPEndPoint ServerEndPoint {
      get {
        return (System.Net.IPEndPoint) _client.Client.LocalEndPoint;
      }
    }

    /// <summary>
    /// Gets the client information (identity, authentication information and
    /// security roles).
    /// </summary>
    /// <value>
    /// A <see cref="IPrincipal"/> that represents the client information.
    /// </value>
    public override IPrincipal User {
      get {
        return _user;
      }
    }

    /// <summary>
    /// Gets the client endpoint as an IP address and a port number.
    /// </summary>
    /// <value>
    /// A <see cref="System.Net.IPEndPoint"/> that represents the client endpoint.
    /// </value>
    public override System.Net.IPEndPoint UserEndPoint {
      get {
        return (System.Net.IPEndPoint) _client.Client.RemoteEndPoint;
      }
    }

    /// <summary>
    /// Gets the WebSocket instance used for two-way communication between client
    /// and server.
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

    #region Private Methods

    private Uri createRequestUri ()
    {
      var scheme = _secure ? "wss" : "ws";
      var host = _request.Headers ["Host"];
      var rawUri = _request.RequestUri;
      var path = rawUri.IsAbsoluteUri
               ? rawUri.PathAndQuery
               : HttpUtility.UrlDecode (_request.RawUrl);

      return String.Format ("{0}://{1}{2}", scheme, host, path).ToUri ();
    }

    #endregion

    #region Internal Methods

    internal void Close ()
    {
      _stream.Close ();
      _client.Close ();
    }

    internal void Close (HttpStatusCode code)
    {
      _websocket.Close (HandshakeResponse.CreateCloseResponse (code));
    }

    internal void SendAuthChallenge (string challenge)
    {
      var res = new HandshakeResponse (HttpStatusCode.Unauthorized);
      res.Headers ["WWW-Authenticate"] = challenge;
      _stream.WriteHandshake (res);
      _request = HandshakeRequest.Parse (_stream.ReadHandshake ());
    }

    internal void SetUser (
      AuthenticationSchemes expectedScheme,
      string realm,
      Func<IIdentity, NetworkCredential> credentialsFinder)
    {
      var authRes = _request.AuthResponse;
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

    #region Public Methods

    /// <summary>
    /// Returns a <see cref="string"/> that represents the current
    /// <see cref="TcpListenerWebSocketContext"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that represents the current
    /// <see cref="TcpListenerWebSocketContext"/>.
    /// </returns>
    public override string ToString ()
    {
      return _request.ToString ();
    }

    #endregion
  }
}
