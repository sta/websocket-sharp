#region MIT License
/**
 * MessageEventArgs.cs
 *
 * The MIT License
 *
 * Copyright (c) 2012 sta.blockhead
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
using System.Text;
using WebSocketSharp.Frame;

namespace WebSocketSharp
{
  public class MessageEventArgs : EventArgs
  {
    private Opcode      _type;
    private PayloadData _data;

    public Opcode Type
    {
      get
      {
        return _type;
      }
    }

    public string Data
    {
      get
      {
        if (((Opcode.TEXT | Opcode.PING | Opcode.PONG) & _type) == _type)
        {
          if (_data.Length > 0)
          {
            return Encoding.UTF8.GetString(_data.ToBytes());
          }
          else
          {
            return String.Empty;
          }
        }

        return _type.ToString();
      }
    }

    public byte[] RawData
    {
      get
      {
        return _data.ToBytes();
      }
    }

    public MessageEventArgs(string data)
    : this(Opcode.TEXT, new PayloadData(data))
    {
    }

    public MessageEventArgs(Opcode type, PayloadData data)
    {
      _type = type;
      _data = data;
    }
  }
}
