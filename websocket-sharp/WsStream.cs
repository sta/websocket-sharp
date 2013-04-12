#region License
/*
 * WsStream.cs
 *
 * The MIT License
 *
 * Copyright (c) 2010-2013 sta.blockhead
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
using System.Configuration;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using WebSocketSharp.Net.Security;

namespace WebSocketSharp {

  internal class WsStream : IDisposable
  {
    #region Private Fields

    private Stream _innerStream;
    private bool   _isSecure;
    private Object _forRead;
    private Object _forWrite;

    #endregion

    #region Private Constructors

    private WsStream()
    {
      _forRead  = new object();
      _forWrite = new object();
    }

    #endregion

    #region Public Constructors

    public WsStream(NetworkStream innerStream)
      : this()
    {
      if (innerStream.IsNull())
        throw new ArgumentNullException("innerStream");

      _innerStream = innerStream;
      _isSecure    = false;
    }

    public WsStream(SslStream innerStream)
      : this()
    {
      if (innerStream.IsNull())
        throw new ArgumentNullException("innerStream");

      _innerStream = innerStream;
      _isSecure    = true;
    }

    #endregion

    #region Public Properties

    public bool DataAvailable {
      get {
        return _isSecure
               ? ((SslStream)_innerStream).DataAvailable
               : ((NetworkStream)_innerStream).DataAvailable;
      }
    }

    public bool IsSecure {
      get {
        return _isSecure;
      }
    }

    #endregion

    #region Private Methods

    private int read(byte[] buffer, int offset, int size)
    {
      var readLen = _innerStream.Read(buffer, offset, size);
      if (readLen < size)
      {
        var msg = String.Format("Data can not be read from {0}.", _innerStream.GetType().Name);
        throw new IOException(msg);
      }

      return readLen;
    }

    private int readByte()
    {
      return _innerStream.ReadByte();
    }

    private string[] readHandshake()
    {
      var buffer = new List<byte>();
      while (true)
      {
        if (readByte().EqualsAndSaveTo('\r', buffer) &&
            readByte().EqualsAndSaveTo('\n', buffer) &&
            readByte().EqualsAndSaveTo('\r', buffer) &&
            readByte().EqualsAndSaveTo('\n', buffer))
          break;
      }

      return Encoding.UTF8.GetString(buffer.ToArray())
             .Replace("\r\n", "\n").Replace("\n\n", "\n").TrimEnd('\n')
             .Split('\n');
    }

    private bool write(byte[] data)
    {
      lock (_forWrite)
      {
        try {
          _innerStream.Write(data, 0, data.Length);
          return true;
        }
        catch {
          return false;
        }
      }
    }

    #endregion

    #region Internal Methods

    internal static WsStream CreateClientStream(TcpClient client, string host, bool secure)
    {
      var netStream = client.GetStream();
      if (secure)
      {
        System.Net.Security.RemoteCertificateValidationCallback validationCb = (sender, certificate, chain, sslPolicyErrors) =>
        {
          // FIXME: Always returns true
          return true;
        };

        var sslStream = new SslStream(netStream, false, validationCb);
        sslStream.AuthenticateAsClient(host);

        return new WsStream(sslStream);
      }

      return new WsStream(netStream);
    }

    internal static WsStream CreateServerStream(TcpClient client, bool secure)
    {
      var netStream = client.GetStream();
      if (secure)
      {
        var sslStream = new SslStream(netStream, false);
        var certPath  = ConfigurationManager.AppSettings["ServerCertPath"];
        sslStream.AuthenticateAsServer(new X509Certificate2(certPath));

        return new WsStream(sslStream);
      }

      return new WsStream(netStream);
    }

    internal static WsStream CreateServerStream(WebSocketSharp.Net.HttpListenerContext context)
    {
      var conn   = context.Connection;
      var stream = conn.Stream;

      return conn.IsSecure
             ? new WsStream((SslStream)stream)
             : new WsStream((NetworkStream)stream);
    }

    #endregion

    #region Public Methods

    public void Close()
    {
      _innerStream.Close();
    }

    public void Dispose()
    {
      _innerStream.Dispose();
    }

    public WsFrame ReadFrame()
    {
      lock (_forRead)
      {
        try
        {
          return WsFrame.Parse(_innerStream);
        }
        catch
        {
          return null;
        }
      }
    }

    public void ReadFrameAsync(Action<WsFrame> completed)
    {
      WsFrame.ParseAsync(_innerStream, completed);
    }

    public string[] ReadHandshake()
    {
      lock (_forRead)
      {
        try
        {
          return readHandshake();
        }
        catch
        {
          return null;
        }
      }
    }

    public bool Write(WsFrame frame)
    {
      return write(frame.ToByteArray());
    }

    public bool Write(Handshake handshake)
    {
      return write(handshake.ToBytes());
    }

    #endregion
  }
}
