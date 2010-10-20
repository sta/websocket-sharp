#region MIT License
/**
 * WsStream.cs
 *
 * The MIT License
 *
 * Copyright (c) 2010 sta.blockhead
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

namespace WebSocketSharp
{
  public class WsStream<T> : IWsStream
    where T : Stream
  {
    private T innerStream;

    public WsStream(T innerStream)
    {
      Type streamType = typeof(T);
      if (streamType != typeof(NetworkStream) &&
          streamType != typeof(SslStream))
      {
        throw new NotSupportedException("Unsupported Stream type: " + streamType.ToString());
      }

      if (innerStream == null)
      {
        throw new ArgumentNullException("innerStream");
      }

      this.innerStream = innerStream;
    }

    public void Close()
    {
      innerStream.Close();
    }

    public void Dispose()
    {
      innerStream.Dispose();
    }

    public int Read(byte[] buffer, int offset, int size)
    {
      return innerStream.Read(buffer, offset, size);
    }

    public int ReadByte()
    {
      return innerStream.ReadByte();
    }

    public void Write(byte[] buffer, int offset, int count)
    {
      innerStream.Write(buffer, offset, count);
    }

    public void WriteByte(byte value)
    {
      innerStream.WriteByte(value);
    }
  }
}
