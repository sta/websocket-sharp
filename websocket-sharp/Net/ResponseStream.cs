#region License
/*
 * ResponseStream.cs
 *
 * This code is derived from System.Net.ResponseStream.cs of Mono
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
using System.Text;

namespace WebSocketSharp.Net
{
  // FIXME: Does this buffer the response until close?
  // Update: We send a single packet for the first non-chunked write.
  // What happens when we set Content-Length to X and write X-1 bytes then close?
  // What happens if we don't set Content-Length at all?
  internal class ResponseStream : Stream
  {
    #region Private Fields

    private MemoryStream           _body;
    private static readonly byte[] _crlf = new byte[] { 13, 10 };
    private bool                   _disposed;
    private bool                   _ignoreWriteExceptions;
    private HttpListenerResponse   _response;
    private Stream                 _stream;
    private bool                   _trailerSent;

    #endregion

    #region Internal Constructors

    internal ResponseStream (
      Stream stream, HttpListenerResponse response, bool ignoreWriteExceptions)
    {
      _stream = stream;
      _response = response;
      _ignoreWriteExceptions = ignoreWriteExceptions;

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

    private void flush (bool closing)
    {
      if (!_response.HeadersSent) {
        using (var headers = new MemoryStream ()) {
          _response.SendHeaders (headers, closing);
          var start = headers.Position;
          InternalWrite (headers.GetBuffer (), (int) start, (int) (headers.Length - start));
        }
      }

      var len = _body.Length;
      if (len == 0)
        return;

      var chunked = _response.SendChunked;
      using (_body) {
        if (len > Int32.MaxValue) {
          _body.Position = 0;

          var buffLen = 1024;
          var buff = new byte[buffLen];
          var nread = 0;
          while ((nread = _body.Read (buff, 0, buffLen)) > 0) {
            if (chunked) {
              var size = getChunkSizeBytes (nread, false);
              InternalWrite (size, 0, size.Length);
              InternalWrite (buff, 0, nread);
              InternalWrite (_crlf, 0, 2);

              continue;
            }

            InternalWrite (buff, 0, nread);
          }
        }
        else {
          if (chunked) {
            var size = getChunkSizeBytes ((int) len, false);
            InternalWrite (size, 0, size.Length);
            InternalWrite (_body.GetBuffer (), 0, (int) len);
            InternalWrite (_crlf, 0, 2);
          }
          else {
            InternalWrite (_body.GetBuffer (), 0, (int) len);
          }
        }
      }

      if (closing && chunked) {
        var size = getChunkSizeBytes (0, true);
        InternalWrite (size, 0, size.Length);
      }

      _body = null;
      if (!closing)
        _body = new MemoryStream ();
    }

    private static byte[] getChunkSizeBytes (int size, bool final)
    {
      return Encoding.ASCII.GetBytes (String.Format ("{0:x}\r\n{1}", size, final ? "\r\n" : ""));
    }

    #endregion

    #region Internal Methods

    internal void Close (bool force)
    {
      if (_disposed)
        return;

      _disposed = true;
      if (!force) {
        flush (true);
        _response.Close ();
      }
      else {
        _response.Abort ();
      }

      if (_body != null) {
        _body.Dispose ();
        _body = null;
      }

      _response = null;
      _stream = null;
    }

    internal void InternalWrite (byte[] buffer, int offset, int count)
    {
      if (_ignoreWriteExceptions) {
        try {
          _stream.Write (buffer, offset, count);
        }
        catch {
        }
      }
      else {
        _stream.Write (buffer, offset, count);
      }
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
      if (_response.SendChunked)
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
