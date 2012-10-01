#region MIT License
/**
 * Ext.cs
 *  IsPredefinedScheme and MaybeUri methods derived from System.Uri.cs
 *  GetStatusDescription method derived from System.Net.HttpListenerResponse.cs
 *
 * The MIT License
 *
 * (C) 2001 Garrett Rooney (System.Uri)
 * (C) 2003 Ian MacLean (System.Uri)
 * (C) 2003 Ben Maurer (System.Uri)
 * Copyright (C) 2003, 2005, 2009 Novell, Inc. (http://www.novell.com) (System.Uri, System.Net.HttpListenerResponse)
 * Copyright (c) 2009 Stephane Delcroix (System.Uri)
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
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using WebSocketSharp.Net;
using WebSocketSharp.Net.Sockets;

namespace WebSocketSharp {

  public static class Ext {

    public static TcpListenerWebSocketContext AcceptWebSocket(this TcpClient client)
    {
      return new TcpListenerWebSocketContext(client);
    }

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
      if (value < 0)
        throw new ArgumentOutOfRangeException("value");

      byte b = (byte)value;
      dest.Add(b);
      return b == Convert.ToByte(c);
    }

    public static bool Exists(this NameValueCollection headers, string name)
    {
      return headers[name] != null
             ? true
             : false;
    }

    public static bool Exists(this NameValueCollection headers, string name, string value)
    {
      var values = headers[name];
      if (values == null)
        return false;

      foreach (string v in values.Split(','))
        if (String.Compare(v.Trim(), value, true) == 0)
          return true;

      return false;
    }

    public static string GetDescription(this HttpStatusCode code)
    {
      return ((int)code).GetStatusDescription();
    }

    public static string GetHeaderValue(this string src, string separater)
    {
      int i = src.IndexOf(separater);
      return src.Substring(i + 1).Trim();
    }

    // Derived from System.Net.HttpListenerResponse.GetStatusDescription method
    public static string GetStatusDescription(this int code)
    {
      switch (code)
      {
        case 100: return "Continue";
        case 101: return "Switching Protocols";
        case 102: return "Processing";
        case 200: return "OK";
        case 201: return "Created";
        case 202: return "Accepted";
        case 203: return "Non-Authoritative Information";
        case 204: return "No Content";
        case 205: return "Reset Content";
        case 206: return "Partial Content";
        case 207: return "Multi-Status";
        case 300: return "Multiple Choices";
        case 301: return "Moved Permanently";
        case 302: return "Found";
        case 303: return "See Other";
        case 304: return "Not Modified";
        case 305: return "Use Proxy";
        case 307: return "Temporary Redirect";
        case 400: return "Bad Request";
        case 401: return "Unauthorized";
        case 402: return "Payment Required";
        case 403: return "Forbidden";
        case 404: return "Not Found";
        case 405: return "Method Not Allowed";
        case 406: return "Not Acceptable";
        case 407: return "Proxy Authentication Required";
        case 408: return "Request Timeout";
        case 409: return "Conflict";
        case 410: return "Gone";
        case 411: return "Length Required";
        case 412: return "Precondition Failed";
        case 413: return "Request Entity Too Large";
        case 414: return "Request-Uri Too Long";
        case 415: return "Unsupported Media Type";
        case 416: return "Requested Range Not Satisfiable";
        case 417: return "Expectation Failed";
        case 422: return "Unprocessable Entity";
        case 423: return "Locked";
        case 424: return "Failed Dependency";
        case 500: return "Internal Server Error";
        case 501: return "Not Implemented";
        case 502: return "Bad Gateway";
        case 503: return "Service Unavailable";
        case 504: return "Gateway Timeout";
        case 505: return "Http Version Not Supported";
        case 507: return "Insufficient Storage";
      }

      return String.Empty;
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

    // Derived from System.Uri.IsPredefinedScheme method
    public static bool IsPredefinedScheme(this string scheme)
    {
      if (scheme == null && scheme.Length < 2)
        return false;

      char c = scheme[0];

      if (c == 'h')
        return (scheme == "http" || scheme == "https");

      if (c == 'f')
        return (scheme == "file" || scheme == "ftp");

      if (c == 'w')
        return (scheme == "ws" || scheme == "wss");

      if (c == 'n')
      {
        c = scheme[1];

        if (c == 'e')
          return (scheme == "news" || scheme == "net.pipe" || scheme == "net.tcp");
          
        if (scheme == "nntp")
          return true;

        return false;
      }

      if ((c == 'g' && scheme == "gopher") || (c == 'm' && scheme == "mailto"))
        return true;

      return false;
    }

    public static bool IsValidWsUri(this Uri uri, out string message)
    {
      if (!uri.IsAbsoluteUri)
      {
        message = "Not absolute uri: " + uri.ToString();
        return false;
      }

      var scheme = uri.Scheme;
      if (scheme != "ws" && scheme != "wss")
      {
        message = "Unsupported WebSocket URI scheme: " + scheme;
        return false;
      }

      var port = uri.Port;
      if (port > 0)
      {
        if ((scheme == "wss" && port != 443) ||
            (scheme != "wss" && port == 443))
        {
          message = String.Format(
            "Invalid pair of WebSocket URI scheme and port: {0}, {1}", scheme, port);
          return false;
        }
      }

      var host  = uri.DnsSafeHost;
      var addrs = System.Net.Dns.GetHostAddresses(host);
      if (addrs.Length == 0)
      {
        message = "Invalid WebSocket URI host: " + host;
        return false;
      }

      message = String.Empty;
      return true;
    }

    // Derived from System.Uri.MaybeUri method
    public static bool MaybeUri(this string uriString)
    {
      int p = uriString.IndexOf(':');
      if (p == -1)
        return false;

      if (p >= 10)
        return false;

      return uriString.Substring(0, p).IsPredefinedScheme();
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

    public static byte[] ReadBytes(this Stream stream, int length)
    {
      if (length <= 0)
        return new byte[]{};

      var buffer = new byte[length];
      stream.Read(buffer, 0, length);
      return buffer;
    }

    public static byte[] ReadBytes(this Stream stream, long length, int bufferLength)
    {
      var count    = length / bufferLength;
      var rem      = length % bufferLength;
      var readData = new List<byte>();
      var readLen  = 0;
      var buffer   = new byte[bufferLength];

      count.Times(() =>
      {
        readLen = stream.Read(buffer, 0, bufferLength);
        if (readLen > 0)
          readData.AddRange(buffer.SubArray(0, readLen));
      });

      if (rem > 0)
      {
        buffer = new byte[rem];
        readLen = stream.Read(buffer, 0, (int)rem);
        if (readLen > 0)
          readData.AddRange(buffer.SubArray(0, readLen));
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

    public static void Times(this int n, Action act)
    {
      ((ulong)n).Times(act);
    }

    public static void Times(this uint n, Action act)
    {
      ((ulong)n).Times(act);
    }

    public static void Times(this long n, Action act)
    {
      ((ulong)n).Times(act);
    }

    public static void Times(this ulong n, Action act)
    {
      for (ulong i = 0; i < n; i++)
        act();
    }

    public static void Times(this int n, Action<ulong> act)
    {
      ((ulong)n).Times(act);
    }

    public static void Times(this uint n, Action<ulong> act)
    {
      ((ulong)n).Times(act);
    }

    public static void Times(this long n, Action<ulong> act)
    {
      ((ulong)n).Times(act);
    }

    public static void Times(this ulong n, Action<ulong> act)
    {
      for (ulong i = 0; i < n; i++)
        act(i);
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

    public static Uri ToUri(this string uriString)
    {
      if (!uriString.MaybeUri())
        return new Uri(uriString, UriKind.Relative);

      return new Uri(uriString);
    }

    public static void WriteContent(this HttpListenerResponse response, byte[] content)
    {
      var output = response.OutputStream;
      response.ContentLength64 = content.Length;
      output.Write(content, 0, content.Length);
      output.Close();
    }
  }
}
