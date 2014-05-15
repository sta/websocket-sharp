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

    private const int _handshakeHeadersLimitLen = 8192;

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

    #region Public Constructors

    public WebSocketStream (NetworkStream innerStream)
      : this (innerStream, false)
    {
    }

    public WebSocketStream (SslStream innerStream)
      : this (innerStream, true)
    {
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

    private static byte [] readHandshakeEntityBody (Stream stream, string length)
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

    private static string [] readHandshakeHeaders (Stream stream)
    {
      var buff = new List<byte> ();
      var count = 0;
      Action<int> add = i => {
        buff.Add ((byte) i);
        count++;
      };

      var read = false;
      while (count < _handshakeHeadersLimitLen) {
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
          "The header part of a handshake is greater than the limit length.");

      var crlf = "\r\n";
      return Encoding.UTF8.GetString (buff.ToArray ())
             .Replace (crlf + " ", " ")
             .Replace (crlf + "\t", " ")
             .Split (new [] { crlf }, StringSplitOptions.RemoveEmptyEntries);
    }

    #endregion

    #region Internal Methods

    internal T ReadHandshake<T> (Func<string [], T> parser, int millisecondsTimeout)
      where T : HandshakeBase
    {
      var timeout = false;
      var timer = new Timer (
        state => {
          timeout = true;
          _innerStream.Close ();
        },
        null,
        millisecondsTimeout,
        -1);

      T handshake = null;
      Exception exception = null;
      try {
        handshake = parser (readHandshakeHeaders (_innerStream));
        var contentLen = handshake.Headers ["Content-Length"];
        if (contentLen != null && contentLen.Length > 0)
          handshake.EntityBodyData = readHandshakeEntityBody (_innerStream, contentLen);
      }
      catch (Exception ex) {
        exception = ex;
      }
      finally {
        timer.Change (-1, -1);
        timer.Dispose ();
      }

      var msg = timeout
                ? "A timeout has occurred while receiving a handshake."
                : exception != null
                  ? "An exception has occurred while receiving a handshake."
                  : null;

      if (msg != null)
        throw new WebSocketException (msg, exception);

      return handshake;
    }

    internal bool Write (byte [] data)
    {
      lock (_forWrite) {
        try {
          _innerStream.Write (data, 0, data.Length);
          return true;
        }
        catch {
          return false;
        }
      }
    }

    #endregion

    #region Public Methods

    public void Close ()
    {
      _innerStream.Close ();
    }

    public static WebSocketStream CreateClientStream (
      TcpClient client,
      bool secure,
      string host,
      System.Net.Security.RemoteCertificateValidationCallback validationCallback)
    {
      var netStream = client.GetStream ();
      if (secure) {
        if (validationCallback == null)
          validationCallback = (sender, certificate, chain, sslPolicyErrors) => true;

        var sslStream = new SslStream (netStream, false, validationCallback);
        sslStream.AuthenticateAsClient (host);

        return new WebSocketStream (sslStream);
      }

      return new WebSocketStream (netStream);
    }

    public static WebSocketStream CreateServerStream (
      TcpClient client, bool secure, X509Certificate cert)
    {
      var netStream = client.GetStream ();
      if (secure) {
        var sslStream = new SslStream (netStream, false);
        sslStream.AuthenticateAsServer (cert);

        return new WebSocketStream (sslStream);
      }

      return new WebSocketStream (netStream);
    }

    public void Dispose ()
    {
      _innerStream.Dispose ();
    }

    public WsFrame ReadFrame ()
    {
      return WsFrame.Parse (_innerStream, true);
    }

    public void ReadFrameAsync (Action<WsFrame> completed, Action<Exception> error)
    {
      WsFrame.ParseAsync (_innerStream, true, completed, error);
    }

    public HandshakeRequest ReadHandshakeRequest ()
    {
      return ReadHandshake<HandshakeRequest> (HandshakeRequest.Parse, 90000);
    }

    public HandshakeResponse ReadHandshakeResponse ()
    {
      return ReadHandshake<HandshakeResponse> (HandshakeResponse.Parse, 90000);
    }

    public bool WriteFrame (WsFrame frame)
    {
      return Write (frame.ToByteArray ());
    }

    public bool WriteHandshake (HandshakeBase handshake)
    {
      return Write (handshake.ToByteArray ());
    }

    #endregion
  }
}
