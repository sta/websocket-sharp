#region License
/*
 * Ext.cs
 *  IsPredefinedScheme and MaybeUri methods are derived from System.Uri.cs
 *  GetStatusDescription method is derived from System.Net.HttpListenerResponse.cs
 *
 * The MIT License
 *
 * Copyright (c) 2010-2013 sta.blockhead
 *
 * System.Uri.cs
 *  (C) 2001 Garrett Rooney
 *  (C) 2003 Ian MacLean
 *  (C) 2003 Ben Maurer
 *  Copyright (C) 2003, 2005, 2009 Novell, Inc. (http://www.novell.com)
 *  Copyright (c) 2009 Stephane Delcroix
 *
 * System.Net.HttpListenerResponse.cs
 *  Copyright (C) 2003, 2005, 2009 Novell, Inc. (http://www.novell.com)
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
using System.IO.Compression;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using WebSocketSharp.Net;
using WebSocketSharp.Net.WebSockets;
using WebSocketSharp.Server;

namespace WebSocketSharp
{
  /// <summary>
  /// Provides a set of static methods for the websocket-sharp.
  /// </summary>
  public static class Ext
  {
    #region Private Const Fields

    private const string _tspecials = "()<>@,;:\\\"/[]?={} \t";

    #endregion

    #region Private Methods

    private static byte [] compress (this byte [] value)
    {
      if (value.LongLength == 0)
        //return new Byte [] { 0x00, 0x00, 0x00, 0xff, 0xff };
        return value;

      using (var input = new MemoryStream (value))
      {
        return input.compressToArray ();
      }
    }

    private static MemoryStream compress (this Stream stream)
    {
      var output = new MemoryStream ();
      if (stream.Length == 0)
        return output;

      stream.Position = 0;
      using (var ds = new DeflateStream (output, CompressionMode.Compress, true))
      {
        stream.CopyTo (ds);
        ds.Close (); // "BFINAL" set to 1.
        output.Position = 0;

        return output;
      }
    }

    private static byte [] compressToArray (this Stream stream)
    {
      using (var comp = stream.compress ())
      {
        comp.Close ();
        return comp.ToArray ();
      }
    }

    private static byte [] decompress (this byte [] value)
    {
      if (value.LongLength == 0)
        return value;

      using (var input = new MemoryStream (value))
      {
        return input.decompressToArray ();
      }
    }

    private static MemoryStream decompress (this Stream stream)
    {
      var output = new MemoryStream ();
      if (stream.Length == 0)
        return output;

      stream.Position = 0;
      using (var ds = new DeflateStream (stream, CompressionMode.Decompress, true))
      {
        ds.CopyTo (output, true);
        return output;
      }
    }

    private static byte [] decompressToArray (this Stream stream)
    {
      using (var decomp = stream.decompress ())
      {
        decomp.Close ();
        return decomp.ToArray ();
      }
    }

    private static byte [] readBytes (this Stream stream, byte [] buffer, int offset, int length)
    {
      var len = stream.Read (buffer, offset, length);
      if (len < 1)
        return buffer.SubArray (0, offset);

      var tmp = 0;
      while (len < length)
      {
        tmp = stream.Read (buffer, offset + len, length - len);
        if (tmp < 1)
          break;

        len += tmp;
      }

      return len < length
             ? buffer.SubArray (0, offset + len)
             : buffer;
    }

    private static bool readBytes (
      this Stream stream, byte [] buffer, int offset, int length, Stream dest)
    {
      var bytes = stream.readBytes (buffer, offset, length);
      var len = bytes.Length;
      dest.Write (bytes, 0, len);

      return len == offset + length;
    }

    private static void times (this ulong n, Action act)
    {
      for (ulong i = 0; i < n; i++)
        act ();
    }

    #endregion

    #region Internal Methods

    internal static byte [] Append (this ushort code, string reason)
    {
      using (var buffer = new MemoryStream ())
      {
        var tmp = code.ToByteArrayInternally (ByteOrder.BIG);
        buffer.Write (tmp, 0, 2);
        if (reason != null && reason.Length > 0)
        {
          tmp = Encoding.UTF8.GetBytes (reason);
          buffer.Write (tmp, 0, tmp.Length);
        }

        buffer.Close ();
        return buffer.ToArray ();
      }
    }

    internal static string CheckIfCanRead (this Stream stream)
    {
      return stream == null
             ? "'stream' must not be null."
             : !stream.CanRead
               ? "'stream' cannot be read."
               : null;
    }

    internal static string CheckIfOpen (this WebSocketState state)
    {
      return state != WebSocketState.OPEN
             ? "A WebSocket connection isn't established or has been closed."
             : null;
    }

    internal static string CheckIfStarted (this ServerState state)
    {
      return state != ServerState.START
             ? "Any of not started, on shutdown or stopped."
             : null;
    }

    internal static string CheckIfStopped (this ServerState state)
    {
      return state == ServerState.START || state == ServerState.SHUTDOWN
             ? "Already started or on shutdown."
             : null;
    }

    internal static string CheckIfValidCloseData (this byte [] data)
    {
      return data.Length > 125
             ? "'reason' length must be less."
             : null;
    }

    internal static string CheckIfValidCloseStatusCode (this ushort code)
    {
      return !code.IsCloseStatusCode ()
             ? "Invalid close status code."
             : null;
    }

    internal static string CheckIfValidPingData (this byte [] data)
    {
      return data.Length > 125
             ? "'message' length must be less."
             : null;
    }

    internal static string CheckIfValidSendData (this byte [] data)
    {
      return data == null
             ? "'data' must not be null."
             : null;
    }

    internal static string CheckIfValidSendData (this string data)
    {
      return data == null
             ? "'data' must not be null."
             : null;
    }

    internal static string CheckIfValidServicePath (this string servicePath)
    {
      return servicePath == null || servicePath.Length == 0
             ? "'servicePath' must not be null or empty."
             : servicePath [0] != '/'
               ? "'servicePath' not absolute path."
               : servicePath.IndexOfAny (new [] {'?', '#'}) != -1
                 ? "'servicePath' must not contain either or both query and fragment components."
                 : null;
    }

    internal static string CheckIfValidSessionID (this string id)
    {
      return id == null || id.Length == 0
             ? "'id' must not be null or empty."
             : null;
    }

    internal static byte [] Compress (this byte [] value, CompressionMethod method)
    {
      return method == CompressionMethod.DEFLATE
             ? value.compress ()
             : value;
    }

    internal static Stream Compress (this Stream stream, CompressionMethod method)
    {
      return method == CompressionMethod.DEFLATE
             ? stream.compress ()
             : stream;
    }

    internal static byte [] CompressToArray (this Stream stream, CompressionMethod method)
    {
      return method == CompressionMethod.DEFLATE
             ? stream.compressToArray ()
             : stream.ToByteArray ();
    }

    internal static T [] Copy<T> (this T [] src, long length)
    {
      var dest = new T [length];
      Array.Copy (src, 0, dest, 0, length);

      return dest;
    }

    internal static void CopyTo (this Stream src, Stream dest)
    {
      src.CopyTo (dest, false);
    }

    internal static void CopyTo (this Stream src, Stream dest, bool setDefaultPosition)
    {
      var readLen = 0;
      var bufferLen = 256;
      var buffer = new byte [bufferLen];
      while ((readLen = src.Read (buffer, 0, bufferLen)) > 0)
      {
        dest.Write (buffer, 0, readLen);
      }

      if (setDefaultPosition)
        dest.Position = 0;
    }

    internal static byte [] Decompress (this byte [] value, CompressionMethod method)
    {
      return method == CompressionMethod.DEFLATE
             ? value.decompress ()
             : value;
    }

    internal static Stream Decompress (this Stream stream, CompressionMethod method)
    {
      return method == CompressionMethod.DEFLATE
             ? stream.decompress ()
             : stream;
    }

    internal static byte [] DecompressToArray (this Stream stream, CompressionMethod method)
    {
      return method == CompressionMethod.DEFLATE
             ? stream.decompressToArray ()
             : stream.ToByteArray ();
    }

    internal static bool Equals (this string value, CompressionMethod method)
    {
      return value == method.ToCompressionExtension ();
    }

    /// <summary>
    /// Determines whether the specified <see cref="int"/> equals the specified <see cref="char"/>,
    /// and invokes the specified Action&lt;int&gt; delegate at the same time.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="value"/> equals <paramref name="c"/>; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="value">
    /// An <see cref="int"/> to compare.
    /// </param>
    /// <param name="c">
    /// A <see cref="char"/> to compare.
    /// </param>
    /// <param name="action">
    /// An Action&lt;int&gt; delegate that references the method(s) called at the same time as comparing.
    /// An <see cref="int"/> parameter to pass to the method(s) is <paramref name="value"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is not between 0 and 255.
    /// </exception>
    internal static bool EqualsWith (this int value, char c, Action<int> action)
    {
      if (value < 0 || value > 255)
        throw new ArgumentOutOfRangeException ("value");

      action (value);
      return value == c - 0;
    }

    /// <summary>
    /// Gets the absolute path from the specified <see cref="Uri"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that contains the absolute path if it is successfully found;
    /// otherwise, <see langword="null"/>.
    /// </returns>
    /// <param name="uri">
    /// A <see cref="Uri"/> that contains a URI to get the absolute path from.
    /// </param>
    internal static string GetAbsolutePath (this Uri uri)
    {
      if (uri.IsAbsoluteUri)
        return uri.AbsolutePath;

      var original = uri.OriginalString;
      if (original [0] != '/')
        return null;

      var i = original.IndexOfAny (new [] {'?', '#'});
      return i > 0
             ? original.Substring (0, i)
             : original;
    }

    internal static string GetMessage (this CloseStatusCode code)
    {
      return code == CloseStatusCode.PROTOCOL_ERROR
             ? "A WebSocket protocol error has occurred."
             : code == CloseStatusCode.INCORRECT_DATA
               ? "An incorrect data has been received."
               : code == CloseStatusCode.ABNORMAL
                 ? "An exception has occurred."
                 : code == CloseStatusCode.INCONSISTENT_DATA
                   ? "An inconsistent data has been received."
                   : code == CloseStatusCode.POLICY_VIOLATION
                     ? "A policy violation data has been received."
                     : code == CloseStatusCode.TOO_BIG
                       ? "A too big data has been received."
                       : code == CloseStatusCode.IGNORE_EXTENSION
                         ? "WebSocket client did not receive expected extension(s)."
                         : code == CloseStatusCode.SERVER_ERROR
                           ? "WebSocket server got an internal error."
                           : code == CloseStatusCode.TLS_HANDSHAKE_FAILURE
                             ? "An error has occurred while handshaking."
                             : String.Empty;
    }

    internal static string GetNameInternal (this string nameAndValue, string separator)
    {
      int i = nameAndValue.IndexOf (separator);
      return i > 0
             ? nameAndValue.Substring (0, i).Trim ()
             : null;
    }

    internal static string GetValueInternal (this string nameAndValue, string separator)
    {
      int i = nameAndValue.IndexOf (separator);
      return i >= 0 && i < nameAndValue.Length - 1
             ? nameAndValue.Substring (i + 1).Trim ()
             : null;
    }

    internal static TcpListenerWebSocketContext GetWebSocketContext (
      this TcpClient client, bool secure, X509Certificate cert)
    {
      return new TcpListenerWebSocketContext (client, secure, cert);
    }

    internal static bool IsCompressionExtension (this string value)
    {
      return value.StartsWith ("permessage-");
    }

    internal static bool IsPortNumber (this int value)
    {
      return value > 0 && value < 65536;
    }

    internal static bool IsReserved (this ushort code)
    {
      return code == (ushort) CloseStatusCode.UNDEFINED ||
             code == (ushort) CloseStatusCode.NO_STATUS_CODE ||
             code == (ushort) CloseStatusCode.ABNORMAL ||
             code == (ushort) CloseStatusCode.TLS_HANDSHAKE_FAILURE;
    }

    internal static bool IsReserved (this CloseStatusCode code)
    {
      return code == CloseStatusCode.UNDEFINED ||
             code == CloseStatusCode.NO_STATUS_CODE ||
             code == CloseStatusCode.ABNORMAL ||
             code == CloseStatusCode.TLS_HANDSHAKE_FAILURE;
    }

    internal static bool IsText (this string value)
    {
      int len = value.Length;
      for (int i = 0; i < len; i++)
      {
        char c = value [i];
        if (c < 0x20 && !"\r\n\t".Contains (c))
          return false;

        if (c == 0x7f)
          return false;

        if (c == '\n' && ++i < len)
        {
          c = value [i];
          if (!" \t".Contains (c))
            return false;
        }
      }

      return true;
    }

    internal static bool IsToken (this string value)
    {
      foreach (char c in value)
        if (c < 0x20 || c >= 0x7f || _tspecials.Contains (c))
          return false;

      return true;
    }

    internal static string Quote (this string value)
    {
      return value.IsToken ()
             ? value
             : String.Format ("\"{0}\"", value.Replace ("\"", "\\\""));
    }

    internal static byte [] ReadBytes (this Stream stream, int length)
    {
      return stream.readBytes (new byte [length], 0, length);
    }

    internal static byte [] ReadBytes (this Stream stream, long length, int bufferLength)
    {
      using (var result = new MemoryStream ())
      {
        var count = length / bufferLength;
        var rem = (int) (length % bufferLength);

        var buffer = new byte [bufferLength];
        var end = false;
        for (long i = 0; i < count; i++)
        {
          if (!stream.readBytes (buffer, 0, bufferLength, result))
          {
            end = true;
            break;
          }
        }

        if (!end && rem > 0)
          stream.readBytes (new byte [rem], 0, rem, result);

        result.Close ();
        return result.ToArray ();
      }
    }

    internal static void ReadBytesAsync (
      this Stream stream, int length, Action<byte []> completed, Action<Exception> error)
    {
      var buffer = new byte [length];
      AsyncCallback callback = ar =>
      {
        try {
          var len = stream.EndRead (ar);
          var bytes = len < 1
                    ? new byte []{}
                    : len < length
                      ? stream.readBytes (buffer, len, length - len)
                      : buffer;

          if (completed != null)
            completed (bytes);
        }
        catch (Exception ex) {
          if (error != null)
            error (ex);
        }
      };

      stream.BeginRead (buffer, 0, length, callback, null);
    }

    internal static string RemovePrefix (this string value, params string [] prefixes)
    {
      int i = 0;
      foreach (var prefix in prefixes)
      {
        if (value.StartsWith (prefix))
        {
          i = prefix.Length;
          break;
        }
      }

      return i > 0
             ? value.Substring (i)
             : value;
    }

    internal static IEnumerable<string> SplitHeaderValue (this string value, params char [] separator)
    {
      var separators = new string (separator);
      var buffer = new StringBuilder (64);
      int len = value.Length;
      bool quoted = false;
      bool escaped = false;
      for (int i = 0; i < len; i++)
      {
        char c = value [i];
        if (c == '"')
        {
          if (escaped)
            escaped = !escaped;
          else
            quoted = !quoted;
        }
        else if (c == '\\')
        {
          if (i < len - 1 && value [i + 1] == '"')
            escaped = true;
        }
        else if (separators.Contains (c))
        {
          if (!quoted)
          {
            yield return buffer.ToString ();
            buffer.Length = 0;
            continue;
          }
        }
        else {
        }

        buffer.Append (c);
      }

      if (buffer.Length > 0)
        yield return buffer.ToString ();
    }

    internal static byte [] ToByteArray (this Stream stream)
    {
      using (var output = new MemoryStream ())
      {
        stream.Position = 0;
        stream.CopyTo (output);
        output.Close ();

        return output.ToArray ();
      }
    }

    internal static byte [] ToByteArrayInternally (this ushort value, ByteOrder order)
    {
      var buffer = BitConverter.GetBytes (value);
      return order.IsHostOrder ()
             ? buffer
             : buffer.Reverse ().ToArray ();
    }

    internal static byte [] ToByteArrayInternally (this ulong value, ByteOrder order)
    {
      var buffer = BitConverter.GetBytes (value);
      return order.IsHostOrder ()
             ? buffer
             : buffer.Reverse ().ToArray ();
    }

    internal static string ToCompressionExtension (this CompressionMethod method)
    {
      return method != CompressionMethod.NONE
             ? String.Format ("permessage-{0}", method.ToString ().ToLower ())
             : String.Empty;
    }

    internal static CompressionMethod ToCompressionMethod (this string value)
    {
      foreach (CompressionMethod method in Enum.GetValues (typeof (CompressionMethod)))
        if (value.Equals (method))
          return method;

      return CompressionMethod.NONE;
    }

    internal static System.Net.IPAddress ToIPAddress (this string hostNameOrAddress)
    {
      try {
        var addrs = System.Net.Dns.GetHostAddresses (hostNameOrAddress);
        return addrs [0];
      }
      catch {
        return null;
      }
    }

    internal static ushort ToUInt16 (this byte [] src, ByteOrder srcOrder)
    {
      return BitConverter.ToUInt16 (src.ToHostOrder (srcOrder), 0);
    }

    internal static ulong ToUInt64 (this byte [] src, ByteOrder srcOrder)
    {
      return BitConverter.ToUInt64 (src.ToHostOrder (srcOrder), 0);
    }

    internal static string TrimEndSlash (this string value)
    {
      value = value.TrimEnd ('/');
      return value.Length > 0
             ? value
             : "/";
    }

    /// <summary>
    /// Tries to create a <see cref="Uri"/> for WebSocket with the specified <paramref name="uriString"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the <see cref="Uri"/> is successfully created; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="uriString">
    /// A <see cref="string"/> that contains a WebSocket URL to try.
    /// </param>
    /// <param name="result">
    /// When this method returns, a <see cref="Uri"/> that represents the WebSocket URL
    /// if <paramref name="uriString"/> is valid; otherwise, <see langword="null"/>.
    /// </param>
    /// <param name="message">
    /// When this method returns, a <see cref="string"/> that contains an error message
    /// if <paramref name="uriString"/> is invalid; otherwise, <c>String.Empty</c>.
    /// </param>
    internal static bool TryCreateWebSocketUri (this string uriString, out Uri result, out string message)
    {
      result = null;
      if (uriString.Length == 0)
      {
        message = "Must not be empty.";
        return false;
      }

      var uri = uriString.ToUri ();
      if (!uri.IsAbsoluteUri)
      {
        message = "Must be the absolute URI: " + uriString;
        return false;
      }

      var scheme = uri.Scheme;
      if (scheme != "ws" && scheme != "wss")
      {
        message = "The scheme part must be 'ws' or 'wss': " + scheme;
        return false;
      }

      var fragment = uri.Fragment;
      if (fragment.Length != 0)
      {
        message = "Must not contain the fragment component: " + uriString;
        return false;
      }

      var port = uri.Port;
      if (port > 0)
      {
        if (port > 65535)
        {
          message = "The port part must be between 1 and 65535: " + port;
          return false;
        }

        if ((scheme == "ws" && port == 443) || (scheme == "wss" && port == 80))
        {
          message = String.Format ("Invalid pair of scheme and port: {0}, {1}", scheme, port);
          return false;
        }
      }
      else
      {
        port = scheme == "ws" ? 80 : 443;
        var url = String.Format ("{0}://{1}:{2}{3}", scheme, uri.Host, port, uri.PathAndQuery);
        uri = url.ToUri ();
      }

      result = uri;
      message = String.Empty;

      return true;
    }

    internal static string Unquote (this string value)
    {
      var start = value.IndexOf ('\"');
      var end = value.LastIndexOf ('\"');
      if (start < end)
        value = value.Substring (start + 1, end - start - 1).Replace ("\\\"", "\"");

      return value.Trim ();
    }

    internal static void WriteBytes (this Stream stream, byte [] value)
    {
      using (var src = new MemoryStream (value))
      {
        src.CopyTo (stream);
      }
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Determines whether the specified <see cref="string"/> contains any of characters
    /// in the specified array of <see cref="char"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="value"/> contains any of <paramref name="chars"/>;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <param name="value">
    /// A <see cref="string"/> to test.
    /// </param>
    /// <param name="chars">
    /// An array of <see cref="char"/> that contains characters to find.
    /// </param>
    public static bool Contains (this string value, params char [] chars)
    {
      return chars == null || chars.Length == 0
             ? true
             : value == null || value.Length == 0
               ? false
               : value.IndexOfAny (chars) != -1;
    }

    /// <summary>
    /// Determines whether the specified <see cref="NameValueCollection"/> contains the entry
    /// with the specified <paramref name="name"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="collection"/> contains the entry with <paramref name="name"/>;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <param name="collection">
    /// A <see cref="NameValueCollection"/> to test.
    /// </param>
    /// <param name="name">
    /// A <see cref="string"/> that contains the key of the entry to find.
    /// </param>
    public static bool Contains (this NameValueCollection collection, string name)
    {
      return collection == null
             ? false
             : collection [name] != null;
    }

    /// <summary>
    /// Determines whether the specified <see cref="NameValueCollection"/> contains the entry
    /// with the specified both <paramref name="name"/> and <paramref name="value"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="collection"/> contains the entry with both <paramref name="name"/>
    /// and <paramref name="value"/>; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="collection">
    /// A <see cref="NameValueCollection"/> to test.
    /// </param>
    /// <param name="name">
    /// A <see cref="string"/> that contains the key of the entry to find.
    /// </param>
    /// <param name="value">
    /// A <see cref="string"/> that contains the value of the entry to find.
    /// </param>
    public static bool Contains (this NameValueCollection collection, string name, string value)
    {
      if (collection == null)
        return false;

      var values = collection [name];
      if (values == null)
        return false;

      foreach (string v in values.Split (','))
        if (v.Trim ().Equals (value, StringComparison.OrdinalIgnoreCase))
          return true;

      return false;
    }

    /// <summary>
    /// Emits the specified <see cref="EventHandler"/> delegate if not <see langword="null"/>.
    /// </summary>
    /// <param name="eventHandler">
    /// A <see cref="EventHandler"/> to emit.
    /// </param>
    /// <param name="sender">
    /// An <see cref="object"/> from which emits this <paramref name="eventHandler"/>.
    /// </param>
    /// <param name="e">
    /// A <see cref="EventArgs"/> that contains no event data.
    /// </param>
    public static void Emit (
      this EventHandler eventHandler, object sender, EventArgs e)
    {
      if (eventHandler != null)
        eventHandler (sender, e);
    }

    /// <summary>
    /// Emits the specified <b>EventHandler&lt;TEventArgs&gt;</b> delegate if not <see langword="null"/>.
    /// </summary>
    /// <param name="eventHandler">
    /// An <b>EventHandler&lt;TEventArgs&gt;</b> to emit.
    /// </param>
    /// <param name="sender">
    /// An <see cref="object"/> from which emits this <paramref name="eventHandler"/>.
    /// </param>
    /// <param name="e">
    /// A <b>TEventArgs</b> that contains the event data.
    /// </param>
    /// <typeparam name="TEventArgs">
    /// The type of the event data generated by the event.
    /// </typeparam>
    public static void Emit<TEventArgs> (
      this EventHandler<TEventArgs> eventHandler, object sender, TEventArgs e)
      where TEventArgs : EventArgs
    {
      if (eventHandler != null)
        eventHandler (sender, e);
    }

    /// <summary>
    /// Gets the collection of cookies from the specified <see cref="NameValueCollection"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="CookieCollection"/> that receives a collection of the HTTP Cookies.
    /// </returns>
    /// <param name="headers">
    /// A <see cref="NameValueCollection"/> that contains a collection of the HTTP Headers.
    /// </param>
    /// <param name="response">
    /// <c>true</c> if gets from the response <paramref name="headers"/>;
    /// from the request <paramref name="headers"/>, <c>false</c>.
    /// </param>
    public static CookieCollection GetCookies (this NameValueCollection headers, bool response)
    {
      var name = response ? "Set-Cookie" : "Cookie";
      return headers == null || !headers.Contains (name)
             ? new CookieCollection ()
             : CookieCollection.Parse (headers [name], response);
    }

    /// <summary>
    /// Gets the description of the HTTP status code using the specified <see cref="HttpStatusCode"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that contains the description of an HTTP status code.
    /// </returns>
    /// <param name="code">
    /// One of <see cref="HttpStatusCode"/> values that indicates an HTTP status code.
    /// </param>
    public static string GetDescription (this HttpStatusCode code)
    {
      return ((int) code).GetStatusDescription ();
    }

    /// <summary>
    /// Gets the name from the specified <see cref="string"/> that contains a pair of name and value
    /// separated by a separator string.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that contains the name if any; otherwise, <c>null</c>.
    /// </returns>
    /// <param name="nameAndValue">
    /// A <see cref="string"/> that contains a pair of name and value separated by a separator string.
    /// </param>
    /// <param name="separator">
    /// A <see cref="string"/> that contains a separator string.
    /// </param>
    public static string GetName (this string nameAndValue, string separator)
    {
      return (nameAndValue != null && nameAndValue.Length != 0) &&
             (separator != null && separator.Length != 0)
             ? nameAndValue.GetNameInternal (separator)
             : null;
    }

    /// <summary>
    /// Gets the name and value from the specified <see cref="string"/> that contains a pair of name and value
    /// separated by a separator string.
    /// </summary>
    /// <returns>
    /// A <b>KeyValuePair&lt;string, string&gt;</b> that contains the name and value if any.
    /// </returns>
    /// <param name="nameAndValue">
    /// A <see cref="string"/> that contains a pair of name and value separated by a separator string.
    /// </param>
    /// <param name="separator">
    /// A <see cref="string"/> that contains a separator string.
    /// </param>
    public static KeyValuePair<string, string> GetNameAndValue (this string nameAndValue, string separator)
    {
      var name = nameAndValue.GetName (separator);
      var value = nameAndValue.GetValue (separator);
      return name != null
             ? new KeyValuePair<string, string> (name, value)
             : new KeyValuePair<string, string> (null, null);
    }

    /// <summary>
    /// Gets the description of the HTTP status code using the specified <see cref="int"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that contains the description of an HTTP status code.
    /// </returns>
    /// <param name="code">
    /// An <see cref="int"/> that contains an HTTP status code.
    /// </param>
    public static string GetStatusDescription (this int code)
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

    /// <summary>
    /// Gets the value from the specified <see cref="string"/> that contains a pair of name and value
    /// separated by a separator string.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that contains the value if any; otherwise, <c>null</c>.
    /// </returns>
    /// <param name="nameAndValue">
    /// A <see cref="string"/> that contains a pair of name and value separated by a separator string.
    /// </param>
    /// <param name="separator">
    /// A <see cref="string"/> that contains a separator string.
    /// </param>
    public static string GetValue (this string nameAndValue, string separator)
    {
      return (nameAndValue != null && nameAndValue.Length != 0) &&
             (separator != null && separator.Length != 0)
             ? nameAndValue.GetValueInternal (separator)
             : null;
    }

    /// <summary>
    /// Determines whether the specified <see cref="ushort"/> is in the allowable range of
    /// the WebSocket close status code.
    /// </summary>
    /// <remarks>
    /// Not allowable ranges are the followings.
    ///   <list type="bullet">
    ///     <item>
    ///       <term>
    ///       Numbers in the range 0-999 are not used.
    ///       </term>
    ///     </item>
    ///     <item>
    ///       <term>
    ///       Numbers which are greater than 4999 are out of the reserved close status code ranges.
    ///       </term>
    ///     </item>
    ///   </list>
    /// </remarks>
    /// <returns>
    /// <c>true</c> if <paramref name="value"/> is in the allowable range of the WebSocket close status code;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <param name="value">
    /// A <see cref="ushort"/> to test.
    /// </param>
    public static bool IsCloseStatusCode (this ushort value)
    {
      return value > 999 && value < 5000;
    }

    /// <summary>
    /// Determines whether the specified <see cref="string"/> is enclosed in the specified <see cref="char"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="value"/> is enclosed in <paramref name="c"/>;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <param name="value">
    /// A <see cref="string"/> to test.
    /// </param>
    /// <param name="c">
    /// A <see cref="char"/> that contains character to find.
    /// </param>
    public static bool IsEnclosedIn (this string value, char c)
    {
      return value != null &&
             value.Length > 1 &&
             value [0] == c &&
             value [value.Length - 1] == c;
    }

    /// <summary>
    /// Determines whether the specified <see cref="ByteOrder"/> is host (this computer architecture) byte order.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="order"/> is host byte order; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="order">
    /// A <see cref="ByteOrder"/> to test.
    /// </param>
    public static bool IsHostOrder (this ByteOrder order)
    {
      // true : !(true ^ true)  or !(false ^ false)
      // false: !(true ^ false) or !(false ^ true)
      return !(BitConverter.IsLittleEndian ^ (order == ByteOrder.LITTLE));
    }

    /// <summary>
    /// Determines whether the specified <see cref="System.Net.IPAddress"/> represents
    /// the local IP address.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="address"/> represents the local IP address;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <param name="address">
    /// A <see cref="System.Net.IPAddress"/> to test.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="address"/> is <see langword="null"/>.
    /// </exception>
    public static bool IsLocal (this System.Net.IPAddress address)
    {
      if (address == null)
        throw new ArgumentNullException ("address");

      if (address.Equals (System.Net.IPAddress.Any) ||
          System.Net.IPAddress.IsLoopback (address))
        return true;

      var host = System.Net.Dns.GetHostName ();
      var addrs = System.Net.Dns.GetHostAddresses (host);
      foreach (var addr in addrs)
        if (address.Equals (addr))
          return true;

      return false;
    }

    /// <summary>
    /// Determines whether the specified <see cref="string"/> is <see langword="null"/> or empty.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="value"/> is <see langword="null"/> or empty; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="value">
    /// A <see cref="string"/> to test.
    /// </param>
    public static bool IsNullOrEmpty (this string value)
    {
      return value == null || value.Length == 0;
    }

    /// <summary>
    /// Determines whether the specified <see cref="string"/> is a predefined scheme.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="scheme"/> is a predefined scheme; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="scheme">
    /// A <see cref="string"/> to test.
    /// </param>
    public static bool IsPredefinedScheme (this string scheme)
    {
      if (scheme == null && scheme.Length < 2)
        return false;

      char c = scheme [0];
      if (c == 'h')
        return scheme == "http" || scheme == "https";

      if (c == 'f')
        return scheme == "file" || scheme == "ftp";

      if (c == 'w')
        return scheme == "ws" || scheme == "wss";

      if (c == 'n')
      {
        c = scheme [1];
        return c == 'e'
               ? scheme == "news" || scheme == "net.pipe" || scheme == "net.tcp"
               : scheme == "nntp";
      }

      return (c == 'g' && scheme == "gopher") || (c == 'm' && scheme == "mailto");
    }

    /// <summary>
    /// Determines whether the specified <see cref="HttpListenerRequest"/> is an HTTP Upgrade request
    /// to switch to the specified <paramref name="protocol"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="request"/> is an HTTP Upgrade request
    /// to switch to <paramref name="protocol"/>; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="request">
    /// A <see cref="HttpListenerRequest"/> that contains an HTTP request information.
    /// </param>
    /// <param name="protocol">
    /// A <see cref="string"/> that contains a protocol name.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <para>
    ///   <paramref name="request"/> is <see langword="null"/>.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="protocol"/> is <see langword="null"/>.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="protocol"/> is <see cref="String.Empty"/>.
    /// </exception>
    public static bool IsUpgradeTo (this HttpListenerRequest request, string protocol)
    {
      if (request == null)
        throw new ArgumentNullException ("request");

      if (protocol == null)
        throw new ArgumentNullException ("protocol");

      if (protocol.Length == 0)
        throw new ArgumentException ("Must not be empty.", "protocol");

      return request.Headers.Contains ("Upgrade", protocol) &&
             request.Headers.Contains ("Connection", "Upgrade");
    }

    /// <summary>
    /// Determines whether the specified <see cref="string"/> is a URI string.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="uriString"/> is maybe a URI string; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="uriString">
    /// A <see cref="string"/> to test.
    /// </param>
    public static bool MaybeUri (this string uriString)
    {
      if (uriString == null || uriString.Length == 0)
        return false;

      int p = uriString.IndexOf (':');
      if (p == -1)
        return false;

      if (p >= 10)
        return false;

      return uriString.Substring (0, p).IsPredefinedScheme ();
    }

    /// <summary>
    /// Retrieves a sub-array from the specified <paramref name="array"/>.
    /// A sub-array starts at the specified element position.
    /// </summary>
    /// <returns>
    /// An array of T that receives a sub-array, or an empty array of T if any problems with the parameters.
    /// </returns>
    /// <param name="array">
    /// An array of T that contains the data to retrieve a sub-array.
    /// </param>
    /// <param name="startIndex">
    /// An <see cref="int"/> that contains the zero-based starting position of a sub-array in <paramref name="array"/>.
    /// </param>
    /// <param name="length">
    /// An <see cref="int"/> that contains the number of elements to retrieve a sub-array.
    /// </param>
    /// <typeparam name="T">
    /// The type of elements in the <paramref name="array"/>.
    /// </typeparam>
    public static T [] SubArray<T> (this T [] array, int startIndex, int length)
    {
      if (array == null || array.Length == 0)
        return new T []{};

      if (startIndex < 0 || length <= 0)
        return new T []{};

      if (startIndex + length > array.Length)
        return new T []{};

      if (startIndex == 0 && array.Length == length)
        return array;

      T [] subArray = new T [length];
      Array.Copy (array, startIndex, subArray, 0, length);

      return subArray;
    }

    /// <summary>
    /// Executes the specified <see cref="Action"/> delegate <paramref name="n"/> times.
    /// </summary>
    /// <param name="n">
    /// An <see cref="int"/> is the number of times to execute.
    /// </param>
    /// <param name="act">
    /// An <see cref="Action"/> delegate that references the method(s) to execute.
    /// </param>
    public static void Times (this int n, Action act)
    {
      if (n > 0 && act != null)
        ((ulong) n).times (act);
    }

    /// <summary>
    /// Executes the specified <see cref="Action"/> delegate <paramref name="n"/> times.
    /// </summary>
    /// <param name="n">
    /// A <see cref="long"/> is the number of times to execute.
    /// </param>
    /// <param name="act">
    /// An <see cref="Action"/> delegate that references the method(s) to execute.
    /// </param>
    public static void Times( this long n, Action act)
    {
      if (n > 0 && act != null)
        ((ulong) n).times (act);
    }

    /// <summary>
    /// Executes the specified <see cref="Action"/> delegate <paramref name="n"/> times.
    /// </summary>
    /// <param name="n">
    /// A <see cref="uint"/> is the number of times to execute.
    /// </param>
    /// <param name="act">
    /// An <see cref="Action"/> delegate that references the method(s) to execute.
    /// </param>
    public static void Times (this uint n, Action act)
    {
      if (n > 0 && act != null)
        ((ulong) n).times (act);
    }

    /// <summary>
    /// Executes the specified <see cref="Action"/> delegate <paramref name="n"/> times.
    /// </summary>
    /// <param name="n">
    /// A <see cref="ulong"/> is the number of times to execute.
    /// </param>
    /// <param name="act">
    /// An <see cref="Action"/> delegate that references the method(s) to execute.
    /// </param>
    public static void Times (this ulong n, Action act)
    {
      if (n > 0 && act != null)
        n.times (act);
    }

    /// <summary>
    /// Executes the specified <b>Action&lt;int&gt;</b> delegate <paramref name="n"/> times.
    /// </summary>
    /// <param name="n">
    /// An <see cref="int"/> is the number of times to execute.
    /// </param>
    /// <param name="act">
    /// An <b>Action&lt;int&gt;</b> delegate that references the method(s) to execute.
    /// An <see cref="int"/> parameter to pass to the method(s) is the zero-based count of iteration.
    /// </param>
    public static void Times (this int n, Action<int> act)
    {
      if (n > 0 && act != null)
        for (int i = 0; i < n; i++)
          act (i);
    }

    /// <summary>
    /// Executes the specified <b>Action&lt;long&gt;</b> delegate <paramref name="n"/> times.
    /// </summary>
    /// <param name="n">
    /// A <see cref="long"/> is the number of times to execute.
    /// </param>
    /// <param name="act">
    /// An <b>Action&lt;long&gt;</b> delegate that references the method(s) to execute.
    /// A <see cref="long"/> parameter to pass to the method(s) is the zero-based count of iteration.
    /// </param>
    public static void Times (this long n, Action<long> act)
    {
      if (n > 0 && act != null)
        for (long i = 0; i < n; i++)
          act (i);
    }

    /// <summary>
    /// Executes the specified <b>Action&lt;uint&gt;</b> delegate <paramref name="n"/> times.
    /// </summary>
    /// <param name="n">
    /// A <see cref="uint"/> is the number of times to execute.
    /// </param>
    /// <param name="act">
    /// An <b>Action&lt;uint&gt;</b> delegate that references the method(s) to execute.
    /// A <see cref="uint"/> parameter to pass to the method(s) is the zero-based count of iteration.
    /// </param>
    public static void Times (this uint n, Action<uint> act)
    {
      if (n > 0 && act != null)
        for (uint i = 0; i < n; i++)
          act (i);
    }

    /// <summary>
    /// Executes the specified <b>Action&lt;ulong&gt;</b> delegate <paramref name="n"/> times.
    /// </summary>
    /// <param name="n">
    /// A <see cref="ulong"/> is the number of times to execute.
    /// </param>
    /// <param name="act">
    /// An <b>Action&lt;ulong&gt;</b> delegate that references the method(s) to execute.
    /// A <see cref="ulong"/> parameter to pass to this method(s) is the zero-based count of iteration.
    /// </param>
    public static void Times (this ulong n, Action<ulong> act)
    {
      if (n > 0 && act != null)
        for (ulong i = 0; i < n; i++)
          act (i);
    }

    /// <summary>
    /// Converts the specified array of <see cref="byte"/> to the specified type data.
    /// </summary>
    /// <returns>
    /// A T converted from <paramref name="src"/>, or a default value of T
    /// if <paramref name="src"/> is an empty array of <see cref="byte"/>
    /// or if the type of T isn't <see cref="bool"/>, <see cref="char"/>, <see cref="double"/>,
    /// <see cref="float"/>, <see cref="int"/>, <see cref="long"/>, <see cref="short"/>,
    /// <see cref="uint"/>, <see cref="ulong"/> or <see cref="ushort"/>.
    /// </returns>
    /// <param name="src">
    /// An array of <see cref="byte"/> to convert.
    /// </param>
    /// <param name="srcOrder">
    /// A <see cref="ByteOrder"/> that indicates the byte order of <paramref name="src"/>.
    /// </param>
    /// <typeparam name="T">
    /// The type of the return. The T must be a value type.
    /// </typeparam>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="src"/> is <see langword="null"/>.
    /// </exception>
    public static T To<T> (this byte [] src, ByteOrder srcOrder)
      where T : struct
    {
      if (src == null)
        throw new ArgumentNullException ("src");

      if (src.Length == 0)
        return default (T);

      var type = typeof (T);
      var buffer = src.ToHostOrder (srcOrder);

      return type == typeof (Boolean)
             ? (T)(object) BitConverter.ToBoolean (buffer, 0)
             : type == typeof (Char)
               ? (T)(object) BitConverter.ToChar (buffer, 0)
               : type == typeof (Double)
                 ? (T)(object) BitConverter.ToDouble (buffer, 0)
                 : type == typeof (Int16)
                   ? (T)(object) BitConverter.ToInt16 (buffer, 0)
                   : type == typeof (Int32)
                     ? (T)(object) BitConverter.ToInt32 (buffer, 0)
                     : type == typeof (Int64)
                       ? (T)(object) BitConverter.ToInt64 (buffer, 0)
                       : type == typeof (Single)
                         ? (T)(object) BitConverter.ToSingle (buffer, 0)
                         : type == typeof (UInt16)
                           ? (T)(object) BitConverter.ToUInt16 (buffer, 0)
                           : type == typeof (UInt32)
                             ? (T)(object) BitConverter.ToUInt32 (buffer, 0)
                             : type == typeof (UInt64)
                               ? (T)(object) BitConverter.ToUInt64 (buffer, 0)
                               : default (T);
    }

    /// <summary>
    /// Converts the specified <paramref name="value"/> to an array of <see cref="byte"/>.
    /// </summary>
    /// <returns>
    /// An array of <see cref="byte"/> converted from <paramref name="value"/>.
    /// </returns>
    /// <param name="value">
    /// A T to convert.
    /// </param>
    /// <param name="order">
    /// A <see cref="ByteOrder"/> that indicates the byte order of the return.
    /// </param>
    /// <typeparam name="T">
    /// The type of <paramref name="value"/>. The T must be a value type.
    /// </typeparam>
    public static byte [] ToByteArray<T> (this T value, ByteOrder order)
      where T : struct
    {
      var type = typeof (T);
      var buffer = type == typeof (Boolean)
                 ? BitConverter.GetBytes ((Boolean)(object) value)
                 : type == typeof (Byte)
                   ? new byte [] { (Byte)(object) value }
                   : type == typeof (Char)
                     ? BitConverter.GetBytes ((Char)(object) value)
                     : type == typeof (Double)
                       ? BitConverter.GetBytes ((Double)(object) value)
                       : type == typeof (Int16)
                         ? BitConverter.GetBytes ((Int16)(object) value)
                         : type == typeof (Int32)
                           ? BitConverter.GetBytes ((Int32)(object) value)
                           : type == typeof (Int64)
                             ? BitConverter.GetBytes ((Int64)(object) value)
                             : type == typeof (Single)
                               ? BitConverter.GetBytes ((Single)(object) value)
                               : type == typeof (UInt16)
                                 ? BitConverter.GetBytes ((UInt16)(object) value)
                                 : type == typeof (UInt32)
                                   ? BitConverter.GetBytes ((UInt32)(object) value)
                                   : type == typeof (UInt64)
                                     ? BitConverter.GetBytes ((UInt64)(object) value)
                                     : new byte []{};

      return buffer.Length <= 1 || order.IsHostOrder ()
             ? buffer
             : buffer.Reverse ().ToArray ();
    }

    /// <summary>
    /// Converts the order of the specified array of <see cref="byte"/> to the host byte order.
    /// </summary>
    /// <returns>
    /// An array of <see cref="byte"/> converted from <paramref name="src"/>.
    /// </returns>
    /// <param name="src">
    /// An array of <see cref="byte"/> to convert.
    /// </param>
    /// <param name="srcOrder">
    /// A <see cref="WebSocketSharp.ByteOrder"/> that indicates the byte order of <paramref name="src"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="src"/> is <see langword="null"/>.
    /// </exception>
    public static byte [] ToHostOrder (this byte [] src, ByteOrder srcOrder)
    {
      if (src == null)
        throw new ArgumentNullException ("src");

      return src.Length <= 1 || srcOrder.IsHostOrder ()
             ? src
             : src.Reverse ().ToArray ();
    }

    /// <summary>
    /// Converts the specified <paramref name="array"/> to a <see cref="string"/> that concatenates
    /// the each element of <paramref name="array"/> across the specified <paramref name="separator"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> converted from <paramref name="array"/>, or a <see cref="String.Empty"/>
    /// if <paramref name="array"/> is empty.
    /// </returns>
    /// <param name="array">
    /// An array of T to convert.
    /// </param>
    /// <param name="separator">
    /// A <see cref="string"/> that contains a separator string.
    /// </param>
    /// <typeparam name="T">
    /// The type of elements in <paramref name="array"/>.
    /// </typeparam>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="array"/> is <see langword="null"/>.
    /// </exception>
    public static string ToString<T> (this T [] array, string separator)
    {
      if (array == null)
        throw new ArgumentNullException ("array");

      var len = array.Length;
      if (len == 0)
        return String.Empty;

      if (separator == null)
        separator = String.Empty;

      var buffer = new StringBuilder (64);
      (len - 1).Times (i =>
        buffer.AppendFormat ("{0}{1}", array [i].ToString (), separator)
      );

      buffer.Append (array [len - 1].ToString ());
      return buffer.ToString ();
    }

    /// <summary>
    /// Converts the specified <see cref="string"/> to a <see cref="Uri"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="Uri"/> converted from <paramref name="uriString"/>, or <see langword="null"/>
    /// if <paramref name="uriString"/> is <see langword="null"/> or <see cref="String.Empty"/>.
    /// </returns>
    /// <param name="uriString">
    /// A <see cref="string"/> to convert.
    /// </param>
    public static Uri ToUri (this string uriString)
    {
      return uriString == null || uriString.Length == 0
             ? null
             : uriString.MaybeUri ()
               ? new Uri (uriString)
               : new Uri (uriString, UriKind.Relative);
    }

    /// <summary>
    /// URL-decodes the specified <see cref="string"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that receives a decoded string, or the <paramref name="s"/>
    /// if <paramref name="s"/> is <see langword="null"/> or <see cref="String.Empty"/>.
    /// </returns>
    /// <param name="s">
    /// A <see cref="string"/> to decode.
    /// </param>
    public static string UrlDecode (this string s)
    {
      return s == null || s.Length == 0
             ? s
             : HttpUtility.UrlDecode (s);
    }

    /// <summary>
    /// URL-encodes the specified <see cref="string"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that receives a encoded string, or the <paramref name="s"/>
    /// if <paramref name="s"/> is <see langword="null"/> or <see cref="String.Empty"/>.
    /// </returns>
    /// <param name="s">
    /// A <see cref="string"/> to encode.
    /// </param>
    public static string UrlEncode (this string s)
    {
      return s == null || s.Length == 0
             ? s
             : HttpUtility.UrlEncode (s);
    }

    /// <summary>
    /// Writes the specified content data using the specified <see cref="HttpListenerResponse"/>.
    /// </summary>
    /// <param name="response">
    /// A <see cref="HttpListenerResponse"/> that contains a network stream to write a content data.
    /// </param>
    /// <param name="content">
    /// An array of <see cref="byte"/> that contains a content data to write.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="response"/> is <see langword="null"/>.
    /// </exception>
    public static void WriteContent (this HttpListenerResponse response, byte [] content)
    {
      if (response == null)
        throw new ArgumentNullException ("response");

      if (content == null || content.Length == 0)
        return;

      var output = response.OutputStream;
      response.ContentLength64 = content.Length;
      output.Write (content, 0, content.Length);
      output.Close ();
    }

    #endregion
  }
}
