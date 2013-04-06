#region MIT License
/*
 * Ext.cs
 *  IsPredefinedScheme and MaybeUri methods derived from System.Uri.cs
 *  GetStatusDescription method derived from System.Net.HttpListenerResponse.cs
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
using System.Linq;
using System.Net.Sockets;
using System.Text;
using WebSocketSharp.Net;
using WebSocketSharp.Net.WebSockets;

namespace WebSocketSharp {

  /// <summary>
  /// Provides a set of static methods for the websocket-sharp.
  /// </summary>
  public static class Ext {

    #region Field

    private const string _tspecials = "()<>@,;:\\\"/[]?={} \t";

    #endregion

    #region Private Methods

    private static void times(this ulong n, Action act)
    {
      for (ulong i = 0; i < n; i++)
        act();
    }

    #endregion

    #region Internal Method

    internal static string GetNameInternal(this string nameAndValue, string separator)
    {
      int i = nameAndValue.IndexOf(separator);
      return i > 0
             ? nameAndValue.Substring(0, i).Trim()
             : null;
    }

    internal static string GetValueInternal(this string nameAndValue, string separator)
    {
      int i = nameAndValue.IndexOf(separator);
      return i >= 0 && i < nameAndValue.Length - 1
             ? nameAndValue.Substring(i + 1).Trim()
             : null;
    }

    internal static bool IsText(this string value)
    {
      int len = value.Length;
      for (int i = 0; i < len; i++)
      {
        char c = value[i];
        if (c < 0x20 && !"\r\n\t".Contains(c))
          return false;

        if (c == 0x7f)
          return false;

        if (c == '\n' && ++i < len)
        {
          c = value[i];
          if (!" \t".Contains(c))
            return false;
        }
      }

      return true;
    }

    internal static bool IsToken(this string value)
    {
      foreach (char c in value)
      {
        if (c < 0x20 || c >= 0x7f || _tspecials.Contains(c))
          return false;
      }

      return true;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Accepts a WebSocket connection by the <see cref="TcpListener"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="TcpListenerWebSocketContext"/> that contains a WebSocket connection.
    /// </returns>
    /// <param name="listener">
    /// A <see cref="TcpListener"/> that provides a TCP connection to accept a WebSocket connection.
    /// </param>
    /// <param name="secure">
    /// A <see cref="bool"/> that indicates a secure connection or not. (<c>true</c> indicates a secure connection.)
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="listener"/> is <see langword="null"/>.
    /// </exception>
    public static TcpListenerWebSocketContext AcceptWebSocket(this TcpListener listener, bool secure)
    {
      if (listener.IsNull())
        throw new ArgumentNullException("listener");

      var client = listener.AcceptTcpClient();
      return new TcpListenerWebSocketContext(client, secure);
    }

    /// <summary>
    /// Accepts a WebSocket connection asynchronously by the <see cref="TcpListener"/>.
    /// </summary>
    /// <param name="listener">
    /// A <see cref="TcpListener"/> that provides a TCP connection to accept a WebSocket connection.
    /// </param>
    /// <param name="secure">
    /// A <see cref="bool"/> that indicates a secure connection or not. (<c>true</c> indicates a secure connection.)
    /// </param>
    /// <param name="completed">
    /// An Action&lt;TcpListenerWebSocketContext&gt; delegate that contains the method(s) that is called when an asynchronous operation completes.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="listener"/> is <see langword="null"/>.
    /// </exception>
    public static void AcceptWebSocketAsync(this TcpListener listener, bool secure, Action<TcpListenerWebSocketContext> completed)
    {
      if (listener.IsNull())
        throw new ArgumentNullException("listener");

      AsyncCallback callback = (ar) =>
      {
        var client  = listener.EndAcceptTcpClient(ar);
        var context = new TcpListenerWebSocketContext(client, secure);
        completed(context);
      };

      listener.BeginAcceptTcpClient(callback, null);
    }

    /// <summary>
    /// Determines whether the specified <see cref="string"/> contains any of characters
    /// in the specified array of <see cref="char"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="str"/> contains any of <paramref name="chars"/>; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="str">
    /// A <see cref="string"/> to test.
    /// </param>
    /// <param name="chars">
    /// An array of <see cref="char"/> that contains characters to find.
    /// </param>
    public static bool Contains(this string str, params char[] chars)
    {
      return str.IsNullOrEmpty()
             ? false
             : chars.Length == 0
               ? true
               : str.IndexOfAny(chars) != -1;
    }

    /// <summary>
    /// Emit the specified <see cref="EventHandler"/> delegate if is not <see langword="null"/>.
    /// </summary>
    /// <param name="eventHandler">
    /// An <see cref="EventHandler"/> to emit.
    /// </param>
    /// <param name="sender">
    /// An <see cref="object"/> that emits the <paramref name="eventHandler"/>.
    /// </param>
    /// <param name="e">
    /// An <see cref="EventArgs"/> that contains no event data.
    /// </param>
    public static void Emit(
      this EventHandler eventHandler, object sender, EventArgs e)
    {
      if (!eventHandler.IsNull())
        eventHandler(sender, e);
    }

    /// <summary>
    /// Emit the specified <b>EventHandler&lt;TEventArgs&gt;</b> delegate if is not <see langword="null"/>.
    /// </summary>
    /// <param name="eventHandler">
    /// An <b>EventHandler&lt;TEventArgs&gt;</b> to emit.
    /// </param>
    /// <param name="sender">
    /// An <see cref="object"/> that emits the <paramref name="eventHandler"/>.
    /// </param>
    /// <param name="e">
    /// A <b>TEventArgs</b> that contains the event data.
    /// </param>
    /// <typeparam name="TEventArgs">
    /// The type of the event data generated by the event.
    /// </typeparam>
    public static void Emit<TEventArgs>(
      this EventHandler<TEventArgs> eventHandler, object sender, TEventArgs e)
      where TEventArgs : EventArgs
    {
      if (!eventHandler.IsNull())
        eventHandler(sender, e);
    }

    /// <summary>
    /// Determines whether the specified <see cref="int"/> equals the specified <see cref="char"/> as <see cref="byte"/>.
    /// And save this specified <see cref="int"/> as <see cref="byte"/> to the specified <strong>List&lt;byte&gt;</strong>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the <paramref name="value"/> parameter equals the <paramref name="c"/> parameter as <see cref="byte"/>; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="value">
    /// An <see cref="int"/> to compare.
    /// </param>
    /// <param name="c">
    /// A <see cref="char"/> to compare.
    /// </param>
    /// <param name="dest">
    /// A <strong>List&lt;byte&gt;</strong> to save the <paramref name="value"/> as <see cref="byte"/>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Is thrown when the <paramref name="value"/> parameter passed to a method is invalid because it is outside the allowable range of values as <see cref="byte"/>.
    /// </exception>
    public static bool EqualsAndSaveTo(this int value, char c, List<byte> dest)
    {
      if (value < 0 || value > 255)
        throw new ArgumentOutOfRangeException("value");

      var b = (byte)value;
      dest.Add(b);

      return b == Convert.ToByte(c);
    }

    /// <summary>
    /// Determines whether the entry with the specified key exists in the specified <see cref="NameValueCollection"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the entry with the <paramref name="name"/> exists in the <paramref name="collection"/>; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="collection">
    /// A <see cref="NameValueCollection"/> that contains the entries.
    /// </param>
    /// <param name="name">
    /// A <see cref="string"/> that contains the key of the entry to find.
    /// </param>
    public static bool Exists(this NameValueCollection collection, string name)
    {
      return collection.IsNull()
             ? false
             : !collection[name].IsNull();
    }

    /// <summary>
    /// Determines whether the entry with the specified both key and value exists in the specified <see cref="NameValueCollection"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the entry with the both <paramref name="name"/> and <paramref name="value"/> exists in the <paramref name="collection"/>; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="collection">
    /// A <see cref="NameValueCollection"/> that contains the entries.
    /// </param>
    /// <param name="name">
    /// A <see cref="string"/> that contains the key of the entry to find.
    /// </param>
    /// <param name="value">
    /// A <see cref="string"/> that contains the value of the entry to find.
    /// </param>
    public static bool Exists(this NameValueCollection collection, string name, string value)
    {
      if (collection.IsNull())
        return false;

      var values = collection[name];
      if (values.IsNull())
        return false;

      foreach (string v in values.Split(','))
        if (String.Compare(v.Trim(), value, true) == 0)
          return true;

      return false;
    }

    /// <summary>
    /// Gets the absolute path from the specified <see cref="Uri"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that contains the absolute path if got successfully; otherwise, <see langword="null"/>.
    /// </returns>
    /// <param name="uri">
    /// A <see cref="Uri"/> that contains the URI to get the absolute path from.
    /// </param>
    public static string GetAbsolutePath(this Uri uri)
    {
      if (uri.IsNull())
        return null;

      if (uri.IsAbsoluteUri)
        return uri.AbsolutePath;

      var uriString = uri.OriginalString;
      var i = uriString.IndexOf('/');
      if (i != 0)
        return null;

      i = uriString.IndexOfAny(new []{'?', '#'});
      return i > 0
             ? uriString.Substring(0, i)
             : uriString;
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
    public static CookieCollection GetCookies(this NameValueCollection headers, bool response)
    {
      var name = response ? "Set-Cookie" : "Cookie";
      if (headers.IsNull() || !headers.Exists(name))
        return new CookieCollection();

      return CookieCollection.Parse(headers[name], response);
    }

    /// <summary>
    /// Gets the description of the HTTP status code using the specified <see cref="WebSocketSharp.Net.HttpStatusCode"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that contains the description of the HTTP status code.
    /// </returns>
    /// <param name="code">
    /// One of <see cref="WebSocketSharp.Net.HttpStatusCode"/> values that contains an HTTP status code.
    /// </param>
    public static string GetDescription(this HttpStatusCode code)
    {
      return ((int)code).GetStatusDescription();
    }

    /// <summary>
    /// Gets the name from the specified <see cref="string"/> that contains a pair of name and value are separated by a separator string.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that contains the name if any; otherwise, <c>null</c>.
    /// </returns>
    /// <param name="nameAndValue">
    /// A <see cref="string"/> that contains a pair of name and value are separated by a separator string.
    /// </param>
    /// <param name="separator">
    /// A <see cref="string"/> that contains a separator string.
    /// </param>
    public static string GetName(this string nameAndValue, string separator)
    {
      return !nameAndValue.IsNullOrEmpty() && !separator.IsNullOrEmpty()
             ? nameAndValue.GetNameInternal(separator)
             : null;
    }

    /// <summary>
    /// Gets the name and value from the specified <see cref="string"/> that contains a pair of name and value are separated by a separator string.
    /// </summary>
    /// <returns>
    /// A <b>KeyValuePair&lt;string, string&gt;</b> that contains the name and value if any.
    /// </returns>
    /// <param name="nameAndValue">
    /// A <see cref="string"/> that contains a pair of name and value are separated by a separator string.
    /// </param>
    /// <param name="separator">
    /// A <see cref="string"/> that contains a separator string.
    /// </param>
    public static KeyValuePair<string, string> GetNameAndValue(this string nameAndValue, string separator)
    {
      var name  = nameAndValue.GetName(separator);
      var value = nameAndValue.GetValue(separator);
      return !name.IsNull()
             ? new KeyValuePair<string, string>(name, value)
             : new KeyValuePair<string, string>(null, null);
    }

    /// <summary>
    /// Gets the description of the HTTP status code using the specified <see cref="int"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that contains the description of the HTTP status code.
    /// </returns>
    /// <param name="code">
    /// An <see cref="int"/> that contains an HTTP status code.
    /// </param>
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

    /// <summary>
    /// Gets the value from the specified <see cref="string"/> that contains a pair of name and value are separated by a separator string.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that contains the value if any; otherwise, <c>null</c>.
    /// </returns>
    /// <param name="nameAndValue">
    /// A <see cref="string"/> that contains a pair of name and value are separated by a separator string.
    /// </param>
    /// <param name="separator">
    /// A <see cref="string"/> that contains a separator string.
    /// </param>
    public static string GetValue(this string nameAndValue, string separator)
    {
      return !nameAndValue.IsNullOrEmpty() && !separator.IsNullOrEmpty()
             ? nameAndValue.GetValueInternal(separator)
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
    /// <c>true</c> if <paramref name="code"/> is in the allowable range of the WebSocket close status code; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="code">
    /// A <see cref="ushort"/> to test.
    /// </param>
    public static bool IsCloseStatusCode(this ushort code)
    {
      return code < 1000
             ? false
             : code > 4999
               ? false
               : true;
    }

    /// <summary>
    /// Determines whether the specified <see cref="string"/> is a <see cref="String.Empty"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="value"/> is <see cref="String.Empty"/>; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="value">
    /// A <see cref="string"/> to test.
    /// </param>
    public static bool IsEmpty(this string value)
    {
      return value == String.Empty ? true : false;
    }

    /// <summary>
    /// Determines whether the specified <see cref="string"/> is enclosed in the specified <see cref="char"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="str"/> is enclosed in <paramref name="c"/>; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="str">
    /// A <see cref="string"/> to test.
    /// </param>
    /// <param name="c">
    /// A <see cref="char"/> that contains character to find.
    /// </param>
    public static bool IsEnclosedIn(this string str, char c)
    {
      return str.IsNullOrEmpty()
             ? false
             : str[0] == c && str[str.Length - 1] == c;
    }

    /// <summary>
    /// Determines whether the specified <see cref="WebSocketSharp.ByteOrder"/> is host (this computer architecture) byte order.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the <paramref name="order"/> parameter is host byte order; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="order">
    /// A <see cref="WebSocketSharp.ByteOrder"/> to test.
    /// </param>
    public static bool IsHostOrder(this ByteOrder order)
    {
      // true : !(true ^ true)  or !(false ^ false)
      // false: !(true ^ false) or !(false ^ true)
      return !(BitConverter.IsLittleEndian ^ (order == ByteOrder.LITTLE));
    }

    /// <summary>
    /// Determines whether the specified <see cref="System.Net.IPAddress"/> represents a local IP address.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="address"/> represents a local IP address; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="address">
    /// A <see cref="System.Net.IPAddress"/> to test.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="address"/> is <see langword="null"/>.
    /// </exception>
    public static bool IsLocal(this System.Net.IPAddress address)
    {
      if (address.IsNull())
        throw new ArgumentNullException("address");

      if (System.Net.IPAddress.IsLoopback(address))
        return true;

      var host  = System.Net.Dns.GetHostName();
      var addrs = System.Net.Dns.GetHostAddresses(host);
      foreach (var addr in addrs)
        if (address.Equals(addr))
          return true;

      return false;
    }

    /// <summary>
    /// Determines whether the specified object is <see langword="null"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the <paramref name="obj"/> parameter is <see langword="null"/>; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="obj">
    /// A <b>class</b> to test.
    /// </param>
    /// <typeparam name="T">
    /// The type of the <paramref name="obj"/> parameter.
    /// </typeparam>
    public static bool IsNull<T>(this T obj)
      where T : class
    {
      return obj == null ? true : false;
    }

    /// <summary>
    /// Determines whether the specified object is <see langword="null"/>.
    /// And invokes the specified <see cref="Action"/> delegate if the specified object is <see langword="null"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the <paramref name="obj"/> parameter is <see langword="null"/>; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="obj">
    /// A <b>class</b> to test.
    /// </param>
    /// <param name="act">
    /// An <see cref="Action"/> delegate that contains the method(s) called if the <paramref name="obj"/> is <see langword="null"/>.
    /// </param>
    /// <typeparam name="T">
    /// The type of the <paramref name="obj"/> parameter.
    /// </typeparam>
    public static bool IsNullDo<T>(this T obj, Action act)
      where T : class
    {
      if (obj.IsNull())
      {
        act();
        return true;
      }

      return false;
    }

    /// <summary>
    /// Determines whether the specified <see cref="string"/> is <see langword="null"/> or <see cref="String.Empty"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the <paramref name="value"/> parameter is <see langword="null"/> or <see cref="String.Empty"/>; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="value">
    /// A <see cref="string"/> to test.
    /// </param>
    public static bool IsNullOrEmpty(this string value)
    {
      return String.IsNullOrEmpty(value);
    }

    /// <summary>
    /// Determines whether the specified <see cref="string"/> is predefined scheme.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the <paramref name="scheme"/> parameter is the predefined scheme; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="scheme">
    /// A <see cref="string"/> to test.
    /// </param>
    public static bool IsPredefinedScheme(this string scheme)
    {
      if (scheme.IsNull() && scheme.Length < 2)
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

    /// <summary>
    /// Determines whether the specified <see cref="HttpListenerRequest"/> is the HTTP Upgrade request
    /// to switch to the specified <paramref name="protocol"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the specified <see cref="HttpListenerRequest"/> is the HTTP Upgrade request
    /// to switch to the specified <paramref name="protocol"/>; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="request">
    /// A <see cref="HttpListenerRequest"/> that contains an HTTP request information.
    /// </param>
    /// <param name="protocol">
    /// A <see cref="string"/> that contains a protocol name.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <para>
    /// <paramref name="request"/> is <see langword="null"/>.
    /// </para>
    /// <para>
    /// -or-
    /// </para>
    /// <para>
    /// <paramref name="protocol"/> is <see langword="null"/>.
    /// </para>
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="protocol"/> is <see cref="String.Empty"/>.
    /// </exception>
    public static bool IsUpgradeTo(this HttpListenerRequest request, string protocol)
    {
      if (request.IsNull())
        throw new ArgumentNullException("request");

      if (protocol.IsNull())
        throw new ArgumentNullException("protocol");

      if (protocol.IsEmpty())
        throw new ArgumentException("Must not be empty.", "protocol");

      if (!request.Headers.Exists("Upgrade", protocol))
        return false;

      if (!request.Headers.Exists("Connection", "Upgrade"))
        return false;

      return true;
    }

    /// <summary>
    /// Determines whether the specified <see cref="string"/> is valid absolute path.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the <paramref name="absPath"/> parameter is valid absolute path; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="absPath">
    /// A <see cref="string"/> to test.
    /// </param>
    /// <param name="message">
    /// A <see cref="string"/> that receives a message if the <paramref name="absPath"/> is invalid.
    /// </param>
    public static bool IsValidAbsolutePath(this string absPath, out string message)
    {
      if (absPath.IsNullOrEmpty())
      {
        message = "Must not be null or empty.";
        return false;
      }

      var i = absPath.IndexOf('/');
      if (i != 0)
      {
        message = "Not absolute path: " + absPath;
        return false;
      }

      i = absPath.IndexOfAny(new []{'?', '#'});
      if (i != -1)
      {
        message = "Must not contain either or both query and fragment components: " + absPath;
        return false;
      }

      message = String.Empty;
      return true;
    }

    /// <summary>
    /// Determines whether the specified <see cref="string"/> is a URI string.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the <paramref name="uriString"/> parameter is maybe a URI string; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="uriString">
    /// A <see cref="string"/> to test.
    /// </param>
    public static bool MaybeUri(this string uriString)
    {
      if (uriString.IsNullOrEmpty())
        return false;

      int p = uriString.IndexOf(':');
      if (p == -1)
        return false;

      if (p >= 10)
        return false;

      return uriString.Substring(0, p).IsPredefinedScheme();
    }

    /// <summary>
    /// Determines whether two specified <see cref="string"/> objects don't have the same value.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the value of <paramref name="expected"/> parameter isn't the same as the value of <paramref name="actual"/> parameter; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="expected">
    /// The first <see cref="string"/> to compare.
    /// </param>
    /// <param name="actual">
    /// The second <see cref="string"/> to compare.
    /// </param>
    /// <param name="ignoreCase">
    /// A <see cref="bool"/> that indicates a case-sensitive or insensitive comparison. (<c>true</c> indicates a case-insensitive comparison.)
    /// </param>
    public static bool NotEqual(this string expected, string actual, bool ignoreCase)
    {
      return String.Compare(expected, actual, ignoreCase) != 0
             ? true
             : false;
    }

    /// <summary>
    /// Reads a block of bytes from the specified stream and returns the read data in an array of <see cref="byte"/>.
    /// </summary>
    /// <returns>
    /// An array of <see cref="byte"/> that receives the read data.
    /// </returns>
    /// <param name="stream">
    /// A <see cref="Stream"/> that contains the data to read.
    /// </param>
    /// <param name="length">
    /// An <see cref="int"/> that contains the number of bytes to read.
    /// </param>
    public static byte[] ReadBytes(this Stream stream, int length)
    {
      if (stream.IsNull() || length <= 0)
        return new byte[]{};

      var buffer  = new byte[length];
      var readLen = stream.Read(buffer, 0, length);

      return readLen == length
             ? buffer
             : readLen > 0
               ? buffer.SubArray(0, readLen)
               : new byte[]{};
    }

    /// <summary>
    /// Reads a block of bytes from the specified stream and returns the read data in an array of <see cref="byte"/>.
    /// </summary>
    /// <returns>
    /// An array of <see cref="byte"/> that receives the read data.
    /// </returns>
    /// <param name="stream">
    /// A <see cref="Stream"/> that contains the data to read.
    /// </param>
    /// <param name="length">
    /// A <see cref="long"/> that contains the number of bytes to read.
    /// </param>
    public static byte[] ReadBytes(this Stream stream, long length)
    {
      return stream.ReadBytes(length, 1024);
    }

    /// <summary>
    /// Reads a block of bytes from the specified stream and returns the read data in an array of <see cref="byte"/>.
    /// </summary>
    /// <returns>
    /// An array of <see cref="byte"/> that receives the read data.
    /// </returns>
    /// <param name="stream">
    /// A <see cref="Stream"/> that contains the data to read.
    /// </param>
    /// <param name="length">
    /// A <see cref="long"/> that contains the number of bytes to read.
    /// </param>
    /// <param name="bufferLength">
    /// An <see cref="int"/> that contains the buffer size in bytes of each internal read.
    /// </param>
    public static byte[] ReadBytes(this Stream stream, long length, int bufferLength)
    {
      if (stream.IsNull() || length <= 0)
        return new byte[]{};

      if (bufferLength <= 0)
        bufferLength = 1024;

      var  count      = length / bufferLength;
      var  rem        = length % bufferLength;
      var  readData   = new List<byte>();
      var  readBuffer = new byte[bufferLength];
      long readLen    = 0;
      var  tmpLen     = 0;

      Action<byte[]> read = (buffer) =>
      {
        tmpLen = stream.Read(buffer, 0, buffer.Length);
        if (tmpLen > 0)
        {
          readLen += tmpLen;
          readData.AddRange(buffer.SubArray(0, tmpLen));
        }
      };

      count.Times(() =>
      {
        read(readBuffer);
      });

      if (rem > 0)
        read(new byte[rem]);

      return readLen > 0
             ? readData.ToArray()
             : new byte[]{};
    }

    /// <summary>
    /// Retrieves a sub-array from the specified <paramref name="array"/>. A sub-array starts at the specified element position.
    /// </summary>
    /// <returns>
    /// An array of T that receives a sub-array, or an empty array of T if any problems with the parameters.
    /// </returns>
    /// <param name="array">
    /// An array of T that contains the data to retrieve a sub-array.
    /// </param>
    /// <param name="startIndex">
    /// An <see cref="int"/> that contains the zero-based starting position of a sub-array in the <paramref name="array"/>.
    /// </param>
    /// <param name="length">
    /// An <see cref="int"/> that contains the number of elements to retrieve a sub-array.
    /// </param>
    /// <typeparam name="T">
    /// The type of elements in the <paramref name="array"/>.
    /// </typeparam>
    public static T[] SubArray<T>(this T[] array, int startIndex, int length)
    {
      if (array.IsNull() || array.Length == 0)
        return new T[]{};

      if (startIndex < 0 || length <= 0)
        return new T[]{};

      if (startIndex + length > array.Length)
        return new T[]{};

      if (startIndex == 0 && array.Length == length)
        return array;

      T[] subArray = new T[length];
      Array.Copy(array, startIndex, subArray, 0, length);

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
    public static void Times(this int n, Action act)
    {
      if (n > 0 && !act.IsNull())
        ((ulong)n).times(act);
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
    public static void Times(this long n, Action act)
    {
      if (n > 0 && !act.IsNull())
        ((ulong)n).times(act);
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
    public static void Times(this uint n, Action act)
    {
      if (n > 0 && !act.IsNull())
        ((ulong)n).times(act);
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
    public static void Times(this ulong n, Action act)
    {
      if (n > 0 && !act.IsNull())
        n.times(act);
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
    public static void Times(this int n, Action<int> act)
    {
      if (n > 0 && !act.IsNull())
        for (int i = 0; i < n; i++)
          act(i);
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
    public static void Times(this long n, Action<long> act)
    {
      if (n > 0 && !act.IsNull())
        for (long i = 0; i < n; i++)
          act(i);
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
    public static void Times(this uint n, Action<uint> act)
    {
      if (n > 0 && !act.IsNull())
        for (uint i = 0; i < n; i++)
          act(i);
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
    public static void Times(this ulong n, Action<ulong> act)
    {
      if (n > 0 && !act.IsNull())
        for (ulong i = 0; i < n; i++)
          act(i);
    }

    /// <summary>
    /// Converts the specified array of <see cref="byte"/> to the specified type data.
    /// </summary>
    /// <returns>
    /// A T converted from the <paramref name="src"/>, or a default value of T
    /// if the <paramref name="src"/> is an empty array of <see cref="byte"/>
    /// or if the types of T aren't the <see cref="bool"/>, <see cref="char"/>, <see cref="double"/>,
    /// <see cref="float"/>, <see cref="int"/>, <see cref="long"/>, <see cref="short"/>,
    /// <see cref="uint"/>, <see cref="ulong"/>, <see cref="ushort"/>.
    /// </returns>
    /// <param name="src">
    /// An array of <see cref="byte"/> to convert.
    /// </param>
    /// <param name="srcOrder">
    /// A <see cref="WebSocketSharp.ByteOrder"/> that indicates the byte order of the <paramref name="src"/>.
    /// </param>
    /// <typeparam name="T">
    /// The type of the return value. The T must be a value type.
    /// </typeparam>
    /// <exception cref="ArgumentNullException">
    /// Is thrown when the <paramref name="src"/> parameter passed to a method is invalid because it is <see langword="null"/>.
    /// </exception>
    public static T To<T>(this byte[] src, ByteOrder srcOrder)
      where T : struct
    {
      if (src.IsNull())
        throw new ArgumentNullException("src");

      if (src.Length == 0)
        return default(T);

      var type   = typeof(T);
      var buffer = src.ToHostOrder(srcOrder);
      if (type == typeof(Boolean))
        return (T)(object)BitConverter.ToBoolean(buffer, 0);

      if (type == typeof(Char))
        return (T)(object)BitConverter.ToChar(buffer, 0);

      if (type == typeof(Double))
        return (T)(object)BitConverter.ToDouble(buffer, 0);

      if (type == typeof(Int16))
        return (T)(object)BitConverter.ToInt16(buffer, 0);

      if (type == typeof(Int32))
        return (T)(object)BitConverter.ToInt32(buffer, 0);

      if (type == typeof(Int64))
        return (T)(object)BitConverter.ToInt64(buffer, 0);

      if (type == typeof(Single))
        return (T)(object)BitConverter.ToSingle(buffer, 0);

      if (type == typeof(UInt16))
        return (T)(object)BitConverter.ToUInt16(buffer, 0);

      if (type == typeof(UInt32))
        return (T)(object)BitConverter.ToUInt32(buffer, 0);

      if (type == typeof(UInt64))
        return (T)(object)BitConverter.ToUInt64(buffer, 0);

      return default(T);
    }

    /// <summary>
    /// Converts the specified data to an array of <see cref="byte"/>.
    /// </summary>
    /// <returns>
    /// An array of <see cref="byte"/> converted from the <paramref name="value"/>.
    /// </returns>
    /// <param name="value">
    /// A T to convert.
    /// </param>
    /// <param name="order">
    /// A <see cref="WebSocketSharp.ByteOrder"/> that indicates the byte order of the return.
    /// </param>
    /// <typeparam name="T">
    /// The type of the <paramref name="value"/>. The T must be a value type.
    /// </typeparam>
    public static byte[] ToBytes<T>(this T value, ByteOrder order)
      where T : struct
    {
      var type = typeof(T);
      byte[] buffer;
      if (type == typeof(Boolean))
      {
        buffer = BitConverter.GetBytes((Boolean)(object)value);
      }
      else if (type == typeof(Char))
      {
        buffer = BitConverter.GetBytes((Char)(object)value);
      }
      else if (type == typeof(Double))
      {
        buffer = BitConverter.GetBytes((Double)(object)value);
      }
      else if (type == typeof(Int16))
      {
        buffer = BitConverter.GetBytes((Int16)(object)value);
      }
      else if (type == typeof(Int32))
      {
        buffer = BitConverter.GetBytes((Int32)(object)value);
      }
      else if (type == typeof(Int64))
      {
        buffer = BitConverter.GetBytes((Int64)(object)value);
      }
      else if (type == typeof(Single))
      {
        buffer = BitConverter.GetBytes((Single)(object)value);
      }
      else if (type == typeof(UInt16))
      {
        buffer = BitConverter.GetBytes((UInt16)(object)value);
      }
      else if (type == typeof(UInt32))
      {
        buffer = BitConverter.GetBytes((UInt32)(object)value);
      }
      else if (type == typeof(UInt64))
      {
        buffer = BitConverter.GetBytes((UInt64)(object)value);
      }
      else
      {
        buffer = new byte[]{};
      }

      return buffer.Length == 0 || order.IsHostOrder()
             ? buffer
             : buffer.Reverse().ToArray();
    }

    /// <summary>
    /// Converts the order of the specified array of <see cref="byte"/> to the host byte order.
    /// </summary>
    /// <returns>
    /// An array of <see cref="byte"/> converted from the <paramref name="src"/>.
    /// </returns>
    /// <param name="src">
    /// An array of <see cref="byte"/> to convert.
    /// </param>
    /// <param name="srcOrder">
    /// A <see cref="WebSocketSharp.ByteOrder"/> that indicates the byte order of the <paramref name="src"/>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Is thrown when the <paramref name="src"/> parameter passed to a method is invalid because it is <see langword="null"/>.
    /// </exception>
    public static byte[] ToHostOrder(this byte[] src, ByteOrder srcOrder)
    {
      if (src.IsNull())
        throw new ArgumentNullException("src");

      return src.Length == 0 || srcOrder.IsHostOrder()
             ? src
             : src.Reverse().ToArray();
    }

    /// <summary>
    /// Converts the specified array to a <see cref="string"/> concatenated the specified separator string
    /// between each element of this array.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> converted from the <paramref name="array"/> parameter, or a <see cref="String.Empty"/>
    /// if the length of the <paramref name="array"/> is zero.
    /// </returns>
    /// <param name="array">
    /// An array of T to convert.
    /// </param>
    /// <param name="separator">
    /// A <see cref="string"/> that contains a separator string.
    /// </param>
    /// <typeparam name="T">
    /// The type of elements in the <paramref name="array"/>.
    /// </typeparam>
    /// <exception cref="ArgumentNullException">
    /// Is thrown when the <paramref name="array"/> parameter passed to a method is invalid because it is <see langword="null"/>.
    /// </exception>
    public static string ToString<T>(this T[] array, string separator)
    {
      if (array.IsNull())
        throw new ArgumentNullException("array");

      var len = array.Length;
      if (len == 0)
        return String.Empty;

      if (separator.IsNull())
        separator = String.Empty;

      var sb = new StringBuilder();
      (len - 1).Times(i =>
        sb.AppendFormat("{0}{1}", array[i].ToString(), separator)
      );

      sb.Append(array[len - 1].ToString());
      return sb.ToString();
    }

    /// <summary>
    /// Converts the specified <see cref="string"/> to a <see cref="Uri"/> object.
    /// </summary>
    /// <returns>
    /// A <see cref="Uri"/> converted from the <paramref name="uriString"/> parameter, or <see langword="null"/>
    /// if the <paramref name="uriString"/> is <see langword="null"/> or <see cref="String.Empty"/>.
    /// </returns>
    /// <param name="uriString">
    /// A <see cref="string"/> to convert.
    /// </param>
    public static Uri ToUri(this string uriString)
    {
      return uriString.IsNullOrEmpty()
             ? null
             : uriString.MaybeUri()
               ? new Uri(uriString)
               : new Uri(uriString, UriKind.Relative);
    }

    /// <summary>
    /// Tries to create a new WebSocket <see cref="Uri"/> using the specified <paramref name="uriString"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the WebSocket <see cref="Uri"/> was successfully created; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="uriString">
    /// A <see cref="string"/> that contains a WebSocket URI.
    /// </param>
    /// <param name="result">
    /// When this method returns, contains a created WebSocket <see cref="Uri"/> if the <paramref name="uriString"/> parameter is valid WebSocket URI; otherwise, <see langword="null"/>.
    /// </param>
    /// <param name="message">
    /// When this method returns, contains a error message <see cref="string"/> if the <paramref name="uriString"/> parameter is invalid WebSocket URI; otherwise, <c>String.Empty</c>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Is thrown when the <paramref name="uriString"/> parameter passed to a method is invalid because it is <see langword="null"/>.
    /// </exception>
    public static bool TryCreateWebSocketUri(this string uriString, out Uri result, out string message)
    {
      if (uriString.IsNull())
        throw new ArgumentNullException("uriString");

      result = null;
      if (uriString.IsEmpty())
      {
        message = "Must not be empty.";
        return false;
      }

      var uri = uriString.ToUri();
      if (!uri.IsAbsoluteUri)
      {
        message = "Not absolute URI: " + uriString;
        return false;
      }

      var scheme = uri.Scheme;
      if (scheme != "ws" && scheme != "wss")
      {
        message = "Unsupported scheme: " + scheme;
        return false;
      }

      var fragment = uri.Fragment;
      if (!String.IsNullOrEmpty(fragment))
      {
        message = "Must not contain the fragment component: " + uriString;
        return false;
      }

      var port = uri.Port;
      if (port > 0)
      {
        if ((scheme == "ws"  && port == 443) ||
            (scheme == "wss" && port == 80))
        {
          message = String.Format(
            "Invalid pair of scheme and port: {0}, {1}", scheme, port);
          return false;
        }
      }
      else
      {
        port = scheme == "ws"
             ? 80
             : 443;
        var url = String.Format("{0}://{1}:{2}{3}", scheme, uri.Host, port, uri.PathAndQuery);
        uri = url.ToUri();
      }

      result  = uri;
      message = String.Empty;

      return true;
    }

    /// <summary>
    /// URL-decodes the specified <see cref="string"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that receives a decoded string, or the <paramref name="s"/> parameter
    /// if the <paramref name="s"/> is <see langword="null"/> or <see cref="String.Empty"/>.
    /// </returns>
    /// <param name="s">
    /// A <see cref="string"/> to decode.
    /// </param>
    public static string UrlDecode(this string s)
    {
      return s.IsNullOrEmpty()
             ? s
             : HttpUtility.UrlDecode(s);
    }

    /// <summary>
    /// URL-encodes the specified <see cref="string"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that receives a encoded string, or the <paramref name="s"/> parameter
    /// if the <paramref name="s"/> is <see langword="null"/> or <see cref="String.Empty"/>.
    /// </returns>
    /// <param name="s">
    /// A <see cref="string"/> to encode.
    /// </param>
    public static string UrlEncode(this string s)
    {
      return s.IsNullOrEmpty()
             ? s
             : HttpUtility.UrlEncode(s);
    }

    /// <summary>
    /// Writes the specified content data using the specified <see cref="WebSocketSharp.Net.HttpListenerResponse"/>.
    /// </summary>
    /// <param name="response">
    /// A <see cref="WebSocketSharp.Net.HttpListenerResponse"/> that contains a network stream to write a content data.
    /// </param>
    /// <param name="content">
    /// An array of <see cref="byte"/> that contains a content data to write.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// Is thrown when the <paramref name="response"/> parameter passed to a method is invalid because it is <see langword="null"/>.
    /// </exception>
    public static void WriteContent(this HttpListenerResponse response, byte[] content)
    {
      if (response.IsNull())
        throw new ArgumentNullException("response");

      if (content.IsNull() || content.Length == 0)
        return;

      var output = response.OutputStream;
      response.ContentLength64 = content.Length;
      output.Write(content, 0, content.Length);
      output.Close();
    }

    #endregion
  }
}
