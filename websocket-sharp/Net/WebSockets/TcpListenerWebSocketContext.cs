#region License
/*
 * TcpListenerWebSocketContext.cs
 *
 * The MIT License
 *
 * Copyright (c) 2012-2016 sta.blockhead
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

#region Contributors
/*
 * Contributors:
 * - Liryna <liryna.stark@gmail.com>
 */
#endregion

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;

namespace WebSocketSharp.Net.WebSockets
{
  /// <summary>
  /// Provides the access to the information in a WebSocket handshake request to
  /// a <see cref="TcpListener"/> instance.
  /// </summary>
  internal class TcpListenerWebSocketContext : WebSocketContext
  {
    #region Private Fields

    private Logger              _log;
    private NameValueCollection _queryString;
    private HttpRequest         _request;
    private bool                _secure;
    private Stream              _stream;
    private TcpClient           _tcpClient;
    private Uri                 _uri;
    private IPrincipal          _user;
    private WebSocket           _websocket;

    #endregion

    #region Internal Constructors

    internal TcpListenerWebSocketContext (
      TcpClient tcpClient,
      string protocol,
      bool secure,
      ServerSslConfiguration sslConfig,
      Logger log
    )
    {
      _tcpClient = tcpClient;
      _secure = secure;
      _log = log;

      var netStream = tcpClient.GetStream ();
      if (secure) {
        var sslStream = new SslStream (
                          netStream,
                          false,
                          sslConfig.ClientCertificateValidationCallback
                        );

        sslStream.AuthenticateAsServer (
          sslConfig.ServerCertificate,
          sslConfig.ClientCertificateRequired,
          sslConfig.EnabledSslProtocols,
          sslConfig.CheckCertificateRevocation
        );

        _stream = sslStream;
      }
      else {
        _stream = netStream;
      }

      _request = HttpRequest.Read (_stream, 90000);
      _websocket = new WebSocket (this, protocol);
    }

    #endregion

    #region Internal Properties

    internal Logger Log {
      get {
        return _log;
      }
    }

    internal Stream Stream {
      get {
        return _stream;
      }
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the HTTP cookies included in the handshake request.
    /// </summary>
    /// <value>
    ///   <para>
    ///   A <see cref="WebSocketSharp.Net.CookieCollection"/> that contains
    ///   the cookies.
    ///   </para>
    ///   <para>
    ///   An empty collection if not included.
    ///   </para>
    /// </value>
    public override CookieCollection CookieCollection {
      get {
        return _request.Cookies;
      }
    }

    /// <summary>
    /// Gets the HTTP headers included in the handshake request.
    /// </summary>
    /// <value>
    /// A <see cref="NameValueCollection"/> that contains the headers.
    /// </value>
    public override NameValueCollection Headers {
      get {
        return _request.Headers;
      }
    }

    /// <summary>
    /// Gets the value of the Host header included in the handshake request.
    /// </summary>
    /// <value>
    ///   <para>
    ///   A <see cref="string"/> that represents the server host name requested
    ///   by the client.
    ///   </para>
    ///   <para>
    ///   It includes the port number if provided.
    ///   </para>
    /// </value>
    public override string Host {
      get {
        return _request.Headers["Host"];
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
        return _user != null;
      }
    }

    /// <summary>
    /// Gets a value indicating whether the handshake request is sent from
    /// the local computer.
    /// </summary>
    /// <value>
    /// <c>true</c> if the handshake request is sent from the same computer
    /// as the server; otherwise, <c>false</c>.
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
    /// <c>true</c> if the connection is secured; otherwise, <c>false</c>.
    /// </value>
    public override bool IsSecureConnection {
      get {
        return _secure;
      }
    }

    /// <summary>
    /// Gets a value indicating whether the request is a WebSocket handshake
    /// request.
    /// </summary>
    /// <value>
    /// <c>true</c> if the request is a WebSocket handshake request; otherwise,
    /// <c>false</c>.
    /// </value>
    public override bool IsWebSocketRequest {
      get {
        return _request.IsWebSocketRequest;
      }
    }

    /// <summary>
    /// Gets the value of the Origin header included in the request.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the value of the Origin header.
    /// </value>
    public override string Origin {
      get {
        return _request.Headers["Origin"];
      }
    }

    /// <summary>
    /// Gets the query string included in the handshake request.
    /// </summary>
    /// <value>
    ///   <para>
    ///   A <see cref="NameValueCollection"/> that contains the query
    ///   parameters.
    ///   </para>
    ///   <para>
    ///   An empty collection if not included.
    ///   </para>
    /// </value>
    public override NameValueCollection QueryString {
      get {
        if (_queryString == null) {
          var uri = RequestUri;
          _queryString = HttpUtility.InternalParseQueryString (
                           uri != null ? uri.Query : null,
                           Encoding.UTF8
                         );
        }

        return _queryString;
      }
    }

    /// <summary>
    /// Gets the URI requested by the client.
    /// </summary>
    /// <value>
    ///   <para>
    ///   A <see cref="Uri"/> that represents the URI parsed from the request.
    ///   </para>
    ///   <para>
    ///   <see langword="null"/> if the URI cannot be parsed.
    ///   </para>
    /// </value>
    public override Uri RequestUri {
      get {
        if (_uri == null) {
          _uri = HttpUtility.CreateRequestUrl (
                   _request.RequestUri,
                   _request.Headers["Host"],
                   _request.IsWebSocketRequest,
                   _secure
                 );
        }

        return _uri;
      }
    }

    /// <summary>
    /// Gets the value of the Sec-WebSocket-Key header included in the request.
    /// </summary>
    /// <remarks>
    /// This property provides a part of the information used by the server to prove that
    /// it received a valid WebSocket handshake request.
    /// </remarks>
    /// <value>
    /// A <see cref="string"/> that represents the value of the Sec-WebSocket-Key header.
    /// </value>
    public override string SecWebSocketKey {
      get {
        return _request.Headers["Sec-WebSocket-Key"];
      }
    }

    /// <summary>
    /// Gets the names of the subprotocols from the Sec-WebSocket-Protocol
    /// header included in the handshake request.
    /// </summary>
    /// <value>
    ///   <para>
    ///   An <see cref="T:System.Collections.Generic.IEnumerable{string}"/>
    ///   instance.
    ///   </para>
    ///   <para>
    ///   It provides an enumerator which supports the iteration over
    ///   the collection of the names of the subprotocols.
    ///   </para>
    /// </value>
    public override IEnumerable<string> SecWebSocketProtocols {
      get {
        var val = _request.Headers["Sec-WebSocket-Protocol"];
        if (val == null || val.Length == 0)
          yield break;

        foreach (var elm in val.Split (',')) {
          var protocol = elm.Trim ();
          if (protocol.Length == 0)
            continue;

          yield return protocol;
        }
      }
    }

    /// <summary>
    /// Gets the value of the Sec-WebSocket-Version header included in the request.
    /// </summary>
    /// <remarks>
    /// This property represents the WebSocket protocol version.
    /// </remarks>
    /// <value>
    /// A <see cref="string"/> that represents the value of the Sec-WebSocket-Version header.
    /// </value>
    public override string SecWebSocketVersion {
      get {
        return _request.Headers["Sec-WebSocket-Version"];
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
        return (System.Net.IPEndPoint) _tcpClient.Client.LocalEndPoint;
      }
    }

    /// <summary>
    /// Gets the client information (identity, authentication, and security roles).
    /// </summary>
    /// <value>
    /// A <see cref="IPrincipal"/> instance that represents the client information.
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
        return (System.Net.IPEndPoint) _tcpClient.Client.RemoteEndPoint;
      }
    }

    /// <summary>
    /// Gets the WebSocket instance used for two-way communication between
    /// the client and server.
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

    internal bool Authenticate (
      AuthenticationSchemes scheme,
      string realm,
      Func<IIdentity, NetworkCredential> credentialsFinder
    )
    {
      if (scheme == AuthenticationSchemes.Anonymous)
        return true;

      if (scheme == AuthenticationSchemes.None) {
        Close (HttpStatusCode.Forbidden);
        return false;
      }

      var chal = new AuthenticationChallenge (scheme, realm).ToString ();

      var retry = -1;
      Func<bool> auth = null;
      auth =
        () => {
          retry++;
          if (retry > 99) {
            Close (HttpStatusCode.Forbidden);
            return false;
          }

          var user =
            HttpUtility.CreateUser (
              _request.Headers["Authorization"],
              scheme,
              realm,
              _request.HttpMethod,
              credentialsFinder
            );

          if (user == null || !user.Identity.IsAuthenticated) {
            SendAuthenticationChallenge (chal);
            return auth ();
          }

          _user = user;
          return true;
        };

      return auth ();
    }

    internal void Close ()
    {
      _stream.Close ();
      _tcpClient.Close ();
    }

    internal void Close (HttpStatusCode code)
    {
      _websocket.Close (HttpResponse.CreateCloseResponse (code));
    }

    internal void SendAuthenticationChallenge (string challenge)
    {
      var buff = HttpResponse.CreateUnauthorizedResponse (challenge).ToByteArray ();
      _stream.Write (buff, 0, buff.Length);
      _request = HttpRequest.Read (_stream, 15000);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Returns a string that represents the current instance.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that contains the request line and headers
    /// included in the handshake request.
    /// </returns>
    public override string ToString ()
    {
      return _request.ToString ();
    }

    #endregion
  }
}
