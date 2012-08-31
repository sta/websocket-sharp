#region MIT License
/**
 * WsStream.cs
 *
 * The MIT License
 *
 * Copyright (c) 2010-2012 sta.blockhead
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Reflection;
using WebSocketSharp.Frame;

namespace WebSocketSharp
{
  public class WsStream<TStream> : IWsStream
    where TStream : Stream
  {
    private TStream _innerStream;
    private Object  _forRead;
    private Object  _forWrite;

    public WsStream(TStream innerStream)
    {
      Type streamType = typeof(TStream);
      if (streamType != typeof(NetworkStream) &&
          streamType != typeof(SslStream))
      {
        throw new NotSupportedException("Not supported Stream type: " + streamType.ToString());
      }

      if (innerStream == null)
      {
        throw new ArgumentNullException("innerStream");
      }

      _innerStream = innerStream;
      _forRead     = new object();
      _forWrite    = new object();
    }

    public void Close()
    {
      _innerStream.Close();
    }

    public void Dispose()
    {
      _innerStream.Dispose();
    }

    public int Read(byte[] buffer, int offset, int size)
    {
      lock (_forRead)
      {
        var readLen = _innerStream.Read(buffer, offset, size);
        if (readLen < size)
        {
          var msg = String.Format("Data can not be read from {0}.", typeof(TStream).Name);
          throw new IOException(msg);
        }
        return readLen;
      }
    }

    public int ReadByte()
    {
      lock (_forRead)
      {
        return _innerStream.ReadByte();
      }
    }

    public WsFrame ReadFrame()
    {
      lock (_forRead)
      {
        return WsFrame.Parse(_innerStream);
      }
    }

    public void Write(byte[] buffer, int offset, int count)
    {
      lock (_forWrite)
      {
        _innerStream.Write(buffer, offset, count);
      }
    }

    public void WriteByte(byte value)
    {
      lock (_forWrite)
      {
        _innerStream.WriteByte(value);
      }
    }

    public void WriteFrame(WsFrame frame)
    {
      lock (_forWrite)
      {
        var buffer = frame.ToBytes();
        _innerStream.Write(buffer, 0, buffer.Length);
      }
    }
  }
}
