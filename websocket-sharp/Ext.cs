#region License
/*
 * Ext.cs
 *
 * Some parts of this code are derived from Mono (http://www.mono-project.com):
 * - GetStatusDescription is derived from HttpListenerResponse.cs (System.Net)
 * - IsPredefinedScheme is derived from Uri.cs (System)
 * - MaybeUri is derived from Uri.cs (System)
 *
 * The MIT License
 *
 * Copyright (c) 2001 Garrett Rooney
 * Copyright (c) 2003 Ian MacLean
 * Copyright (c) 2003 Ben Maurer
 * Copyright (c) 2003, 2005, 2009 Novell, Inc. (http://www.novell.com)
 * Copyright (c) 2009 Stephane Delcroix
 * Copyright (c) 2010-2016 sta.blockhead
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

#region Contributors
/*
 * Contributors:
 * - Liryna <liryna.stark@gmail.com>
 * - Nikola Kovacevic <nikolak@outlook.com>
 * - Chris Swiedler
 */
#endregion

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.IO.Compression;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using WebSocketSharp.Net;
using WebSocketSharp.Net.WebSockets;
using WebSocketSharp.Server;

namespace WebSocketSharp
{
  /// <summary>
  /// Provides a set of static methods for websocket-sharp.
  /// </summary>
  public static class Ext
  {
    #region Private Fields

    private static readonly byte[] _last = new byte[] { 0x00 };
    private static readonly int    _retry = 5;
    private const string           _tspecials = "()<>@,;:\\\"/[]?={} \t";

    #endregion

    #region Private Methods

    private static byte[] compress (this byte[] data)
    {
      if (data.LongLength == 0)
        //return new byte[] { 0x00, 0x00, 0x00, 0xff, 0xff };
        return data;

      using (var input = new MemoryStream (data))
        return input.compressToArray ();
    }

    private static MemoryStream compress (this Stream stream)
    {
      var output = new MemoryStream ();
      if (stream.Length == 0)
        return output;

      stream.Position = 0;
      using (var ds = new DeflateStream (output, CompressionMode.Compress, true)) {
        stream.CopyTo (ds, 1024);
        ds.Close (); // BFINAL set to 1.
        output.Write (_last, 0, 1);
        output.Position = 0;

        return output;
      }
    }

    private static byte[] compressToArray (this Stream stream)
    {
      using (var output = stream.compress ()) {
        output.Close ();
        return output.ToArray ();
      }
    }

    private static byte[] decompress (this byte[] data)
    {
      if (data.LongLength == 0)
        return data;

      using (var input = new MemoryStream (data))
        return input.decompressToArray ();
    }

    private static MemoryStream decompress (this Stream stream)
    {
      var output = new MemoryStream ();
      if (stream.Length == 0)
        return output;

      stream.Position = 0;
      using (var ds = new DeflateStream (stream, CompressionMode.Decompress, true)) {
        ds.CopyTo (output, 1024);
        output.Position = 0;

        return output;
      }
    }

    private static byte[] decompressToArray (this Stream stream)
    {
      using (var output = stream.decompress ()) {
        output.Close ();
        return output.ToArray ();
      }
    }

    private static bool isHttpMethod (this string value)
    {
      return value == "GET"
             || value == "HEAD"
             || value == "POST"
             || value == "PUT"
             || value == "DELETE"
             || value == "CONNECT"
             || value == "OPTIONS"
             || value == "TRACE";
    }

    private static bool isHttpMethod10 (this string value)
    {
      return value == "GET"
             || value == "HEAD"
             || value == "POST";
    }

    #endregion

    #region Internal Methods

    internal static byte[] Append (this ushort code, string reason)
    {
      var bytes = code.InternalToByteArray (ByteOrder.Big);

      if (reason == null || reason.Length == 0)
        return bytes;

      var buff = new List<byte> (bytes);
      buff.AddRange (Encoding.UTF8.GetBytes (reason));

      return buff.ToArray ();
    }

    internal static byte[] Compress (this byte[] data, CompressionMethod method)
    {
      return method == CompressionMethod.Deflate
             ? data.compress ()
             : data;
    }

    internal static Stream Compress (this Stream stream, CompressionMethod method)
    {
      return method == CompressionMethod.Deflate
             ? stream.compress ()
             : stream;
    }

    internal static byte[] CompressToArray (this Stream stream, CompressionMethod method)
    {
      return method == CompressionMethod.Deflate
             ? stream.compressToArray ()
             : stream.ToByteArray ();
    }

    /// <summary>
    /// Determines whether the specified string contains any of characters in
    /// the specified array of <see cref="char"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="value"/> contains any of characters in
    /// <paramref name="anyOf"/>; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="value">
    /// A <see cref="string"/> to test.
    /// </param>
    /// <param name="anyOf">
    /// An array of <see cref="char"/> that contains one or more characters to
    /// seek.
    /// </param>
    internal static bool Contains (this string value, params char[] anyOf)
    {
      return anyOf != null && anyOf.Length > 0
             ? value.IndexOfAny (anyOf) > -1
             : false;
    }

    internal static bool Contains (
      this NameValueCollection collection, string name
    )
    {
      return collection[name] != null;
    }

    internal static bool Contains (
      this NameValueCollection collection,
      string name,
      string value,
      StringComparison comparisonTypeForValue
    )
    {
      var val = collection[name];
      if (val == null)
        return false;

      foreach (var elm in val.Split (',')) {
        if (elm.Trim ().Equals (value, comparisonTypeForValue))
          return true;
      }

      return false;
    }

    internal static bool Contains<T> (
      this IEnumerable<T> source, Func<T, bool> condition
    )
    {
      foreach (T elm in source) {
        if (condition (elm))
          return true;
      }

      return false;
    }

    internal static bool ContainsTwice (this string[] values)
    {
      var len = values.Length;
      var end = len - 1;

      Func<int, bool> seek = null;
      seek = idx => {
               if (idx == end)
                 return false;

               var val = values[idx];
               for (var i = idx + 1; i < len; i++) {
                 if (values[i] == val)
                   return true;
               }

               return seek (++idx);
             };

      return seek (0);
    }

    internal static T[] Copy<T> (this T[] source, int length)
    {
      var dest = new T[length];
      Array.Copy (source, 0, dest, 0, length);

      return dest;
    }

    internal static T[] Copy<T> (this T[] source, long length)
    {
      var dest = new T[length];
      Array.Copy (source, 0, dest, 0, length);

      return dest;
    }

    internal static void CopyTo (
      this Stream source, Stream destination, int bufferLength
    )
    {
      var buff = new byte[bufferLength];
      var nread = 0;

      while (true) {
        nread = source.Read (buff, 0, bufferLength);
        if (nread <= 0)
          break;

        destination.Write (buff, 0, nread);
      }
    }

    internal static void CopyToAsync (
      this Stream source,
      Stream destination,
      int bufferLength,
      Action completed,
      Action<Exception> error
    )
    {
      var buff = new byte[bufferLength];

      AsyncCallback callback = null;
      callback =
        ar => {
          try {
            var nread = source.EndRead (ar);
            if (nread <= 0) {
              if (completed != null)
                completed ();

              return;
            }

            destination.Write (buff, 0, nread);
            source.BeginRead (buff, 0, bufferLength, callback, null);
          }
          catch (Exception ex) {
            if (error != null)
              error (ex);
          }
        };

      try {
        source.BeginRead (buff, 0, bufferLength, callback, null);
      }
      catch (Exception ex) {
        if (error != null)
          error (ex);
      }
    }

    internal static byte[] Decompress (this byte[] data, CompressionMethod method)
    {
      return method == CompressionMethod.Deflate
             ? data.decompress ()
             : data;
    }

    internal static Stream Decompress (this Stream stream, CompressionMethod method)
    {
      return method == CompressionMethod.Deflate
             ? stream.decompress ()
             : stream;
    }

    internal static byte[] DecompressToArray (this Stream stream, CompressionMethod method)
    {
      return method == CompressionMethod.Deflate
             ? stream.decompressToArray ()
             : stream.ToByteArray ();
    }

    internal static void Emit (
      this EventHandler eventHandler, object sender, EventArgs e
    )
    {
      if (eventHandler == null)
        return;

      eventHandler (sender, e);
    }

    internal static void Emit<TEventArgs> (
      this EventHandler<TEventArgs> eventHandler, object sender, TEventArgs e
    )
      where TEventArgs : EventArgs
    {
      if (eventHandler == null)
        return;

      eventHandler (sender, e);
    }

    /// <summary>
    /// Determines whether the specified <see cref="int"/> equals the specified <see cref="char"/>,
    /// and invokes the specified <c>Action&lt;int&gt;</c> delegate at the same time.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="value"/> equals <paramref name="c"/>;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <param name="value">
    /// An <see cref="int"/> to compare.
    /// </param>
    /// <param name="c">
    /// A <see cref="char"/> to compare.
    /// </param>
    /// <param name="action">
    /// An <c>Action&lt;int&gt;</c> delegate that references the method(s) called
    /// at the same time as comparing. An <see cref="int"/> parameter to pass to
    /// the method(s) is <paramref name="value"/>.
    /// </param>
    internal static bool EqualsWith (this int value, char c, Action<int> action)
    {
      action (value);
      return value == c - 0;
    }

    /// <summary>
    /// Gets the absolute path from the specified <see cref="Uri"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that represents the absolute path if it's successfully found;
    /// otherwise, <see langword="null"/>.
    /// </returns>
    /// <param name="uri">
    /// A <see cref="Uri"/> that represents the URI to get the absolute path from.
    /// </param>
    internal static string GetAbsolutePath (this Uri uri)
    {
      if (uri.IsAbsoluteUri)
        return uri.AbsolutePath;

      var original = uri.OriginalString;
      if (original[0] != '/')
        return null;

      var idx = original.IndexOfAny (new[] { '?', '#' });
      return idx > 0 ? original.Substring (0, idx) : original;
    }

    internal static CookieCollection GetCookies (
      this NameValueCollection headers, bool response
    )
    {
      var val = headers[response ? "Set-Cookie" : "Cookie"];
      return val != null
             ? CookieCollection.Parse (val, response)
             : new CookieCollection ();
    }

    internal static string GetDnsSafeHost (this Uri uri, bool bracketIPv6)
    {
      return bracketIPv6 && uri.HostNameType == UriHostNameType.IPv6
             ? uri.Host
             : uri.DnsSafeHost;
    }

    internal static string GetMessage (this CloseStatusCode code)
    {
      return code == CloseStatusCode.ProtocolError
             ? "A WebSocket protocol error has occurred."
             : code == CloseStatusCode.UnsupportedData
               ? "Unsupported data has been received."
               : code == CloseStatusCode.Abnormal
                 ? "An exception has occurred."
                 : code == CloseStatusCode.InvalidData
                   ? "Invalid data has been received."
                   : code == CloseStatusCode.PolicyViolation
                     ? "A policy violation has occurred."
                     : code == CloseStatusCode.TooBig
                       ? "A too big message has been received."
                       : code == CloseStatusCode.MandatoryExtension
                         ? "WebSocket client didn't receive expected extension(s)."
                         : code == CloseStatusCode.ServerError
                           ? "WebSocket server got an internal error."
                           : code == CloseStatusCode.TlsHandshakeFailure
                             ? "An error has occurred during a TLS handshake."
                             : String.Empty;
    }

    /// <summary>
    /// Gets the name from the specified string that contains a pair of
    /// name and value separated by a character.
    /// </summary>
    /// <returns>
    ///   <para>
    ///   A <see cref="string"/> that represents the name.
    ///   </para>
    ///   <para>
    ///   <see langword="null"/> if the name is not present.
    ///   </para>
    /// </returns>
    /// <param name="nameAndValue">
    /// A <see cref="string"/> that contains a pair of name and value.
    /// </param>
    /// <param name="separator">
    /// A <see cref="char"/> used to separate name and value.
    /// </param>
    internal static string GetName (this string nameAndValue, char separator)
    {
      var idx = nameAndValue.IndexOf (separator);
      return idx > 0 ? nameAndValue.Substring (0, idx).Trim () : null;
    }

    internal static string GetUTF8DecodedString (this byte[] bytes)
    {
      return Encoding.UTF8.GetString (bytes);
    }

    internal static byte[] GetUTF8EncodedBytes (this string s)
    {
      return Encoding.UTF8.GetBytes (s);
    }

    /// <summary>
    /// Gets the value from the specified string that contains a pair of
    /// name and value separated by a character.
    /// </summary>
    /// <returns>
    ///   <para>
    ///   A <see cref="string"/> that represents the value.
    ///   </para>
    ///   <para>
    ///   <see langword="null"/> if the value is not present.
    ///   </para>
    /// </returns>
    /// <param name="nameAndValue">
    /// A <see cref="string"/> that contains a pair of name and value.
    /// </param>
    /// <param name="separator">
    /// A <see cref="char"/> used to separate name and value.
    /// </param>
    internal static string GetValue (this string nameAndValue, char separator)
    {
      return nameAndValue.GetValue (separator, false);
    }

    /// <summary>
    /// Gets the value from the specified string that contains a pair of
    /// name and value separated by a character.
    /// </summary>
    /// <returns>
    ///   <para>
    ///   A <see cref="string"/> that represents the value.
    ///   </para>
    ///   <para>
    ///   <see langword="null"/> if the value is not present.
    ///   </para>
    /// </returns>
    /// <param name="nameAndValue">
    /// A <see cref="string"/> that contains a pair of name and value.
    /// </param>
    /// <param name="separator">
    /// A <see cref="char"/> used to separate name and value.
    /// </param>
    /// <param name="unquote">
    /// A <see cref="bool"/>: <c>true</c> if unquotes the value; otherwise,
    /// <c>false</c>.
    /// </param>
    internal static string GetValue (
      this string nameAndValue, char separator, bool unquote
    )
    {
      var idx = nameAndValue.IndexOf (separator);
      if (idx < 0 || idx == nameAndValue.Length - 1)
        return null;

      var val = nameAndValue.Substring (idx + 1).Trim ();
      return unquote ? val.Unquote () : val;
    }

    internal static byte[] InternalToByteArray (
      this ushort value, ByteOrder order
    )
    {
      var ret = BitConverter.GetBytes (value);

      if (!order.IsHostOrder ())
        Array.Reverse (ret);

      return ret;
    }

    internal static byte[] InternalToByteArray (
      this ulong value, ByteOrder order
    )
    {
      var ret = BitConverter.GetBytes (value);

      if (!order.IsHostOrder ())
        Array.Reverse (ret);

      return ret;
    }

    internal static bool IsCompressionExtension (
      this string value, CompressionMethod method
    )
    {
      return value.StartsWith (method.ToExtensionString ());
    }

    internal static bool IsControl (this byte opcode)
    {
      return opcode > 0x7 && opcode < 0x10;
    }

    internal static bool IsControl (this Opcode opcode)
    {
      return opcode >= Opcode.Close;
    }

    internal static bool IsData (this byte opcode)
    {
      return opcode == 0x1 || opcode == 0x2;
    }

    internal static bool IsData (this Opcode opcode)
    {
      return opcode == Opcode.Text || opcode == Opcode.Binary;
    }

    internal static bool IsHttpMethod (this string value, Version version)
    {
      return version == HttpVersion.Version10
             ? value.isHttpMethod10 ()
             : value.isHttpMethod ();
    }

    internal static bool IsPortNumber (this int value)
    {
      return value > 0 && value < 65536;
    }

    internal static bool IsReserved (this ushort code)
    {
      return code == 1004
             || code == 1005
             || code == 1006
             || code == 1015;
    }

    internal static bool IsReserved (this CloseStatusCode code)
    {
      return code == CloseStatusCode.Undefined
             || code == CloseStatusCode.NoStatus
             || code == CloseStatusCode.Abnormal
             || code == CloseStatusCode.TlsHandshakeFailure;
    }

    internal static bool IsSupported (this byte opcode)
    {
      return Enum.IsDefined (typeof (Opcode), opcode);
    }

    internal static bool IsText (this string value)
    {
      var len = value.Length;

      for (var i = 0; i < len; i++) {
        var c = value[i];
        if (c < 0x20) {
          if ("\r\n\t".IndexOf (c) == -1)
            return false;

          if (c == '\n') {
            i++;
            if (i == len)
              break;

            c = value[i];
            if (" \t".IndexOf (c) == -1)
              return false;
          }

          continue;
        }

        if (c == 0x7f)
          return false;
      }

      return true;
    }

    internal static bool IsToken (this string value)
    {
      foreach (var c in value) {
        if (c < 0x20)
          return false;

        if (c > 0x7e)
          return false;

        if (_tspecials.IndexOf (c) > -1)
          return false;
      }

      return true;
    }

    internal static bool KeepsAlive (
      this NameValueCollection headers, Version version
    )
    {
      var comparison = StringComparison.OrdinalIgnoreCase;
      return version < HttpVersion.Version11
             ? headers.Contains ("Connection", "keep-alive", comparison)
             : !headers.Contains ("Connection", "close", comparison);
    }

    internal static string Quote (this string value)
    {
      return String.Format ("\"{0}\"", value.Replace ("\"", "\\\""));
    }

    internal static byte[] ReadBytes (this Stream stream, int length)
    {
      var buff = new byte[length];
      var offset = 0;
      var retry = 0;
      var nread = 0;

      while (length > 0) {
        nread = stream.Read (buff, offset, length);
        if (nread <= 0) {
          if (retry < _retry) {
            retry++;
            continue;
          }

          return buff.SubArray (0, offset);
        }

        retry = 0;

        offset += nread;
        length -= nread;
      }

      return buff;
    }

    internal static byte[] ReadBytes (
      this Stream stream, long length, int bufferLength
    )
    {
      using (var dest = new MemoryStream ()) {
        var buff = new byte[bufferLength];
        var retry = 0;
        var nread = 0;

        while (length > 0) {
          if (length < bufferLength)
            bufferLength = (int) length;

          nread = stream.Read (buff, 0, bufferLength);
          if (nread <= 0) {
            if (retry < _retry) {
              retry++;
              continue;
            }

            break;
          }

          retry = 0;

          dest.Write (buff, 0, nread);
          length -= nread;
        }

        dest.Close ();
        return dest.ToArray ();
      }
    }

    internal static void ReadBytesAsync (
      this Stream stream,
      int length,
      Action<byte[]> completed,
      Action<Exception> error
    )
    {
      var buff = new byte[length];
      var offset = 0;
      var retry = 0;

      AsyncCallback callback = null;
      callback =
        ar => {
          try {
            var nread = stream.EndRead (ar);
            if (nread <= 0) {
              if (retry < _retry) {
                retry++;
                stream.BeginRead (buff, offset, length, callback, null);

                return;
              }

              if (completed != null)
                completed (buff.SubArray (0, offset));

              return;
            }

            if (nread == length) {
              if (completed != null)
                completed (buff);

              return;
            }

            retry = 0;

            offset += nread;
            length -= nread;

            stream.BeginRead (buff, offset, length, callback, null);
          }
          catch (Exception ex) {
            if (error != null)
              error (ex);
          }
        };

      try {
        stream.BeginRead (buff, offset, length, callback, null);
      }
      catch (Exception ex) {
        if (error != null)
          error (ex);
      }
    }

    internal static void ReadBytesAsync (
      this Stream stream,
      long length,
      int bufferLength,
      Action<byte[]> completed,
      Action<Exception> error
    )
    {
      var dest = new MemoryStream ();
      var buff = new byte[bufferLength];
      var retry = 0;

      Action<long> read = null;
      read =
        len => {
          if (len < bufferLength)
            bufferLength = (int) len;

          stream.BeginRead (
            buff,
            0,
            bufferLength,
            ar => {
              try {
                var nread = stream.EndRead (ar);
                if (nread <= 0) {
                  if (retry < _retry) {
                    retry++;
                    read (len);

                    return;
                  }

                  if (completed != null) {
                    dest.Close ();
                    completed (dest.ToArray ());
                  }

                  dest.Dispose ();
                  return;
                }

                dest.Write (buff, 0, nread);

                if (nread == len) {
                  if (completed != null) {
                    dest.Close ();
                    completed (dest.ToArray ());
                  }

                  dest.Dispose ();
                  return;
                }

                retry = 0;

                read (len - nread);
              }
              catch (Exception ex) {
                dest.Dispose ();
                if (error != null)
                  error (ex);
              }
            },
            null
          );
        };

      try {
        read (length);
      }
      catch (Exception ex) {
        dest.Dispose ();
        if (error != null)
          error (ex);
      }
    }

    internal static T[] Reverse<T> (this T[] array)
    {
      var len = array.Length;
      var ret = new T[len];

      var end = len - 1;
      for (var i = 0; i <= end; i++)
        ret[i] = array[end - i];

      return ret;
    }

    internal static IEnumerable<string> SplitHeaderValue (
      this string value, params char[] separators
    )
    {
      var len = value.Length;
      var end = len - 1;

      var buff = new StringBuilder (32);
      var escaped = false;
      var quoted = false;

      for (var i = 0; i <= end; i++) {
        var c = value[i];
        buff.Append (c);

        if (c == '"') {
          if (escaped) {
            escaped = false;

            continue;
          }

          quoted = !quoted;

          continue;
        }

        if (c == '\\') {
          if (i == end)
            break;

          if (value[i + 1] == '"')
            escaped = true;

          continue;
        }

        if (Array.IndexOf (separators, c) > -1) {
          if (quoted)
            continue;

          buff.Length -= 1;

          yield return buff.ToString ();

          buff.Length = 0;

          continue;
        }
      }

      yield return buff.ToString ();
    }

    internal static byte[] ToByteArray (this Stream stream)
    {
      using (var output = new MemoryStream ()) {
        stream.Position = 0;
        stream.CopyTo (output, 1024);
        output.Close ();

        return output.ToArray ();
      }
    }

    internal static CompressionMethod ToCompressionMethod (this string value)
    {
      var methods = Enum.GetValues (typeof (CompressionMethod));
      foreach (CompressionMethod method in methods) {
        if (method.ToExtensionString () == value)
          return method;
      }

      return CompressionMethod.None;
    }

    internal static string ToExtensionString (
      this CompressionMethod method, params string[] parameters
    )
    {
      if (method == CompressionMethod.None)
        return String.Empty;

      var name = String.Format (
                   "permessage-{0}", method.ToString ().ToLower ()
                 );

      return parameters != null && parameters.Length > 0
             ? String.Format ("{0}; {1}", name, parameters.ToString ("; "))
             : name;
    }

    internal static System.Net.IPAddress ToIPAddress (this string value)
    {
      if (value == null || value.Length == 0)
        return null;

      System.Net.IPAddress addr;
      if (System.Net.IPAddress.TryParse (value, out addr))
        return addr;

      try {
        var addrs = System.Net.Dns.GetHostAddresses (value);
        return addrs[0];
      }
      catch {
        return null;
      }
    }

    internal static List<TSource> ToList<TSource> (
      this IEnumerable<TSource> source
    )
    {
      return new List<TSource> (source);
    }

    internal static string ToString (
      this System.Net.IPAddress address, bool bracketIPv6
    )
    {
      return bracketIPv6
             && address.AddressFamily == AddressFamily.InterNetworkV6
             ? String.Format ("[{0}]", address.ToString ())
             : address.ToString ();
    }

    internal static ushort ToUInt16 (this byte[] source, ByteOrder sourceOrder)
    {
      return BitConverter.ToUInt16 (source.ToHostOrder (sourceOrder), 0);
    }

    internal static ulong ToUInt64 (this byte[] source, ByteOrder sourceOrder)
    {
      return BitConverter.ToUInt64 (source.ToHostOrder (sourceOrder), 0);
    }

    internal static IEnumerable<string> TrimEach (
      this IEnumerable<string> source
    )
    {
      foreach (var elm in source)
        yield return elm.Trim ();
    }

    internal static string TrimSlashFromEnd (this string value)
    {
      var ret = value.TrimEnd ('/');
      return ret.Length > 0 ? ret : "/";
    }

    internal static string TrimSlashOrBackslashFromEnd (this string value)
    {
      var ret = value.TrimEnd ('/', '\\');
      return ret.Length > 0 ? ret : value[0].ToString ();
    }

    internal static bool TryCreateVersion (
      this string versionString, out Version result
    )
    {
      result = null;

      try {
        result = new Version (versionString);
      }
      catch {
        return false;
      }

      return true;
    }

    /// <summary>
    /// Tries to create a new <see cref="Uri"/> for WebSocket with
    /// the specified <paramref name="uriString"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the <see cref="Uri"/> was successfully created;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <param name="uriString">
    /// A <see cref="string"/> that represents a WebSocket URL to try.
    /// </param>
    /// <param name="result">
    /// When this method returns, a <see cref="Uri"/> that
    /// represents the WebSocket URL or <see langword="null"/>
    /// if <paramref name="uriString"/> is invalid.
    /// </param>
    /// <param name="message">
    /// When this method returns, a <see cref="string"/> that
    /// represents an error message or <see langword="null"/>
    /// if <paramref name="uriString"/> is valid.
    /// </param>
    internal static bool TryCreateWebSocketUri (
      this string uriString, out Uri result, out string message
    )
    {
      result = null;
      message = null;

      var uri = uriString.ToUri ();
      if (uri == null) {
        message = "An invalid URI string.";
        return false;
      }

      if (!uri.IsAbsoluteUri) {
        message = "A relative URI.";
        return false;
      }

      var schm = uri.Scheme;
      if (!(schm == "ws" || schm == "wss")) {
        message = "The scheme part is not 'ws' or 'wss'.";
        return false;
      }

      var port = uri.Port;
      if (port == 0) {
        message = "The port part is zero.";
        return false;
      }

      if (uri.Fragment.Length > 0) {
        message = "It includes the fragment component.";
        return false;
      }

      result = port != -1
               ? uri
               : new Uri (
                   String.Format (
                     "{0}://{1}:{2}{3}",
                     schm,
                     uri.Host,
                     schm == "ws" ? 80 : 443,
                     uri.PathAndQuery
                   )
                 );

      return true;
    }

    internal static bool TryGetUTF8DecodedString (this byte[] bytes, out string s)
    {
      s = null;

      try {
        s = Encoding.UTF8.GetString (bytes);
      }
      catch {
        return false;
      }

      return true;
    }

    internal static bool TryGetUTF8EncodedBytes (this string s, out byte[] bytes)
    {
      bytes = null;

      try {
        bytes = Encoding.UTF8.GetBytes (s);
      }
      catch {
        return false;
      }

      return true;
    }

    internal static bool TryOpenRead (
      this FileInfo fileInfo, out FileStream fileStream
    )
    {
      fileStream = null;

      try {
        fileStream = fileInfo.OpenRead ();
      }
      catch {
        return false;
      }

      return true;
    }

    internal static string Unquote (this string value)
    {
      var start = value.IndexOf ('"');
      if (start == -1)
        return value;

      var end = value.LastIndexOf ('"');
      if (end == start)
        return value;

      var len = end - start - 1;
      return len > 0
             ? value.Substring (start + 1, len).Replace ("\\\"", "\"")
             : String.Empty;
    }

    internal static bool Upgrades (
      this NameValueCollection headers, string protocol
    )
    {
      var comparison = StringComparison.OrdinalIgnoreCase;
      return headers.Contains ("Upgrade", protocol, comparison)
             && headers.Contains ("Connection", "Upgrade", comparison);
    }

    internal static string UrlDecode (this string value, Encoding encoding)
    {
      return HttpUtility.UrlDecode (value, encoding);
    }

    internal static string UrlEncode (this string value, Encoding encoding)
    {
      return HttpUtility.UrlEncode (value, encoding);
    }

    internal static void WriteBytes (
      this Stream stream, byte[] bytes, int bufferLength
    )
    {
      using (var src = new MemoryStream (bytes))
        src.CopyTo (stream, bufferLength);
    }

    internal static void WriteBytesAsync (
      this Stream stream,
      byte[] bytes,
      int bufferLength,
      Action completed,
      Action<Exception> error
    )
    {
      var src = new MemoryStream (bytes);
      src.CopyToAsync (
        stream,
        bufferLength,
        () => {
          if (completed != null)
            completed ();

          src.Dispose ();
        },
        ex => {
          src.Dispose ();
          if (error != null)
            error (ex);
        }
      );
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Gets the description of the specified HTTP status code.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that represents the description of
    /// the HTTP status code.
    /// </returns>
    /// <param name="code">
    ///   <para>
    ///   One of the <see cref="HttpStatusCode"/> enum values.
    ///   </para>
    ///   <para>
    ///   It specifies the HTTP status code.
    ///   </para>
    /// </param>
    public static string GetDescription (this HttpStatusCode code)
    {
      return ((int) code).GetStatusDescription ();
    }

    /// <summary>
    /// Gets the description of the specified HTTP status code.
    /// </summary>
    /// <returns>
    ///   <para>
    ///   A <see cref="string"/> that represents the description of
    ///   the HTTP status code.
    ///   </para>
    ///   <para>
    ///   An empty string if the description is not present.
    ///   </para>
    /// </returns>
    /// <param name="code">
    /// An <see cref="int"/> that specifies the HTTP status code.
    /// </param>
    public static string GetStatusDescription (this int code)
    {
      switch (code) {
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
    /// Determines whether the specified ushort is in the range of
    /// the status code for the WebSocket connection close.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   The ranges are the following:
    ///   </para>
    ///   <list type="bullet">
    ///     <item>
    ///       <term>
    ///       1000-2999: These numbers are reserved for definition by
    ///       the WebSocket protocol.
    ///       </term>
    ///     </item>
    ///     <item>
    ///       <term>
    ///       3000-3999: These numbers are reserved for use by libraries,
    ///       frameworks, and applications.
    ///       </term>
    ///     </item>
    ///     <item>
    ///       <term>
    ///       4000-4999: These numbers are reserved for private use.
    ///       </term>
    ///     </item>
    ///   </list>
    /// </remarks>
    /// <returns>
    /// <c>true</c> if <paramref name="value"/> is in the range of
    /// the status code for the close; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="value">
    /// A <see cref="ushort"/> to test.
    /// </param>
    public static bool IsCloseStatusCode (this ushort value)
    {
      return value > 999 && value < 5000;
    }

    /// <summary>
    /// Determines whether the specified string is enclosed in
    /// the specified character.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="value"/> is enclosed in
    /// <paramref name="c"/>; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="value">
    /// A <see cref="string"/> to test.
    /// </param>
    /// <param name="c">
    /// A <see cref="char"/> to find.
    /// </param>
    public static bool IsEnclosedIn (this string value, char c)
    {
      if (value == null)
        return false;

      var len = value.Length;
      if (len < 2)
        return false;

      return value[0] == c && value[len - 1] == c;
    }

    /// <summary>
    /// Determines whether the specified byte order is host (this computer
    /// architecture) byte order.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="order"/> is host byte order; otherwise,
    /// <c>false</c>.
    /// </returns>
    /// <param name="order">
    /// One of the <see cref="ByteOrder"/> enum values to test.
    /// </param>
    public static bool IsHostOrder (this ByteOrder order)
    {
      // true: !(true ^ true) or !(false ^ false)
      // false: !(true ^ false) or !(false ^ true)
      return !(BitConverter.IsLittleEndian ^ (order == ByteOrder.Little));
    }

    /// <summary>
    /// Determines whether the specified IP address is a local IP address.
    /// </summary>
    /// <remarks>
    /// This local means NOT REMOTE for the current host.
    /// </remarks>
    /// <returns>
    /// <c>true</c> if <paramref name="address"/> is a local IP address;
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

      if (address.Equals (System.Net.IPAddress.Any))
        return true;

      if (address.Equals (System.Net.IPAddress.Loopback))
        return true;

      if (Socket.OSSupportsIPv6) {
        if (address.Equals (System.Net.IPAddress.IPv6Any))
          return true;

        if (address.Equals (System.Net.IPAddress.IPv6Loopback))
          return true;
      }

      var host = System.Net.Dns.GetHostName ();
      var addrs = System.Net.Dns.GetHostAddresses (host);
      foreach (var addr in addrs) {
        if (address.Equals (addr))
          return true;
      }

      return false;
    }

    /// <summary>
    /// Determines whether the specified string is <see langword="null"/> or
    /// an empty string.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="value"/> is <see langword="null"/> or
    /// an empty string; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="value">
    /// A <see cref="string"/> to test.
    /// </param>
    public static bool IsNullOrEmpty (this string value)
    {
      return value == null || value.Length == 0;
    }

    /// <summary>
    /// Determines whether the specified string is a predefined scheme.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="value"/> is a predefined scheme;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <param name="value">
    /// A <see cref="string"/> to test.
    /// </param>
    public static bool IsPredefinedScheme (this string value)
    {
      if (value == null || value.Length < 2)
        return false;

      var c = value[0];
      if (c == 'h')
        return value == "http" || value == "https";

      if (c == 'w')
        return value == "ws" || value == "wss";

      if (c == 'f')
        return value == "file" || value == "ftp";

      if (c == 'g')
        return value == "gopher";

      if (c == 'm')
        return value == "mailto";

      if (c == 'n') {
        c = value[1];
        return c == 'e'
               ? value == "news" || value == "net.pipe" || value == "net.tcp"
               : value == "nntp";
      }

      return false;
    }

    /// <summary>
    /// Determines whether the specified string is a URI string.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="value"/> may be a URI string;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <param name="value">
    /// A <see cref="string"/> to test.
    /// </param>
    public static bool MaybeUri (this string value)
    {
      if (value == null)
        return false;

      if (value.Length == 0)
        return false;

      var idx = value.IndexOf (':');
      if (idx == -1)
        return false;

      if (idx >= 10)
        return false;

      var schm = value.Substring (0, idx);
      return schm.IsPredefinedScheme ();
    }

    /// <summary>
    /// Retrieves a sub-array from the specified array. A sub-array starts at
    /// the specified index in the array.
    /// </summary>
    /// <returns>
    /// An array of T that receives a sub-array.
    /// </returns>
    /// <param name="array">
    /// An array of T from which to retrieve a sub-array.
    /// </param>
    /// <param name="startIndex">
    /// An <see cref="int"/> that represents the zero-based index in the array
    /// at which retrieving starts.
    /// </param>
    /// <param name="length">
    /// An <see cref="int"/> that represents the number of elements to retrieve.
    /// </param>
    /// <typeparam name="T">
    /// The type of elements in the array.
    /// </typeparam>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="array"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   <para>
    ///   <paramref name="startIndex"/> is less than zero.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="startIndex"/> is greater than the end of the array.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="length"/> is less than zero.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="length"/> is greater than the number of elements from
    ///   <paramref name="startIndex"/> to the end of the array.
    ///   </para>
    /// </exception>
    public static T[] SubArray<T> (this T[] array, int startIndex, int length)
    {
      if (array == null)
        throw new ArgumentNullException ("array");

      var len = array.Length;
      if (len == 0) {
        if (startIndex != 0)
          throw new ArgumentOutOfRangeException ("startIndex");

        if (length != 0)
          throw new ArgumentOutOfRangeException ("length");

        return array;
      }

      if (startIndex < 0 || startIndex >= len)
        throw new ArgumentOutOfRangeException ("startIndex");

      if (length < 0 || length > len - startIndex)
        throw new ArgumentOutOfRangeException ("length");

      if (length == 0)
        return new T[0];

      if (length == len)
        return array;

      var subArray = new T[length];
      Array.Copy (array, startIndex, subArray, 0, length);

      return subArray;
    }

    /// <summary>
    /// Retrieves a sub-array from the specified array. A sub-array starts at
    /// the specified index in the array.
    /// </summary>
    /// <returns>
    /// An array of T that receives a sub-array.
    /// </returns>
    /// <param name="array">
    /// An array of T from which to retrieve a sub-array.
    /// </param>
    /// <param name="startIndex">
    /// A <see cref="long"/> that represents the zero-based index in the array
    /// at which retrieving starts.
    /// </param>
    /// <param name="length">
    /// A <see cref="long"/> that represents the number of elements to retrieve.
    /// </param>
    /// <typeparam name="T">
    /// The type of elements in the array.
    /// </typeparam>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="array"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///   <para>
    ///   <paramref name="startIndex"/> is less than zero.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="startIndex"/> is greater than the end of the array.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="length"/> is less than zero.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="length"/> is greater than the number of elements from
    ///   <paramref name="startIndex"/> to the end of the array.
    ///   </para>
    /// </exception>
    public static T[] SubArray<T> (this T[] array, long startIndex, long length)
    {
      if (array == null)
        throw new ArgumentNullException ("array");

      var len = array.LongLength;
      if (len == 0) {
        if (startIndex != 0)
          throw new ArgumentOutOfRangeException ("startIndex");

        if (length != 0)
          throw new ArgumentOutOfRangeException ("length");

        return array;
      }

      if (startIndex < 0 || startIndex >= len)
        throw new ArgumentOutOfRangeException ("startIndex");

      if (length < 0 || length > len - startIndex)
        throw new ArgumentOutOfRangeException ("length");

      if (length == 0)
        return new T[0];

      if (length == len)
        return array;

      var subArray = new T[length];
      Array.Copy (array, startIndex, subArray, 0, length);

      return subArray;
    }

    /// <summary>
    /// Executes the specified delegate <paramref name="n"/> times.
    /// </summary>
    /// <param name="n">
    /// An <see cref="int"/> that specifies the number of times to execute.
    /// </param>
    /// <param name="action">
    /// An <see cref="Action"/> delegate to execute.
    /// </param>
    public static void Times (this int n, Action action)
    {
      if (n <= 0)
        return;

      if (action == null)
        return;

      for (int i = 0; i < n; i++)
        action ();
    }

    /// <summary>
    /// Executes the specified delegate <paramref name="n"/> times.
    /// </summary>
    /// <param name="n">
    /// A <see cref="long"/> that specifies the number of times to execute.
    /// </param>
    /// <param name="action">
    /// An <see cref="Action"/> delegate to execute.
    /// </param>
    public static void Times (this long n, Action action)
    {
      if (n <= 0)
        return;

      if (action == null)
        return;

      for (long i = 0; i < n; i++)
        action ();
    }

    /// <summary>
    /// Executes the specified delegate <paramref name="n"/> times.
    /// </summary>
    /// <param name="n">
    /// A <see cref="uint"/> that specifies the number of times to execute.
    /// </param>
    /// <param name="action">
    /// An <see cref="Action"/> delegate to execute.
    /// </param>
    public static void Times (this uint n, Action action)
    {
      if (n == 0)
        return;

      if (action == null)
        return;

      for (uint i = 0; i < n; i++)
        action ();
    }

    /// <summary>
    /// Executes the specified delegate <paramref name="n"/> times.
    /// </summary>
    /// <param name="n">
    /// A <see cref="ulong"/> that specifies the number of times to execute.
    /// </param>
    /// <param name="action">
    /// An <see cref="Action"/> delegate to execute.
    /// </param>
    public static void Times (this ulong n, Action action)
    {
      if (n == 0)
        return;

      if (action == null)
        return;

      for (ulong i = 0; i < n; i++)
        action ();
    }

    /// <summary>
    /// Executes the specified delegate <paramref name="n"/> times.
    /// </summary>
    /// <param name="n">
    /// An <see cref="int"/> that specifies the number of times to execute.
    /// </param>
    /// <param name="action">
    ///   <para>
    ///   An <c>Action&lt;int&gt;</c> delegate to execute.
    ///   </para>
    ///   <para>
    ///   The <see cref="int"/> parameter is the zero-based count of iteration.
    ///   </para>
    /// </param>
    public static void Times (this int n, Action<int> action)
    {
      if (n <= 0)
        return;

      if (action == null)
        return;

      for (int i = 0; i < n; i++)
        action (i);
    }

    /// <summary>
    /// Executes the specified delegate <paramref name="n"/> times.
    /// </summary>
    /// <param name="n">
    /// A <see cref="long"/> that specifies the number of times to execute.
    /// </param>
    /// <param name="action">
    ///   <para>
    ///   An <c>Action&lt;long&gt;</c> delegate to execute.
    ///   </para>
    ///   <para>
    ///   The <see cref="long"/> parameter is the zero-based count of iteration.
    ///   </para>
    /// </param>
    public static void Times (this long n, Action<long> action)
    {
      if (n <= 0)
        return;

      if (action == null)
        return;

      for (long i = 0; i < n; i++)
        action (i);
    }

    /// <summary>
    /// Executes the specified delegate <paramref name="n"/> times.
    /// </summary>
    /// <param name="n">
    /// A <see cref="uint"/> that specifies the number of times to execute.
    /// </param>
    /// <param name="action">
    ///   <para>
    ///   An <c>Action&lt;uint&gt;</c> delegate to execute.
    ///   </para>
    ///   <para>
    ///   The <see cref="uint"/> parameter is the zero-based count of iteration.
    ///   </para>
    /// </param>
    public static void Times (this uint n, Action<uint> action)
    {
      if (n == 0)
        return;

      if (action == null)
        return;

      for (uint i = 0; i < n; i++)
        action (i);
    }

    /// <summary>
    /// Executes the specified delegate <paramref name="n"/> times.
    /// </summary>
    /// <param name="n">
    /// A <see cref="ulong"/> that specifies the number of times to execute.
    /// </param>
    /// <param name="action">
    ///   <para>
    ///   An <c>Action&lt;ulong&gt;</c> delegate to execute.
    ///   </para>
    ///   <para>
    ///   The <see cref="ulong"/> parameter is the zero-based count of iteration.
    ///   </para>
    /// </param>
    public static void Times (this ulong n, Action<ulong> action)
    {
      if (n == 0)
        return;

      if (action == null)
        return;

      for (ulong i = 0; i < n; i++)
        action (i);
    }

    /// <summary>
    /// Converts the specified byte array to the specified type value.
    /// </summary>
    /// <returns>
    ///   <para>
    ///   A T converted from <paramref name="source"/>.
    ///   </para>
    ///   <para>
    ///   The default value of T if not converted.
    ///   </para>
    /// </returns>
    /// <param name="source">
    /// An array of <see cref="byte"/> to convert.
    /// </param>
    /// <param name="sourceOrder">
    ///   <para>
    ///   One of the <see cref="ByteOrder"/> enum values.
    ///   </para>
    ///   <para>
    ///   It specifies the byte order of <paramref name="source"/>.
    ///   </para>
    /// </param>
    /// <typeparam name="T">
    ///   <para>
    ///   The type of the return.
    ///   </para>
    ///   <para>
    ///   <see cref="bool"/>, <see cref="char"/>, <see cref="double"/>,
    ///   <see cref="float"/>, <see cref="int"/>, <see cref="long"/>,
    ///   <see cref="short"/>, <see cref="uint"/>, <see cref="ulong"/>,
    ///   or <see cref="ushort"/>.
    ///   </para>
    /// </typeparam>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> is <see langword="null"/>.
    /// </exception>
    [Obsolete ("This method will be removed.")]
    public static T To<T> (this byte[] source, ByteOrder sourceOrder)
      where T : struct
    {
      if (source == null)
        throw new ArgumentNullException ("source");

      if (source.Length == 0)
        return default (T);

      var type = typeof (T);
      var val = source.ToHostOrder (sourceOrder);

      return type == typeof (Boolean)
             ? (T)(object) BitConverter.ToBoolean (val, 0)
             : type == typeof (Char)
               ? (T)(object) BitConverter.ToChar (val, 0)
               : type == typeof (Double)
                 ? (T)(object) BitConverter.ToDouble (val, 0)
                 : type == typeof (Int16)
                   ? (T)(object) BitConverter.ToInt16 (val, 0)
                   : type == typeof (Int32)
                     ? (T)(object) BitConverter.ToInt32 (val, 0)
                     : type == typeof (Int64)
                       ? (T)(object) BitConverter.ToInt64 (val, 0)
                       : type == typeof (Single)
                         ? (T)(object) BitConverter.ToSingle (val, 0)
                         : type == typeof (UInt16)
                           ? (T)(object) BitConverter.ToUInt16 (val, 0)
                           : type == typeof (UInt32)
                             ? (T)(object) BitConverter.ToUInt32 (val, 0)
                             : type == typeof (UInt64)
                               ? (T)(object) BitConverter.ToUInt64 (val, 0)
                               : default (T);
    }

    /// <summary>
    /// Converts the specified value to a byte array.
    /// </summary>
    /// <returns>
    /// An array of <see cref="byte"/> converted from <paramref name="value"/>.
    /// </returns>
    /// <param name="value">
    /// A T to convert.
    /// </param>
    /// <param name="order">
    ///   <para>
    ///   One of the <see cref="ByteOrder"/> enum values.
    ///   </para>
    ///   <para>
    ///   It specifies the byte order of the return.
    ///   </para>
    /// </param>
    /// <typeparam name="T">
    ///   <para>
    ///   The type of <paramref name="value"/>.
    ///   </para>
    ///   <para>
    ///   <see cref="bool"/>, <see cref="byte"/>, <see cref="char"/>,
    ///   <see cref="double"/>, <see cref="float"/>, <see cref="int"/>,
    ///   <see cref="long"/>, <see cref="short"/>, <see cref="uint"/>,
    ///   <see cref="ulong"/>, or <see cref="ushort"/>.
    ///   </para>
    /// </typeparam>
    [Obsolete ("This method will be removed.")]
    public static byte[] ToByteArray<T> (this T value, ByteOrder order)
      where T : struct
    {
      var type = typeof (T);
      var bytes = type == typeof (Boolean)
                  ? BitConverter.GetBytes ((Boolean)(object) value)
                  : type == typeof (Byte)
                    ? new byte[] { (Byte)(object) value }
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
                                      : WebSocket.EmptyBytes;

      if (bytes.Length > 1) {
        if (!order.IsHostOrder ())
          Array.Reverse (bytes);
      }

      return bytes;
    }

    /// <summary>
    /// Converts the order of elements in the specified byte array to
    /// host (this computer architecture) byte order.
    /// </summary>
    /// <returns>
    ///   <para>
    ///   An array of <see cref="byte"/> converted from
    ///   <paramref name="source"/>.
    ///   </para>
    ///   <para>
    ///   <paramref name="source"/> if the number of elements in
    ///   it is less than 2 or <paramref name="sourceOrder"/> is
    ///   same as host byte order.
    ///   </para>
    /// </returns>
    /// <param name="source">
    /// An array of <see cref="byte"/> to convert.
    /// </param>
    /// <param name="sourceOrder">
    ///   <para>
    ///   One of the <see cref="ByteOrder"/> enum values.
    ///   </para>
    ///   <para>
    ///   It specifies the order of elements in <paramref name="source"/>.
    ///   </para>
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> is <see langword="null"/>.
    /// </exception>
    public static byte[] ToHostOrder (this byte[] source, ByteOrder sourceOrder)
    {
      if (source == null)
        throw new ArgumentNullException ("source");

      if (source.Length < 2)
        return source;

      if (sourceOrder.IsHostOrder ())
        return source;

      return source.Reverse ();
    }

    /// <summary>
    /// Converts the specified array to a string.
    /// </summary>
    /// <returns>
    ///   <para>
    ///   A <see cref="string"/> converted by concatenating each element of
    ///   <paramref name="array"/> across <paramref name="separator"/>.
    ///   </para>
    ///   <para>
    ///   An empty string if <paramref name="array"/> is an empty array.
    ///   </para>
    /// </returns>
    /// <param name="array">
    /// An array of T to convert.
    /// </param>
    /// <param name="separator">
    /// A <see cref="string"/> used to separate each element of
    /// <paramref name="array"/>.
    /// </param>
    /// <typeparam name="T">
    /// The type of elements in <paramref name="array"/>.
    /// </typeparam>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="array"/> is <see langword="null"/>.
    /// </exception>
    public static string ToString<T> (this T[] array, string separator)
    {
      if (array == null)
        throw new ArgumentNullException ("array");

      var len = array.Length;
      if (len == 0)
        return String.Empty;

      if (separator == null)
        separator = String.Empty;

      var buff = new StringBuilder (64);
      var end = len - 1;

      for (var i = 0; i < end; i++)
        buff.AppendFormat ("{0}{1}", array[i], separator);

      buff.Append (array[end].ToString ());
      return buff.ToString ();
    }

    /// <summary>
    /// Converts the specified string to a <see cref="Uri"/>.
    /// </summary>
    /// <returns>
    ///   <para>
    ///   A <see cref="Uri"/> converted from <paramref name="value"/>.
    ///   </para>
    ///   <para>
    ///   <see langword="null"/> if the conversion has failed.
    ///   </para>
    /// </returns>
    /// <param name="value">
    /// A <see cref="string"/> to convert.
    /// </param>
    public static Uri ToUri (this string value)
    {
      Uri ret;
      Uri.TryCreate (
        value, value.MaybeUri () ? UriKind.Absolute : UriKind.Relative, out ret
      );

      return ret;
    }

    /// <summary>
    /// Sends the specified content data with the HTTP response.
    /// </summary>
    /// <param name="response">
    /// A <see cref="HttpListenerResponse"/> that represents the HTTP response
    /// used to send the content data.
    /// </param>
    /// <param name="content">
    /// An array of <see cref="byte"/> that specifies the content data to send.
    /// </param>
    /// <exception cref="ArgumentNullException">
    ///   <para>
    ///   <paramref name="response"/> is <see langword="null"/>.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="content"/> is <see langword="null"/>.
    ///   </para>
    /// </exception>
    [Obsolete ("This method will be removed.")]
    public static void WriteContent (
      this HttpListenerResponse response, byte[] content
    )
    {
      if (response == null)
        throw new ArgumentNullException ("response");

      if (content == null)
        throw new ArgumentNullException ("content");

      var len = content.LongLength;
      if (len == 0) {
        response.Close ();
        return;
      }

      response.ContentLength64 = len;

      var output = response.OutputStream;

      if (len <= Int32.MaxValue)
        output.Write (content, 0, (int) len);
      else
        output.WriteBytes (content, 1024);

      output.Close ();
    }

    #endregion
  }
}
