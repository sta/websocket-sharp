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
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using WebSocketSharp.Net;
using WebSocketSharp.Net.Security;

namespace WebSocketSharp
{
  internal class WebSocketStream : IDisposable
  {
    #region Private Const Fields

    private const int _headersMaxLength = 8192;

    #endregion

    #region Private Fields

    private object _forWrite;
    private Stream _innerStream;
    private bool   _secure;

    #endregion

    #region Internal Constructors

    internal WebSocketStream (Stream innerStream, bool secure)
    {
      _innerStream = innerStream;
      _secure = secure;
      _forWrite = new object ();
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

    #region Private Methods

    private static byte[] readEntityBody (Stream stream, string length)
    {
      long len;
      if (!Int64.TryParse (length, out len))
        throw new ArgumentException ("Cannot be parsed.", "length");

      if (len < 0)
        throw new ArgumentOutOfRangeException ("length", "Less than zero.");

      return len > 1024
             ? stream.ReadBytes (len, 1024)
             : len > 0
               ? stream.ReadBytes ((int) len)
               : null;
    }

    private static string[] readHeaders (Stream stream, int maxLength)
    {
      var buff = new List<byte> ();
      var cnt = 0;
      Action<int> add = i => {
        buff.Add ((byte) i);
        cnt++;
      };

      var read = false;
      while (cnt < maxLength) {
        if (stream.ReadByte ().EqualsWith ('\r', add) &&
            stream.ReadByte ().EqualsWith ('\n', add) &&
            stream.ReadByte ().EqualsWith ('\r', add) &&
            stream.ReadByte ().EqualsWith ('\n', add)) {
          read = true;
          break;
        }
      }

      if (!read)
        throw new WebSocketException (
          "The header part of a HTTP data is greater than the max length.");

      var crlf = "\r\n";
      return Encoding.UTF8.GetString (buff.ToArray ())
             .Replace (crlf + " ", " ")
             .Replace (crlf + "\t", " ")
             .Split (new[] { crlf }, StringSplitOptions.RemoveEmptyEntries);
    }

    private static T readHttp<T> (Stream stream, Func<string[], T> parser, int millisecondsTimeout)
      where T : HttpBase
    {
      var timeout = false;
      var timer = new Timer (
        state => {
          timeout = true;
          stream.Close ();
        },
        null,
        millisecondsTimeout,
        -1);

      T http = null;
      Exception exception = null;
      try {
        http = parser (readHeaders (stream, _headersMaxLength));
        var contentLen = http.Headers["Content-Length"];
        if (contentLen != null && contentLen.Length > 0)
          http.EntityBodyData = readEntityBody (stream, contentLen);
      }
      catch (Exception ex) {
        exception = ex;
      }
      finally {
        timer.Change (-1, -1);
        timer.Dispose ();
      }

      var msg = timeout
                ? "A timeout has occurred while receiving a HTTP data."
                : exception != null
                  ? "An exception has occurred while receiving a HTTP data."
                  : null;

      if (msg != null)
        throw new WebSocketException (msg, exception);

      return http;
    }

    private static HttpResponse sendHttpRequest (
      Stream stream, HttpRequest request, int millisecondsTimeout)
    {
      var buff = request.ToByteArray ();
      stream.Write (buff, 0, buff.Length);

      return readHttp<HttpResponse> (stream, HttpResponse.Parse, millisecondsTimeout);
    }

    private static bool writeBytes (Stream stream, byte[] data)
    {
      try {
        stream.Write (data, 0, data.Length);
        return true;
      }
      catch {
        return false;
      }
    }

    #endregion

    #region Internal Methods

    internal static WebSocketStream CreateClientStream (
      TcpClient tcpClient,
      bool proxy,
      Uri targetUri,
      NetworkCredential proxyCredentials,
      bool secure,
      System.Net.Security.RemoteCertificateValidationCallback validationCallback)
    {
      var netStream = tcpClient.GetStream ();
      if (proxy) {
        var req = HttpRequest.CreateConnectRequest (targetUri);
        var res = sendHttpRequest (netStream, req, 90000);
        if (res.IsProxyAuthenticationRequired) {
          var authChal = res.ProxyAuthenticationChallenge;
          if (authChal != null && proxyCredentials != null) {
            var authRes = new AuthenticationResponse (authChal, proxyCredentials, 0);
            req.Headers["Proxy-Authorization"] = authRes.ToString ();
            res = sendHttpRequest (netStream, req, 15000);
          }

          if (res.IsProxyAuthenticationRequired)
            throw new WebSocketException ("Proxy authentication is required.");
        }

        var code = res.StatusCode;
        if (code.Length != 3 || code[0] != '2')
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
      return readHttp<HttpRequest> (_innerStream, HttpRequest.Parse, millisecondsTimeout);
    }

    internal HttpResponse ReadHttpResponse (int millisecondsTimeout)
    {
      return readHttp<HttpResponse> (_innerStream, HttpResponse.Parse, millisecondsTimeout);
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
      return sendHttpRequest (_innerStream, request, millisecondsTimeout);
    }

    internal bool WriteBytes (byte[] data)
    {
      lock (_forWrite)
        return writeBytes (_innerStream, data);
    }

    internal bool WriteHttp (HttpBase http)
    {
      return writeBytes (_innerStream, http.ToByteArray ());
    }

    internal bool WriteWebSocketFrame (WebSocketFrame frame)
    {
      lock (_forWrite)
        return writeBytes (_innerStream, frame.ToByteArray ());
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
