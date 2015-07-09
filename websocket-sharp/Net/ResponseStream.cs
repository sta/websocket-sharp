#region License
/*
 * ResponseStream.cs
 *
 * This code is derived from ResponseStream.cs (System.Net) of Mono
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

using System;
using System.IO;
using System.Text;

namespace WebSocketSharp.Net
{
  internal class ResponseStream : Stream
  {
    #region Private Fields

    private MemoryStream             _body;
    private static readonly byte[]   _crlf = new byte[] { 13, 10 };
    private bool                     _disposed;
    private HttpListenerResponse     _response;
    private bool                     _sendChunked;
    private Stream                   _stream;
    private Action<byte[], int, int> _write;
    private Action<byte[], int, int> _writeBody;
    private Action<byte[], int, int> _writeChunked;

    #endregion

    #region Internal Constructors

    internal ResponseStream (
      Stream stream, HttpListenerResponse response, bool ignoreWriteExceptions)
    {
      _stream = stream;
      _response = response;

      if (ignoreWriteExceptions) {
        _write = writeWithoutThrowingException;
        _writeChunked = writeChunkedWithoutThrowingException;
      }
      else {
        _write = stream.Write;
        _writeChunked = writeChunked;
      }

      _body = new MemoryStream ();
    }

    #endregion

    #region Public Properties

    public override bool CanRead {
      get {
        return false;
      }
    }

    public override bool CanSeek {
      get {
        return false;
      }
    }

    public override bool CanWrite {
      get {
        return !_disposed;
      }
    }

    public override long Length {
      get {
        throw new NotSupportedException ();
      }
    }

    public override long Position {
      get {
        throw new NotSupportedException ();
      }

      set {
        throw new NotSupportedException ();
      }
    }

    #endregion

    #region Private Methods

    private bool flush (bool closing)
    {
      if (!_response.HeadersSent) {
        if (!flushHeaders (closing)) {
          if (closing)
            _response.CloseConnection = true;

          return false;
        }

        _sendChunked = _response.SendChunked;
        _writeBody = _sendChunked ? _writeChunked : _write;
      }

      flushBody (closing);
      if (closing && _sendChunked) {
        var last = getChunkSizeBytes (0, true);
        _write (last, 0, last.Length);
      }

      return true;
    }

    private void flushBody (bool closing)
    {
      using (_body) {
        var len = _body.Length;
        if (len > Int32.MaxValue) {
          _body.Position = 0;
          var buffLen = 1024;
          var buff = new byte[buffLen];
          var nread = 0;
          while ((nread = _body.Read (buff, 0, buffLen)) > 0)
            _writeBody (buff, 0, nread);
        }
        else if (len > 0) {
          _writeBody (_body.GetBuffer (), 0, (int) len);
        }
      }

      _body = !closing ? new MemoryStream () : null;
    }

    private bool flushHeaders (bool closing)
    {
      using (var buff = new MemoryStream ()) {
        var headers = _response.WriteHeadersTo (buff);
        var start = buff.Position;
        var len = buff.Length - start;
        if (len > 32768)
          return false;

        if (!_response.SendChunked && _response.ContentLength64 != _body.Length)
          return false;

        _write (buff.GetBuffer (), (int) start, (int) len);
        _response.CloseConnection = headers["Connection"] == "close";
        _response.HeadersSent = true;
      }

      return true;
    }

    private static byte[] getChunkSizeBytes (int size, bool final)
    {
      return Encoding.ASCII.GetBytes (String.Format ("{0:x}\r\n{1}", size, final ? "\r\n" : ""));
    }

    private void writeChunked (byte[] buffer, int offset, int count)
    {
      var size = getChunkSizeBytes (count, false);
      _stream.Write (size, 0, size.Length);
      _stream.Write (buffer, offset, count);
      _stream.Write (_crlf, 0, 2);
    }

    private void writeChunkedWithoutThrowingException (byte[] buffer, int offset, int count)
    {
      try {
        writeChunked (buffer, offset, count);
      }
      catch {
      }
    }

    private void writeWithoutThrowingException (byte[] buffer, int offset, int count)
    {
      try {
        _stream.Write (buffer, offset, count);
      }
      catch {
      }
    }

    #endregion

    #region Internal Methods

    internal void Close (bool force)
    {
      if (_disposed)
        return;

      _disposed = true;
      if (!force && flush (true)) {
        _response.Close ();
      }
      else {
        if (_sendChunked) {
          var last = getChunkSizeBytes (0, true);
          _write (last, 0, last.Length);
        }

        _body.Dispose ();
        _body = null;

        _response.Abort ();
      }

      _response = null;
      _stream = null;
    }

    internal void InternalWrite (byte[] buffer, int offset, int count)
    {
      _write (buffer, offset, count);
    }

    #endregion

    #region Public Methods

    public override IAsyncResult BeginRead (
      byte[] buffer, int offset, int count, AsyncCallback callback, object state)
    {
      throw new NotSupportedException ();
    }

    public override IAsyncResult BeginWrite (
      byte[] buffer, int offset, int count, AsyncCallback callback, object state)
    {
      if (_disposed)
        throw new ObjectDisposedException (GetType ().ToString ());

      return _body.BeginWrite (buffer, offset, count, callback, state);
    }

    public override void Close ()
    {
      Close (false);
    }

    protected override void Dispose (bool disposing)
    {
      Close (!disposing);
    }

    public override int EndRead (IAsyncResult asyncResult)
    {
      throw new NotSupportedException ();
    }

    public override void EndWrite (IAsyncResult asyncResult)
    {
      if (_disposed)
        throw new ObjectDisposedException (GetType ().ToString ());

      _body.EndWrite (asyncResult);
    }

    public override void Flush ()
    {
      if (!_disposed && (_sendChunked || _response.SendChunked))
        flush (false);
    }

    public override int Read (byte[] buffer, int offset, int count)
    {
      throw new NotSupportedException ();
    }

    public override long Seek (long offset, SeekOrigin origin)
    {
      throw new NotSupportedException ();
    }

    public override void SetLength (long value)
    {
      throw new NotSupportedException ();
    }

    public override void Write (byte[] buffer, int offset, int count)
    {
      if (_disposed)
        throw new ObjectDisposedException (GetType ().ToString ());

      _body.Write (buffer, offset, count);
    }

    #endregion
  }
}
