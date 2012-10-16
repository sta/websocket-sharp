#region MIT License
/**
 * CloseEventArgs.cs
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
  public class CloseEventArgs : MessageEventArgs
  {
    private ushort _code;
    private string _reason;
    private bool   _wasClean;

    public ushort Code
    {
      get {
        return _code;
      }
    }

    public string Reason
    {
      get {
        return _reason;
      }
    }

    public bool WasClean
    {
      get {
        return _wasClean;
      }

      set {
        _wasClean = value;
      }
    }

    public CloseEventArgs(PayloadData data)
    : base(Opcode.CLOSE, data)
    {
      _code     = (ushort)CloseStatusCode.NO_STATUS_CODE;
      _reason   = String.Empty;
      _wasClean = false;

      if (data.Length >= 2)
        _code = data.ToBytes().SubArray(0, 2).To<ushort>(ByteOrder.BIG);

      if (data.Length > 2)
      {
        var buffer = data.ToBytes().SubArray(2, (int)(data.Length - 2));
        _reason = Encoding.UTF8.GetString(buffer);
      }
    }
  }
}
