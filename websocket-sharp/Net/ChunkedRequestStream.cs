#region License
/*
 * ChunkedRequestStream.cs
 *
 * This code is derived from System.Net.ChunkedInputStream.cs of Mono
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
  internal class ChunkedRequestStream : RequestStream
  {
    #region Private Const Fields

    private const int _bufferSize = 8192;

    #endregion

    #region Private Fields

    private HttpListenerContext _context;
    private ChunkStream         _decoder;
    private bool                _disposed;
    private bool                _noMoreData;

    #endregion

    #region Public Constructors

    public ChunkedRequestStream (
      HttpListenerContext context, Stream stream, byte [] buffer, int offset, int length)
      : base (stream, buffer, offset, length)
    {
      _context = context;
      _decoder = new ChunkStream ((WebHeaderCollection) context.Request.Headers);
    }

    #endregion

    #region Public Properties

    public ChunkStream Decoder {
      get {
        return _decoder;
      }

      set {
        _decoder = value;
      }
    }

    #endregion

    #region Private Methods

    private void onRead (IAsyncResult asyncResult)
    {
      var readState = (ReadBufferState) asyncResult.AsyncState;
      var ares = readState.AsyncResult;
      try {
        var nread = base.EndRead (asyncResult);
        _decoder.Write (ares.Buffer, ares.Offset, nread);
        nread = _decoder.Read (readState.Buffer, readState.Offset, readState.Count);
        readState.Offset += nread;
        readState.Count -= nread;
        if (readState.Count == 0 || !_decoder.WantMore || nread == 0) {
          _noMoreData = !_decoder.WantMore && nread == 0;
          ares.Count = readState.InitialCount - readState.Count;
          ares.Complete ();

          return;
        }

        ares.Offset = 0;
        ares.Count = Math.Min (_bufferSize, _decoder.ChunkLeft + 6);
        base.BeginRead (ares.Buffer, ares.Offset, ares.Count, onRead, readState);
      }
      catch (Exception ex) {
        _context.Connection.SendError (ex.Message, 400);
        ares.Complete (ex);
      }
    }

    #endregion

    #region Public Methods

    public override IAsyncResult BeginRead (
      byte [] buffer, int offset, int count, AsyncCallback callback, object state)
    {
      if (_disposed)
        throw new ObjectDisposedException (GetType ().ToString ());

      if (buffer == null)
        throw new ArgumentNullException ("buffer");

      var len = buffer.Length;
      if (offset < 0 || offset > len)
        throw new ArgumentOutOfRangeException ("'offset' exceeds the size of buffer.");

      if (count < 0 || offset > len - count)
        throw new ArgumentOutOfRangeException ("'offset' + 'count' exceeds the size of buffer.");

      var ares = new HttpStreamAsyncResult (callback, state);
      if (_noMoreData) {
        ares.Complete ();
        return ares;
      }

      var nread = _decoder.Read (buffer, offset, count);
      offset += nread;
      count -= nread;
      if (count == 0) {
        // Got all we wanted, no need to bother the decoder yet.
        ares.Count = nread;
        ares.Complete ();

        return ares;
      }

      if (!_decoder.WantMore) {
        _noMoreData = nread == 0;
        ares.Count = nread;
        ares.Complete ();

        return ares;
      }

      ares.Buffer = new byte [_bufferSize];
      ares.Offset = 0;
      ares.Count = _bufferSize;

      var readState = new ReadBufferState (buffer, offset, count, ares);
      readState.InitialCount += nread;
      base.BeginRead (ares.Buffer, ares.Offset, ares.Count, onRead, readState);

      return ares;
    }

    public override void Close ()
    {
      if (_disposed)
        return;

      _disposed = true;
      base.Close ();
    }

    public override int EndRead (IAsyncResult asyncResult)
    {
      if (_disposed)
        throw new ObjectDisposedException (GetType ().ToString ());

      if (asyncResult == null)
        throw new ArgumentNullException ("asyncResult");

      var ares = asyncResult as HttpStreamAsyncResult;
      if (ares == null)
        throw new ArgumentException ("Wrong IAsyncResult.", "asyncResult");

      if (!ares.IsCompleted)
        ares.AsyncWaitHandle.WaitOne ();

      if (ares.Error != null)
        throw new HttpListenerException (400, "I/O operation aborted.");

      return ares.Count;
    }

    public override int Read (byte [] buffer, int offset, int count)
    {
      var ares = BeginRead (buffer, offset, count, null, null);
      return EndRead (ares);
    }

    #endregion
  }
}
