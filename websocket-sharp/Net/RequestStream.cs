#region License
/*
 * RequestStream.cs
 *
 * This code is derived from RequestStream.cs (System.Net) of Mono
 * (http://www.mono-project.com).
 *
 * The MIT License
 *
 * Copyright (c) 2005 Novell, Inc. (http://www.novell.com)
 * Copyright (c) 2012-2021 sta.blockhead
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

namespace WebSocketSharp.Net
{
  internal class RequestStream : Stream
  {
    #region Private Fields

    private long   _bodyLeft;
    private byte[] _buffer;
    private int    _count;
    private bool   _disposed;
    private int    _offset;
    private Stream _stream;

    #endregion

    #region Internal Constructors

    internal RequestStream (
      Stream stream, byte[] buffer, int offset, int count, long contentLength
    )
    {
      _stream = stream;
      _buffer = buffer;
      _offset = offset;
      _count = count;
      _bodyLeft = contentLength;
    }

    #endregion

    #region Public Properties

    public override bool CanRead {
      get {
        return true;
      }
    }

    public override bool CanSeek {
      get {
        return false;
      }
    }

    public override bool CanWrite {
      get {
        return false;
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

    private int fillFromBuffer (byte[] buffer, int offset, int count)
    {
      // This method returns a int:
      // - > 0 The number of bytes read from the internal buffer
      // - 0   No more bytes read from the internal buffer
      // - -1  No more content data

      if (_bodyLeft == 0)
        return -1;

      if (_count == 0)
        return 0;

      if (count > _count)
        count = _count;

      if (_bodyLeft > 0 && _bodyLeft < count)
        count = (int) _bodyLeft;

      Buffer.BlockCopy (_buffer, _offset, buffer, offset, count);

      _offset += count;
      _count -= count;

      if (_bodyLeft > 0)
        _bodyLeft -= count;

      return count;
    }

    #endregion

    #region Public Methods

    public override IAsyncResult BeginRead (
      byte[] buffer, int offset, int count, AsyncCallback callback, object state
    )
    {
      if (_disposed) {
        var name = GetType ().ToString ();

        throw new ObjectDisposedException (name);
      }

      if (buffer == null)
        throw new ArgumentNullException ("buffer");

      if (offset < 0) {
        var msg = "A negative value.";

        throw new ArgumentOutOfRangeException ("offset", msg);
      }

      if (count < 0) {
        var msg = "A negative value.";

        throw new ArgumentOutOfRangeException ("count", msg);
      }

      var len = buffer.Length;

      if (offset + count > len) {
        var msg = "The sum of 'offset' and 'count' is greater than the length of 'buffer'.";

        throw new ArgumentException (msg);
      }

      if (count == 0)
        return _stream.BeginRead (buffer, offset, 0, callback, state);

      var nread = fillFromBuffer (buffer, offset, count);

      if (nread != 0) {
        var ares = new HttpStreamAsyncResult (callback, state);
        ares.Buffer = buffer;
        ares.Offset = offset;
        ares.Count = count;
        ares.SyncRead = nread > 0 ? nread : 0;
        ares.Complete ();

        return ares;
      }

      if (_bodyLeft >= 0 && _bodyLeft < count)
        count = (int) _bodyLeft;

      return _stream.BeginRead (buffer, offset, count, callback, state);
    }

    public override IAsyncResult BeginWrite (
      byte[] buffer, int offset, int count, AsyncCallback callback, object state
    )
    {
      throw new NotSupportedException ();
    }

    public override void Close ()
    {
      _disposed = true;
    }

    public override int EndRead (IAsyncResult asyncResult)
    {
      if (_disposed) {
        var name = GetType ().ToString ();

        throw new ObjectDisposedException (name);
      }

      if (asyncResult == null)
        throw new ArgumentNullException ("asyncResult");

      if (asyncResult is HttpStreamAsyncResult) {
        var ares = (HttpStreamAsyncResult) asyncResult;

        if (!ares.IsCompleted)
          ares.AsyncWaitHandle.WaitOne ();

        return ares.SyncRead;
      }

      var nread = _stream.EndRead (asyncResult);

      if (nread > 0 && _bodyLeft > 0)
        _bodyLeft -= nread;

      return nread;
    }

    public override void EndWrite (IAsyncResult asyncResult)
    {
      throw new NotSupportedException ();
    }

    public override void Flush ()
    {
    }

    public override int Read (byte[] buffer, int offset, int count)
    {
      if (_disposed) {
        var name = GetType ().ToString ();

        throw new ObjectDisposedException (name);
      }

      if (buffer == null)
        throw new ArgumentNullException ("buffer");

      if (offset < 0) {
        var msg = "A negative value.";

        throw new ArgumentOutOfRangeException ("offset", msg);
      }

      if (count < 0) {
        var msg = "A negative value.";

        throw new ArgumentOutOfRangeException ("count", msg);
      }

      var len = buffer.Length;

      if (offset + count > len) {
        var msg = "The sum of 'offset' and 'count' is greater than the length of 'buffer'.";

        throw new ArgumentException (msg);
      }

      if (count == 0)
        return 0;

      var nread = fillFromBuffer (buffer, offset, count);

      if (nread == -1)
        return 0;

      if (nread > 0)
        return nread;

      nread = _stream.Read (buffer, offset, count);

      if (nread > 0 && _bodyLeft > 0)
        _bodyLeft -= nread;

      return nread;
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
      throw new NotSupportedException ();
    }

    #endregion
  }
}
