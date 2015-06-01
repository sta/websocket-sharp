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

    private MemoryStream           _buffer;
    private static readonly byte[] _crlf = new byte[] { 13, 10 };
    private bool                   _disposed;
    private bool                   _ignoreWriteExceptions;
    private HttpListenerResponse   _response;
    private long                   _start;
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

    private static void checkWriteParameters (byte[] buffer, int offset, int count)
    {
      if (buffer == null)
        throw new ArgumentNullException ("buffer");

      if (offset < 0)
        throw new ArgumentOutOfRangeException ("offset", "A negative value.");

      if (count < 0)
        throw new ArgumentOutOfRangeException ("count", "A negative value.");

      if (offset + count > buffer.Length)
        throw new ArgumentException (
          "The sum of 'offset' and 'count' is greater than 'buffer' length.");
    }

    private MemoryStream getBuffer (bool closing)
    {
      if (_buffer != null)
        return _buffer;

      _buffer = new MemoryStream ();
      _response.SendHeaders (_buffer, closing);
      _start = _buffer.Position;
      _buffer.Position = _buffer.Length;

      return _buffer;
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
        var buff = getBuffer (true);
        if (_response.SendChunked && !_trailerSent) {
          var size = getChunkSizeBytes (0, true);
          buff.Write (size, 0, size.Length);
          _trailerSent = true;
        }

        InternalWrite (buff.GetBuffer (), (int) _start, (int) (buff.Position - _start));
        _response.Close ();
      }
      else {
        _response.Abort ();
      }

      if (_buffer != null) {
        _buffer.Dispose ();
        _buffer = null;
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

      checkWriteParameters (buffer, offset, count);
      if (count == 0)
        return null;

      var buff = getBuffer (false);
      if (_response.SendChunked) {
        var size = getChunkSizeBytes (count, false);
        buff.Write (size, 0, size.Length);
      }

      return buff.BeginWrite (buffer, offset, count, callback, state);
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

      if (_buffer == null)
        throw new InvalidOperationException ("The BeginWrite method hasn't been done.");

      if (asyncResult == null)
        throw new ArgumentNullException ("asyncResult");

      var buff = getBuffer (false);
      buff.EndWrite (asyncResult);
      if (_response.SendChunked)
        buff.Write (_crlf, 0, 2);
    }

    public override void Flush ()
    {
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

      checkWriteParameters (buffer, offset, count);
      if (count == 0)
        return;

      var buff = getBuffer (false);
      var chunked = _response.SendChunked;
      if (chunked) {
        var size = getChunkSizeBytes (count, false);
        buff.Write (size, 0, size.Length);
      }

      buff.Write (buffer, offset, count);
      if (chunked)
        buff.Write (_crlf, 0, 2);
    }

    #endregion
  }
}
