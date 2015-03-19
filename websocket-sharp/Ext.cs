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
 * Copyright (c) 2010-2015 sta.blockhead
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

    private const string _tspecials = "()<>@,;:\\\"/[]?={} \t";

    #endregion

    #region Private Methods

    private static byte[] compress (this byte[] data)
    {
      if (data.LongLength == 0)
        //return new Byte[] { 0x00, 0x00, 0x00, 0xff, 0xff };
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
        stream.CopyTo (ds);
        ds.Close (); // "BFINAL" set to 1.
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
        ds.CopyTo (output);
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

    private static byte[] readBytes (this Stream stream, byte[] buffer, int offset, int length)
    {
      var len = 0;
      try {
        len = stream.Read (buffer, offset, length);
        if (len < 1)
          return buffer.SubArray (0, offset);

        while (len < length) {
          var readLen = stream.Read (buffer, offset + len, length - len);
          if (readLen < 1)
            break;

          len += readLen;
        }
      }
      catch {
      }

      return len < length
             ? buffer.SubArray (0, offset + len)
             : buffer;
    }

    private static bool readBytes (
      this Stream stream, byte[] buffer, int offset, int length, Stream destination)
    {
      var bytes = stream.readBytes (buffer, offset, length);
      var len = bytes.Length;
      destination.Write (bytes, 0, len);

      return len == offset + length;
    }

    private static void times (this ulong n, Action action)
    {
      for (ulong i = 0; i < n; i++)
        action ();
    }

    #endregion

    #region Internal Methods

    internal static byte[] Append (this ushort code, string reason)
    {
      using (var buff = new MemoryStream ()) {
        var bytes = code.InternalToByteArray (ByteOrder.Big);
        buff.Write (bytes, 0, 2);
        if (reason != null && reason.Length > 0) {
          bytes = Encoding.UTF8.GetBytes (reason);
          buff.Write (bytes, 0, bytes.Length);
        }

        buff.Close ();
        return buff.ToArray ();
      }
    }

    internal static string CheckIfCanRead (this Stream stream)
    {
      return stream == null
             ? "'stream' is null."
             : !stream.CanRead
               ? "'stream' cannot be read."
               : null;
    }

    internal static string CheckIfClosable (this WebSocketState state)
    {
      return state == WebSocketState.Closing
             ? "While closing the WebSocket connection."
             : state == WebSocketState.Closed
               ? "The WebSocket connection has already been closed."
               : null;
    }

    internal static string CheckIfConnectable (this WebSocketState state)
    {
      return state == WebSocketState.Open || state == WebSocketState.Closing
             ? "A WebSocket connection has already been established."
             : null;
    }

    internal static string CheckIfOpen (this WebSocketState state)
    {
      return state == WebSocketState.Connecting
             ? "A WebSocket connection isn't established."
             : state == WebSocketState.Closing
               ? "While closing the WebSocket connection."
               : state == WebSocketState.Closed
                 ? "The WebSocket connection has already been closed."
                 : null;
    }

    internal static string CheckIfStart (this ServerState state)
    {
      return state == ServerState.Ready
             ? "The server hasn't yet started."
             : state == ServerState.ShuttingDown
               ? "The server is shutting down."
               : state == ServerState.Stop
                 ? "The server has already stopped."
                 : null;
    }

    internal static string CheckIfStartable (this ServerState state)
    {
      return state == ServerState.Start
             ? "The server has already started."
             : state == ServerState.ShuttingDown
               ? "The server is shutting down."
               : null;
    }

    internal static string CheckIfValidCloseParameters (this ushort code, string reason)
    {
      return !code.IsCloseStatusCode ()
             ? "An invalid close status code."
             : code.IsNoStatusCode () && !reason.IsNullOrEmpty ()
               ? "NoStatusCode cannot have a reason."
               : !reason.IsNullOrEmpty () && Encoding.UTF8.GetBytes (reason).Length > 123
                 ? "A reason has greater than the allowable max size."
                 : null;
    }

    internal static string CheckIfValidCloseParameters (this CloseStatusCode code, string reason)
    {
      return code.IsNoStatusCode () && !reason.IsNullOrEmpty ()
             ? "NoStatusCode cannot have a reason."
             : !reason.IsNullOrEmpty () && Encoding.UTF8.GetBytes (reason).Length > 123
               ? "A reason has greater than the allowable max size."
               : null;
    }

    internal static string CheckIfValidCloseStatusCode (this ushort code)
    {
      return !code.IsCloseStatusCode ()
             ? "An invalid close status code."
             : null;
    }

    internal static string CheckIfValidControlData (this byte[] data, string paramName)
    {
      return data.Length > 125
             ? String.Format ("'{0}' has greater than the allowable max size.", paramName)
             : null;
    }

    internal static string CheckIfValidProtocols (this string[] protocols)
    {
      return protocols.Contains (
               protocol => protocol == null || protocol.Length == 0 || !protocol.IsToken ())
             ? "Contains an invalid value."
             : protocols.ContainsTwice ()
               ? "Contains a value twice."
               : null;
    }

    internal static string CheckIfValidSendData (this byte[] data)
    {
      return data == null
             ? "'data' is null."
             : null;
    }

    internal static string CheckIfValidSendData (this FileInfo file)
    {
      return file == null
             ? "'file' is null."
             : null;
    }

    internal static string CheckIfValidSendData (this string data)
    {
      return data == null
             ? "'data' is null."
             : null;
    }

    internal static string CheckIfValidServicePath (this string path)
    {
      return path == null || path.Length == 0
             ? "'path' is null or empty."
             : path[0] != '/'
               ? "'path' isn't an absolute path."
               : path.IndexOfAny (new[] { '?', '#' }) > -1
                 ? "'path' includes either or both query and fragment components."
                 : null;
    }

    internal static string CheckIfValidSessionID (this string id)
    {
      return id == null || id.Length == 0
             ? "'id' is null or empty."
             : null;
    }

    internal static string CheckIfValidWaitTime (this TimeSpan time)
    {
      return time <= TimeSpan.Zero
             ? "A wait time is zero or less."
             : null;
    }

    internal static void Close (this HttpListenerResponse response, HttpStatusCode code)
    {
      response.StatusCode = (int) code;
      response.OutputStream.Close ();
    }

    internal static void CloseWithAuthChallenge (
      this HttpListenerResponse response, string challenge)
    {
      response.Headers.InternalSet ("WWW-Authenticate", challenge, true);
      response.Close (HttpStatusCode.Unauthorized);
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

    internal static bool Contains<T> (this IEnumerable<T> source, Func<T, bool> condition)
    {
      foreach (T elm in source)
        if (condition (elm))
          return true;

      return false;
    }

    internal static bool ContainsTwice (this string[] values)
    {
      var len = values.Length;

      Func<int, bool> contains = null;
      contains = idx => {
        if (idx < len - 1) {
          for (var i = idx + 1; i < len; i++)
            if (values[i] == values[idx])
              return true;

          return contains (++idx);
        }

        return false;
      };

      return contains (0);
    }

    internal static T[] Copy<T> (this T[] source, long length)
    {
      var dest = new T[length];
      Array.Copy (source, 0, dest, 0, length);

      return dest;
    }

    internal static void CopyTo (this Stream source, Stream destination)
    {
      var buffLen = 256;
      var buff = new byte[buffLen];
      var readLen = 0;
      while ((readLen = source.Read (buff, 0, buffLen)) > 0)
        destination.Write (buff, 0, readLen);
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

      var i = original.IndexOfAny (new[] { '?', '#' });
      return i > 0
             ? original.Substring (0, i)
             : original;
    }

    internal static string GetMessage (this CloseStatusCode code)
    {
      return code == CloseStatusCode.ProtocolError
             ? "A WebSocket protocol error has occurred."
             : code == CloseStatusCode.IncorrectData
               ? "An incorrect data has been received."
               : code == CloseStatusCode.Abnormal
                 ? "An exception has occurred."
                 : code == CloseStatusCode.InconsistentData
                   ? "An inconsistent data has been received."
                   : code == CloseStatusCode.PolicyViolation
                     ? "A policy violation has occurred."
                     : code == CloseStatusCode.TooBig
                       ? "A too big data has been received."
                       : code == CloseStatusCode.IgnoreExtension
                         ? "WebSocket client didn't receive expected extension(s)."
                         : code == CloseStatusCode.ServerError
                           ? "WebSocket server got an internal error."
                           : code == CloseStatusCode.TlsHandshakeFailure
                             ? "An error has occurred while handshaking."
                             : String.Empty;
    }

    /// <summary>
    /// Gets the name from the specified <see cref="string"/> that contains a pair of name and
    /// value separated by a separator character.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that represents the name if any; otherwise, <c>null</c>.
    /// </returns>
    /// <param name="nameAndValue">
    /// A <see cref="string"/> that contains a pair of name and value separated by a separator
    /// character.
    /// </param>
    /// <param name="separator">
    /// A <see cref="char"/> that represents the separator character.
    /// </param>
    internal static string GetName (this string nameAndValue, char separator)
    {
      var i = nameAndValue.IndexOf (separator);
      return i > 0
             ? nameAndValue.Substring (0, i).Trim ()
             : null;
    }

    /// <summary>
    /// Gets the value from the specified <see cref="string"/> that contains a pair of name and
    /// value separated by a separator character.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that represents the value if any; otherwise, <c>null</c>.
    /// </returns>
    /// <param name="nameAndValue">
    /// A <see cref="string"/> that contains a pair of name and value separated by a separator
    /// character.
    /// </param>
    /// <param name="separator">
    /// A <see cref="char"/> that represents the separator character.
    /// </param>
    internal static string GetValue (this string nameAndValue, char separator)
    {
      var i = nameAndValue.IndexOf (separator);
      return i > -1 && i < nameAndValue.Length - 1
             ? nameAndValue.Substring (i + 1).Trim ()
             : null;
    }

    internal static string GetValue (this string nameAndValue, char separator, bool unquote)
    {
      var i = nameAndValue.IndexOf (separator);
      if (i < 0 || i == nameAndValue.Length - 1)
        return null;

      var val = nameAndValue.Substring (i + 1).Trim ();
      return unquote
             ? val.Unquote ()
             : val;
    }

    internal static TcpListenerWebSocketContext GetWebSocketContext (
      this TcpClient tcpClient,
      string protocol,
      bool secure,
      ServerSslConfiguration sslConfig,
      Logger logger)
    {
      return new TcpListenerWebSocketContext (tcpClient, protocol, secure, sslConfig, logger);
    }

    internal static byte[] InternalToByteArray (this ushort value, ByteOrder order)
    {
      var bytes = BitConverter.GetBytes (value);
      if (!order.IsHostOrder ())
        Array.Reverse (bytes);

      return bytes;
    }

    internal static byte[] InternalToByteArray (this ulong value, ByteOrder order)
    {
      var bytes = BitConverter.GetBytes (value);
      if (!order.IsHostOrder ())
        Array.Reverse (bytes);

      return bytes;
    }

    internal static bool IsCompressionExtension (this string value)
    {
      return value.StartsWith ("permessage-");
    }

    internal static bool IsCompressionExtension (this string value, CompressionMethod method)
    {
      return value.StartsWith (method.ToExtensionString ());
    }

    internal static bool IsNoStatusCode (this ushort code)
    {
      return code == (ushort) CloseStatusCode.NoStatusCode;
    }

    internal static bool IsNoStatusCode (this CloseStatusCode code)
    {
      return code == CloseStatusCode.NoStatusCode;
    }

    internal static bool IsPortNumber (this int value)
    {
      return value > 0 && value < 65536;
    }

    internal static bool IsReserved (this ushort code)
    {
      return code == (ushort) CloseStatusCode.Undefined ||
             code == (ushort) CloseStatusCode.NoStatusCode ||
             code == (ushort) CloseStatusCode.Abnormal ||
             code == (ushort) CloseStatusCode.TlsHandshakeFailure;
    }

    internal static bool IsReserved (this CloseStatusCode code)
    {
      return code == CloseStatusCode.Undefined ||
             code == CloseStatusCode.NoStatusCode ||
             code == CloseStatusCode.Abnormal ||
             code == CloseStatusCode.TlsHandshakeFailure;
    }

    internal static bool IsText (this string value)
    {
      var len = value.Length;
      for (var i = 0; i < len; i++) {
        var c = value[i];
        if (c < 0x20 && !"\r\n\t".Contains (c))
          return false;

        if (c == 0x7f)
          return false;

        if (c == '\n' && ++i < len) {
          c = value[i];
          if (!" \t".Contains (c))
            return false;
        }
      }

      return true;
    }

    internal static bool IsToken (this string value)
    {
      foreach (var c in value)
        if (c < 0x20 || c >= 0x7f || _tspecials.Contains (c))
          return false;

      return true;
    }

    internal static string Quote (this string value)
    {
      return String.Format ("\"{0}\"", value.Replace ("\"", "\\\""));
    }

    internal static byte[] ReadBytes (this Stream stream, int length)
    {
      return stream.readBytes (new byte[length], 0, length);
    }

    internal static byte[] ReadBytes (this Stream stream, long length, int bufferLength)
    {
      using (var res = new MemoryStream ()) {
        var cnt = length / bufferLength;
        var rem = (int) (length % bufferLength);

        var buff = new byte[bufferLength];
        var end = false;
        for (long i = 0; i < cnt; i++) {
          if (!stream.readBytes (buff, 0, bufferLength, res)) {
            end = true;
            break;
          }
        }

        if (!end && rem > 0)
          stream.readBytes (new byte[rem], 0, rem, res);

        res.Close ();
        return res.ToArray ();
      }
    }

    internal static void ReadBytesAsync (
      this Stream stream, int length, Action<byte[]> completed, Action<Exception> error)
    {
      var buff = new byte[length];
      stream.BeginRead (
        buff,
        0,
        length,
        ar => {
          try {
            byte[] bytes = null;
            try {
              var len = stream.EndRead (ar);
              bytes = len < 1
                      ? new byte[0]
                      : len < length
                        ? stream.readBytes (buff, len, length - len)
                        : buff;
            }
            catch {
              bytes = new byte[0];
            }

            if (completed != null)
              completed (bytes);
          }
          catch (Exception ex) {
            if (error != null)
              error (ex);
          }
        },
        null);
    }

    internal static string RemovePrefix (this string value, params string[] prefixes)
    {
      var i = 0;
      foreach (var prefix in prefixes) {
        if (value.StartsWith (prefix)) {
          i = prefix.Length;
          break;
        }
      }

      return i > 0
             ? value.Substring (i)
             : value;
    }

    internal static T[] Reverse<T> (this T[] array)
    {
      var len = array.Length;
      var reverse = new T[len];

      var end = len - 1;
      for (var i = 0; i <= end; i++)
        reverse[i] = array[end - i];

      return reverse;
    }

    internal static IEnumerable<string> SplitHeaderValue (
      this string value, params char[] separators)
    {
      var len = value.Length;
      var seps = new string (separators);

      var buff = new StringBuilder (32);
      var escaped = false;
      var quoted = false;

      for (var i = 0; i < len; i++) {
        var c = value[i];
        if (c == '"') {
          if (escaped)
            escaped = !escaped;
          else
            quoted = !quoted;
        }
        else if (c == '\\') {
          if (i < len - 1 && value[i + 1] == '"')
            escaped = true;
        }
        else if (seps.Contains (c)) {
          if (!quoted) {
            yield return buff.ToString ();
            buff.Length = 0;

            continue;
          }
        }
        else {
        }

        buff.Append (c);
      }

      if (buff.Length > 0)
        yield return buff.ToString ();
    }

    internal static byte[] ToByteArray (this Stream stream)
    {
      using (var output = new MemoryStream ()) {
        stream.Position = 0;
        stream.CopyTo (output);
        output.Close ();

        return output.ToArray ();
      }
    }

    internal static CompressionMethod ToCompressionMethod (this string value)
    {
      foreach (CompressionMethod method in Enum.GetValues (typeof (CompressionMethod)))
        if (method.ToExtensionString () == value)
          return method;

      return CompressionMethod.None;
    }

    internal static string ToExtensionString (
      this CompressionMethod method, params string[] parameters)
    {
      if (method == CompressionMethod.None)
        return String.Empty;

      var m = String.Format ("permessage-{0}", method.ToString ().ToLower ());
      if (parameters == null || parameters.Length == 0)
        return m;

      return String.Format ("{0}; {1}", m, parameters.ToString ("; "));
    }

    internal static System.Net.IPAddress ToIPAddress (this string hostNameOrAddress)
    {
      try {
        var addrs = System.Net.Dns.GetHostAddresses (hostNameOrAddress);
        return addrs[0];
      }
      catch {
        return null;
      }
    }

    internal static List<TSource> ToList<TSource> (this IEnumerable<TSource> source)
    {
      return new List<TSource> (source);
    }

    internal static ushort ToUInt16 (this byte[] source, ByteOrder sourceOrder)
    {
      return BitConverter.ToUInt16 (source.ToHostOrder (sourceOrder), 0);
    }

    internal static ulong ToUInt64 (this byte[] source, ByteOrder sourceOrder)
    {
      return BitConverter.ToUInt64 (source.ToHostOrder (sourceOrder), 0);
    }

    internal static string TrimEndSlash (this string value)
    {
      value = value.TrimEnd ('/');
      return value.Length > 0
             ? value
             : "/";
    }

    /// <summary>
    /// Tries to create a <see cref="Uri"/> for WebSocket with the specified
    /// <paramref name="uriString"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if a <see cref="Uri"/> is successfully created; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="uriString">
    /// A <see cref="string"/> that represents the WebSocket URL to try.
    /// </param>
    /// <param name="result">
    /// When this method returns, a <see cref="Uri"/> that represents the WebSocket URL
    /// if <paramref name="uriString"/> is valid; otherwise, <see langword="null"/>.
    /// </param>
    /// <param name="message">
    /// When this method returns, a <see cref="string"/> that represents the error message
    /// if <paramref name="uriString"/> is invalid; otherwise, <see cref="String.Empty"/>.
    /// </param>
    internal static bool TryCreateWebSocketUri (
      this string uriString, out Uri result, out string message)
    {
      result = null;

      var uri = uriString.ToUri ();
      if (!uri.IsAbsoluteUri) {
        message = "Not an absolute URI: " + uriString;
        return false;
      }

      var schm = uri.Scheme;
      if (schm != "ws" && schm != "wss") {
        message = "The scheme part isn't 'ws' or 'wss': " + uriString;
        return false;
      }

      if (uri.Fragment.Length > 0) {
        message = "Includes the fragment component: " + uriString;
        return false;
      }

      var port = uri.Port;
      if (port > 0) {
        if (port > 65535) {
          message = "The port part is greater than 65535: " + uriString;
          return false;
        }

        if ((schm == "ws" && port == 443) || (schm == "wss" && port == 80)) {
          message = "An invalid pair of scheme and port: " + uriString;
          return false;
        }
      }
      else {
        uri = new Uri (
          String.Format (
            "{0}://{1}:{2}{3}", schm, uri.Host, schm == "ws" ? 80 : 443, uri.PathAndQuery));
      }

      result = uri;
      message = String.Empty;

      return true;
    }

    internal static string Unquote (this string value)
    {
      var start = value.IndexOf ('"');
      if (start < 0)
        return value;

      var end = value.LastIndexOf ('"');
      var len = end - start - 1;

      return len < 0
             ? value
             : len == 0
               ? String.Empty
               : value.Substring (start + 1, len).Replace ("\\\"", "\"");
    }

    internal static void WriteBytes (this Stream stream, byte[] bytes)
    {
      using (var input = new MemoryStream (bytes))
        input.CopyTo (stream);
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
    public static bool Contains (this string value, params char[] chars)
    {
      return chars == null || chars.Length == 0
             ? true
             : value == null || value.Length == 0
               ? false
               : value.IndexOfAny (chars) > -1;
    }

    /// <summary>
    /// Determines whether the specified <see cref="NameValueCollection"/> contains the entry
    /// with the specified <paramref name="name"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="collection"/> contains the entry
    /// with <paramref name="name"/>; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="collection">
    /// A <see cref="NameValueCollection"/> to test.
    /// </param>
    /// <param name="name">
    /// A <see cref="string"/> that represents the key of the entry to find.
    /// </param>
    public static bool Contains (this NameValueCollection collection, string name)
    {
      return collection != null && collection.Count > 0
             ? collection[name] != null
             : false;
    }

    /// <summary>
    /// Determines whether the specified <see cref="NameValueCollection"/> contains the entry
    /// with the specified both <paramref name="name"/> and <paramref name="value"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="collection"/> contains the entry with both
    /// <paramref name="name"/> and <paramref name="value"/>; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="collection">
    /// A <see cref="NameValueCollection"/> to test.
    /// </param>
    /// <param name="name">
    /// A <see cref="string"/> that represents the key of the entry to find.
    /// </param>
    /// <param name="value">
    /// A <see cref="string"/> that represents the value of the entry to find.
    /// </param>
    public static bool Contains (this NameValueCollection collection, string name, string value)
    {
      if (collection == null || collection.Count == 0)
        return false;

      var vals = collection[name];
      if (vals == null)
        return false;

      foreach (var val in vals.Split (','))
        if (val.Trim ().Equals (value, StringComparison.OrdinalIgnoreCase))
          return true;

      return false;
    }

    /// <summary>
    /// Emits the specified <see cref="EventHandler"/> delegate if it isn't <see langword="null"/>.
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
    public static void Emit (this EventHandler eventHandler, object sender, EventArgs e)
    {
      if (eventHandler != null)
        eventHandler (sender, e);
    }

    /// <summary>
    /// Emits the specified <c>EventHandler&lt;TEventArgs&gt;</c> delegate
    /// if it isn't <see langword="null"/>.
    /// </summary>
    /// <param name="eventHandler">
    /// An <c>EventHandler&lt;TEventArgs&gt;</c> to emit.
    /// </param>
    /// <param name="sender">
    /// An <see cref="object"/> from which emits this <paramref name="eventHandler"/>.
    /// </param>
    /// <param name="e">
    /// A <c>TEventArgs</c> that represents the event data.
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
    /// Gets the collection of the HTTP cookies from the specified HTTP <paramref name="headers"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="CookieCollection"/> that receives a collection of the HTTP cookies.
    /// </returns>
    /// <param name="headers">
    /// A <see cref="NameValueCollection"/> that contains a collection of the HTTP headers.
    /// </param>
    /// <param name="response">
    /// <c>true</c> if <paramref name="headers"/> is a collection of the response headers;
    /// otherwise, <c>false</c>.
    /// </param>
    public static CookieCollection GetCookies (this NameValueCollection headers, bool response)
    {
      var name = response ? "Set-Cookie" : "Cookie";
      return headers != null && headers.Contains (name)
             ? CookieCollection.Parse (headers[name], response)
             : new CookieCollection ();
    }

    /// <summary>
    /// Gets the description of the specified HTTP status <paramref name="code"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that represents the description of the HTTP status code.
    /// </returns>
    /// <param name="code">
    /// One of <see cref="HttpStatusCode"/> enum values, indicates the HTTP status code.
    /// </param>
    public static string GetDescription (this HttpStatusCode code)
    {
      return ((int) code).GetStatusDescription ();
    }

    /// <summary>
    /// Gets the description of the specified HTTP status <paramref name="code"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that represents the description of the HTTP status code.
    /// </returns>
    /// <param name="code">
    /// An <see cref="int"/> that represents the HTTP status code.
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
    /// Determines whether the specified <see cref="ushort"/> is in the allowable range of
    /// the WebSocket close status code.
    /// </summary>
    /// <remarks>
    /// Not allowable ranges are the following:
    ///   <list type="bullet">
    ///     <item>
    ///       <term>
    ///       Numbers in the range 0-999 are not used.
    ///       </term>
    ///     </item>
    ///     <item>
    ///       <term>
    ///       Numbers greater than 4999 are out of the reserved close status code ranges.
    ///       </term>
    ///     </item>
    ///   </list>
    /// </remarks>
    /// <returns>
    /// <c>true</c> if <paramref name="value"/> is in the allowable range of the WebSocket
    /// close status code; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="value">
    /// A <see cref="ushort"/> to test.
    /// </param>
    public static bool IsCloseStatusCode (this ushort value)
    {
      return value > 999 && value < 5000;
    }

    /// <summary>
    /// Determines whether the specified <see cref="string"/> is enclosed in the specified
    /// <see cref="char"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="value"/> is enclosed in <paramref name="c"/>;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <param name="value">
    /// A <see cref="string"/> to test.
    /// </param>
    /// <param name="c">
    /// A <see cref="char"/> that represents the character to find.
    /// </param>
    public static bool IsEnclosedIn (this string value, char c)
    {
      return value != null &&
             value.Length > 1 &&
             value[0] == c &&
             value[value.Length - 1] == c;
    }

    /// <summary>
    /// Determines whether the specified <see cref="ByteOrder"/> is host (this computer
    /// architecture) byte order.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="order"/> is host byte order; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="order">
    /// One of the <see cref="ByteOrder"/> enum values, to test.
    /// </param>
    public static bool IsHostOrder (this ByteOrder order)
    {
      // true : !(true ^ true)  or !(false ^ false)
      // false: !(true ^ false) or !(false ^ true)
      return !(BitConverter.IsLittleEndian ^ (order == ByteOrder.Little));
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

      if (address.Equals (System.Net.IPAddress.Any) || System.Net.IPAddress.IsLoopback (address))
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
    /// <c>true</c> if <paramref name="value"/> is <see langword="null"/> or empty;
    /// otherwise, <c>false</c>.
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
    /// <c>true</c> if <paramref name="value"/> is a predefined scheme; otherwise, <c>false</c>.
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

      if (c == 'n') {
        c = value[1];
        return c == 'e'
               ? value == "news" || value == "net.pipe" || value == "net.tcp"
               : value == "nntp";
      }

      return (c == 'g' && value == "gopher") || (c == 'm' && value == "mailto");
    }

    /// <summary>
    /// Determines whether the specified <see cref="HttpListenerRequest"/> is an HTTP Upgrade
    /// request to switch to the specified <paramref name="protocol"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="request"/> is an HTTP Upgrade request to switch to
    /// <paramref name="protocol"/>; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="request">
    /// A <see cref="HttpListenerRequest"/> that represents the HTTP request.
    /// </param>
    /// <param name="protocol">
    /// A <see cref="string"/> that represents the protocol name.
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
    /// <paramref name="protocol"/> is empty.
    /// </exception>
    public static bool IsUpgradeTo (this HttpListenerRequest request, string protocol)
    {
      if (request == null)
        throw new ArgumentNullException ("request");

      if (protocol == null)
        throw new ArgumentNullException ("protocol");

      if (protocol.Length == 0)
        throw new ArgumentException ("An empty string.", "protocol");

      return request.Headers.Contains ("Upgrade", protocol) &&
             request.Headers.Contains ("Connection", "Upgrade");
    }

    /// <summary>
    /// Determines whether the specified <see cref="string"/> is a URI string.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="value"/> may be a URI string; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="value">
    /// A <see cref="string"/> to test.
    /// </param>
    public static bool MaybeUri (this string value)
    {
      if (value == null || value.Length == 0)
        return false;

      var i = value.IndexOf (':');
      if (i == -1)
        return false;

      if (i >= 10)
        return false;

      return value.Substring (0, i).IsPredefinedScheme ();
    }

    /// <summary>
    /// Retrieves a sub-array from the specified <paramref name="array"/>.
    /// A sub-array starts at the specified element position in <paramref name="array"/>.
    /// </summary>
    /// <returns>
    /// An array of T that receives a sub-array, or an empty array of T
    /// if any problems with the parameters.
    /// </returns>
    /// <param name="array">
    /// An array of T from which to retrieve a sub-array.
    /// </param>
    /// <param name="startIndex">
    /// An <see cref="int"/> that represents the zero-based starting position of
    /// a sub-array in <paramref name="array"/>.
    /// </param>
    /// <param name="length">
    /// An <see cref="int"/> that represents the number of elements to retrieve.
    /// </param>
    /// <typeparam name="T">
    /// The type of elements in <paramref name="array"/>.
    /// </typeparam>
    public static T[] SubArray<T> (this T[] array, int startIndex, int length)
    {
      int len;
      if (array == null || (len = array.Length) == 0)
        return new T[0];

      if (startIndex < 0 || length <= 0 || startIndex + length > len)
        return new T[0];

      if (startIndex == 0 && length == len)
        return array;

      var subArray = new T[length];
      Array.Copy (array, startIndex, subArray, 0, length);

      return subArray;
    }

    /// <summary>
    /// Retrieves a sub-array from the specified <paramref name="array"/>.
    /// A sub-array starts at the specified element position in <paramref name="array"/>.
    /// </summary>
    /// <returns>
    /// An array of T that receives a sub-array, or an empty array of T
    /// if any problems with the parameters.
    /// </returns>
    /// <param name="array">
    /// An array of T from which to retrieve a sub-array.
    /// </param>
    /// <param name="startIndex">
    /// A <see cref="long"/> that represents the zero-based starting position of
    /// a sub-array in <paramref name="array"/>.
    /// </param>
    /// <param name="length">
    /// A <see cref="long"/> that represents the number of elements to retrieve.
    /// </param>
    /// <typeparam name="T">
    /// The type of elements in <paramref name="array"/>.
    /// </typeparam>
    public static T[] SubArray<T> (this T[] array, long startIndex, long length)
    {
      long len;
      if (array == null || (len = array.LongLength) == 0)
        return new T[0];

      if (startIndex < 0 || length <= 0 || startIndex + length > len)
        return new T[0];

      if (startIndex == 0 && length == len)
        return array;

      var subArray = new T[length];
      Array.Copy (array, startIndex, subArray, 0, length);

      return subArray;
    }

    /// <summary>
    /// Executes the specified <see cref="Action"/> delegate <paramref name="n"/> times.
    /// </summary>
    /// <param name="n">
    /// An <see cref="int"/> is the number of times to execute.
    /// </param>
    /// <param name="action">
    /// An <see cref="Action"/> delegate that references the method(s) to execute.
    /// </param>
    public static void Times (this int n, Action action)
    {
      if (n > 0 && action != null)
        ((ulong) n).times (action);
    }

    /// <summary>
    /// Executes the specified <see cref="Action"/> delegate <paramref name="n"/> times.
    /// </summary>
    /// <param name="n">
    /// A <see cref="long"/> is the number of times to execute.
    /// </param>
    /// <param name="action">
    /// An <see cref="Action"/> delegate that references the method(s) to execute.
    /// </param>
    public static void Times (this long n, Action action)
    {
      if (n > 0 && action != null)
        ((ulong) n).times (action);
    }

    /// <summary>
    /// Executes the specified <see cref="Action"/> delegate <paramref name="n"/> times.
    /// </summary>
    /// <param name="n">
    /// A <see cref="uint"/> is the number of times to execute.
    /// </param>
    /// <param name="action">
    /// An <see cref="Action"/> delegate that references the method(s) to execute.
    /// </param>
    public static void Times (this uint n, Action action)
    {
      if (n > 0 && action != null)
        ((ulong) n).times (action);
    }

    /// <summary>
    /// Executes the specified <see cref="Action"/> delegate <paramref name="n"/> times.
    /// </summary>
    /// <param name="n">
    /// A <see cref="ulong"/> is the number of times to execute.
    /// </param>
    /// <param name="action">
    /// An <see cref="Action"/> delegate that references the method(s) to execute.
    /// </param>
    public static void Times (this ulong n, Action action)
    {
      if (n > 0 && action != null)
        n.times (action);
    }

    /// <summary>
    /// Executes the specified <c>Action&lt;int&gt;</c> delegate <paramref name="n"/> times.
    /// </summary>
    /// <param name="n">
    /// An <see cref="int"/> is the number of times to execute.
    /// </param>
    /// <param name="action">
    /// An <c>Action&lt;int&gt;</c> delegate that references the method(s) to execute.
    /// An <see cref="int"/> parameter to pass to the method(s) is the zero-based count
    /// of iteration.
    /// </param>
    public static void Times (this int n, Action<int> action)
    {
      if (n > 0 && action != null)
        for (int i = 0; i < n; i++)
          action (i);
    }

    /// <summary>
    /// Executes the specified <c>Action&lt;long&gt;</c> delegate <paramref name="n"/> times.
    /// </summary>
    /// <param name="n">
    /// A <see cref="long"/> is the number of times to execute.
    /// </param>
    /// <param name="action">
    /// An <c>Action&lt;long&gt;</c> delegate that references the method(s) to execute.
    /// A <see cref="long"/> parameter to pass to the method(s) is the zero-based count
    /// of iteration.
    /// </param>
    public static void Times (this long n, Action<long> action)
    {
      if (n > 0 && action != null)
        for (long i = 0; i < n; i++)
          action (i);
    }

    /// <summary>
    /// Executes the specified <c>Action&lt;uint&gt;</c> delegate <paramref name="n"/> times.
    /// </summary>
    /// <param name="n">
    /// A <see cref="uint"/> is the number of times to execute.
    /// </param>
    /// <param name="action">
    /// An <c>Action&lt;uint&gt;</c> delegate that references the method(s) to execute.
    /// A <see cref="uint"/> parameter to pass to the method(s) is the zero-based count
    /// of iteration.
    /// </param>
    public static void Times (this uint n, Action<uint> action)
    {
      if (n > 0 && action != null)
        for (uint i = 0; i < n; i++)
          action (i);
    }

    /// <summary>
    /// Executes the specified <c>Action&lt;ulong&gt;</c> delegate <paramref name="n"/> times.
    /// </summary>
    /// <param name="n">
    /// A <see cref="ulong"/> is the number of times to execute.
    /// </param>
    /// <param name="action">
    /// An <c>Action&lt;ulong&gt;</c> delegate that references the method(s) to execute.
    /// A <see cref="ulong"/> parameter to pass to this method(s) is the zero-based count
    /// of iteration.
    /// </param>
    public static void Times (this ulong n, Action<ulong> action)
    {
      if (n > 0 && action != null)
        for (ulong i = 0; i < n; i++)
          action (i);
    }

    /// <summary>
    /// Converts the specified array of <see cref="byte"/> to the specified type data.
    /// </summary>
    /// <returns>
    /// A T converted from <paramref name="source"/>, or a default value of T if
    /// <paramref name="source"/> is an empty array of <see cref="byte"/> or if the
    /// type of T isn't <see cref="bool"/>, <see cref="char"/>, <see cref="double"/>,
    /// <see cref="float"/>, <see cref="int"/>, <see cref="long"/>, <see cref="short"/>,
    /// <see cref="uint"/>, <see cref="ulong"/>, or <see cref="ushort"/>.
    /// </returns>
    /// <param name="source">
    /// An array of <see cref="byte"/> to convert.
    /// </param>
    /// <param name="sourceOrder">
    /// One of the <see cref="ByteOrder"/> enum values, indicates the byte order of
    /// <paramref name="source"/>.
    /// </param>
    /// <typeparam name="T">
    /// The type of the return. The T must be a value type.
    /// </typeparam>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> is <see langword="null"/>.
    /// </exception>
    public static T To<T> (this byte[] source, ByteOrder sourceOrder)
      where T : struct
    {
      if (source == null)
        throw new ArgumentNullException ("source");

      if (source.Length == 0)
        return default (T);

      var type = typeof (T);
      var buff = source.ToHostOrder (sourceOrder);

      return type == typeof (Boolean)
             ? (T)(object) BitConverter.ToBoolean (buff, 0)
             : type == typeof (Char)
               ? (T)(object) BitConverter.ToChar (buff, 0)
               : type == typeof (Double)
                 ? (T)(object) BitConverter.ToDouble (buff, 0)
                 : type == typeof (Int16)
                   ? (T)(object) BitConverter.ToInt16 (buff, 0)
                   : type == typeof (Int32)
                     ? (T)(object) BitConverter.ToInt32 (buff, 0)
                     : type == typeof (Int64)
                       ? (T)(object) BitConverter.ToInt64 (buff, 0)
                       : type == typeof (Single)
                         ? (T)(object) BitConverter.ToSingle (buff, 0)
                         : type == typeof (UInt16)
                           ? (T)(object) BitConverter.ToUInt16 (buff, 0)
                           : type == typeof (UInt32)
                             ? (T)(object) BitConverter.ToUInt32 (buff, 0)
                             : type == typeof (UInt64)
                               ? (T)(object) BitConverter.ToUInt64 (buff, 0)
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
    /// One of the <see cref="ByteOrder"/> enum values, indicates the byte order of the return.
    /// </param>
    /// <typeparam name="T">
    /// The type of <paramref name="value"/>. The T must be a value type.
    /// </typeparam>
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
                                      : new byte[0];

      if (bytes.Length > 1 && !order.IsHostOrder ())
        Array.Reverse (bytes);

      return bytes;
    }

    /// <summary>
    /// Converts the order of the specified array of <see cref="byte"/> to the host byte order.
    /// </summary>
    /// <returns>
    /// An array of <see cref="byte"/> converted from <paramref name="source"/>.
    /// </returns>
    /// <param name="source">
    /// An array of <see cref="byte"/> to convert.
    /// </param>
    /// <param name="sourceOrder">
    /// One of the <see cref="ByteOrder"/> enum values, indicates the byte order of
    /// <paramref name="source"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> is <see langword="null"/>.
    /// </exception>
    public static byte[] ToHostOrder (this byte[] source, ByteOrder sourceOrder)
    {
      if (source == null)
        throw new ArgumentNullException ("source");

      return source.Length > 1 && !sourceOrder.IsHostOrder ()
             ? source.Reverse ()
             : source;
    }

    /// <summary>
    /// Converts the specified <paramref name="array"/> to a <see cref="string"/>
    /// that concatenates the each element of <paramref name="array"/> across the
    /// specified <paramref name="separator"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> converted from <paramref name="array"/>,
    /// or <see cref="String.Empty"/> if <paramref name="array"/> is empty.
    /// </returns>
    /// <param name="array">
    /// An array of T to convert.
    /// </param>
    /// <param name="separator">
    /// A <see cref="string"/> that represents the separator string.
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
      (len - 1).Times (i => buff.AppendFormat ("{0}{1}", array[i].ToString (), separator));

      buff.Append (array[len - 1].ToString ());
      return buff.ToString ();
    }

    /// <summary>
    /// Converts the specified <see cref="string"/> to a <see cref="Uri"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="Uri"/> converted from <paramref name="uriString"/>, or <see langword="null"/>
    /// if <paramref name="uriString"/> isn't successfully converted.
    /// </returns>
    /// <param name="uriString">
    /// A <see cref="string"/> to convert.
    /// </param>
    public static Uri ToUri (this string uriString)
    {
      Uri res;
      return Uri.TryCreate (
               uriString, uriString.MaybeUri () ? UriKind.Absolute : UriKind.Relative, out res)
             ? res
             : null;
    }

    /// <summary>
    /// URL-decodes the specified <see cref="string"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that receives the decoded string, or the <paramref name="value"/>
    /// if it's <see langword="null"/> or empty.
    /// </returns>
    /// <param name="value">
    /// A <see cref="string"/> to decode.
    /// </param>
    public static string UrlDecode (this string value)
    {
      return value != null && value.Length > 0
             ? HttpUtility.UrlDecode (value)
             : value;
    }

    /// <summary>
    /// URL-encodes the specified <see cref="string"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that receives the encoded string, or <paramref name="value"/>
    /// if it's <see langword="null"/> or empty.
    /// </returns>
    /// <param name="value">
    /// A <see cref="string"/> to encode.
    /// </param>
    public static string UrlEncode (this string value)
    {
      return value != null && value.Length > 0
             ? HttpUtility.UrlEncode (value)
             : value;
    }

    /// <summary>
    /// Writes the specified <paramref name="content"/> data with the specified
    /// <see cref="HttpListenerResponse"/>.
    /// </summary>
    /// <param name="response">
    /// A <see cref="HttpListenerResponse"/> that represents the HTTP response
    /// used to write the content data.
    /// </param>
    /// <param name="content">
    /// An array of <see cref="byte"/> that represents the content data to write.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="response"/> is <see langword="null"/>.
    /// </exception>
    public static void WriteContent (this HttpListenerResponse response, byte[] content)
    {
      if (response == null)
        throw new ArgumentNullException ("response");

      var len = 0;
      if (content == null || (len = content.Length) == 0)
        return;

      var output = response.OutputStream;
      response.ContentLength64 = len;
      output.Write (content, 0, len);
      output.Close ();
    }

    #endregion
  }
}
