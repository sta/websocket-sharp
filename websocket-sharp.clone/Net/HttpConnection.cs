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

/*
 * Authors:
 * - Gonzalo Paniagua Javier <gonzalo@novell.com>
 */

/*
 * Contributors:
 * - Liryna <liryna.stark@gmail.com>
 */

namespace WebSocketSharp.Net
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Security;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class HttpConnection
    {
        private byte[] _buffer;
        private const int BufferSize = 8192;
        private bool _chunked;
        private HttpListenerContext _context;
        private bool _contextWasBound;
        private StringBuilder _currentLine;
        private InputState _inputState;
        private RequestStream _inputStream;
        private HttpListener _lastListener;
        private LineState _lineState;
        private readonly EndPointListener _listener;
        private ResponseStream _outputStream;
        private int _position;
        private HttpListenerPrefix _prefix;
        private MemoryStream _requestBuffer;
        private int _reuses;
        private readonly bool _secure;
        private Socket _socket;
        private Stream _stream;
        private int _timeout;
        private Timer _timer;

        public HttpConnection(Socket socket, EndPointListener listener)
        {
            _socket = socket;
            _listener = listener;
            _secure = listener.IsSecure;

            var netStream = new NetworkStream(socket, false);
            if (_secure)
            {
                var conf = listener.SslConfiguration;
                var sslStream = new SslStream(netStream, false, conf.ClientCertificateValidationCallback);
                sslStream.AuthenticateAsServer(
                  conf.ServerCertificate,
                  conf.ClientCertificateRequired,
                  conf.EnabledSslProtocols,
                  conf.CheckCertificateRevocation);

                _stream = sslStream;
            }
            else
            {
                _stream = netStream;
            }

            _timeout = 90000; // 90k ms for first request, 15k ms from then on.
            _timer = new Timer(OnTimeout, this, Timeout.Infinite, Timeout.Infinite);

            Init();
        }

        public bool IsClosed => _socket == null;

        public bool IsSecure => _secure;

        public IPEndPoint LocalEndPoint => (IPEndPoint)_socket.LocalEndPoint;

        public HttpListenerPrefix Prefix
        {
            get
            {
                return _prefix;
            }

            set
            {
                _prefix = value;
            }
        }

        public IPEndPoint RemoteEndPoint => (IPEndPoint)_socket.RemoteEndPoint;

        public int Reuses => _reuses;

        public Stream Stream => _stream;

        private void InnerClose()
        {
            if (_socket == null)
            {
                return;
            }

            DisposeTimer();
            DisposeRequestBuffer();
            DisposeStream();
            CloseSocket();

            Unbind();
            RemoveConnection();
        }

        private void CloseSocket()
        {
            try
            {
                _socket.Shutdown(SocketShutdown.Both);
            }
            catch
            {
            }

            _socket.Close();
            _socket = null;
        }

        private void DisposeRequestBuffer()
        {
            if (_requestBuffer == null)
                return;

            _requestBuffer.Dispose();
            _requestBuffer = null;
        }

        private void DisposeStream()
        {
            if (_stream == null)
                return;

            _inputStream = null;
            _outputStream = null;

            _stream.Dispose();
            _stream = null;
        }

        private void DisposeTimer()
        {
            if (_timer == null)
                return;

            try
            {
                _timer.Change(Timeout.Infinite, Timeout.Infinite);
            }
            catch
            {
            }

            _timer.Dispose();
            _timer = null;
        }

        private void Init()
        {
            _chunked = false;
            _context = new HttpListenerContext(this);
            _inputState = InputState.RequestLine;
            _inputStream = null;
            _lineState = LineState.None;
            _outputStream = null;
            _position = 0;
            _prefix = null;
            _requestBuffer = new MemoryStream();
        }

        private static async Task OnRead(HttpConnection conn, int nread)
        {
            if (conn._socket == null)
            {
                return;
            }

            //var nread = -1;
            try
            {
                conn._timer.Change(Timeout.Infinite, Timeout.Infinite);
                // nread = conn._stream.EndRead(asyncResult);
                await conn._requestBuffer.WriteAsync(conn._buffer, 0, nread).ConfigureAwait(false);
                if (conn._requestBuffer.Length > 32768)
                {
                    await conn.SendError("Bad request", 400).ConfigureAwait(false);
                    await conn.Close(true).ConfigureAwait(false);

                    return;
                }
            }
            catch
            {
                if (conn._requestBuffer != null && conn._requestBuffer.Length > 0)
                {
                    await conn.SendError().ConfigureAwait(false);
                }

                conn.InnerClose();
                return;
            }

            if (nread <= 0)
            {
                conn.InnerClose();
                return;
            }

            if (conn.ProcessInput(conn._requestBuffer.GetBuffer()))
            {
                if (!conn._context.HasError)
                {
                    await conn._context.Request.FinishInitialization().ConfigureAwait(false);
                }

                if (conn._context.HasError)
                {
                    await conn.SendError().ConfigureAwait(false);
                    await conn.Close(true).ConfigureAwait(false);

                    return;
                }

                if (!conn._listener.BindContext(conn._context))
                {
                    await conn.SendError("Invalid host", 400).ConfigureAwait(false);
                    await conn.Close(true).ConfigureAwait(false);

                    return;
                }

                var listener = conn._context.Listener;
                if (conn._lastListener != listener)
                {
                    conn.RemoveConnection();
                    listener.AddConnection(conn);
                    conn._lastListener = listener;
                }

                conn._contextWasBound = true;
                listener.RegisterContext(conn._context);

                return;
            }

            var bytesRead = await conn._stream.ReadAsync(conn._buffer, 0, BufferSize).ConfigureAwait(false);
            await OnRead(conn, bytesRead).ConfigureAwait(false);
        }

        private static void OnTimeout(object state)
        {
            var conn = (HttpConnection)state;
            conn.InnerClose();
        }

        // true -> Done processing.
        // false -> Need more input.
        private bool ProcessInput(byte[] data)
        {
            var len = data.Length;
            int used;
            string line;
            try
            {
                while ((line = ReadLine(data, _position, len - _position, out used)) != null)
                {
                    _position += used;
                    if (line.Length == 0)
                    {
                        if (_inputState == InputState.RequestLine)
                            continue;

                        _currentLine = null;
                        return true;
                    }

                    if (_inputState == InputState.RequestLine)
                    {
                        _context.Request.SetRequestLine(line);
                        _inputState = InputState.Headers;
                    }
                    else
                    {
                        _context.Request.AddHeader(line);
                    }

                    if (_context.HasError)
                        return true;
                }
            }
            catch (Exception ex)
            {
                _context.ErrorMessage = ex.Message;
                return true;
            }

            _position += used;
            if (used == len)
            {
                _requestBuffer.SetLength(0);
                _position = 0;
            }

            return false;
        }

        private string ReadLine(byte[] buffer, int offset, int length, out int used)
        {
            if (_currentLine == null)
                _currentLine = new StringBuilder();

            var last = offset + length;
            used = 0;
            for (int i = offset; i < last && _lineState != LineState.Lf; i++)
            {
                used++;
                var b = buffer[i];
                if (b == 13)
                    _lineState = LineState.Cr;
                else if (b == 10)
                    _lineState = LineState.Lf;
                else
                    _currentLine.Append((char)b);
            }

            string res = null;
            if (_lineState == LineState.Lf)
            {
                _lineState = LineState.None;
                res = _currentLine.ToString();
                _currentLine.Length = 0;
            }

            return res;
        }

        private void RemoveConnection()
        {
            if (_lastListener == null)
                _listener.RemoveConnection(this);
            else
                _lastListener.RemoveConnection(this);
        }

        private void Unbind()
        {
            if (_contextWasBound)
            {
                _listener.UnbindContext(_context);
                _contextWasBound = false;
            }
        }

        internal async Task Close(bool force = false)
        {
            if (_socket == null)
            {
                return;
            }

            if (!force)
            {
                GetResponseStream().Close();

                var req = _context.Request;
                var res = _context.Response;
                if (req.KeepAlive
                    && !res.ConnectionClose
                    && await req.FlushInput().ConfigureAwait(false)
                    && (!_chunked || (_chunked && !res.ForceCloseChunked)))
                {
                    // Don't close. Keep working.
                    _reuses++;
                    DisposeRequestBuffer();
                    Unbind();
                    Init();
                    await ReadRequest().ConfigureAwait(false);

                    return;
                }
            }

            InnerClose();
        }

        public async Task ReadRequest()
        {
            if (_buffer == null)
            {
                _buffer = new byte[BufferSize];
            }

            if (_reuses == 1)
            {
                _timeout = 15000;
            }

            try
            {
                _timer.Change(_timeout, Timeout.Infinite);
                var read = await _stream.ReadAsync(_buffer, 0, BufferSize).ConfigureAwait(false);
                await OnRead(this, read).ConfigureAwait(false);
            }
            catch
            {
                InnerClose();
            }
        }

        public RequestStream GetRequestStream(long contentlength)
        {
            if (_inputStream != null || _socket == null)
            {
                return _inputStream;
            }

            if (_socket == null)
            {
                return _inputStream;
            }

            var buff = _requestBuffer.GetBuffer();
            var len = buff.Length;
            DisposeRequestBuffer();

            _inputStream = new RequestStream(_stream, buff, _position, len - _position, contentlength);

            return _inputStream;
        }

        public ResponseStream GetResponseStream()
        {
            // TODO: Can we get this stream before reading the input?

            if (_outputStream != null || _socket == null)
            {
                return _outputStream;
            }

            if (_socket == null)
            {
                return _outputStream;
            }

            var ignore = _context.Listener == null;
            _outputStream = new ResponseStream(_stream, _context.Response, ignore);

            return _outputStream;
        }

        private Task SendError()
        {
            return SendError(_context.ErrorMessage, _context.ErrorStatus);
        }

        private async Task SendError(string message, int status)
        {
            if (_socket == null)
            {
                return;
            }

            try
            {
                var res = _context.Response;
                res.StatusCode = status;
                res.ContentType = "text/html";

                var desc = status.GetStatusDescription();
                var msg = !string.IsNullOrEmpty(message)
                          ? string.Format("<h1>{0} ({1})</h1>", desc, message)
                          : string.Format("<h1>{0}</h1>", desc);

                var entity = res.ContentEncoding.GetBytes(msg);
                await res.Close(entity).ConfigureAwait(false);
            }
            catch
            {
                // Response was already closed.
            }

        }
    }
}
