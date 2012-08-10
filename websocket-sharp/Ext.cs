#region MIT License
/**
 * Ext.cs
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
using System.Linq;
using System.Text;

namespace WebSocketSharp
{
  public static class Ext
  {
    public static void Emit(
      this EventHandler eventHandler, object sender, EventArgs e)
    {
      if (eventHandler != null)
      {
        eventHandler(sender, e);
      }
    }

    public static void Emit<TEventArgs>(
      this EventHandler<TEventArgs> eventHandler, object sender, TEventArgs e)
      where TEventArgs : EventArgs
    {
      if (eventHandler != null)
      {
        eventHandler(sender, e);
      }
    }

    public static bool EqualsAndSaveTo(this int value, char c, List<byte> dest)
    {
      byte b = (byte)value;
      dest.Add(b);
      return b == Convert.ToByte(c);
    }

    public static string GetHeaderValue(this string src, string separater)
    {
      int i = src.IndexOf(separater);
      return src.Substring(i + 1).Trim();
    }

    public static bool IsHostOrder(this ByteOrder order)
    {
      if (BitConverter.IsLittleEndian ^ (order == ByteOrder.LITTLE))
      {// true ^ false or false ^ true
        return false;
      }
      else
      {// true ^ true or false ^ false
        return true;
      }
    }

    public static bool IsNullDo<T>(this T value, Action act)
      where T : class
    {
      if (value == null)
      {
        act();
        return true;
      }

      return false;
    }

    public static bool NotEqualsDo(
      this string expected,
      string actual,
      Func<string, string, string> func,
      out string ret,
      bool ignoreCase)
    {
      if (String.Compare(expected, actual, ignoreCase) != 0)
      {
        ret = func(expected, actual);
        return true;
      }

      ret = String.Empty;
      return false;
    }

    public static byte[] ReadBytes<TStream>(this TStream stream, ulong length, int bufferLength)
      where TStream : System.IO.Stream
    {
      ulong count     = length / (ulong)bufferLength;
      int   remainder = (int)(length % (ulong)bufferLength);

      List<byte> readData = new List<byte>();
      byte[]     buffer1  = new byte[bufferLength];
      int        readLen  = 0;

      count.Times(() =>
      {
        readLen = stream.Read(buffer1, 0, bufferLength);
        if (readLen > 0) readData.AddRange(buffer1.SubArray(0, readLen));
      });

      if (remainder > 0)
      {
        byte[] buffer2 = new byte[remainder];
        readLen = stream.Read(buffer2, 0, remainder);
        if (readLen > 0) readData.AddRange(buffer2.SubArray(0, readLen));
      }

      return readData.ToArray();
    }

    public static T[] SubArray<T>(this T[] array, int startIndex, int length)
    {
      if (startIndex == 0 && array.Length == length)
      {
        return array;
      }

      T[] subArray = new T[length];
      Array.Copy(array, startIndex, subArray, 0, length); 
      return subArray;
    }

    public static void Times<T>(this T n, Action act)
      where T : struct
    {
      if (typeof(T) != typeof(byte)   &&
          typeof(T) != typeof(Int16)  &&
          typeof(T) != typeof(Int32)  &&
          typeof(T) != typeof(Int64)  &&
          typeof(T) != typeof(UInt16) &&
          typeof(T) != typeof(UInt32) &&
          typeof(T) != typeof(UInt64))
      {
        throw new NotSupportedException("Not supported Struct type: " + typeof(T).ToString());
      }

      ulong m = (ulong)(object)n;

      for (ulong i = 0; i < m; i++)
      {
        act();
      }
    }

    public static void Times<T>(this T n, Action<ulong> act)
      where T : struct
    {
      if (typeof(T) != typeof(byte)   &&
          typeof(T) != typeof(Int16)  &&
          typeof(T) != typeof(Int32)  &&
          typeof(T) != typeof(Int64)  &&
          typeof(T) != typeof(UInt16) &&
          typeof(T) != typeof(UInt32) &&
          typeof(T) != typeof(UInt64))
      {
        throw new NotSupportedException("Not supported Struct type: " + typeof(T).ToString());
      }

      ulong m = (ulong)(object)n;

      for (ulong i = 0; i < m; i++)
      {
        act(i);
      }
    }

    public static T To<T>(this byte[] src, ByteOrder srcOrder)
      where T : struct
    {
      T      dest;
      byte[] buffer = src.ToHostOrder(srcOrder);

      if (typeof(T) == typeof(Boolean))
      {
        dest = (T)(object)BitConverter.ToBoolean(buffer, 0);
      }
      else if (typeof(T) == typeof(Char))
      {
        dest = (T)(object)BitConverter.ToChar(buffer, 0);
      }
      else if (typeof(T) == typeof(Double))
      {
        dest = (T)(object)BitConverter.ToDouble(buffer, 0);
      }
      else if (typeof(T) == typeof(Int16))
      {
        dest = (T)(object)BitConverter.ToInt16(buffer, 0);
      }
      else if (typeof(T) == typeof(Int32))
      {
        dest = (T)(object)BitConverter.ToInt32(buffer, 0);
      }
      else if (typeof(T) == typeof(Int64))
      {
        dest = (T)(object)BitConverter.ToInt64(buffer, 0);
      }
      else if (typeof(T) == typeof(Single))
      {
        dest = (T)(object)BitConverter.ToSingle(buffer, 0);
      }
      else if (typeof(T) == typeof(UInt16))
      {
        dest = (T)(object)BitConverter.ToUInt16(buffer, 0);
      }
      else if (typeof(T) == typeof(UInt32))
      {
        dest = (T)(object)BitConverter.ToUInt32(buffer, 0);
      }
      else if (typeof(T) == typeof(UInt64))
      {
        dest = (T)(object)BitConverter.ToUInt64(buffer, 0);
      }
      else
      {
        dest = default(T);
      }

      return dest;
    }

    public static byte[] ToBytes<T>(this T value, ByteOrder order)
      where T : struct
    {
      byte[] buffer;

      if (typeof(T) == typeof(Boolean))
      {
        buffer = BitConverter.GetBytes((Boolean)(object)value);
      }
      else if (typeof(T) == typeof(Char))
      {
        buffer = BitConverter.GetBytes((Char)(object)value);
      }
      else if (typeof(T) == typeof(Double))
      {
        buffer = BitConverter.GetBytes((Double)(object)value);
      }
      else if (typeof(T) == typeof(Int16))
      {
        buffer = BitConverter.GetBytes((Int16)(object)value);
      }
      else if (typeof(T) == typeof(Int32))
      {
        buffer = BitConverter.GetBytes((Int32)(object)value);
      }
      else if (typeof(T) == typeof(Int64))
      {
        buffer = BitConverter.GetBytes((Int64)(object)value);
      }
      else if (typeof(T) == typeof(Single))
      {
        buffer = BitConverter.GetBytes((Single)(object)value);
      }
      else if (typeof(T) == typeof(UInt16))
      {
        buffer = BitConverter.GetBytes((UInt16)(object)value);
      }
      else if (typeof(T) == typeof(UInt32))
      {
        buffer = BitConverter.GetBytes((UInt32)(object)value);
      }
      else if (typeof(T) == typeof(UInt64))
      {
        buffer = BitConverter.GetBytes((UInt64)(object)value);
      }
      else
      {
        buffer = new byte[]{};
      }

      return order.IsHostOrder()
             ? buffer
             : buffer.Reverse().ToArray();
    }

    public static byte[] ToHostOrder(this byte[] src, ByteOrder srcOrder)
    {
      byte[] buffer = new byte[src.Length];
      src.CopyTo(buffer, 0);

      return srcOrder.IsHostOrder()
             ? buffer
             : buffer.Reverse().ToArray();
    }

    public static string ToString<T>(this T[] array, string separater)
    {
      int len;
      StringBuilder sb;

      len = array.Length;
      if (len == 0)
      {
        return String.Empty;
      }

      sb = new StringBuilder();
      for (int i = 0; i < len - 1; i++)
      {
        sb.AppendFormat("{0}{1}", array[i].ToString(), separater);
      }
      sb.Append(array[len - 1].ToString());

      return sb.ToString();
    }
  }
}
