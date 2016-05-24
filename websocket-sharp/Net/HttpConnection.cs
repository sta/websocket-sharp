#region License
/*
 * HttpConnection.cs
 *
 * This code is derived from HttpConnection.cs (System.Net) of Mono
 * (http://www.mono-project.com).
 *
 * The MIT License
 *
 * Copyright (c) 2005 Novell, Inc. (http://www.novell.com)
 * Copyright (c) 2012-2015 sta.blockhead
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

#region Contributors
/*
 * Contributors:
 * - Liryna <liryna.stark@gmail.com>
 * - Rohan Singh <rohan-singh@hotmail.com>
 */
#endregion

using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace WebSocketSharp.Net
{
  internal sealed class HttpConnection
  {
    #region Private Fields

    private byte[]              _buffer;
    private const int           _bufferLength = 8192;
    private HttpListenerContext _context;
    private bool                _contextRegistered;
    private StringBuilder       _currentLine;
    private InputState          _inputState;
    private RequestStream       _inputStream;
    private HttpListener        _lastListener;
    private LineState           _lineState;
    private EndPointListener    _listener;
    private ResponseStream      _outputStream;
    private int                 _position;
    private HttpListenerPrefix  _prefix;
    private MemoryStream        _requestBuffer;
    private int                 _reuses;
    private bool                _secure;
    private Socket              _socket;
    private Stream              _stream;
    private object              _sync;
    private int                 _timeout;
    private Timer               _timer;

    #endregion

    #region Internal Constructors

    internal HttpConnection (Socket socket, EndPointListener listener)
    {
      _socket = socket;
      _listener = listener;
      _secure = listener.IsSecure;

      var netStream = new NetworkStream (socket, false);
      if (_secure) {
        var conf = listener.SslConfiguration;
        var sslStream = new SslStream (netStream, false, conf.ClientCertificateValidationCallback);
        sslStream.AuthenticateAsServer (
          conf.ServerCertificate,
          conf.ClientCertificateRequired,
          conf.EnabledSslProtocols,
          conf.CheckCertificateRevocation);

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

    public HttpListenerPrefix Prefix {
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

      unregisterContext ();
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
        var len = 0;
        try {
          conn._timer.Change (Timeout.Infinite, Timeout.Infinite);
          nread = conn._stream.EndRead (asyncResult);
          conn._requestBuffer.Write (conn._buffer, 0, nread);
          len = (int) conn._requestBuffer.Length;
        }
        catch (Exception ex) {
          if (conn._requestBuffer != null && conn._requestBuffer.Length > 0) {
            conn.SendError (ex.Message, 400);
            return;
          }

          conn.close ();
          return;
        }

        if (nread <= 0) {
          conn.close ();
          return;
        }

        if (conn.processInput (conn._requestBuffer.GetBuffer (), len)) {
          if (!conn._context.HasError)
            conn._context.Request.FinishInitialization ();

          if (conn._context.HasError) {
            conn.SendError ();
            return;
          }

          if (!conn._listener.BindContext (conn._context)) {
            conn.SendError ("Invalid host", 400);
            return;
          }

          var lsnr = conn._context.Listener;
          if (conn._lastListener != lsnr) {
            conn.removeConnection ();
            if (!lsnr.AddConnection (conn)) {
              conn.close ();
              return;
            }

            conn._lastListener = lsnr;
          }

          if (lsnr.RegisterContext (conn._context))
            conn._contextRegistered = true;

          return;
        }

        conn._stream.BeginRead (conn._buffer, 0, _bufferLength, onRead, conn);
      }
    }

    private static void onTimeout (object state)
    {
      var conn = (HttpConnection) state;
      conn.close ();
    }

    // true -> Done processing.
    // false -> Need more input.
    private bool processInput (byte[] data, int length)
    {
      if (_currentLine == null)
        _currentLine = new StringBuilder (64);

      var nread = 0;
      try {
        string line;
        while ((line = readLineFrom (data, _position, length, out nread)) != null) {
          _position += nread;
          if (line.Length == 0) {
            if (_inputState == InputState.RequestLine)
              continue;

            if (_position > 32768)
              _context.ErrorMessage = "Headers too long";

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

      _position += nread;
      if (_position >= 32768) {
        _context.ErrorMessage = "Headers too long";
        return true;
      }

      return false;
    }

    private string readLineFrom (byte[] buffer, int offset, int length, out int read)
    {
      read = 0;
      for (var i = offset; i < length && _lineState != LineState.Lf; i++) {
        read++;
        var b = buffer[i];
        if (b == 13)
          _lineState = LineState.Cr;
        else if (b == 10)
          _lineState = LineState.Lf;
        else
          _currentLine.Append ((char) b);
      }

      if (_lineState == LineState.Lf) {
        _lineState = LineState.None;
        var line = _currentLine.ToString ();
        _currentLine.Length = 0;

        return line;
      }

      return null;
    }

    private void removeConnection ()
    {
      if (_lastListener != null)
        _lastListener.RemoveConnection (this);
      else
        _listener.RemoveConnection (this);
    }

    private void unregisterContext ()
    {
      if (!_contextRegistered)
        return;

      _context.Unregister ();
      _contextRegistered = false;
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
          GetResponseStream ().Close (false);
          if (!_context.Response.CloseConnection && _context.Request.FlushInput ()) {
            // Don't close. Keep working.
            _reuses++;
            disposeRequestBuffer ();
            unregisterContext ();
            init ();
            BeginReadRequest ();

            return;
          }
        }
        else if (_outputStream != null) {
          _outputStream.Close (true);
        }

        close ();
      }
    }

    #endregion

    #region Public Methods

    public void BeginReadRequest ()
    {
      if (_buffer == null)
        _buffer = new byte[_bufferLength];

      if (_reuses == 1)
        _timeout = 15000;

      try {
        _timer.Change (_timeout, Timeout.Infinite);
        _stream.BeginRead (_buffer, 0, _bufferLength, onRead, this);
      }
      catch {
        close ();
      }
    }

    public void Close ()
    {
      Close (false);
    }

    public RequestStream GetRequestStream (long contentLength, bool chunked)
    {
      if (_inputStream != null || _socket == null)
        return _inputStream;

      lock (_sync) {
        if (_socket == null)
          return _inputStream;

        var buff = _requestBuffer.GetBuffer ();
        var len = (int) _requestBuffer.Length;
        disposeRequestBuffer ();
        if (chunked) {
          _context.Response.SendChunked = true;
          _inputStream = new ChunkedRequestStream (
            _stream, buff, _position, len - _position, _context);
        }
        else {
          _inputStream = new RequestStream (
            _stream, buff, _position, len - _position, contentLength);
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

        var lsnr = _context.Listener;
        var ignore = lsnr != null ? lsnr.IgnoreWriteExceptions : true;
        _outputStream = new ResponseStream (_stream, _context.Response, ignore);

        return _outputStream;
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

          var content = new StringBuilder (64);
          content.AppendFormat ("<html><body><h1>{0} {1}", status, res.StatusDescription);
          if (message != null && message.Length > 0)
            content.AppendFormat (" ({0})</h1></body></html>", message);
          else
            content.Append ("</h1></body></html>");

          var enc = Encoding.UTF8;
          var entity = enc.GetBytes (content.ToString ());
          res.ContentEncoding = enc;
          res.ContentLength64 = entity.LongLength;

          res.Close (entity, true);
        }
        catch {
          Close (true);
        }
      }
    }

    #endregion
  }
}
