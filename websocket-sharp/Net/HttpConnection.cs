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

#region Authors
/*
 * Authors:
 *   Gonzalo Paniagua Javier <gonzalo@novell.com>
 */
#endregion

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using WebSocketSharp.Net.Security;

namespace WebSocketSharp.Net
{
  internal sealed class HttpConnection
  {
    #region Internal Enums

    enum InputState {
      RequestLine,
      Headers
    }

    enum LineState {
      None,
      CR,
      LF
    }

    #endregion

    #region Private Const Fields

    private const int _bufferSize = 8192;

    #endregion

    #region Private Fields

    private byte []             _buffer;
    private bool                _chunked;
    private HttpListenerContext _context;
    private bool                _contextWasBound;
    private StringBuilder       _currentLine;
    private EndPointListener    _epListener;
    private InputState          _inputState;
    private RequestStream       _inputStream;
    private HttpListener        _lastListener;
    private LineState           _lineState;
    private ResponseStream      _outputStream;
    private int                 _position;
    private ListenerPrefix      _prefix;
    private MemoryStream        _requestBuffer;
    private int                 _reuses;
    private bool                _secure;
    private Socket              _socket;
    private Stream              _stream;
    private int                 _timeout;
    private Timer               _timer;

    #endregion

    #region Public Constructors

    public HttpConnection (
      Socket socket,
      EndPointListener listener,
      bool secure,
      X509Certificate2 cert)
    {
      _socket = socket;
      _epListener = listener;
      _secure = secure;

      var netStream = new NetworkStream (socket, false);
      if (!secure)
        _stream = netStream;
      else {
        var sslStream = new SslStream (netStream, false);
        sslStream.AuthenticateAsServer (cert);
        _stream = sslStream;
      }

      _timeout = 90000; // 90k ms for first request, 15k ms from then on.
      _timer = new Timer (onTimeout, null, Timeout.Infinite, Timeout.Infinite);
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

    private void closeSocket ()
    {
      if (_socket == null)
        return;

      try {
        _socket.Close ();
      }
      catch {
      }
      finally {
        _socket = null;
      }

      removeConnection ();
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
      conn.onReadInternal (asyncResult);
    }

    private void onReadInternal (IAsyncResult asyncResult)
    {
      _timer.Change (Timeout.Infinite, Timeout.Infinite);

      var read = -1;
      try {
        read = _stream.EndRead (asyncResult);
        _requestBuffer.Write (_buffer, 0, read);
        if (_requestBuffer.Length > 32768) {
          SendError ();
          Close (true);

          return;
        }
      }
      catch {
        if (_requestBuffer != null && _requestBuffer.Length > 0)
          SendError ();

        if (_socket != null) {
          closeSocket ();
          unbind ();
        }

        return;
      }

      if (read <= 0) {
        closeSocket ();
        unbind ();

        return;
      }

      if (processInput (_requestBuffer.GetBuffer ())) {
        if (!_context.HaveError)
          _context.Request.FinishInitialization ();
        else {
          SendError ();
          Close (true);

          return;
        }

        if (!_epListener.BindContext (_context)) {
          SendError ("Invalid host", 400);
          Close (true);

          return;
        }

        var listener = _context.Listener;
        if (_lastListener != listener) {
          removeConnection ();
          listener.AddConnection (this);
          _lastListener = listener;
        }

        _contextWasBound = true;
        listener.RegisterContext (_context);

        return;
      }

      _stream.BeginRead (_buffer, 0, _bufferSize, onRead, this);
    }

    private void onTimeout (object unused)
    {
      closeSocket ();
      unbind ();
    }

    // true -> Done processing.
    // false -> Need more input.
    private bool processInput (byte [] data)
    {
      var length = data.Length;
      var used = 0;
      string line;
      try {
        while ((line = readLine (
          data, _position, length - _position, ref used)) != null) {
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

          if (_context.HaveError)
            return true;
        }
      }
      catch (Exception ex) {
        _context.ErrorMessage = ex.Message;
        return true;
      }

      _position += used;
      if (used == length) {
        _requestBuffer.SetLength (0);
        _position = 0;
      }

      return false;
    }

    private string readLine (
      byte [] buffer, int offset, int length, ref int used)
    {
      if (_currentLine == null)
        _currentLine = new StringBuilder ();

      var last = offset + length;
      used = 0;
      for (int i = offset; i < last && _lineState != LineState.LF; i++) {
        used++;
        var b = buffer [i];
        if (b == 13) {
          _lineState = LineState.CR;
        }
        else if (b == 10) {
          _lineState = LineState.LF;
        }
        else {
          _currentLine.Append ((char) b);
        }
      }

      string result = null;
      if (_lineState == LineState.LF) {
        _lineState = LineState.None;
        result = _currentLine.ToString ();
        _currentLine.Length = 0;
      }

      return result;
    }

    private void removeConnection ()
    {
      if (_lastListener == null)
        _epListener.RemoveConnection (this);
      else
        _lastListener.RemoveConnection (this);
    }

    private void unbind ()
    {
      if (_contextWasBound) {
        _epListener.UnbindContext (_context);
        _contextWasBound = false;
      }
    }

    #endregion

    #region Internal Methods

    internal void Close (bool force)
    {
      if (_socket != null) {
        if (_outputStream != null) {
          _outputStream.Close ();
          _outputStream = null;
        }

        var req = _context.Request;
        var res = _context.Response;
        force |= !req.KeepAlive;
        if (!force)
          force = res.Headers ["Connection"] == "close";

        if (!force &&
            req.FlushInput () &&
            (!_chunked || (_chunked && !res.ForceCloseChunked))) {
          // Don't close. Keep working.
          _reuses++;
          unbind ();
          init ();
          BeginReadRequest ();

          return;
        }

        var socket = _socket;
        _socket = null;
        try {
          socket.Shutdown (SocketShutdown.Both);
        }
        catch {
        }
        finally {
          if (socket != null)
            socket.Close ();
        }

        unbind ();
        removeConnection ();

        return;
      }
    }

    #endregion

    #region Public Methods

    public void BeginReadRequest ()
    {
      if (_buffer == null)
        _buffer = new byte [_bufferSize];

      try {
        if (_reuses == 1)
          _timeout = 15000;

        _timer.Change (_timeout, Timeout.Infinite);
        _stream.BeginRead (_buffer, 0, _bufferSize, onRead, this);
      }
      catch {
        _timer.Change (Timeout.Infinite, Timeout.Infinite);
        closeSocket ();
        unbind ();
      }
    }

    public void Close ()
    {
      Close (false);
    }

    public RequestStream GetRequestStream (bool chunked, long contentlength)
    {
      if (_inputStream == null) {
        var buffer = _requestBuffer.GetBuffer ();
        var length = buffer.Length;

        _requestBuffer = null;
        if (chunked) {
          _chunked = true;
          _context.Response.SendChunked = true;
          _inputStream = new ChunkedInputStream (
            _context, _stream, buffer, _position, length - _position);
        }
        else {
          _inputStream = new RequestStream (
            _stream, buffer, _position, length - _position, contentlength);
        }
      }

      return _inputStream;
    }

    public ResponseStream GetResponseStream ()
    {
      // TODO: Can we get this stream before reading the input?
      if (_outputStream == null) {
        var listener = _context.Listener;
        var ignore = listener == null ? true : listener.IgnoreWriteExceptions;
        _outputStream = new ResponseStream (_stream, _context.Response, ignore);
      }

      return _outputStream;
    }

    public void SendError ()
    {
      SendError (_context.ErrorMessage, _context.ErrorStatus);
    }

    public void SendError (string message, int status)
    {
      try {
        var res = _context.Response;
        res.StatusCode = status;
        res.ContentType = "text/html";

        var description = status.GetStatusDescription ();
        var error = message != null && message.Length > 0
                  ? String.Format ("<h1>{0} ({1})</h1>", description, message)
                  : String.Format ("<h1>{0}</h1>", description);

        var entity = res.ContentEncoding.GetBytes (error);
        res.Close (entity, false);
      }
      catch {
        // Response was already closed.
      }
    }

    #endregion
  }
}
