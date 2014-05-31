#region License
/*
 * RequestStream.cs
 *
 * This code is derived from System.Net.RequestStream.cs of Mono
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

namespace WebSocketSharp.Net
{
  internal class RequestStream : Stream
  {
    #region Private Fields

    private byte [] _buffer;
    private bool    _disposed;
    private int     _length;
    private int     _offset;
    private long    _remainingBody;
    private Stream  _stream;

    #endregion

    #region Internal Constructors

    internal RequestStream (Stream stream, byte [] buffer, int offset, int length)
      : this (stream, buffer, offset, length, -1)
    {
    }

    internal RequestStream (
      Stream stream, byte [] buffer, int offset, int length, long contentlength)
    {
      _stream = stream;
      _buffer = buffer;
      _offset = offset;
      _length = length;
      _remainingBody = contentlength;
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

    // Returns 0 if we can keep reading from the base stream,
    // > 0 if we read something from the buffer.
    // -1 if we had a content length set and we finished reading that many bytes.
    private int fillFromBuffer (byte [] buffer, int offset, int count)
    {
      if (buffer == null)
        throw new ArgumentNullException ("buffer");

      if (offset < 0)
        throw new ArgumentOutOfRangeException ("offset", "Less than zero.");

      if (count < 0)
        throw new ArgumentOutOfRangeException ("count", "Less than zero.");

      var len = buffer.Length;
      if (offset > len)
        throw new ArgumentException ("'offset' is greater than 'buffer' size.");

      if (offset > len - count)
        throw new ArgumentException ("Reading would overrun 'buffer'.");

      if (_remainingBody == 0)
        return -1;

      if (_length == 0)
        return 0;

      var size = _length < count ? _length : count;
      if (_remainingBody > 0 && _remainingBody < size)
        size = (int) _remainingBody;

      var remainingBuffer = _buffer.Length - _offset;
      if (remainingBuffer < size)
        size = remainingBuffer;

      if (size == 0)
        return 0;

      Buffer.BlockCopy (_buffer, _offset, buffer, offset, size);
      _offset += size;
      _length -= size;
      if (_remainingBody > 0)
        _remainingBody -= size;

      return size;
    }

    #endregion

    #region Public Methods

    public override IAsyncResult BeginRead (
      byte [] buffer, int offset, int count, AsyncCallback callback, object state)
    {
      if (_disposed)
        throw new ObjectDisposedException (GetType ().ToString ());

      var nread = fillFromBuffer (buffer, offset, count);
      if (nread > 0 || nread == -1) {
        var ares = new HttpStreamAsyncResult (callback, state);
        ares.Buffer = buffer;
        ares.Offset = offset;
        ares.Count = count;
        ares.SyncRead = nread;
        ares.Complete ();

        return ares;
      }

      // Avoid reading past the end of the request to allow for HTTP pipelining.
      if (_remainingBody >= 0 && _remainingBody < count)
        count = (int) _remainingBody;

      return _stream.BeginRead (buffer, offset, count, callback, state);
    }

    public override IAsyncResult BeginWrite (
      byte [] buffer, int offset, int count, AsyncCallback callback, object state)
    {
      throw new NotSupportedException ();
    }

    public override void Close ()
    {
      _disposed = true;
    }

    public override int EndRead (IAsyncResult asyncResult)
    {
      if (_disposed)
        throw new ObjectDisposedException (GetType ().ToString ());

      if (asyncResult == null)
        throw new ArgumentNullException ("asyncResult");

      if (asyncResult is HttpStreamAsyncResult) {
        var ares = (HttpStreamAsyncResult) asyncResult;
        if (!ares.IsCompleted)
          ares.AsyncWaitHandle.WaitOne ();

        return ares.SyncRead;
      }

      // Close on exception?
      var nread = _stream.EndRead (asyncResult);
      if (nread > 0 && _remainingBody > 0)
        _remainingBody -= nread;

      return nread;
    }

    public override void EndWrite (IAsyncResult asyncResult)
    {
      throw new NotSupportedException ();
    }

    public override void Flush ()
    {
    }

    public override int Read (byte [] buffer, int offset, int count)
    {
      if (_disposed)
        throw new ObjectDisposedException (GetType ().ToString ());

      // Call fillFromBuffer to check for buffer boundaries even when
      // _remainingBody is 0.
      var nread = fillFromBuffer (buffer, offset, count);
      if (nread == -1) // No more bytes available (Content-Length).
        return 0;

      if (nread > 0)
        return nread;

      nread = _stream.Read (buffer, offset, count);
      if (nread > 0 && _remainingBody > 0)
        _remainingBody -= nread;

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

    public override void Write (byte [] buffer, int offset, int count)
    {
      throw new NotSupportedException ();
    }

    #endregion
  }
}
