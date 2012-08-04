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
 * Copyright (c) 2010-2012 sta.blockhead
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using WebSocketSharp.Frame;
using WebSocketSharp.Stream;

namespace WebSocketSharp
{
  public class WebSocket : IDisposable
  {
    #region Private Const Fields

    private const string _guid    = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    private const string _version = "13";

    #endregion

    #region Private Fields

    private AutoResetEvent                  _autoEvent;
    private string                          _base64key;
    private string                          _binaryType;
    private string                          _extensions;
    private Object                          _forClose;
    private Object                          _forSend;
    private int                             _fragmentLen;
    private bool                            _isClient;
    private Thread                          _msgThread;
    private NetworkStream                   _netStream;
    private string                          _protocol;
    private string                          _protocols;
    private volatile WsState                _readyState;
    private SslStream                       _sslStream;
    private TcpClient                       _tcpClient;
    private Uri                             _uri;
    private SynchronizedCollection<WsFrame> _unTransmittedBuffer;
    private IWsStream                       _wsStream;

    #endregion

    #region Properties

    public string BinaryType
    {
      get { return _binaryType; }
    }

    public ulong BufferedAmount
    {
      get
      {
        ulong bufferedAmount = 0;

        lock (_unTransmittedBuffer.SyncRoot)
        {
          foreach (WsFrame frame in _unTransmittedBuffer)
          {
            bufferedAmount += frame.PayloadLength;
          }
        }

        return bufferedAmount;
      }
    }

    public string Extensions
    {
      get { return _extensions; }
    }

    public string Protocol
    {
      get { return _protocol; }
    }

    public WsState ReadyState
    {
      get { return _readyState; }

      private set
      {
        _readyState = value;

        switch (value)
        {
          case WsState.OPEN:
            startMessageThread();
            OnOpen.Emit(this, EventArgs.Empty);
            break;
          case WsState.CLOSING:
            break;
          case WsState.CLOSED:
            closeConnection();
            break;
        }
      }
    }

    public SynchronizedCollection<WsFrame> UnTransmittedBuffer
    {
      get { return _unTransmittedBuffer; }
    }

    public string Url
    {
      get { return _uri.ToString(); }
    }

    #endregion

    #region Events

    public event EventHandler                   OnOpen;
    public event EventHandler<MessageEventArgs> OnMessage;
    public event EventHandler<ErrorEventArgs>   OnError;
    public event EventHandler<CloseEventArgs>   OnClose;

    #endregion

    #region Private Constructors

    private WebSocket()
    {
      _binaryType          = String.Empty;
      _extensions          = String.Empty;
      _forClose            = new Object();
      _forSend             = new Object();
      _fragmentLen         = 1024; // Max value is int.MaxValue - 14.
      _protocol            = String.Empty;
      _readyState          = WsState.CONNECTING;
      _unTransmittedBuffer = new SynchronizedCollection<WsFrame>();
    }

    #endregion

    #region Internal Constructors

    internal WebSocket(string url, TcpClient tcpClient)
      : this()
    {
      _uri = new Uri(url);
      if (!isValidScheme(_uri))
      {
        throw new ArgumentException("Unsupported WebSocket URI scheme: " + _uri.Scheme);
      }

      _tcpClient = tcpClient;
      _isClient  = false;
    }

    #endregion

    #region Public Constructors

    public WebSocket(string url, params string[] protocols)
      : this()
    {
      _uri = new Uri(url);
      if (!isValidScheme(_uri))
      {
        throw new ArgumentException("Unsupported WebSocket URI scheme: " + _uri.Scheme);
      }

      _protocols = protocols.ToString(", ");
      _isClient  = true;
    }

    public WebSocket(
      string                         url,
      EventHandler                   onOpen,
      EventHandler<MessageEventArgs> onMessage,
      EventHandler<ErrorEventArgs>   onError,
      EventHandler<CloseEventArgs>   onClose,
      params string[]                protocols)
      : this(url, protocols)
    {
      OnOpen    = onOpen;
      OnMessage = onMessage;
      OnError   = onError;
      OnClose   = onClose;

      Connect();
    }

    #endregion

    #region Private Methods

    private void acceptHandshake()
    {
      string   msg, response;
      string[] request;

      request = receiveOpeningHandshake();
      #if DEBUG
      Console.WriteLine("\nWS: Info@acceptHandshake: Opening handshake from client:\n");
      foreach (string s in request)
      {
        Console.WriteLine("{0}", s);
      }
      #endif
      if (!isValidRequest(request, out msg))
      {
        throw new InvalidOperationException(msg);
      }

      response = createResponseHandshake();
      #if DEBUG
      Console.WriteLine("\nWS: Info@acceptHandshake: Opening handshake from server:\n{0}", response);
      #endif
      sendResponseHandshake(response);

      ReadyState = WsState.OPEN;
    }

    private void close(PayloadData data)
    {
      #if DEBUG
      Console.WriteLine("\nWS: Info@close: Current thread IsBackground?: {0}", Thread.CurrentThread.IsBackground);
      #endif
      lock(_forClose)
      {
        if (_readyState == WsState.CLOSING ||
            _readyState == WsState.CLOSED)
        {
          return;
        }
        else if (_readyState == WsState.CONNECTING)
        {
          ReadyState = WsState.CLOSED;
          OnClose.Emit(this, new CloseEventArgs(data));
          return;
        }

        ReadyState = WsState.CLOSING;
      }
      
      OnClose.Emit(this, new CloseEventArgs(data));
      var frame = new WsFrame(Opcode.CLOSE, data);
      closeHandshake(frame);
      #if DEBUG
      Console.WriteLine("WS: Info@close: Exit close method.");
      #endif
    }

    private void close(CloseStatusCode code, string reason)
    {
      byte[]     buffer;
      List<byte> data;

      data = new List<byte>(((ushort)code).ToBytes(ByteOrder.BIG));

      if (reason != String.Empty)
      {
        buffer = Encoding.UTF8.GetBytes(reason);
        data.AddRange(buffer);
      }

      close(new PayloadData(data.ToArray()));
    }

    private void closeConnection()
    {
      if (_wsStream != null)
      {
        _wsStream.Dispose();
        _wsStream = null;
      }
      else if (_netStream != null)
      {
        _netStream.Dispose();
        _netStream = null;
      }

      if (_tcpClient != null)
      {
        _tcpClient.Close();
        _tcpClient = null;
      }
    }

    private void closeHandshake(WsFrame frame)
    {
      try
      {
        _wsStream.WriteFrame(frame);
      }
      catch (Exception ex)
      {
        _unTransmittedBuffer.Add(frame);
        error(ex.Message);
      }

      if (!Thread.CurrentThread.IsBackground)
      {
        if (_isClient)
        {
          _msgThread.Join(5000);
        }
        else
        {
          _autoEvent.WaitOne();
        }
      }

      ReadyState = WsState.CLOSED;
    }

    private void createClientStream()
    {
      string scheme = _uri.Scheme;
      string host   = _uri.DnsSafeHost;
      int    port   = _uri.Port;

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

      _tcpClient = new TcpClient(host, port);
      _netStream = _tcpClient.GetStream();

      if (scheme == "wss")
      {
        RemoteCertificateValidationCallback validation = (sender, certificate, chain, sslPolicyErrors) =>
        {
          // Temporary implementation
          return true;
        };

        _sslStream = new SslStream(_netStream, false, validation);
        _sslStream.AuthenticateAsClient(host);
        
        _wsStream  = new WsStream<SslStream>(_sslStream);
      }
      else
      {
        _wsStream = new WsStream<NetworkStream>(_netStream);
      }
    }

    private string createExpectedKey()
    {
      byte[]        keySrc;
      SHA1          sha1 = new SHA1CryptoServiceProvider();
      StringBuilder sb   = new StringBuilder(_base64key);

      sb.Append(_guid);
      keySrc = sha1.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));

      return Convert.ToBase64String(keySrc);
    }

    private string createOpeningHandshake()
    {
      byte[] keySrc;
      int    port;
      string crlf, host, origin, path;
      string reqConnection, reqHost, reqMethod, reqOrigin, reqUpgrade;
      string secWsKey, secWsProtocol, secWsVersion;
      Random rand;

      path = _uri.PathAndQuery;

      host = _uri.DnsSafeHost;
      port = ((IPEndPoint)_tcpClient.Client.RemoteEndPoint).Port;
      if (port != 80)
      {
        host += ":" + port;
      }

      origin = "http://" + host;

      keySrc     = new byte[16];
      rand       = new Random();
      rand.NextBytes(keySrc);
      _base64key = Convert.ToBase64String(keySrc);

      crlf = "\r\n";

      reqMethod     = String.Format("GET {0} HTTP/1.1{1}", path, crlf);
      reqHost       = String.Format("Host: {0}{1}", host, crlf);
      reqUpgrade    = String.Format("Upgrade: websocket{0}", crlf);
      reqConnection = String.Format("Connection: Upgrade{0}", crlf);
      reqOrigin     = String.Format("Origin: {0}{1}", origin, crlf);
      secWsKey      = String.Format("Sec-WebSocket-Key: {0}{1}", _base64key, crlf);
      
      secWsProtocol = _protocols != String.Empty
                    ? String.Format("Sec-WebSocket-Protocol: {0}{1}", _protocols, crlf)
                    : _protocols;

      secWsVersion  = String.Format("Sec-WebSocket-Version: {0}{1}", _version, crlf);

      return reqMethod +
             reqHost +
             reqUpgrade +
             reqConnection +
             secWsKey +
             reqOrigin +
             secWsProtocol +
             secWsVersion +
             crlf;
    }

    private string createResponseHandshake()
    {
      string crlf          = "\r\n";

      string resStatus     = "HTTP/1.1 101 Switching Protocols" + crlf;
      string resUpgrade    = "Upgrade: websocket" + crlf;
      string resConnection = "Connection: Upgrade" + crlf;
      string secWsAccept   = String.Format("Sec-WebSocket-Accept: {0}{1}", createExpectedKey(), crlf);
      //string secWsProtocol = "Sec-WebSocket-Protocol: chat" + crlf;
      string secWsVersion  = String.Format("Sec-WebSocket-Version: {0}{1}", _version, crlf);

      return resStatus +
             resUpgrade +
             resConnection +
             secWsAccept +
             //secWsProtocol +
             secWsVersion +
             crlf;
    }

    private void createServerStream()
    {
      _netStream = _tcpClient.GetStream();

      if (_uri.Scheme == "wss")
      {
        _sslStream = new SslStream(_netStream);

        string certPath = ConfigurationManager.AppSettings["ServerCertPath"];
        _sslStream.AuthenticateAsServer(new X509Certificate(certPath));

        _wsStream = new WsStream<SslStream>(_sslStream);
      }
      else
      {
        _wsStream = new WsStream<NetworkStream>(_netStream);
      }
    }

    private void doHandshake()
    {
      string   msg, request;
      string[] response;

      request  = createOpeningHandshake();
      #if DEBUG
      Console.WriteLine("\nWS: Info@doHandshake: Opening handshake from client:\n{0}", request);
      #endif
      response = sendOpeningHandshake(request);
      #if DEBUG
      Console.WriteLine("\nWS: Info@doHandshake: Opening handshake from server:\n");
      foreach (string s in response)
      {
        Console.WriteLine("{0}", s);
      }
      #endif
      if (!isValidResponse(response, out msg))
      {
        throw new InvalidOperationException(msg);
      }

      ReadyState = WsState.OPEN;
    }

    private void error(string message)
    {
      #if DEBUG
      var callerFrame = new StackFrame(1);
      var caller      = callerFrame.GetMethod();
      Console.WriteLine("WS: Error@{0}: {1}", caller.Name, message);
      #endif
      OnError.Emit(this, new ErrorEventArgs(message));
    }

    private bool isValidRequest(string[] request, out string message)
    {
      string   reqConnection, reqHost, reqUpgrade, secWsVersion;
      string[] reqRequest;

      List<string> extensionList = new List<string>();

      Func<string, Func<string, string, string>> func = s =>
      {
        return (e, a) =>
        {
          return String.Format("Invalid request {0} value: {1}(expected: {2})", s, a, e);
        };
      };

      string expectedHost = _uri.DnsSafeHost;
      int port = ((IPEndPoint)_tcpClient.Client.LocalEndPoint).Port;
      if (port != 80)
      {
        expectedHost += ":" + port;
      }

      reqRequest = request[0].Split(' ');
      if ("GET".NotEqualsDo(reqRequest[0], func("HTTP Method"), out message, false))
      {
        return false;
      }
      if ("HTTP/1.1".NotEqualsDo(reqRequest[2], func("HTTP Version"), out message, false))
      {
        return false;
      }

      for (int i = 1; i < request.Length; i++)
      {
        if (request[i].Contains("Connection:"))
        {
          reqConnection = request[i].GetHeaderValue(":");
          if ("Upgrade".NotEqualsDo(reqConnection, func("Connection"), out message, true))
          {
            return false;
          }
        }
        else if (request[i].Contains("Host:"))
        {
          reqHost = request[i].GetHeaderValue(":");
          if (expectedHost.NotEqualsDo(reqHost, func("Host"), out message, true))
          {
            return false;
          }
        }
        else if (request[i].Contains("Origin:"))
        {
          continue;
        }
        else if (request[i].Contains("Upgrade:"))
        {
          reqUpgrade = request[i].GetHeaderValue(":");
          if ("websocket".NotEqualsDo(reqUpgrade, func("Upgrade"), out message, true))
          {
            return false;
          }
        }
        else if (request[i].Contains("Sec-WebSocket-Extensions:"))
        {
          extensionList.Add(request[i].GetHeaderValue(":"));
        }
        else if (request[i].Contains("Sec-WebSocket-Key:"))
        {
          _base64key = request[i].GetHeaderValue(":");
        }
        else if (request[i].Contains("Sec-WebSocket-Protocol:"))
        {
          _protocols = request[i].GetHeaderValue(":");
          #if DEBUG
          Console.WriteLine("WS: Info@isValidRequest: Sub protocol: {0}", _protocols);
          #endif
        }
        else if (request[i].Contains("Sec-WebSocket-Version:"))
        {
          secWsVersion = request[i].GetHeaderValue(":");
          if (_version.NotEqualsDo(secWsVersion, func("Sec-WebSocket-Version"), out message, true))
          {
            return false;
          }
        }
        else
        {
          Console.WriteLine("WS: Info@isValidRequest: Unsupported request header line: {0}", request[i]);
        }
      }

      if (String.IsNullOrEmpty(_base64key))
      {
        message = "Sec-WebSocket-Key header field does not exist or the value isn't set.";
        return false;
      }
      #if DEBUG
      foreach (string s in extensionList)
      {
        Console.WriteLine("WS: Info@isValidRequest: Extensions: {0}", s);
      }
      #endif
      message = String.Empty;
      return true;
    }

    private bool isValidResponse(string[] response, out string message)
    {
      string       resUpgrade, resConnection, secWsAccept, secWsVersion;
      string[]     resStatus;

      List<string> extensionList = new List<string>();

      Func<string, Func<string, string, string>> func = s =>
      {
        return (e, a) =>
        {
          return String.Format("Invalid response {0} value: {1}(expected: {2})", s, a, e);
        };
      };

      resStatus = response[0].Split(' ');
      if ("HTTP/1.1".NotEqualsDo(resStatus[0], func("HTTP Version"), out message, false))
      {
        return false;
      }
      if ("101".NotEqualsDo(resStatus[1], func("Status Code"), out message, false))
      {
        return false;
      }

      for (int i = 1; i < response.Length; i++)
      {
        if (response[i].Contains("Upgrade:"))
        {
          resUpgrade = response[i].GetHeaderValue(":");
          if ("websocket".NotEqualsDo(resUpgrade, func("Upgrade"), out message, true))
          {
            return false;
          }
        }
        else if (response[i].Contains("Connection:"))
        {
          resConnection = response[i].GetHeaderValue(":");
          if ("Upgrade".NotEqualsDo(resConnection, func("Connection"), out message, true))
          {
            return false;
          }
        }
        else if (response[i].Contains("Sec-WebSocket-Accept:"))
        {
          secWsAccept = response[i].GetHeaderValue(":");
          if (createExpectedKey().NotEqualsDo(secWsAccept, func("Sec-WebSocket-Accept"), out message, false))
          {
            return false;
          }
        }
        else if (response[i].Contains("Sec-WebSocket-Extensions:"))
        {
          extensionList.Add(response[i].GetHeaderValue(":"));
        }
        else if (response[i].Contains("Sec-WebSocket-Protocol:"))
        {
          _protocol = response[i].GetHeaderValue(":");
          #if DEBUG
          Console.WriteLine("WS: Info@isValidResponse: Sub protocol: {0}", _protocol);
          #endif
        }
        else if (response[i].Contains("Sec-WebSocket-Version:"))
        {
          secWsVersion = response[i].GetHeaderValue(":");
          if (_version.NotEqualsDo(secWsVersion, func("Sec-WebSocket-Version"), out message, true))
          {
            return false;
          }
        }
        else
        {
          Console.WriteLine("WS: Info@isValidResponse: Unsupported response header line: {0}", response[i]);
        }
      }
      #if DEBUG
      foreach (string s in extensionList)
      {
        Console.WriteLine("WS: Info@isValidResponse: Extensions: {0}", s);
      }
      #endif
      message = String.Empty;
      return true;
    }

    private bool isValidScheme(Uri uri)
    {
      string scheme = uri.Scheme;
      if (scheme == "ws" || scheme == "wss")
      {
        return true;
      }

      return false;
    }

    private void message()
    {
      try
      {
        MessageEventArgs eventArgs = receive();

        if (eventArgs != null)
        {
          OnMessage.Emit(this, eventArgs);
        }
      }
      catch (WsReceivedTooBigMessageException ex)
      {
        close(CloseStatusCode.TOO_BIG, ex.Message);
      }
    }

    private void messageLoop()
    {
      #if DEBUG
      Console.WriteLine("\nWS: Info@messageLoop: Current thread IsBackground?: {0}", Thread.CurrentThread.IsBackground);
      #endif
      while (_readyState == WsState.OPEN)
      {
        message();
      }
      #if DEBUG
      Console.WriteLine("WS: Info@messageLoop: Exit messageLoop method.");
      #endif
    }

    private void startMessageThread()
    {
      if (_isClient)
      {
        _msgThread = new Thread(new ThreadStart(messageLoop)); 
        _msgThread.IsBackground = true;
        _msgThread.Start();
      }
      else
      {
        _autoEvent = new AutoResetEvent(false);
        Action act = () =>
        {
          if (_readyState == WsState.OPEN)
          {
            message();
          }
        };
        AsyncCallback callback = (ar) =>
        {
          act.EndInvoke(ar);

          if (_readyState == WsState.OPEN)
          {
            act.BeginInvoke(callback, null);
          }
          else
          {
            _autoEvent.Set();
          }
        };
        act.BeginInvoke(callback, null);
      }
    }

    private MessageEventArgs receive()
    {
      List<byte>       dataBuffer;
      MessageEventArgs eventArgs;
      Fin              fin;
      WsFrame          frame;
      Opcode           opcode;
      PayloadData      payloadData;

      frame = _wsStream.ReadFrame();
      if ((frame.Fin == Fin.FINAL && frame.Opcode == Opcode.CONT) ||
          (frame.Fin == Fin.MORE  && frame.Opcode == Opcode.CONT))
      {
        return null;
      }

      fin         = frame.Fin;
      opcode      = frame.Opcode;
      payloadData = frame.PayloadData;
      eventArgs   = null;

      switch (fin)
      {
        case Fin.MORE:
          dataBuffer = new List<byte>(payloadData.ToBytes());
          while (true)
          {
            frame = _wsStream.ReadFrame();

            if (frame.Fin == Fin.MORE)
            {
              if (frame.Opcode == Opcode.CONT)
              {// MORE & CONT
                dataBuffer.AddRange(frame.PayloadData.ToBytes());
              }
              else
              {
                #if DEBUG
                Console.WriteLine("WS: Info@receive: Client starts closing handshake.");
                #endif
                close(CloseStatusCode.INCORRECT_DATA, String.Empty);
                return null;
              }
            }
            else if (frame.Opcode == Opcode.CONT)
            {// FINAL & CONT
              dataBuffer.AddRange(frame.PayloadData.ToBytes());
              break;
            }
            else if (frame.Opcode == Opcode.CLOSE)
            {// FINAL & CLOSE
              #if DEBUG
              Console.WriteLine("WS: Info@receive: Server starts closing handshake.");
              #endif
              close(frame.PayloadData);
              return null;
            }
            else if (frame.Opcode == Opcode.PING)
            {// FINAL & PING
              pong(frame.PayloadData);
              OnMessage.Emit(this, new MessageEventArgs(frame.Opcode, frame.PayloadData));
            }
            else if (frame.Opcode == Opcode.PONG)
            {// FINAL & PONG
              OnMessage.Emit(this, new MessageEventArgs(frame.Opcode, frame.PayloadData));
            }
            else
            {// FINAL & (TEXT | BINARY)
              #if DEBUG
              Console.WriteLine("WS: Info@receive: Client starts closing handshake.");
              #endif
              close(CloseStatusCode.INCORRECT_DATA, String.Empty);
              return null;
            }
          }

          eventArgs = new MessageEventArgs(opcode, new PayloadData(dataBuffer.ToArray()));
          break;
        case Fin.FINAL:
          switch (opcode)
          {
            case Opcode.TEXT:
            case Opcode.BINARY:
            case Opcode.PONG:
              goto default;
            case Opcode.CLOSE:
              #if DEBUG
              Console.WriteLine("WS: Info@receive: Server starts closing handshake.");
              #endif
              close(payloadData);
              break;
            case Opcode.PING:
              pong(payloadData);
              goto default;
            default:
              eventArgs = new MessageEventArgs(opcode, payloadData);
              break;
          }
        
          break;
      }

      return eventArgs;
    }

    private void pong(PayloadData data)
    {
      var frame = new WsFrame(Opcode.PONG, data);
      send(frame);
    }

    private void pong(string data)
    {
      var payloadData = new PayloadData(data);
      pong(payloadData);
    }

    private string[] receiveOpeningHandshake()
    {
      var readData = new List<byte>();
    
      while (true)
      {
        if (_wsStream.ReadByte().EqualsAndSaveTo('\r', readData) &&
            _wsStream.ReadByte().EqualsAndSaveTo('\n', readData) &&
            _wsStream.ReadByte().EqualsAndSaveTo('\r', readData) &&
            _wsStream.ReadByte().EqualsAndSaveTo('\n', readData))
        {
          break;
        }
      }

      return Encoding.UTF8.GetString(readData.ToArray())
             .Replace("\r\n", "\n").Replace("\n\n", "\n").TrimEnd('\n')
             .Split('\n');
    }

    private bool send(WsFrame frame)
    {
      if (_readyState != WsState.OPEN)
      {
        var msg = "Connection isn't established or connection was closed.";
        error(msg);

        return false;
      }

      try
      {
        if (_unTransmittedBuffer.Count == 0)
        {
          _wsStream.WriteFrame(frame);
        }
        else
        {
          _unTransmittedBuffer.Add(frame);
        }
      }
      catch (Exception ex)
      {
        _unTransmittedBuffer.Add(frame);
        error(ex.Message);

        return false;
      }

      return true;
    }

    private void send(Opcode opcode, PayloadData data)
    {
      using (MemoryStream ms = new MemoryStream(data.ToBytes()))
      {
        send(opcode, ms);
      }
    }

    private void send<TStream>(Opcode opcode, TStream stream)
      where TStream : System.IO.Stream
    {
      if (_unTransmittedBuffer.Count > 0)
      {
        var msg = "Current data can not be sent because there is untransmitted data.";
        error(msg);
      }

      lock(_forSend)
      {
        var length = stream.Length;
        if (length <= _fragmentLen)
        {
          var buffer = new byte[length];
          stream.Read(buffer, 0, (int)length);
          var frame  = new WsFrame(opcode, new PayloadData(buffer));
          send(frame);
        }
        else
        {
          sendFragmented(opcode, stream);
        }
      }
    }

    private ulong sendFragmented<TStream>(Opcode opcode, TStream stream)
      where TStream : System.IO.Stream
    {
      if (_readyState != WsState.OPEN)
      {
        var msg = "Connection isn't established or connection was closed.";
        error(msg);

        return 0;
      }

      WsFrame     frame;
      PayloadData payloadData;

      var   buffer1 = new byte[_fragmentLen];
      var   buffer2 = new byte[_fragmentLen];
      ulong readLen = 0;
      int   tmpLen1 = 0;
      int   tmpLen2 = 0;

      tmpLen1 = stream.Read(buffer1, 0, _fragmentLen);
      while (tmpLen1 > 0)
      {
        payloadData = new PayloadData(buffer1.SubArray(0, tmpLen1));

        tmpLen2 = stream.Read(buffer2, 0, _fragmentLen);
        if (tmpLen2 > 0)
        {
          if (readLen > 0)
          {
            frame = new WsFrame(Fin.MORE, Opcode.CONT, payloadData);
          }
          else
          {
            frame = new WsFrame(Fin.MORE, opcode, payloadData);
          }
        }
        else
        {
          if (readLen > 0)
          {
            frame = new WsFrame(Opcode.CONT, payloadData);
          }
          else
          {
            frame = new WsFrame(opcode, payloadData);
          }
        }

        readLen += (ulong)tmpLen1;
        send(frame);

        if (tmpLen2 == 0) break;
        payloadData = new PayloadData(buffer2.SubArray(0, tmpLen2));

        tmpLen1 = stream.Read(buffer1, 0, _fragmentLen);
        if (tmpLen1 > 0)
        {
          frame = new WsFrame(Fin.MORE, Opcode.CONT, payloadData);
        }
        else
        {
          frame = new WsFrame(Opcode.CONT, payloadData);
        }

        readLen += (ulong)tmpLen2;
        send(frame);
      }

      return readLen;
    }

    private string[] sendOpeningHandshake(string value)
    {
      var readData = new List<byte>();
      var buffer   = Encoding.UTF8.GetBytes(value);

      _wsStream.Write(buffer, 0, buffer.Length);

      while (true)
      {
        if (_wsStream.ReadByte().EqualsAndSaveTo('\r', readData) &&
            _wsStream.ReadByte().EqualsAndSaveTo('\n', readData) &&
            _wsStream.ReadByte().EqualsAndSaveTo('\r', readData) &&
            _wsStream.ReadByte().EqualsAndSaveTo('\n', readData))
        {
          break;
        }
      }

      return Encoding.UTF8.GetString(readData.ToArray())
             .Replace("\r\n", "\n").Replace("\n\n", "\n").TrimEnd('\n')
             .Split('\n');
    }

    private void sendResponseHandshake(string value)
    {
      var buffer = Encoding.UTF8.GetBytes(value);
      _wsStream.Write(buffer, 0, buffer.Length);
    }

    #endregion

    #region Public Methods

    public void Connect()
    {
      if (_readyState == WsState.OPEN)
      {
        Console.WriteLine("\nWS: Info@Connect: Connection is already established.");
        return;
      }

      try
      {
        if (_isClient)
        {
          createClientStream();
          doHandshake();
        }
        else
        {
          createServerStream();
          acceptHandshake();
        }
      }
      catch (Exception ex)
      {
        error(ex.Message);
        close(CloseStatusCode.HANDSHAKE_FAILURE, ex.Message);
      }
    }

    public void Close()
    {
      Close(CloseStatusCode.NORMAL);
    }

    public void Close(CloseStatusCode code)
    {
      Close(code, String.Empty);
    }

    public void Close(CloseStatusCode code, string reason)
    {
      close(code, reason);
    }

    public void Dispose()
    {
      Close(CloseStatusCode.AWAY);
    }

    public void Ping()
    {
      Ping(String.Empty);
    }

    public void Ping(string data)
    {
      var payloadData = new PayloadData(data);
      var frame       = new WsFrame(Opcode.PING, payloadData);
      send(frame);
    }

    public void Send(string data)
    {
      var payloadData = new PayloadData(data);
      send(Opcode.TEXT, payloadData);
    }

    public void Send(byte[] data)
    {
      var payloadData = new PayloadData(data);
      send(Opcode.BINARY, payloadData);
    }

    public void Send(FileInfo file)
    {
      using (FileStream fs = file.OpenRead())
      {
        send(Opcode.BINARY, fs);
      }
    }

    #endregion
  }
}
