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
  // FIXME: Does this buffer the response until Close?
  // Update: we send a single packet for the first non-chunked Write
  // What happens when we set content-length to X and write X-1 bytes then close?
  // what if we don't set content-length at all?
  internal class ResponseStream : Stream
  {
    #region Private Static Fields

    private static byte [] _crlf = new byte [] { 13, 10 };

    #endregion

    #region Private Fields

    private bool                 _disposed;
    private bool                 _ignoreErrors;
    private HttpListenerResponse _response;
    private Stream               _stream;
    private bool                 _trailerSent;

    #endregion

    #region Internal Constructors

    internal ResponseStream (Stream stream, HttpListenerResponse response, bool ignoreErrors)
    {
      _stream = stream;
      _response = response;
      _ignoreErrors = ignoreErrors;
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
        return true;
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

    private static byte [] getChunkSizeBytes (int size, bool final)
    {
      return Encoding.ASCII.GetBytes (String.Format ("{0:x}\r\n{1}", size, final ? "\r\n" : ""));
    }

    private MemoryStream getHeaders (bool closing)
    {
      if (_response.HeadersSent)
        return null;

      var stream = new MemoryStream ();
      _response.SendHeaders (stream, closing);

      return stream;
    }

    #endregion

    #region Internal Methods

    internal void WriteInternally (byte [] buffer, int offset, int count)
    {
      if (_ignoreErrors) {
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
      byte [] buffer, int offset, int count, AsyncCallback callback, object state)
    {
      throw new NotSupportedException ();
    }

    public override IAsyncResult BeginWrite (
      byte [] buffer, int offset, int count, AsyncCallback callback, object state)
    {
      if (_disposed)
        throw new ObjectDisposedException (GetType ().ToString ());

      var headers = getHeaders (false);
      var chunked = _response.SendChunked;
      byte [] bytes = null;
      if (headers != null) {
        using (headers) {
          var start = headers.Position;
          headers.Position = headers.Length;
          if (chunked) {
            bytes = getChunkSizeBytes (count, false);
            headers.Write (bytes, 0, bytes.Length);
          }

          headers.Write (buffer, offset, count);
          buffer = headers.GetBuffer ();
          offset = (int) start;
          count = (int) (headers.Position - start);
        }
      }
      else if (chunked) {
        bytes = getChunkSizeBytes (count, false);
        WriteInternally (bytes, 0, bytes.Length);
      }

      return _stream.BeginWrite (buffer, offset, count, callback, state);
    }

    public override void Close ()
    {
      if (_disposed)
        return;

      _disposed = true;

      var headers = getHeaders (true);
      var chunked = _response.SendChunked;
      byte [] bytes = null;
      if (headers != null) {
        using (headers) {
          var start = headers.Position;
          if (chunked && !_trailerSent) {
            bytes = getChunkSizeBytes (0, true);
            headers.Position = headers.Length;
            headers.Write (bytes, 0, bytes.Length);
          }

          WriteInternally (headers.GetBuffer (), (int) start, (int) (headers.Length - start));
        }

        _trailerSent = true;
      }
      else if (chunked && !_trailerSent) {
        bytes = getChunkSizeBytes (0, true);
        WriteInternally (bytes, 0, bytes.Length);
        _trailerSent = true;
      }

      _response.Close ();
    }

    public override int EndRead (IAsyncResult asyncResult)
    {
      throw new NotSupportedException ();
    }

    public override void EndWrite (IAsyncResult asyncResult)
    {
      if (_disposed)
        throw new ObjectDisposedException (GetType ().ToString ());

      Action<IAsyncResult> endWrite = ares => {
        _stream.EndWrite (ares);
        if (_response.SendChunked)
          _stream.Write (_crlf, 0, 2);
      };

      if (_ignoreErrors) {
        try {
          endWrite (asyncResult);
        }
        catch {
        }
      }
      else {
        endWrite (asyncResult);
      }
    }

    public override void Flush ()
    {
    }

    public override int Read (byte [] buffer, int offset, int count)
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

    public override void Write (byte [] buffer, int offset, int count)
    {
      if (_disposed)
        throw new ObjectDisposedException (GetType ().ToString ());

      var headers = getHeaders (false);
      var chunked = _response.SendChunked;
      byte [] bytes = null;
      if (headers != null) {
        // After the possible preamble for the encoding.
        using (headers) {
          var start = headers.Position;
          headers.Position = headers.Length;
          if (chunked) {
            bytes = getChunkSizeBytes (count, false);
            headers.Write (bytes, 0, bytes.Length);
          }

          var newCount = Math.Min (count, 16384 - (int) headers.Position + (int) start);
          headers.Write (buffer, offset, newCount);
          count -= newCount;
          offset += newCount;
          WriteInternally (headers.GetBuffer (), (int) start, (int) (headers.Length - start));
        }
      }
      else if (chunked) {
        bytes = getChunkSizeBytes (count, false);
        WriteInternally (bytes, 0, bytes.Length);
      }

      if (count > 0)
        WriteInternally (buffer, offset, count);

      if (chunked)
        WriteInternally (_crlf, 0, 2);
    }

    #endregion
  }
}
