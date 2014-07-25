#region License
/*
 * WebSocketStream.cs
 *
 * The MIT License
 *
 * Copyright (c) 2010-2014 sta.blockhead
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
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using WebSocketSharp.Net;
using WebSocketSharp.Net.Security;

namespace WebSocketSharp
{
  internal class WebSocketStream : IDisposable
  {
    #region Private Fields

    private Stream _innerStream;
    private bool   _secure;

    #endregion

    #region Internal Constructors

    internal WebSocketStream (Stream innerStream, bool secure)
    {
      _innerStream = innerStream;
      _secure = secure;
    }

    #endregion

    #region Public Properties

    public bool DataAvailable {
      get {
        return _secure
               ? ((SslStream) _innerStream).DataAvailable
               : ((NetworkStream) _innerStream).DataAvailable;
      }
    }

    public bool IsSecure {
      get {
        return _secure;
      }
    }

    #endregion

    #region Internal Methods

    internal static WebSocketStream CreateClientStream (
      Uri targetUri,
      Uri proxyUri,
      NetworkCredential proxyCredentials,
      bool secure,
      System.Net.Security.RemoteCertificateValidationCallback validationCallback,
      out TcpClient tcpClient)
    {
      var proxy = proxyUri != null;
      tcpClient = proxy
                  ? new TcpClient (proxyUri.DnsSafeHost, proxyUri.Port)
                  : new TcpClient (targetUri.DnsSafeHost, targetUri.Port);

      var netStream = tcpClient.GetStream ();
      if (proxy) {
        var req = HttpRequest.CreateConnectRequest (targetUri);
        var res = req.GetResponse (netStream, 90000);
        if (res.IsProxyAuthenticationRequired) {
          var authChal = res.ProxyAuthenticationChallenge;
          if (authChal != null && proxyCredentials != null) {
            if (res.Headers.Contains ("Connection", "close")) {
              netStream.Dispose ();
              tcpClient.Close ();

              tcpClient = new TcpClient (proxyUri.DnsSafeHost, proxyUri.Port);
              netStream = tcpClient.GetStream ();
            }

            var authRes = new AuthenticationResponse (authChal, proxyCredentials, 0);
            req.Headers["Proxy-Authorization"] = authRes.ToString ();
            res = req.GetResponse (netStream, 15000);
          }

          if (res.IsProxyAuthenticationRequired)
            throw new WebSocketException ("Proxy authentication is required.");
        }

        var code = res.StatusCode;
        if (code[0] != '2')
          throw new WebSocketException (
            String.Format (
              "The proxy has failed a connection to the requested host and port. ({0})", code));
      }

      if (secure) {
        var sslStream = new SslStream (
          netStream,
          false,
          validationCallback ?? ((sender, certificate, chain, sslPolicyErrors) => true));

        sslStream.AuthenticateAsClient (targetUri.DnsSafeHost);
        return new WebSocketStream (sslStream, secure);
      }

      return new WebSocketStream (netStream, secure);
    }

    internal static WebSocketStream CreateServerStream (
      TcpClient tcpClient, bool secure, X509Certificate certificate)
    {
      var netStream = tcpClient.GetStream ();
      if (secure) {
        var sslStream = new SslStream (netStream, false);
        sslStream.AuthenticateAsServer (certificate);

        return new WebSocketStream (sslStream, secure);
      }

      return new WebSocketStream (netStream, secure);
    }

    internal HttpRequest ReadHttpRequest (int millisecondsTimeout)
    {
      return HttpRequest.Read (_innerStream, millisecondsTimeout);
    }

    internal HttpResponse ReadHttpResponse (int millisecondsTimeout)
    {
      return HttpResponse.Read (_innerStream, millisecondsTimeout);
    }

    internal WebSocketFrame ReadWebSocketFrame ()
    {
      return WebSocketFrame.Parse (_innerStream, true);
    }

    internal void ReadWebSocketFrameAsync (
      Action<WebSocketFrame> completed, Action<Exception> error)
    {
      WebSocketFrame.ParseAsync (_innerStream, true, completed, error);
    }

    internal HttpResponse SendHttpRequest (HttpRequest request, int millisecondsTimeout)
    {
      return request.GetResponse (_innerStream, millisecondsTimeout);
    }

    internal bool WriteBytes (byte[] data)
    {
      try {
        _innerStream.Write (data, 0, data.Length);
        return true;
      }
      catch {
        return false;
      }
    }

    #endregion

    #region Public Methods

    public void Close ()
    {
      _innerStream.Close ();
    }

    public void Dispose ()
    {
      _innerStream.Dispose ();
    }

    #endregion
  }
}
