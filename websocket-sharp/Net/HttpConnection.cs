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
 * Copyright (c) 2012-2020 sta.blockhead
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
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace WebSocketSharp.Net {
    internal sealed class HttpConnection {
        #region Private Fields

        private byte[] _buffer;
        private static readonly int _bufferLength;
        private HttpListenerContext _context;
        private bool _contextRegistered;
        private StringBuilder _currentLine;
        private InputState _inputState;
        private RequestStream _inputStream;
        private HttpListener _lastListener;
        private LineState _lineState;
        private EndPointListener _listener;
        private EndPoint _localEndPoint;
        private static readonly int _maxInputLength;
        private ResponseStream _outputStream;
        private int _position;
        private EndPoint _remoteEndPoint;
        private MemoryStream _requestBuffer;
        private int _reuses;
        private bool _secure;
        private Socket _socket;
        private Stream _stream;
        private object _sync;
        private int _timeout;
        private Dictionary<int, bool> _timeoutCanceled;
        private Timer _timer;

        #endregion

        #region Static Constructor

        static HttpConnection() {
            _bufferLength = 8192;
            _maxInputLength = 32768;
        }

        #endregion

        #region Internal Constructors

        internal HttpConnection(Socket socket, EndPointListener listener) {
            _socket = socket;
            _listener = listener;

            var netStream = new NetworkStream(socket, false);

            if (listener.IsSecure) {
                var sslConf = listener.SslConfiguration;
                var sslStream = new SslStream(
                                  netStream,
                                  false,
                                  sslConf.ClientCertificateValidationCallback
                                );

                sslStream.AuthenticateAsServer(
                  sslConf.ServerCertificate,
                  sslConf.ClientCertificateRequired,
                  sslConf.EnabledSslProtocols,
                  sslConf.CheckCertificateRevocation
                );

                _secure = true;
                _stream = sslStream;
            }
            else {
                _stream = netStream;
            }

            _buffer = new byte[_bufferLength];
            _localEndPoint = socket.LocalEndPoint;
            _remoteEndPoint = socket.RemoteEndPoint;
            _sync = new object();
            _timeoutCanceled = new Dictionary<int, bool>();
            _timer = new Timer(onTimeout, this, Timeout.Infinite, Timeout.Infinite);

            init(90000); // 90k ms for first request, 15k ms from then on.
        }

        #endregion

        #region Public Properties

        public bool IsClosed {
            get {
                return _socket == null;
            }
        }

        public bool IsLocal {
            get {
                return ((IPEndPoint)_remoteEndPoint).Address.IsLocal();
            }
        }

        public bool IsSecure {
            get {
                return _secure;
            }
        }

        public IPEndPoint LocalEndPoint {
            get {
                return (IPEndPoint)_localEndPoint;
            }
        }

        public IPEndPoint RemoteEndPoint {
            get {
                return (IPEndPoint)_remoteEndPoint;
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

        private void close() {
            lock (_sync) {
                if (_socket == null)
                    return;

                disposeTimer();
                disposeRequestBuffer();
                disposeStream();
                closeSocket();
            }

            unregisterContext();
            removeConnection();
        }

        private void closeSocket() {
            try {
                _socket.Shutdown(SocketShutdown.Both);
            }
            catch {
            }

            _socket.Close();

            _socket = null;
        }

        private void disposeRequestBuffer() {
            if (_requestBuffer == null)
                return;

            _requestBuffer.Dispose();

            _requestBuffer = null;
        }

        private void disposeStream() {
            if (_stream == null)
                return;

            _stream.Dispose();

            _stream = null;
        }

        private void disposeTimer() {
            if (_timer == null)
                return;

            try {
                _timer.Change(Timeout.Infinite, Timeout.Infinite);
            }
            catch {
            }

            _timer.Dispose();

            _timer = null;
        }

        private void init(int timeout) {
            _timeout = timeout;

            _context = new HttpListenerContext(this);
            _currentLine = new StringBuilder(64);
            _inputState = InputState.RequestLine;
            _inputStream = null;
            _lineState = LineState.None;
            _outputStream = null;
            _position = 0;
            _requestBuffer = new MemoryStream();
        }

        private static void onRead(IAsyncResult asyncResult) {
            var conn = (HttpConnection)asyncResult.AsyncState;
            var current = conn._reuses;

            if (conn._socket == null)
                return;

            lock (conn._sync) {
                if (conn._socket == null)
                    return;

                if (!conn._timeoutCanceled[current]) {
                    conn._timer.Change(Timeout.Infinite, Timeout.Infinite);
                    conn._timeoutCanceled[current] = true;
                }

                var nread = 0;

                try {
                    nread = conn._stream.EndRead(asyncResult);
                }
                catch (Exception) {
                    // TODO: Logging.

                    conn.close();

                    return;
                }

                if (nread <= 0) {
                    conn.close();

                    return;
                }

                conn._requestBuffer.Write(conn._buffer, 0, nread);
                var len = (int)conn._requestBuffer.Length;

                if (conn.processInput(conn._requestBuffer.GetBuffer(), len)) {
                    if (!conn._context.HasErrorMessage)
                        conn._context.Request.FinishInitialization();

                    if (conn._context.HasErrorMessage) {
                        conn._context.SendError();

                        return;
                    }

                    var url = conn._context.Request.Url;
                    HttpListener lsnr;

                    if (conn._listener.TrySearchHttpListener(url, out lsnr)) {
                        conn.registerContext(lsnr);

                        return;
                    }

                    conn._context.ErrorStatusCode = 404;
                    conn._context.SendError();

                    return;
                }

                try {
                    conn._stream.BeginRead(conn._buffer, 0, _bufferLength, onRead, conn);
                }
                catch (Exception) {
                    // TODO: Logging.

                    conn.close();
                }
            }
        }

        private static void onTimeout(object state) {
            var conn = (HttpConnection)state;
            var current = conn._reuses;

            if (conn._socket == null)
                return;

            lock (conn._sync) {
                if (conn._socket == null)
                    return;

                if (conn._timeoutCanceled[current])
                    return;

                conn._context.ErrorStatusCode = 408;
                conn._context.SendError();
            }
        }

        private bool processInput(byte[] data, int length) {
            // This method returns a bool:
            // - true  Done processing
            // - false Need more input

            try {
                while (true) {
                    int nread;
                    var line = readLineFrom(data, _position, length, out nread);

                    _position += nread;

                    if (line == null)
                        break;

                    if (line.Length == 0) {
                        if (_inputState == InputState.RequestLine)
                            continue;

                        if (_position > _maxInputLength)
                            _context.ErrorMessage = "Headers too long";

                        return true;
                    }

                    if (_inputState == InputState.RequestLine) {
                        _context.Request.SetRequestLine(line);
                        _inputState = InputState.Headers;
                    }
                    else {
                        _context.Request.AddHeader(line);
                    }

                    if (_context.HasErrorMessage)
                        return true;
                }
            }
            catch (Exception ex) {
                _context.ErrorMessage = ex.Message;

                return true;
            }

            if (_position >= _maxInputLength) {
                _context.ErrorMessage = "Headers too long";

                return true;
            }

            return false;
        }

        private string readLineFrom(
          byte[] buffer, int offset, int length, out int nread
        ) {
            nread = 0;

            for (var i = offset; i < length; i++) {
                nread++;

                var b = buffer[i];

                if (b == 13) {
                    _lineState = LineState.Cr;

                    continue;
                }

                if (b == 10) {
                    _lineState = LineState.Lf;

                    break;
                }

                _currentLine.Append((char)b);
            }

            if (_lineState != LineState.Lf)
                return null;

            var ret = _currentLine.ToString();

            _currentLine.Length = 0;
            _lineState = LineState.None;

            return ret;
        }

        private void registerContext(HttpListener listener) {
            if (_lastListener != listener) {
                removeConnection();

                if (!listener.AddConnection(this)) {
                    close();

                    return;
                }

                _lastListener = listener;
            }

            _context.Listener = listener;

            if (!_context.Authenticate())
                return;

            if (!_context.Register())
                return;

            _contextRegistered = true;
        }

        private void removeConnection() {
            if (_lastListener == null) {
                _listener.RemoveConnection(this);

                return;
            }

            _lastListener.RemoveConnection(this);
        }

        private void unregisterContext() {
            if (!_contextRegistered)
                return;

            _context.Unregister();

            _contextRegistered = false;
        }

        #endregion

        #region Internal Methods

        internal void BeginReadRequest() {
            _timeoutCanceled.Add(_reuses, false);
            _timer.Change(_timeout, Timeout.Infinite);

            try {
                _stream.BeginRead(_buffer, 0, _bufferLength, onRead, this);
            }
            catch (Exception) {
                // TODO: Logging.

                close();
            }
        }

        internal void Close(bool force) {
            if (_socket == null)
                return;

            lock (_sync) {
                if (_socket == null)
                    return;

                if (force) {
                    if (_outputStream != null)
                        _outputStream.Close(true);

                    close();

                    return;
                }

                GetResponseStream().Close(false);

                if (_context.Response.CloseConnection) {
                    close();

                    return;
                }

                if (!_context.Request.FlushInput()) {
                    close();

                    return;
                }

                disposeRequestBuffer();
                unregisterContext();

                _reuses++;

                init(15000);
                BeginReadRequest();
            }
        }

        #endregion

        #region Public Methods

        public void Close() {
            Close(false);
        }

        public RequestStream GetRequestStream(long contentLength, bool chunked) {
            lock (_sync) {
                if (_socket == null)
                    return null;

                if (_inputStream != null)
                    return _inputStream;

                var buff = _requestBuffer.GetBuffer();
                var len = (int)_requestBuffer.Length;
                var cnt = len - _position;

                disposeRequestBuffer();

                _inputStream = chunked
                               ? new ChunkedRequestStream(
                                   _stream, buff, _position, cnt, _context
                                 )
                               : new RequestStream(
                                   _stream, buff, _position, cnt, contentLength
                                 );

                return _inputStream;
            }
        }

        public ResponseStream GetResponseStream() {
            lock (_sync) {
                if (_socket == null)
                    return null;

                if (_outputStream != null)
                    return _outputStream;

                var lsnr = _context.Listener;
                var ignore = lsnr != null ? lsnr.IgnoreWriteExceptions : true;
                _outputStream = new ResponseStream(_stream, _context.Response, ignore);

                return _outputStream;
            }
        }

        #endregion
    }
}
