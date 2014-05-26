#region License
/*
 * HttpConnection.cs
 *
 * This code is derived from System.Net.HttpConnection.cs of Mono
 * (http://www.mono-project.com).
 *
 * The MIT License
 *
 * Copyright (c) 2005 Novell, Inc. (http://www.novell.com)
 * Copyright (c) 2012-2014 sta.blockhead
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
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using WebSocketSharp.Net.Security;

namespace WebSocketSharp.Net
{
  internal sealed class HttpConnection
  {
    #region Private Const Fields

    private const int _bufferSize = 8192;

    #endregion

    #region Private Fields

    private byte []             _buffer;
    private bool                _chunked;
    private HttpListenerContext _context;
    private bool                _contextWasBound;
    private StringBuilder       _currentLine;
    private InputState          _inputState;
    private RequestStream       _inputStream;
    private HttpListener        _lastListener;
    private LineState           _lineState;
    private EndPointListener    _listener;
    private ResponseStream      _outputStream;
    private int                 _position;
    private ListenerPrefix      _prefix;
    private MemoryStream        _requestBuffer;
    private int                 _reuses;
    private bool                _secure;
    private Socket              _socket;
    private Stream              _stream;
    private object              _sync;
    private int                 _timeout;
    private Timer               _timer;
    private WebSocketStream     _websocketStream;

    #endregion

    #region Public Constructors

    public HttpConnection (Socket socket, EndPointListener listener)
    {
      _socket = socket;
      _listener = listener;
      _secure = listener.IsSecure;

      var netStream = new NetworkStream (socket, false);
      if (_secure) {
        var sslStream = new SslStream (netStream, false);
        sslStream.AuthenticateAsServer (listener.Certificate);
        _stream = sslStream;
      }
      else {
        _stream = netStream;
      }

      _sync = new object ();
      _timeout = 90000; // 90k ms for first request, 15k ms from then on.
      _timer = new Timer (onTimeout, this, Timeout.Infinite, Timeout.Infinite);

      init ();
    }

    #endregion

    #region Public Properties

    public bool IsClosed {
      get {
        return _socket == null;
      }
    }

    public bool IsSecure {
      get {
        return _secure;
      }
    }

    public IPEndPoint LocalEndPoint {
      get {
        return (IPEndPoint) _socket.LocalEndPoint;
      }
    }

    public ListenerPrefix Prefix {
      get {
        return _prefix;
      }

      set {
        _prefix = value;
      }
    }

    public IPEndPoint RemoteEndPoint {
      get {
        return (IPEndPoint) _socket.RemoteEndPoint;
      }
    }

    public int Reuses {
      get {
        return _reuses;
      }
    }

    public Stream Stream {
      get {
        return _stream;
      }
    }

    #endregion

    #region Private Methods

    private void close ()
    {
      lock (_sync) {
        if (_socket == null)
          return;

        disposeTimer ();
        disposeRequestBuffer ();
        disposeStream ();
        closeSocket ();
      }

      unbind ();
      removeConnection ();
    }

    private void closeSocket ()
    {
      try {
        _socket.Shutdown (SocketShutdown.Both);
      }
      catch {
      }

      _socket.Close ();
      _socket = null;
    }

    private void disposeRequestBuffer ()
    {
      if (_requestBuffer == null)
        return;

      _requestBuffer.Dispose ();
      _requestBuffer = null;
    }

    private void disposeStream ()
    {
      if (_stream == null)
        return;

      _inputStream = null;
      _outputStream = null;
      _websocketStream = null;

      _stream.Dispose ();
      _stream = null;
    }

    private void disposeTimer ()
    {
      if (_timer == null)
        return;

      try {
        _timer.Change (Timeout.Infinite, Timeout.Infinite);
      }
      catch {
      }

      _timer.Dispose ();
      _timer = null;
    }

    private void init ()
    {
      _chunked = false;
      _context = new HttpListenerContext (this);
      _inputState = InputState.RequestLine;
      _inputStream = null;
      _lineState = LineState.None;
      _outputStream = null;
      _position = 0;
      _prefix = null;
      _requestBuffer = new MemoryStream ();
    }

    private static void onRead (IAsyncResult asyncResult)
    {
      var conn = (HttpConnection) asyncResult.AsyncState;
      if (conn._socket == null)
        return;

      lock (conn._sync) {
        if (conn._socket == null)
          return;

        var nread = -1;
        try {
          conn._timer.Change (Timeout.Infinite, Timeout.Infinite);
          nread = conn._stream.EndRead (asyncResult);
          conn._requestBuffer.Write (conn._buffer, 0, nread);
          if (conn._requestBuffer.Length > 32768) {
            conn.SendError ("Bad request", 400);
            conn.Close (true);

            return;
          }
        }
        catch {
          if (conn._requestBuffer != null && conn._requestBuffer.Length > 0)
            conn.SendError ();

          conn.close ();
          return;
        }

        if (nread <= 0) {
          conn.close ();
          return;
        }

        if (conn.processInput (conn._requestBuffer.GetBuffer ())) {
          if (!conn._context.HasError)
            conn._context.Request.FinishInitialization ();

          if (conn._context.HasError) {
            conn.SendError ();
            conn.Close (true);

            return;
          }

          if (!conn._listener.BindContext (conn._context)) {
            conn.SendError ("Invalid host", 400);
            conn.Close (true);

            return;
          }

          var listener = conn._context.Listener;
          if (conn._lastListener != listener) {
            conn.removeConnection ();
            listener.AddConnection (conn);
            conn._lastListener = listener;
          }

          conn._contextWasBound = true;
          listener.RegisterContext (conn._context);

          return;
        }

        conn._stream.BeginRead (conn._buffer, 0, _bufferSize, onRead, conn);
      }
    }

    private static void onTimeout (object state)
    {
      var conn = (HttpConnection) state;
      conn.close ();
    }

    // true -> Done processing.
    // false -> Need more input.
    private bool processInput (byte [] data)
    {
      var len = data.Length;
      var used = 0;
      string line;
      try {
        while ((line = readLine (data, _position, len - _position, ref used)) != null) {
          _position += used;
          if (line.Length == 0) {
            if (_inputState == InputState.RequestLine)
              continue;

            _currentLine = null;
            return true;
          }

          if (_inputState == InputState.RequestLine) {
            _context.Request.SetRequestLine (line);
            _inputState = InputState.Headers;
          }
          else {
            _context.Request.AddHeader (line);
          }

          if (_context.HasError)
            return true;
        }
      }
      catch (Exception ex) {
        _context.ErrorMessage = ex.Message;
        return true;
      }

      _position += used;
      if (used == len) {
        _requestBuffer.SetLength (0);
        _position = 0;
      }

      return false;
    }

    private string readLine (byte [] buffer, int offset, int length, ref int used)
    {
      if (_currentLine == null)
        _currentLine = new StringBuilder ();

      var last = offset + length;
      used = 0;
      for (int i = offset; i < last && _lineState != LineState.LF; i++) {
        used++;
        var b = buffer [i];
        if (b == 13)
          _lineState = LineState.CR;
        else if (b == 10)
          _lineState = LineState.LF;
        else
          _currentLine.Append ((char) b);
      }

      string res = null;
      if (_lineState == LineState.LF) {
        _lineState = LineState.None;
        res = _currentLine.ToString ();
        _currentLine.Length = 0;
      }

      return res;
    }

    private void removeConnection ()
    {
      if (_lastListener == null)
        _listener.RemoveConnection (this);
      else
        _lastListener.RemoveConnection (this);
    }

    private void unbind ()
    {
      if (_contextWasBound) {
        _listener.UnbindContext (_context);
        _contextWasBound = false;
      }
    }

    #endregion

    #region Internal Methods

    internal void Close (bool force)
    {
      if (_socket == null)
        return;

      lock (_sync) {
        if (_socket == null)
          return;

        if (!force) {
          GetResponseStream ().Close ();

          var req = _context.Request;
          var res = _context.Response;
          if (req.KeepAlive &&
              !res.CloseConnection &&
              req.FlushInput () &&
              (!_chunked || (_chunked && !res.ForceCloseChunked))) {
            // Don't close. Keep working.
            _reuses++;
            disposeRequestBuffer ();
            unbind ();
            init ();
            BeginReadRequest ();

            return;
          }
        }

        close ();
      }
    }

    #endregion

    #region Public Methods

    public void BeginReadRequest ()
    {
      if (_buffer == null)
        _buffer = new byte [_bufferSize];

      if (_reuses == 1)
        _timeout = 15000;

      try {
        _timer.Change (_timeout, Timeout.Infinite);
        _stream.BeginRead (_buffer, 0, _bufferSize, onRead, this);
      }
      catch {
        close ();
      }
    }

    public void Close ()
    {
      Close (false);
    }

    public RequestStream GetRequestStream (bool chunked, long contentlength)
    {
      if (_inputStream != null || _socket == null)
        return _inputStream;

      lock (_sync) {
        if (_socket == null)
          return _inputStream;

        var buff = _requestBuffer.GetBuffer ();
        var len = buff.Length;
        disposeRequestBuffer ();
        if (chunked) {
          _chunked = true;
          _context.Response.SendChunked = true;
          _inputStream = new ChunkedRequestStream (
            _context, _stream, buff, _position, len - _position);
        }
        else {
          _inputStream = new RequestStream (
            _stream, buff, _position, len - _position, contentlength);
        }

        return _inputStream;
      }
    }

    public ResponseStream GetResponseStream ()
    {
      // TODO: Can we get this stream before reading the input?

      if (_outputStream != null || _socket == null)
        return _outputStream;

      lock (_sync) {
        if (_socket == null)
          return _outputStream;

        var listener = _context.Listener;
        var ignore = listener == null ? true : listener.IgnoreWriteExceptions;
        _outputStream = new ResponseStream (_stream, _context.Response, ignore);

        return _outputStream;
      }
    }

    public WebSocketStream GetWebSocketStream ()
    {
      if (_websocketStream != null || _socket == null)
        return _websocketStream;

      lock (_sync) {
        if (_socket == null)
          return _websocketStream;

        _websocketStream = new WebSocketStream (_stream, _secure);
        return _websocketStream;
      }
    }

    public void SendError ()
    {
      SendError (_context.ErrorMessage, _context.ErrorStatus);
    }

    public void SendError (string message, int status)
    {
      if (_socket == null)
        return;

      lock (_sync) {
        if (_socket == null)
          return;

        try {
          var res = _context.Response;
          res.StatusCode = status;
          res.ContentType = "text/html";

          var desc = status.GetStatusDescription ();
          var msg = message != null && message.Length > 0
                    ? String.Format ("<h1>{0} ({1})</h1>", desc, message)
                    : String.Format ("<h1>{0}</h1>", desc);

          var entity = res.ContentEncoding.GetBytes (msg);
          res.Close (entity, false);
        }
        catch {
          // Response was already closed.
        }
      }
    }

    #endregion
  }
}
