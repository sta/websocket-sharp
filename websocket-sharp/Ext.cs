#region License
/*
 * Ext.cs
 *
 * Some parts of this code are derived from Mono (http://www.mono-project.com):
 * - GetStatusDescription is derived from System.Net.HttpListenerResponse.cs
 * - IsPredefinedScheme is derived from System.Uri.cs
 * - MaybeUri is derived from System.Uri.cs
 *
 * The MIT License
 *
 * Copyright (c) 2001 Garrett Rooney
 * Copyright (c) 2003 Ian MacLean
 * Copyright (c) 2003 Ben Maurer
 * Copyright (c) 2003, 2005, 2009 Novell, Inc. (http://www.novell.com)
 * Copyright (c) 2009 Stephane Delcroix
 * Copyright (c) 2010-2014 sta.blockhead
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
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using WebSocketSharp.Net;
using WebSocketSharp.Net.WebSockets;
using CookieCollection = WebSocketSharp.Net.CookieCollection;
using HttpListenerRequest = WebSocketSharp.Net.HttpListenerRequest;
using HttpListenerResponse = WebSocketSharp.Net.HttpListenerResponse;
using HttpStatusCode = WebSocketSharp.Net.HttpStatusCode;

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

      using (var input = new MemoryStream (value)) {
        return input.compressToArray ();
      }
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

    private static byte [] compressToArray (this Stream stream)
    {
      using (var comp = stream.compress ()) {
        comp.Close ();
        return comp.ToArray ();
      }
    }

    private static byte [] decompress (this byte [] value)
    {
      if (value.LongLength == 0)
        return value;

      using (var input = new MemoryStream (value)) {
        return input.decompressToArray ();
      }
    }

    private static MemoryStream decompress (this Stream stream)
    {
      var output = new MemoryStream ();
      if (stream.Length == 0)
        return output;

      stream.Position = 0;
      using (var ds = new DeflateStream (stream, CompressionMode.Decompress, true)) {
        ds.CopyTo (output, true);
        return output;
      }
    }

    private static byte [] decompressToArray (this Stream stream)
    {
      using (var decomp = stream.decompress ()) {
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
      while (len < length) {
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

    #endregion

    #region Internal Methods

    internal static byte [] Append (this ushort code, string reason)
    {
      using (var buffer = new MemoryStream ()) {
        var tmp = code.ToByteArrayInternally (ByteOrder.Big);
        buffer.Write (tmp, 0, 2);
        if (reason != null && reason.Length > 0) {
          tmp = Encoding.UTF8.GetBytes (reason);
          buffer.Write (tmp, 0, tmp.Length);
        }

        buffer.Close ();
        return buffer.ToArray ();
      }
    }

    internal static string CheckIfClosable (this WebSocketState state)
    {
      return state == WebSocketState.Closing
             ? "While closing the WebSocket connection."
             : state == WebSocketState.Closed
               ? "The WebSocket connection has already been closed."
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

    internal static string CheckIfValidControlData (this byte [] data, string paramName)
    {
      return data.Length > 125
             ? String.Format ("'{0}' length must be less.", paramName)
             : null;
    }

    internal static string CheckIfValidSendData (this byte [] data)
    {
      return data == null
             ? "'data' must not be null."
             : null;
    }

    internal static string CheckIfValidSendData (this FileInfo file)
    {
      return file == null
             ? "'file' must not be null."
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

    internal static void Close (this HttpListenerResponse response, HttpStatusCode code)
    {
      response.StatusCode = (int) code;
      response.OutputStream.Close ();
    }

    internal static byte [] Compress (this byte [] value, CompressionMethod method)
    {
      return method == CompressionMethod.Deflate
             ? value.compress ()
             : value;
    }

    internal static Stream Compress (this Stream stream, CompressionMethod method)
    {
      return method == CompressionMethod.Deflate
             ? stream.compress ()
             : stream;
    }

    internal static byte [] CompressToArray (this Stream stream, CompressionMethod method)
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

    internal static bool ContainsTwice (this string [] values)
    {
      var len = values.Length;

      Func<int, bool> contains = null;
      contains = index => {
        if (index < len - 1) {
          for (var i = index + 1; i < len; i++)
            if (values [i] == values [index])
              return true;

          return contains (++index);
        }

        return false;
      };

      return contains (0);
    }

    internal static void CopyTo (this Stream src, Stream dest, bool setDefaultPosition)
    {
      var readLen = 0;
      var bufferLen = 256;
      var buffer = new byte [bufferLen];
      while ((readLen = src.Read (buffer, 0, bufferLen)) > 0) {
        dest.Write (buffer, 0, readLen);
      }

      if (setDefaultPosition)
        dest.Position = 0;
    }

    internal static byte [] Decompress (this byte [] value, CompressionMethod method)
    {
      return method == CompressionMethod.Deflate
             ? value.decompress ()
             : value;
    }

    internal static byte [] DecompressToArray (this Stream stream, CompressionMethod method)
    {
      return method == CompressionMethod.Deflate
             ? stream.decompressToArray ()
             : stream.ToByteArray ();
    }

    /// <summary>
    /// Determines whether the specified <see cref="int"/> equals the specified <see cref="char"/>,
    /// and invokes the specified Action&lt;int&gt; delegate at the same time.
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
    /// An Action&lt;int&gt; delegate that references the method(s) called at
    /// the same time as comparing. An <see cref="int"/> parameter to pass to
    /// the method(s) is <paramref name="value"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> isn't between 0 and 255.
    /// </exception>
    internal static bool EqualsWith (this int value, char c, Action<int> action)
    {
      if (value < 0 || value > 255)
        throw new ArgumentOutOfRangeException ("value");

      action (value);
      return value == c - 0;
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
                         ? "WebSocket client did not receive expected extension(s)."
                         : code == CloseStatusCode.ServerError
                           ? "WebSocket server got an internal error."
                           : code == CloseStatusCode.TlsHandshakeFailure
                             ? "An error has occurred while handshaking."
                             : String.Empty;
    }

    internal static string GetNameInternal (this string nameAndValue, string separator)
    {
      var i = nameAndValue.IndexOf (separator);
      return i > 0
             ? nameAndValue.Substring (0, i).Trim ()
             : null;
    }

    internal static string GetValueInternal (this string nameAndValue, string separator)
    {
      var i = nameAndValue.IndexOf (separator);
      return i >= 0 && i < nameAndValue.Length - 1
             ? nameAndValue.Substring (i + 1).Trim ()
             : null;
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
        char c = value [i];
        if (c < 0x20 && !"\r\n\t".Contains (c))
          return false;

        if (c == 0x7f)
          return false;

        if (c == '\n' && ++i < len) {
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
      using (var result = new MemoryStream ()) {
        var count = length / bufferLength;
        var rem = (int) (length % bufferLength);

        var buffer = new byte [bufferLength];
        var end = false;
        for (long i = 0; i < count; i++) {
          if (!stream.readBytes (buffer, 0, bufferLength, result)) {
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
      stream.BeginRead (
        buffer,
        0,
        length,
        ar => {
          try {
            var len = stream.EndRead (ar);
            var bytes = len < 1
                      ? new byte [0]
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
        },
        null);
    }

    internal static string RemovePrefix (this string value, params string [] prefixes)
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

    internal static T [] Reverse<T> (this T [] array)
    {
      var len = array.Length;
      T [] reverse = new T [len];

      var end = len - 1;
      for (var i = 0; i <= end; i++)
        reverse [i] = array [end - i];

      return reverse;
    }

    internal static IEnumerable<string> SplitHeaderValue (
      this string value, params char [] separator)
    {
      var len = value.Length;
      var separators = new string (separator);

      var buffer = new StringBuilder (32);
      var quoted = false;
      var escaped = false;

      char c;
      for (var i = 0; i < len; i++) {
        c = value [i];
        if (c == '"') {
          if (escaped)
            escaped = !escaped;
          else
            quoted = !quoted;
        }
        else if (c == '\\') {
          if (i < len - 1 && value [i + 1] == '"')
            escaped = true;
        }
        else if (separators.Contains (c)) {
          if (!quoted) {
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
      using (var output = new MemoryStream ()) {
        stream.Position = 0;
        stream.CopyTo (output);
        output.Close ();

        return output.ToArray ();
      }
    }

    internal static byte [] ToByteArrayInternally (this ushort value, ByteOrder order)
    {
      var bytes = BitConverter.GetBytes (value);
      if (!order.IsHostOrder ())
        Array.Reverse (bytes);

      return bytes;
    }

    internal static byte [] ToByteArrayInternally (this ulong value, ByteOrder order)
    {
      var bytes = BitConverter.GetBytes (value);
      if (!order.IsHostOrder ())
        Array.Reverse (bytes);

      return bytes;
    }

    internal static CompressionMethod ToCompressionMethod (this string value)
    {
      foreach (CompressionMethod method in Enum.GetValues (typeof (CompressionMethod)))
        if (method.ToExtensionString () == value)
          return method;

      return CompressionMethod.None;
    }

    internal static string ToExtensionString (this CompressionMethod method)
    {
      return method != CompressionMethod.None
             ? String.Format ("permessage-{0}", method.ToString ().ToLower ())
             : String.Empty;
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

    internal static List<TSource> ToList<TSource> (this IEnumerable<TSource> source)
    {
      return new List<TSource> (source);
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
    /// When this method returns, a <see cref="Uri"/> that represents the WebSocket URL if
    /// <paramref name="uriString"/> is valid; otherwise, <see langword="null"/>.
    /// </param>
    /// <param name="message">
    /// When this method returns, a <see cref="string"/> that represents the error message if
    /// <paramref name="uriString"/> is invalid; otherwise, <see cref="String.Empty"/>.
    /// </param>
    internal static bool TryCreateWebSocketUri (
      this string uriString, out Uri result, out string message)
    {
      result = null;
      if (uriString.Length == 0) {
        message = "Must not be empty.";
        return false;
      }

      var uri = uriString.ToUri ();
      if (!uri.IsAbsoluteUri) {
        message = "Must be the absolute URI: " + uriString;
        return false;
      }

      var scheme = uri.Scheme;
      if (scheme != "ws" && scheme != "wss") {
        message = "The scheme part must be 'ws' or 'wss': " + scheme;
        return false;
      }

      var fragment = uri.Fragment;
      if (fragment.Length > 0) {
        message = "Must not contain the fragment component: " + uriString;
        return false;
      }

      var port = uri.Port;
      if (port > 0) {
        if (port > 65535) {
          message = "The port part must be between 1 and 65535: " + port;
          return false;
        }

        if ((scheme == "ws" && port == 443) || (scheme == "wss" && port == 80)) {
          message = String.Format (
            "Invalid pair of scheme and port: {0}, {1}", scheme, port);
          return false;
        }
      }
      else {
        port = scheme == "ws" ? 80 : 443;
        var url = String.Format (
          "{0}://{1}:{2}{3}", scheme, uri.Host, port, uri.PathAndQuery);
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
      using (var src = new MemoryStream (value)) {
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
      return collection == null || collection.Count == 0
             ? false
             : collection [name] != null;
    }

    /// <summary>
    /// Determines whether the specified <see cref="NameValueCollection"/> contains the entry
    /// with the specified both <paramref name="name"/> and <paramref name="value"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="collection"/> contains the entry
    /// with both <paramref name="name"/> and <paramref name="value"/>;
    /// otherwise, <c>false</c>.
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

      var values = collection [name];
      if (values == null)
        return false;

      foreach (var v in values.Split (','))
        if (v.Trim ().Equals (value, StringComparison.OrdinalIgnoreCase))
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
    /// A <see cref="Net.CookieCollection"/> that receives a collection of the HTTP cookies.
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
      return headers == null || !headers.Contains (name)
             ? new CookieCollection ()
             : CookieCollection.Parse (headers [name], response);
    }

    /// <summary>
    /// Gets the description of the specified HTTP status <paramref name="code"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that represents the description of the HTTP status code.
    /// </returns>
    /// <param name="code">
    /// One of <see cref="HttpStatusCode"/> enum values, indicates the HTTP status codes.
    /// </param>
    public static string GetDescription (this HttpStatusCode code)
    {
      return ((int) code).GetStatusDescription ();
    }

    /// <summary>
    /// Gets the name from the specified <see cref="string"/> that contains a pair of name and
    /// value separated by a separator string.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that represents the name if any; otherwise, <c>null</c>.
    /// </returns>
    /// <param name="nameAndValue">
    /// A <see cref="string"/> that contains a pair of name and value separated by a separator
    /// string.
    /// </param>
    /// <param name="separator">
    /// A <see cref="string"/> that represents a separator string.
    /// </param>
    public static string GetName (this string nameAndValue, string separator)
    {
      return (nameAndValue != null && nameAndValue.Length > 0) &&
             (separator != null && separator.Length > 0)
             ? nameAndValue.GetNameInternal (separator)
             : null;
    }

    /// <summary>
    /// Gets the name and value from the specified <see cref="string"/> that contains a pair of
    /// name and value separated by a separator string.
    /// </summary>
    /// <returns>
    /// A <c>KeyValuePair&lt;string, string&gt;</c> that represents the name and value if any.
    /// </returns>
    /// <param name="nameAndValue">
    /// A <see cref="string"/> that contains a pair of name and value separated by a separator
    /// string.
    /// </param>
    /// <param name="separator">
    /// A <see cref="string"/> that represents a separator string.
    /// </param>
    public static KeyValuePair<string, string> GetNameAndValue (
      this string nameAndValue, string separator)
    {
      var name = nameAndValue.GetName (separator);
      var value = nameAndValue.GetValue (separator);
      return name != null
             ? new KeyValuePair<string, string> (name, value)
             : new KeyValuePair<string, string> (null, null);
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
    /// Gets the value from the specified <see cref="string"/> that contains a pair of name and
    /// value separated by a separator string.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that represents the value if any; otherwise, <c>null</c>.
    /// </returns>
    /// <param name="nameAndValue">
    /// A <see cref="string"/> that contains a pair of name and value separated by a separator
    /// string.
    /// </param>
    /// <param name="separator">
    /// A <see cref="string"/> that represents a separator string.
    /// </param>
    public static string GetValue (this string nameAndValue, string separator)
    {
      return (nameAndValue != null && nameAndValue.Length > 0) &&
             (separator != null && separator.Length > 0)
             ? nameAndValue.GetValueInternal (separator)
             : null;
    }

    /// <summary>
    /// Determines whether the specified <see cref="ByteOrder"/> is host
    /// (this computer architecture) byte order.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="order"/> is host byte order;
    /// otherwise, <c>false</c>.
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

      var c = value [0];
      if (c == 'h')
        return value == "http" || value == "https";

      if (c == 'w')
        return value == "ws" || value == "wss";

      if (c == 'f')
        return value == "file" || value == "ftp";

      if (c == 'n') {
        c = value [1];
        return c == 'e'
               ? value == "news" || value == "net.pipe" || value == "net.tcp"
               : value == "nntp";
      }

      return (c == 'g' && value == "gopher") || (c == 'm' && value == "mailto");
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
    /// A sub-array starts at the specified element position.
    /// </summary>
    /// <returns>
    /// An array of T that receives a sub-array, or an empty array of T if any problems
    /// with the parameters.
    /// </returns>
    /// <param name="array">
    /// An array of T that contains the data to retrieve a sub-array.
    /// </param>
    /// <param name="startIndex">
    /// An <see cref="int"/> that contains the zero-based starting position of a sub-array
    /// in <paramref name="array"/>.
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
        return new T [0];

      if (startIndex < 0 || length <= 0)
        return new T [0];

      if (startIndex + length > array.Length)
        return new T [0];

      if (startIndex == 0 && array.Length == length)
        return array;

      T [] subArray = new T [length];
      Array.Copy (array, startIndex, subArray, 0, length);

      return subArray;
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
    /// One of the <see cref="ByteOrder"/> enum values, indicates the byte order of
    /// <paramref name="src"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="src"/> is <see langword="null"/>.
    /// </exception>
    public static byte [] ToHostOrder (this byte [] src, ByteOrder srcOrder)
    {
      if (src == null)
        throw new ArgumentNullException ("src");

      return src.Length > 1 && !srcOrder.IsHostOrder ()
             ? src.Reverse ()
             : src;
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
      return value == null || value.Length == 0
             ? value
             : WebUtility.UrlDecode(value);
    }

    #endregion
  }
}
