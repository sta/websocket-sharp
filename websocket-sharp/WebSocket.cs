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
using System.Collections.Specialized;
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
using WebSocketSharp.Net;

namespace WebSocketSharp
{
  public class WebSocket : IDisposable
  {
    #region Private Const Fields

    private const string _guid    = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
    private const string _version = "13";

    #endregion

    #region Private Fields

    private string                          _base64key;
    private string                          _binaryType;
    private HttpListenerWebSocketContext    _context;
    private IPEndPoint                      _endPoint;
    private AutoResetEvent                  _exitedMessageLoop;
    private string                          _extensions;
    private Object                          _forClose;
    private Object                          _forSend;
    private int                             _fragmentLen;
    private bool                            _isClient;
    private bool                            _isSecure;
    private Thread                          _msgThread;
    private NetworkStream                   _netStream;
    private string                          _protocol;
    private string                          _protocols;
    private volatile WsState                _readyState;
    private AutoResetEvent                  _receivedPong;
    private SslStream                       _sslStream;
    private TcpClient                       _tcpClient;
    private Uri                             _uri;
    private SynchronizedCollection<WsFrame> _unTransmittedBuffer;
    private IWsStream                       _wsStream;

    #endregion

    #region Private Constructor

    private WebSocket()
    {
      _binaryType          = String.Empty;
      _extensions          = String.Empty;
      _forClose            = new Object();
      _forSend             = new Object();
      _fragmentLen         = 1024; // Max value is int.MaxValue - 14.
      _protocol            = String.Empty;
      _readyState          = WsState.CONNECTING;
      _receivedPong        = new AutoResetEvent(false);
      _unTransmittedBuffer = new SynchronizedCollection<WsFrame>();
    }

    #endregion

    #region Internal Constructor

    internal WebSocket(HttpListenerWebSocketContext context)
      : this()
    {
      _uri      = new Uri("/", UriKind.Relative);
      _context  = context;
      _isClient = false;
      _isSecure = _context.IsSecureConnection;
    }

    internal WebSocket(Uri uri, TcpClient tcpClient)
      : this()
    {
      _uri       = uri;
      _tcpClient = tcpClient;
      _endPoint  = (IPEndPoint)_tcpClient.Client.LocalEndPoint;
      _isClient  = false;
      _isSecure  = _endPoint.Port == 443 ? true : false;
    }

    #endregion

    #region Public Constructors

    public WebSocket(string url, params string[] protocols)
      : this()
    {
      var uri = new Uri(url);
      if (!isValidScheme(uri))
      {
        var msg = "Unsupported WebSocket URI scheme: " + uri.Scheme;
        throw new ArgumentException(msg, "url");
      }

      _uri       = uri;
      _protocols = protocols.ToString(", ");
      _isClient  = true;
      _isSecure  = _uri.Scheme == "wss" ? true : false;
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

    #region Properties

    public string BinaryType {
      get { return _binaryType; }
    }

    public ulong BufferedAmount {
      get {
        ulong bufferedAmount = 0;

        lock (_unTransmittedBuffer.SyncRoot)
        {
          foreach (WsFrame frame in _unTransmittedBuffer)
            bufferedAmount += frame.PayloadLength;
        }

        return bufferedAmount;
      }
    }

    public string Extensions {
      get { return _extensions; }
    }

    public bool IsAlive {
      get {
        if (_readyState != WsState.OPEN)
          return false;

        return Ping();
      }
    }

    public bool IsSecure {
      get {
        return _isSecure;
      }
    }

    public string Protocol {
      get { return _protocol; }
    }

    public WsState ReadyState {
      get { return _readyState; }

      private set {
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

    public SynchronizedCollection<WsFrame> UnTransmittedBuffer {
      get { return _unTransmittedBuffer; }
    }

    public Uri Url {
      get { return _uri; }
      set {
        if (_readyState != WsState.CONNECTING || _isClient)
          return;

        _uri = value;
      }
    }

    #endregion

    #region Events

    public event EventHandler                   OnOpen;
    public event EventHandler<MessageEventArgs> OnMessage;
    public event EventHandler<ErrorEventArgs>   OnError;
    public event EventHandler<CloseEventArgs>   OnClose;

    #endregion

    #region Private Methods

    private void acceptHandshake()
    {
      var req = receiveOpeningHandshake();

      string msg;
      if (!isValidRequest(req, out msg))
      {
        error(msg);
        close(CloseStatusCode.HANDSHAKE_FAILURE, msg);
        return;
      }

      sendResponseHandshake();

      ReadyState = WsState.OPEN;
    }

    private void close(PayloadData data)
    {
      #if DEBUG
      Console.WriteLine("WS: Info@close: Current thread IsBackground ?: {0}", Thread.CurrentThread.IsBackground);
      #endif
      lock(_forClose)
      {
        if (_readyState == WsState.CLOSING ||
            _readyState == WsState.CLOSED)
          return;

        if (_readyState == WsState.CONNECTING && !_isClient)
        {
          OnClose.Emit(this, new CloseEventArgs(data));
          sendResponseHandshakeForInvalid();
          ReadyState = WsState.CLOSED;
          return;
        }

        ReadyState = WsState.CLOSING;
      }

      OnClose.Emit(this, new CloseEventArgs(data));
      var frame = createFrame(Fin.FINAL, Opcode.CLOSE, data);
      closeHandshake(frame);
      #if DEBUG
      Console.WriteLine("WS: Info@close: Exit close method.");
      #endif
    }

    private void close(CloseStatusCode code, string reason)
    {
      var data = new List<byte>(((ushort)code).ToBytes(ByteOrder.BIG));

      if (reason != String.Empty)
      {
        var buffer = Encoding.UTF8.GetBytes(reason);
        data.AddRange(buffer);
      }

      var payloadData = new PayloadData(data.ToArray());

      if (payloadData.Length > 125)
      {
        var msg = "Close frame must have a payload length of 125 bytes or less.";
        error(msg);
        return;
      }

      close(payloadData);
    }

    private void closeConnection()
    {
      if (_context != null)
      {
        _context.BaseContext.Response.Close();
        _wsStream = null;
        _context  = null;
      }

      if (_wsStream != null)
      {
        _wsStream.Dispose();
        _wsStream = null;
      }

      if (_netStream != null)
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
      if (send(frame) && !Thread.CurrentThread.IsBackground)
      {
        if (_isClient)
        {
          _msgThread.Join(5 * 1000);
        }
        else
        {
          _exitedMessageLoop.WaitOne(5 * 1000);
        }
      }

      ReadyState = WsState.CLOSED;
    }

    private void createClientStream()
    {
      var host = _uri.DnsSafeHost;
      var port = _uri.Port;
      if (port <= 0)
      {
        port = 80;
        if (IsSecure)
          port = 443;
      }

      _tcpClient = new TcpClient(host, port);
      _netStream = _tcpClient.GetStream();

      if (IsSecure)
      {
        RemoteCertificateValidationCallback validation = (sender, certificate, chain, sslPolicyErrors) =>
        {
          // Temporary implementation
          return true;
        };

        _sslStream = new SslStream(_netStream, false, validation);
        _sslStream.AuthenticateAsClient(host);
        _wsStream  = new WsStream<SslStream>(_sslStream);

        return;
      }

      _wsStream = new WsStream<NetworkStream>(_netStream);
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

    private WsFrame createFrame(Fin fin, Opcode opcode, PayloadData payloadData)
    {
      if (_isClient)
      {
        return new WsFrame(fin, opcode, payloadData);
      }
      else
      {
        return new WsFrame(fin, opcode, Mask.UNMASK, payloadData);
      }      
    }

    private RequestHandshake createOpeningHandshake()
    {
      var path = _uri.PathAndQuery;
      var host = _uri.DnsSafeHost;
      var port = ((IPEndPoint)_tcpClient.Client.RemoteEndPoint).Port;
      if (port != 80)
        host += ":" + port;

      var keySrc = new byte[16];
      var rand   = new Random();
      rand.NextBytes(keySrc);
      _base64key = Convert.ToBase64String(keySrc);
      
      var req = new RequestHandshake(path);
      req.AddHeader("Host", host);
      req.AddHeader("Sec-WebSocket-Key", _base64key);
      if (!String.IsNullOrEmpty(_protocols))
        req.AddHeader("Sec-WebSocket-Protocol", _protocols);
      req.AddHeader("Sec-WebSocket-Version", _version);

      return req;
    }

    private ResponseHandshake createResponseHandshake()
    {
      var res = new ResponseHandshake();
      res.AddHeader("Sec-WebSocket-Accept", createExpectedKey());

      return res;
    }

    private void createServerStream()
    {
      if (_context != null)
      {
        _wsStream = createServerStreamFromContext();
        return;
      }

      if (_tcpClient != null)
      {
        _wsStream = createServerStreamFromTcpClient();
        return;
      }
    }

    private IWsStream createServerStreamFromContext()
    {
      var stream = _context.BaseContext.Connection.Stream;

      if (IsSecure)
        return new WsStream<SslStream>((SslStream)stream);

      return new WsStream<NetworkStream>((NetworkStream)stream);
    }

    private IWsStream createServerStreamFromTcpClient()
    {
      _netStream = _tcpClient.GetStream();

      if (IsSecure)
      {
        _sslStream = new SslStream(_netStream);

        var certPath = ConfigurationManager.AppSettings["ServerCertPath"];
        _sslStream.AuthenticateAsServer(new X509Certificate(certPath));

        return new WsStream<SslStream>(_sslStream);
      }

      return new WsStream<NetworkStream>(_netStream);
    }

    private void doHandshake()
    {
      var res = sendOpeningHandshake();

      string msg;
      if (!isValidResponse(res, out msg))
      {
        error(msg);
        close(CloseStatusCode.HANDSHAKE_FAILURE, msg);
        return;
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

    private bool isValidRequest(RequestHandshake request, out string message)
    {
      Func<string, Func<string, string, string>> func = s =>
      {
        return (e, a) =>
        {
          return String.Format("Invalid request {0} value: {1}(expected: {2})", s, a, e);
        };
      };

      if (!request.IsWebSocketRequest)
      {
        message = "Invalid WebSocket request.";
        return false;
      }

      if (!isValidRequestUri(request.RequestUri, func("Request URI"), out message))
        return false;

      if (_uri.IsAbsoluteUri)
        if (!isValidRequestHost(request.GetHeaderValues("Host")[0], func("Host"), out message))
          return false;

      if (!request.HeaderExists("Sec-WebSocket-Version", _version))
      {
        message = "Unsupported Sec-WebSocket-Version.";
        return false;
      }

      _base64key = request.GetHeaderValues("Sec-WebSocket-Key")[0];

      if (request.HeaderExists("Sec-WebSocket-Protocol"))
        _protocols = request.Headers["Sec-WebSocket-Protocol"];

      if (request.HeaderExists("Sec-WebSocket-Extensions"))
        _extensions = request.Headers["Sec-WebSocket-Extensions"];

      message = String.Empty;
      return true;
    }

    private bool isValidRequestHost(string value, Func<string, string, string> func, out string message)
    {
      var host = _uri.DnsSafeHost;
      var type = Uri.CheckHostName(host);

      var address = _endPoint.Address;
      var port    = _endPoint.Port;

      var expectedHost1 = host;
      var expectedHost2 = address.ToString();
      if (type != UriHostNameType.Dns)
        expectedHost2 = Dns.GetHostEntry(address).HostName;

      if (port != 80)
      {
        expectedHost1 += ":" + port;
        expectedHost2 += ":" + port;
      }

      if (expectedHost1.NotEqualsDo(value, func, out message, false))
        if (expectedHost2.NotEqualsDo(value, func, out message, false))
          return false;

      message = String.Empty;
      return true;
    }

    private bool isValidRequestUri(Uri requestUri, Func<string, string, string> func, out string message)
    {
      if (_uri.IsAbsoluteUri && requestUri.IsAbsoluteUri)
        if (_uri.ToString().NotEqualsDo(requestUri.ToString(), func, out message, false))
          return false;

      if (_uri.IsAbsoluteUri && !requestUri.IsAbsoluteUri)
        if (_uri.PathAndQuery.NotEqualsDo(requestUri.ToString(), func, out message, false))
          return false;

      if (!_uri.IsAbsoluteUri && requestUri.IsAbsoluteUri)
        if (_uri.ToString().NotEqualsDo(requestUri.PathAndQuery, func, out message, false))
          return false;

      if (!_uri.IsAbsoluteUri && !requestUri.IsAbsoluteUri)
        if (_uri.ToString().NotEqualsDo(requestUri.ToString(), func, out message, false))
          return false;

      message = String.Empty;
      return true;
    }

    private bool isValidResponse(ResponseHandshake response, out string message)
    {
      if (!response.IsWebSocketResponse)
      {
        message = "Invalid WebSocket response.";
        return false;
      }

      if (!response.HeaderExists("Sec-WebSocket-Accept", createExpectedKey()))
      {
        message = "Invalid Sec-WebSocket-Accept value.";
        return false;
      }

      if (response.HeaderExists("Sec-WebSocket-Version"))
      {
        if (!response.HeaderExists("Sec-WebSocket-Version", _version))
        {
          message = "Unsupported Sec-WebSocket-Version.";
          return false;
        }
      }

      if (response.HeaderExists("Sec-WebSocket-Protocol"))
        _protocol = response.Headers["Sec-WebSocket-Protocol"];

      if (response.HeaderExists("Sec-WebSocket-Extensions"))
        _extensions = response.Headers["Sec-WebSocket-Extensions"];

      message = String.Empty;
      return true;
    }

    private bool isValidScheme(Uri uri)
    {
      string scheme = uri.Scheme;
      if (scheme == "ws" || scheme == "wss")
        return true;

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
      catch (Exception)
      {
        close(CloseStatusCode.ABNORMAL, "An exception has been raised.");
      }
    }

    private void messageLoop()
    {
      while (_readyState == WsState.OPEN)
      {
        message();
      }
    }

    private void messageLoopCallback(IAsyncResult ar)
    {
      Action messageInvoker = (Action)ar.AsyncState;
      messageInvoker.EndInvoke(ar);

      if (_readyState == WsState.OPEN)
      {
        messageInvoker.BeginInvoke(messageLoopCallback, messageInvoker);
      }
      else
      {
        _exitedMessageLoop.Set();
      }
    }

    private void pong(PayloadData data)
    {
      var frame = createFrame(Fin.FINAL, Opcode.PONG, data);
      send(frame);
    }

    private void pong(string data)
    {
      var payloadData = new PayloadData(data);
      pong(payloadData);
    }

    private string[] readHandshake()
    {
      var buffer = new List<byte>();

      while (true)
      {
        if (_wsStream.ReadByte().EqualsAndSaveTo('\r', buffer) &&
            _wsStream.ReadByte().EqualsAndSaveTo('\n', buffer) &&
            _wsStream.ReadByte().EqualsAndSaveTo('\r', buffer) &&
            _wsStream.ReadByte().EqualsAndSaveTo('\n', buffer))
          break;
      }

      return Encoding.UTF8.GetString(buffer.ToArray())
             .Replace("\r\n", "\n").Replace("\n\n", "\n").TrimEnd('\n')
             .Split('\n');
    }

    private MessageEventArgs receive()
    {
      List<byte>       dataBuffer;
      MessageEventArgs eventArgs;
      Fin              fin;
      WsFrame          frame;
      Opcode           opcode;
      PayloadData      payloadData;

      Action act = () =>
      {
        var msg = "WebSocket data frame can not be read from network stream.";
        close(CloseStatusCode.ABNORMAL, msg);
      };

      frame = _wsStream.ReadFrame();
      if (frame.IsNullDo(act)) return null;

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
            if (frame.IsNullDo(act)) return null;

            if (frame.Fin == Fin.MORE)
            {
              if (frame.Opcode == Opcode.CONT)
              {// MORE & CONT
                dataBuffer.AddRange(frame.PayloadData.ToBytes());
              }
              else
              {
                #if DEBUG
                Console.WriteLine("WS: Info@receive: Start closing handshake.");
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
              Console.WriteLine("WS: Info@receive: Start closing handshake.");
              #endif
              close(frame.PayloadData);
              return null;
            }
            else if (frame.Opcode == Opcode.PING)
            {// FINAL & PING
              #if DEBUG
              Console.WriteLine("WS: Info@receive: Return Pong.");
              #endif
              pong(frame.PayloadData);
            }
            else if (frame.Opcode == Opcode.PONG)
            {// FINAL & PONG
              _receivedPong.Set();
              OnMessage.Emit(this, new MessageEventArgs(frame.Opcode, frame.PayloadData));
            }
            else
            {// FINAL & (TEXT | BINARY)
              #if DEBUG
              Console.WriteLine("WS: Info@receive: Start closing handshake.");
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
              goto default;            
            case Opcode.PONG:
              _receivedPong.Set();
              goto default;
            case Opcode.CLOSE:
              #if DEBUG
              Console.WriteLine("WS: Info@receive: Start closing handshake.");
              #endif
              close(payloadData);
              break;
            case Opcode.PING:
              #if DEBUG
              Console.WriteLine("WS: Info@receive: Return Pong.");
              #endif
              pong(payloadData);
              break;
            default:
              eventArgs = new MessageEventArgs(opcode, payloadData);
              break;
          }
        
          break;
      }

      return eventArgs;
    }

    private RequestHandshake receiveOpeningHandshake()
    {
      RequestHandshake req;

      if (_context == null)
        req = RequestHandshake.Parse(readHandshake());
      else
        req = RequestHandshake.Parse(_context);
      #if DEBUG
      Console.WriteLine("WS: Info@receiveOpeningHandshake: Opening handshake from client:\n");
      Console.WriteLine(req.ToString());
      #endif
      return req;
    }

    private bool send(WsFrame frame)
    {
      if (_readyState == WsState.CONNECTING ||
          _readyState == WsState.CLOSED)
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
          var msg = "Current data can not be sent because there is untransmitted data.";
          error(msg);
          return false;
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
      where TStream : Stream
    {
      lock(_forSend)
      {
        if (_readyState != WsState.OPEN)
        {
          var msg = "Connection isn't established or connection was closed.";
          error(msg);
          return;
        }

        var length = stream.Length;
        if (length <= _fragmentLen)
        {
          var buffer = new byte[length];
          stream.Read(buffer, 0, (int)length);
          var frame = createFrame(Fin.FINAL, opcode, new PayloadData(buffer));
          send(frame);
        }
        else
        {
          sendFragmented(opcode, stream);
        }
      }
    }

    private ulong sendFragmented<TStream>(Opcode opcode, TStream stream)
      where TStream : Stream
    {
      WsFrame     frame;
      PayloadData payloadData;

      byte[] buffer1 = new byte[_fragmentLen];
      byte[] buffer2 = new byte[_fragmentLen];
      ulong  readLen = 0;
      int    tmpLen1 = 0;
      int    tmpLen2 = 0;

      tmpLen1 = stream.Read(buffer1, 0, _fragmentLen);
      while (tmpLen1 > 0)
      {
        payloadData = new PayloadData(buffer1.SubArray(0, tmpLen1));

        tmpLen2 = stream.Read(buffer2, 0, _fragmentLen);
        if (tmpLen2 > 0)
        {
          if (readLen > 0)
          {
            frame = createFrame(Fin.MORE, Opcode.CONT, payloadData);
          }
          else
          {
            frame = createFrame(Fin.MORE, opcode, payloadData);
          }
        }
        else
        {
          if (readLen > 0)
          {
            frame = createFrame(Fin.FINAL, Opcode.CONT, payloadData);
          }
          else
          {
            frame = createFrame(Fin.FINAL, opcode, payloadData);
          }
        }

        readLen += (ulong)tmpLen1;
        send(frame);

        if (tmpLen2 == 0) break;
        payloadData = new PayloadData(buffer2.SubArray(0, tmpLen2));

        tmpLen1 = stream.Read(buffer1, 0, _fragmentLen);
        if (tmpLen1 > 0)
        {
          frame = createFrame(Fin.MORE, Opcode.CONT, payloadData);
        }
        else
        {
          frame = createFrame(Fin.FINAL, Opcode.CONT, payloadData);
        }

        readLen += (ulong)tmpLen2;
        send(frame);
      }

      return readLen;
    }

    private ResponseHandshake sendOpeningHandshake()
    {
      var req = createOpeningHandshake();
      #if DEBUG
      Console.WriteLine("WS: Info@sendOpeningHandshake: Opening handshake from client:\n");
      Console.WriteLine(req.ToString());
      #endif
      _wsStream.Write(req.ToBytes(), 0, req.ToBytes().Length);

      var res = ResponseHandshake.Parse(readHandshake());
      #if DEBUG
      Console.WriteLine("WS: Info@sendOpeningHandshake: Response handshake from server:\n");
      Console.WriteLine(res.ToString());
      #endif
      return res;
    }

    private void sendResponseHandshake()
    {
      var res = createResponseHandshake();
      #if DEBUG
      Console.WriteLine("WS: Info@sendResponseHandshake: Response handshake from server:\n");
      Console.WriteLine(res.ToString());
      #endif
      _wsStream.Write(res.ToBytes(), 0, res.ToBytes().Length);
    }

    private void sendResponseHandshakeForInvalid()
    {
      var code = (int)WebSocketSharp.Net.HttpStatusCode.BadRequest;
      var res  = new ResponseHandshake {
        Reason     = "Bad Request",
        StatusCode = code.ToString()
      };
      res.Headers.Clear();
      res.AddHeader("Sec-WebSocket-Version", _version);

      _wsStream.Write(res.ToBytes(), 0, res.ToBytes().Length);
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
        _exitedMessageLoop = new AutoResetEvent(false);
        Action messageInvoker = () =>
        {
          if (_readyState == WsState.OPEN)
          {
            message();
          }
        };
        messageInvoker.BeginInvoke(messageLoopCallback, messageInvoker);
      }
    }

    #endregion

    #region Public Methods

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
        close(CloseStatusCode.HANDSHAKE_FAILURE, "An exception has been raised.");
      }
    }

    public void Dispose()
    {
      Close(CloseStatusCode.AWAY);
    }

    public bool Ping()
    {
      return Ping(String.Empty);
    }

    public bool Ping(string data)
    {
      var payloadData = new PayloadData(data);

      if (payloadData.Length > 125)
      {
        var msg = "Ping frame must have a payload length of 125 bytes or less.";
        error(msg);
        return false;
      }

      var frame = createFrame(Fin.FINAL, Opcode.PING, payloadData);
      if (!send(frame)) return false;

      return _receivedPong.WaitOne(5 * 1000);
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
