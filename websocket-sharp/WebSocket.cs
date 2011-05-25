#region MIT License
/**
 * WebSocket.cs
 *
 * A C# implementation of a WebSocket protocol client.
 * This code derived from WebSocket.java (http://github.com/adamac/Java-WebSocket-client).
 *
 * The MIT License
 *
 * Copyright (c) 2009 Adam MacBeth
 * Copyright (c) 2010 sta.blockhead
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
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Security.Cryptography;

namespace WebSocketSharp
{
  public delegate void MessageEventHandler(object sender, string eventdata);

  public class WebSocket : IDisposable
  {
    private Uri uri;
    public string Url
    {
      get { return uri.ToString(); }
    }

    private volatile WsState readyState;
    public WsState ReadyState
    {
      get { return readyState; }

      private set
      {
          readyState = value;
          switch (value)
          {

              case WsState.OPEN:
                  if (OnOpen != null)
                  {
                      OnOpen(this, EventArgs.Empty);
                  }
                  break;
              case WsState.CLOSING:
              case WsState.CLOSED:
                  close(value);
                  break;
          }
      }
    }

    private StringBuilder unTransmittedBuffer;
    public String UnTransmittedBuffer
    {
      get { return unTransmittedBuffer.ToString(); }
    }

    private long bufferedAmount;
    public long BufferedAmount
    {
      get { return bufferedAmount; }
    }

    private string protocol;
    public string Protocol
    {
      get { return protocol; }
    }

    private TcpClient tcpClient;
    private NetworkStream netStream;
    private SslStream sslStream;
    private IWsStream wsStream;
    private Thread msgThread;

    public event EventHandler OnOpen;
    public event MessageEventHandler OnMessage;
    public event MessageEventHandler OnError;
    public event EventHandler OnClose;

    public WebSocket(string url)
      : this(url, String.Empty)
    {
    }

    public WebSocket(string url, string protocol)
    {
      this.uri = new Uri(url);
      string scheme = uri.Scheme;

      if (scheme != "ws" && scheme != "wss")
      {
        throw new ArgumentException("Unsupported scheme: " + scheme);
      }

      this.readyState = WsState.CONNECTING;
      this.unTransmittedBuffer = new StringBuilder();
      this.bufferedAmount = 0;
      this.protocol = protocol;
    }

    public WebSocket(
      string url,
      EventHandler onOpen,
      MessageEventHandler onMessage,
      MessageEventHandler onError,
      EventHandler onClose)
      : this(url, String.Empty, onOpen, onMessage, onError, onClose)
    {
    }

    public WebSocket(
      string url,
      string protocol,
      EventHandler onOpen,
      MessageEventHandler onMessage,
      MessageEventHandler onError,
      EventHandler onClose)
      : this(url, protocol)
    {
      this.OnOpen += onOpen;
      this.OnMessage += onMessage;
      this.OnError += onError;
      this.OnClose += onClose;
      
      Connect();
    }

    public void Connect()
    {
      if (readyState == WsState.OPEN)
      {
        Console.WriteLine("WS: Info @Connect: Connection is already established.");
        return;
      }

      createConnection();
      doHandshake();

      this.msgThread = new Thread(new ThreadStart(message)); 
      msgThread.IsBackground = true;
      msgThread.Start();
    }

    public void Send(string data)
    {
      if (readyState == WsState.CONNECTING)
      {
        throw new InvalidOperationException("Handshake not complete.");
      }

      byte[] dataBuffer = Encoding.UTF8.GetBytes(data);

      try
      {
        wsStream.WriteByte(0x00);
        wsStream.Write(dataBuffer, 0, dataBuffer.Length);
        wsStream.WriteByte(0xff);
      }
      catch (Exception e)
      {
        unTransmittedBuffer.Append(data);
        bufferedAmount += dataBuffer.Length;

        if (OnError != null)
        {
          OnError(this, e.Message);
        }
#if DEBUG
        Console.WriteLine("WS: Error @Send: {0}", e.Message);
#endif
      }
    }

    public void Close()
    {
      ReadyState = WsState.CLOSING;
    }

    public void Dispose()
    {
      Close();
    }

    private void close(WsState state)
    {
#if DEBUG
      Console.WriteLine("WS: Info @close: Current thread IsBackground: {0}", Thread.CurrentThread.IsBackground);
#endif
      if (readyState == WsState.CLOSING ||
          readyState == WsState.CLOSED)
      {
        return;
      }

      readyState = state;

      if (wsStream != null)
      {
        wsStream.Close();
        wsStream = null;
      }

      if (tcpClient != null)
      {
        tcpClient.Close();
        tcpClient = null;
      }

      if (OnClose != null)
      {
        OnClose(this, EventArgs.Empty);
      }
#if DEBUG
      Console.WriteLine("WS: Info @close: Exit close method.");
#endif
    }

    private void createConnection()
    {
      string scheme = uri.Scheme;
      string host = uri.DnsSafeHost;
      int port = uri.Port;

      if (port <= 0)
      {
        if (scheme == "wss")
        {
          port = 443;
        }
        else
        {
          port = 80;
        }
      }

      this.tcpClient = new TcpClient(host, port);
      this.netStream = tcpClient.GetStream();

      if (scheme == "wss")
      {
        this.sslStream = new SslStream(netStream);
        sslStream.AuthenticateAsClient(host);
        this.wsStream = new WsStream<SslStream>(sslStream);
      }
      else
      {
        this.wsStream = new WsStream<NetworkStream>(netStream);
      }
    }

    private void doHandshake()
    {
      byte[] expectedCR, actualCR;
      string request = createOpeningHandshake(out expectedCR);
#if DEBUG
      Console.WriteLine("WS: Info @doHandshake: Handshake from client: \n{0}", request);
#endif
      string[] response = sendOpeningHandshake(request, out actualCR);
#if DEBUG
      Console.WriteLine("WS: Info @doHandshake: Handshake from server:");
      foreach (string s in response)
      {
        Console.WriteLine("{0}", s);
      }
#endif
      string msg;
      if (!(response.IsValid(expectedCR, actualCR, out msg)))
      {
        throw new IOException(msg);
      }

      for (int i = 3; i < response.Length; i++)
      {
        if (response[i].Contains("Sec-WebSocket-Protocol:"))
        {
          int j = response[i].IndexOf(":");
          protocol = response[i].Substring(j + 1).Trim();
          break;
        }
      }
#if DEBUG
      Console.WriteLine("WS: Info @doHandshake: Sub protocol: {0}", protocol);
#endif
      ReadyState = WsState.OPEN;
    }

    private string createOpeningHandshake(out byte[] expectedCR)
    {
      string path = uri.PathAndQuery;
      string host = uri.DnsSafeHost;
      string origin = "http://" + host;

      int port = ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Port;
      if (port != 80)
      {
        host += ":" + port;
      }

      string subProtocol = protocol != String.Empty
                           ? String.Format("Sec-WebSocket-Protocol: {0}\r\n", protocol)
                           : protocol;

      Random rand = new Random();

      uint key1, key2;
      string secKey1 = rand.GenerateSecKey(out key1);
      string secKey2 = rand.GenerateSecKey(out key2);

      byte[] key3 = new byte[8].InitializeWithPrintableASCII(rand);

      string secKeys = "Sec-WebSocket-Key1: {0}\r\n" +
                       "Sec-WebSocket-Key2: {1}\r\n";
      secKeys = String.Format(secKeys, secKey1, secKey2);

      string key3ToAscii = Encoding.ASCII.GetString(key3);

      expectedCR = createExpectedCR(key1, key2, key3);

      return "GET " + path + " HTTP/1.1\r\n" +
             "Upgrade: WebSocket\r\n" +
             "Connection: Upgrade\r\n" +
             subProtocol +
             "Host: " + host + "\r\n" +
             "Origin: " + origin + "\r\n" +
             secKeys +
             "\r\n" +
             key3ToAscii;
    }

    private byte[] createExpectedCR(uint key1, uint key2, byte[] key3)
    {
      byte[] key1Bytes = BitConverter.GetBytes(key1);
      byte[] key2Bytes = BitConverter.GetBytes(key2);

      Array.Reverse(key1Bytes);
      Array.Reverse(key2Bytes);

      byte[] concatKeys = key1Bytes.Concat(key2Bytes).Concat(key3).ToArray();

      MD5 md5 = MD5.Create();
      return md5.ComputeHash(concatKeys);
    }

    private string[] sendOpeningHandshake(string openingHandshake, out byte[] challengeResponse)
    {
      challengeResponse = new byte[16];
      List<byte> rawdata = new List<byte>();

      byte[] sendBuffer = Encoding.UTF8.GetBytes(openingHandshake);
      wsStream.Write(sendBuffer, 0, sendBuffer.Length);

      while (true)
      {
        if (wsStream.ReadByte().EqualsWithSaveTo('\r', rawdata) &&
            wsStream.ReadByte().EqualsWithSaveTo('\n', rawdata) &&
            wsStream.ReadByte().EqualsWithSaveTo('\r', rawdata) &&
            wsStream.ReadByte().EqualsWithSaveTo('\n', rawdata))
        {
          wsStream.Read(challengeResponse, 0, challengeResponse.Length);
          break;
        }
      }

      return Encoding.UTF8.GetString(rawdata.ToArray())
             .Replace("\r\n", "\n").Replace("\n\n", "\n")
             .Split('\n');
    }

    private void message()
    {
#if DEBUG
      Console.WriteLine("WS: Info @message: Current thread IsBackground: {0}", Thread.CurrentThread.IsBackground);
#endif
      string data;
      while (readyState == WsState.OPEN)
      {
        data = receive();

        if (OnMessage != null && data != null)
        {
          OnMessage(this, data);
        }
      }
#if DEBUG
      Console.WriteLine("WS: Info @message: Exit message method.");
#endif
    }

    private string receive()
    {
      try
      {
        byte frame_type = (byte)wsStream.ReadByte();
        byte b;

        if ((frame_type & 0x80) == 0x80)
        {
          // Skip data frame
          int len = 0;
          int b_v;

          do
          {
            b = (byte)wsStream.ReadByte();
            b_v = b & 0x7f;
            len = len * 128 + b_v;
          }
          while ((b & 0x80) == 0x80);

          for (int i = 0; i < len; i++)
          {
            wsStream.ReadByte();
          }

          if (frame_type == 0xff && len == 0)
          {
            ReadyState = WsState.CLOSED;
#if DEBUG
            Console.WriteLine("WS: Info @receive: Server start closing handshake.");
#endif
          }
        }
        else if (frame_type == 0x00)
        {
          List<byte> raw_data = new List<byte>();

          while (true)
          {
            b = (byte)wsStream.ReadByte();

            if (b == 0xff)
            {
              break;
            }

            raw_data.Add(b);
          }

          return Encoding.UTF8.GetString(raw_data.ToArray());
        }
      }
      catch (Exception e)
      {
        if (readyState == WsState.OPEN)
        {
          if (OnError != null)
          {
            OnError(this, e.Message);
          }

          ReadyState = WsState.CLOSED;
#if DEBUG
          Console.WriteLine("WS: Error @receive: {0}", e.Message);
#endif
        }
      }

      return null;
    }
  }
}
