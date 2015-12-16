#region License
/*
 * WebHeaderCollection.cs
 *
 * This code is derived from WebHeaderCollection.cs (System.Net) of Mono
 * (http://www.mono-project.com).
 *
 * The MIT License
 *
 * Copyright (c) 2003 Ximian, Inc. (http://www.ximian.com)
 * Copyright (c) 2007 Novell, Inc. (http://www.novell.com)
 * Copyright (c) 2012-2015 sta.blockhead
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

#region Authors
/*
 * Authors:
 * - Lawrence Pit <loz@cable.a2000.nl>
 * - Gonzalo Paniagua Javier <gonzalo@ximian.com>
 * - Miguel de Icaza <miguel@novell.com>
 */
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Text;

namespace WebSocketSharp.Net
{
  /// <summary>
  /// Provides a collection of the HTTP headers associated with a request or response.
  /// </summary>
  [Serializable]
  [ComVisible (true)]
  public class WebHeaderCollection : NameValueCollection, ISerializable
  {
    #region Private Fields

    private static readonly Dictionary<string, HttpHeaderInfo> _headers;
    private bool                                               _internallyUsed;
    private HttpHeaderType                                     _state;

    #endregion

    #region Static Constructor

    static WebHeaderCollection ()
    {
      _headers =
        new Dictionary<string, HttpHeaderInfo> (StringComparer.InvariantCultureIgnoreCase) {
          {
            "Accept",
            new HttpHeaderInfo (
              "Accept",
              HttpHeaderType.Request | HttpHeaderType.Restricted | HttpHeaderType.MultiValue)
          },
          {
            "AcceptCharset",
            new HttpHeaderInfo (
              "Accept-Charset",
              HttpHeaderType.Request | HttpHeaderType.MultiValue)
          },
          {
            "AcceptEncoding",
            new HttpHeaderInfo (
              "Accept-Encoding",
              HttpHeaderType.Request | HttpHeaderType.MultiValue)
          },
          {
            "AcceptLanguage",
            new HttpHeaderInfo (
              "Accept-Language",
              HttpHeaderType.Request | HttpHeaderType.MultiValue)
          },
          {
            "AcceptRanges",
            new HttpHeaderInfo (
              "Accept-Ranges",
              HttpHeaderType.Response | HttpHeaderType.MultiValue)
          },
          {
            "Age",
            new HttpHeaderInfo (
              "Age",
              HttpHeaderType.Response)
          },
          {
            "Allow",
            new HttpHeaderInfo (
              "Allow",
              HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.MultiValue)
          },
          {
            "Authorization",
            new HttpHeaderInfo (
              "Authorization",
              HttpHeaderType.Request | HttpHeaderType.MultiValue)
          },
          {
            "CacheControl",
            new HttpHeaderInfo (
              "Cache-Control",
              HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.MultiValue)
          },
          {
            "Connection",
            new HttpHeaderInfo (
              "Connection",
              HttpHeaderType.Request |
              HttpHeaderType.Response |
              HttpHeaderType.Restricted |
              HttpHeaderType.MultiValue)
          },
          {
            "ContentEncoding",
            new HttpHeaderInfo (
              "Content-Encoding",
              HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.MultiValue)
          },
          {
            "ContentLanguage",
            new HttpHeaderInfo (
              "Content-Language",
              HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.MultiValue)
          },
          {
            "ContentLength",
            new HttpHeaderInfo (
              "Content-Length",
              HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.Restricted)
          },
          {
            "ContentLocation",
            new HttpHeaderInfo (
              "Content-Location",
              HttpHeaderType.Request | HttpHeaderType.Response)
          },
          {
            "ContentMd5",
            new HttpHeaderInfo (
              "Content-MD5",
              HttpHeaderType.Request | HttpHeaderType.Response)
          },
          {
            "ContentRange",
            new HttpHeaderInfo (
              "Content-Range",
              HttpHeaderType.Request | HttpHeaderType.Response)
          },
          {
            "ContentType",
            new HttpHeaderInfo (
              "Content-Type",
              HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.Restricted)
          },
          {
            "Cookie",
            new HttpHeaderInfo (
              "Cookie",
              HttpHeaderType.Request)
          },
          {
            "Cookie2",
            new HttpHeaderInfo (
              "Cookie2",
              HttpHeaderType.Request)
          },
          {
            "Date",
            new HttpHeaderInfo (
              "Date",
              HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.Restricted)
          },
          {
            "Expect",
            new HttpHeaderInfo (
              "Expect",
              HttpHeaderType.Request | HttpHeaderType.Restricted | HttpHeaderType.MultiValue)
          },
          {
            "Expires",
            new HttpHeaderInfo (
              "Expires",
              HttpHeaderType.Request | HttpHeaderType.Response)
          },
          {
            "ETag",
            new HttpHeaderInfo (
              "ETag",
              HttpHeaderType.Response)
          },
          {
            "From",
            new HttpHeaderInfo (
              "From",
              HttpHeaderType.Request)
          },
          {
            "Host",
            new HttpHeaderInfo (
              "Host",
              HttpHeaderType.Request | HttpHeaderType.Restricted)
          },
          {
            "IfMatch",
            new HttpHeaderInfo (
              "If-Match",
              HttpHeaderType.Request | HttpHeaderType.MultiValue)
          },
          {
            "IfModifiedSince",
            new HttpHeaderInfo (
              "If-Modified-Since",
              HttpHeaderType.Request | HttpHeaderType.Restricted)
          },
          {
            "IfNoneMatch",
            new HttpHeaderInfo (
              "If-None-Match",
              HttpHeaderType.Request | HttpHeaderType.MultiValue)
          },
          {
            "IfRange",
            new HttpHeaderInfo (
              "If-Range",
              HttpHeaderType.Request)
          },
          {
            "IfUnmodifiedSince",
            new HttpHeaderInfo (
              "If-Unmodified-Since",
              HttpHeaderType.Request)
          },
          {
            "KeepAlive",
            new HttpHeaderInfo (
              "Keep-Alive",
              HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.MultiValue)
          },
          {
            "LastModified",
            new HttpHeaderInfo (
              "Last-Modified",
              HttpHeaderType.Request | HttpHeaderType.Response)
          },
          {
            "Location",
            new HttpHeaderInfo (
              "Location",
              HttpHeaderType.Response)
          },
          {
            "MaxForwards",
            new HttpHeaderInfo (
              "Max-Forwards",
              HttpHeaderType.Request)
          },
          {
            "Pragma",
            new HttpHeaderInfo (
              "Pragma",
              HttpHeaderType.Request | HttpHeaderType.Response)
          },
          {
            "ProxyAuthenticate",
            new HttpHeaderInfo (
              "Proxy-Authenticate",
              HttpHeaderType.Response | HttpHeaderType.MultiValue)
          },
          {
            "ProxyAuthorization",
            new HttpHeaderInfo (
              "Proxy-Authorization",
              HttpHeaderType.Request)
          },
          {
            "ProxyConnection",
            new HttpHeaderInfo (
              "Proxy-Connection",
              HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.Restricted)
          },
          {
            "Public",
            new HttpHeaderInfo (
              "Public",
              HttpHeaderType.Response | HttpHeaderType.MultiValue)
          },
          {
            "Range",
            new HttpHeaderInfo (
              "Range",
              HttpHeaderType.Request | HttpHeaderType.Restricted | HttpHeaderType.MultiValue)
          },
          {
            "Referer",
            new HttpHeaderInfo (
              "Referer",
              HttpHeaderType.Request | HttpHeaderType.Restricted)
          },
          {
            "RetryAfter",
            new HttpHeaderInfo (
              "Retry-After",
              HttpHeaderType.Response)
          },
          {
            "SecWebSocketAccept",
            new HttpHeaderInfo (
              "Sec-WebSocket-Accept",
              HttpHeaderType.Response | HttpHeaderType.Restricted)
          },
          {
            "SecWebSocketExtensions",
            new HttpHeaderInfo (
              "Sec-WebSocket-Extensions",
              HttpHeaderType.Request |
              HttpHeaderType.Response |
              HttpHeaderType.Restricted |
              HttpHeaderType.MultiValueInRequest)
          },
          {
            "SecWebSocketKey",
            new HttpHeaderInfo (
              "Sec-WebSocket-Key",
              HttpHeaderType.Request | HttpHeaderType.Restricted)
          },
          {
            "SecWebSocketProtocol",
            new HttpHeaderInfo (
              "Sec-WebSocket-Protocol",
              HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.MultiValueInRequest)
          },
          {
            "SecWebSocketVersion",
            new HttpHeaderInfo (
              "Sec-WebSocket-Version",
              HttpHeaderType.Request |
              HttpHeaderType.Response |
              HttpHeaderType.Restricted |
              HttpHeaderType.MultiValueInResponse)
          },
          {
            "Server",
            new HttpHeaderInfo (
              "Server",
              HttpHeaderType.Response)
          },
          {
            "SetCookie",
            new HttpHeaderInfo (
              "Set-Cookie",
              HttpHeaderType.Response | HttpHeaderType.MultiValue)
          },
          {
            "SetCookie2",
            new HttpHeaderInfo (
              "Set-Cookie2",
              HttpHeaderType.Response | HttpHeaderType.MultiValue)
          },
          {
            "Te",
            new HttpHeaderInfo (
              "TE",
              HttpHeaderType.Request)
          },
          {
            "Trailer",
            new HttpHeaderInfo (
              "Trailer",
              HttpHeaderType.Request | HttpHeaderType.Response)
          },
          {
            "TransferEncoding",
            new HttpHeaderInfo (
              "Transfer-Encoding",
              HttpHeaderType.Request |
              HttpHeaderType.Response |
              HttpHeaderType.Restricted |
              HttpHeaderType.MultiValue)
          },
          {
            "Translate",
            new HttpHeaderInfo (
              "Translate",
              HttpHeaderType.Request)
          },
          {
            "Upgrade",
            new HttpHeaderInfo (
              "Upgrade",
              HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.MultiValue)
          },
          {
            "UserAgent",
            new HttpHeaderInfo (
              "User-Agent",
              HttpHeaderType.Request | HttpHeaderType.Restricted)
          },
          {
            "Vary",
            new HttpHeaderInfo (
              "Vary",
              HttpHeaderType.Response | HttpHeaderType.MultiValue)
          },
          {
            "Via",
            new HttpHeaderInfo (
              "Via",
              HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.MultiValue)
          },
          {
            "Warning",
            new HttpHeaderInfo (
              "Warning",
              HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.MultiValue)
          },
          {
            "WwwAuthenticate",
            new HttpHeaderInfo (
              "WWW-Authenticate",
              HttpHeaderType.Response | HttpHeaderType.Restricted | HttpHeaderType.MultiValue)
          }
        };
    }

    #endregion

    #region Internal Constructors

    internal WebHeaderCollection (HttpHeaderType state, bool internallyUsed)
    {
      _state = state;
      _internallyUsed = internallyUsed;
    }

    #endregion

    #region Protected Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="WebHeaderCollection"/> class from
    /// the specified <see cref="SerializationInfo"/> and <see cref="StreamingContext"/>.
    /// </summary>
    /// <param name="serializationInfo">
    /// A <see cref="SerializationInfo"/> that contains the serialized object data.
    /// </param>
    /// <param name="streamingContext">
    /// A <see cref="StreamingContext"/> that specifies the source for the deserialization.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="serializationInfo"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// An element with the specified name isn't found in <paramref name="serializationInfo"/>.
    /// </exception>
    protected WebHeaderCollection (
      SerializationInfo serializationInfo, StreamingContext streamingContext)
    {
      if (serializationInfo == null)
        throw new ArgumentNullException ("serializationInfo");

      try {
        _internallyUsed = serializationInfo.GetBoolean ("InternallyUsed");
        _state = (HttpHeaderType) serializationInfo.GetInt32 ("State");

        var cnt = serializationInfo.GetInt32 ("Count");
        for (var i = 0; i < cnt; i++) {
          base.Add (
            serializationInfo.GetString (i.ToString ()),
            serializationInfo.GetString ((cnt + i).ToString ()));
        }
      }
      catch (SerializationException ex) {
        throw new ArgumentException (ex.Message, "serializationInfo", ex);
      }
    }

    #endregion

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="WebHeaderCollection"/> class.
    /// </summary>
    public WebHeaderCollection ()
    {
    }

    #endregion

    #region Internal Properties

    internal HttpHeaderType State {
      get {
        return _state;
      }
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets all header names in the collection.
    /// </summary>
    /// <value>
    /// An array of <see cref="string"/> that contains all header names in the collection.
    /// </value>
    public override string[] AllKeys {
      get {
        return base.AllKeys;
      }
    }

    /// <summary>
    /// Gets the number of headers in the collection.
    /// </summary>
    /// <value>
    /// An <see cref="int"/> that represents the number of headers in the collection.
    /// </value>
    public override int Count {
      get {
        return base.Count;
      }
    }

    /// <summary>
    /// Gets or sets the specified request <paramref name="header"/> in the collection.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the value of the request <paramref name="header"/>.
    /// </value>
    /// <param name="header">
    /// One of the <see cref="HttpRequestHeader"/> enum values, represents
    /// the request header to get or set.
    /// </param>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="header"/> is a restricted header.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="value"/> contains invalid characters.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The length of <paramref name="value"/> is greater than 65,535 characters.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The current <see cref="WebHeaderCollection"/> instance doesn't allow
    /// the request <paramref name="header"/>.
    /// </exception>
    public string this[HttpRequestHeader header] {
      get {
        return Get (Convert (header));
      }

      set {
        Add (header, value);
      }
    }

    /// <summary>
    /// Gets or sets the specified response <paramref name="header"/> in the collection.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the value of the response <paramref name="header"/>.
    /// </value>
    /// <param name="header">
    /// One of the <see cref="HttpResponseHeader"/> enum values, represents
    /// the response header to get or set.
    /// </param>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="header"/> is a restricted header.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="value"/> contains invalid characters.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The length of <paramref name="value"/> is greater than 65,535 characters.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The current <see cref="WebHeaderCollection"/> instance doesn't allow
    /// the response <paramref name="header"/>.
    /// </exception>
    public string this[HttpResponseHeader header] {
      get {
        return Get (Convert (header));
      }

      set {
        Add (header, value);
      }
    }

    /// <summary>
    /// Gets a collection of header names in the collection.
    /// </summary>
    /// <value>
    /// A <see cref="NameObjectCollectionBase.KeysCollection"/> that contains
    /// all header names in the collection.
    /// </value>
    public override NameObjectCollectionBase.KeysCollection Keys {
      get {
        return base.Keys;
      }
    }

    #endregion

    #region Private Methods

    private void add (string name, string value, bool ignoreRestricted)
    {
      var act = ignoreRestricted
                ? (Action <string, string>) addWithoutCheckingNameAndRestricted
                : addWithoutCheckingName;

      doWithCheckingState (act, checkName (name), value, true);
    }

    private void addWithoutCheckingName (string name, string value)
    {
      doWithoutCheckingName (base.Add, name, value);
    }

    private void addWithoutCheckingNameAndRestricted (string name, string value)
    {
      base.Add (name, checkValue (value));
    }

    private static int checkColonSeparated (string header)
    {
      var idx = header.IndexOf (':');
      if (idx == -1)
        throw new ArgumentException ("No colon could be found.", "header");

      return idx;
    }

    private static HttpHeaderType checkHeaderType (string name)
    {
      var info = getHeaderInfo (name);
      return info == null
             ? HttpHeaderType.Unspecified
             : info.IsRequest && !info.IsResponse
               ? HttpHeaderType.Request
               : !info.IsRequest && info.IsResponse
                 ? HttpHeaderType.Response
                 : HttpHeaderType.Unspecified;
    }

    private static string checkName (string name)
    {
      if (name == null || name.Length == 0)
        throw new ArgumentNullException ("name");

      name = name.Trim ();
      if (!IsHeaderName (name))
        throw new ArgumentException ("Contains invalid characters.", "name");

      return name;
    }

    private void checkRestricted (string name)
    {
      if (!_internallyUsed && isRestricted (name, true))
        throw new ArgumentException ("This header must be modified with the appropiate property.");
    }

    private void checkState (bool response)
    {
      if (_state == HttpHeaderType.Unspecified)
        return;

      if (response && _state == HttpHeaderType.Request)
        throw new InvalidOperationException (
          "This collection has already been used to store the request headers.");

      if (!response && _state == HttpHeaderType.Response)
        throw new InvalidOperationException (
          "This collection has already been used to store the response headers.");
    }

    private static string checkValue (string value)
    {
      if (value == null || value.Length == 0)
        return String.Empty;

      value = value.Trim ();
      if (value.Length > 65535)
        throw new ArgumentOutOfRangeException ("value", "Greater than 65,535 characters.");

      if (!IsHeaderValue (value))
        throw new ArgumentException ("Contains invalid characters.", "value");

      return value;
    }

    private static string convert (string key)
    {
      HttpHeaderInfo info;
      return _headers.TryGetValue (key, out info) ? info.Name : String.Empty;
    }

    private void doWithCheckingState (
      Action <string, string> action, string name, string value, bool setState)
    {
      var type = checkHeaderType (name);
      if (type == HttpHeaderType.Request)
        doWithCheckingState (action, name, value, false, setState);
      else if (type == HttpHeaderType.Response)
        doWithCheckingState (action, name, value, true, setState);
      else
        action (name, value);
    }

    private void doWithCheckingState (
      Action <string, string> action, string name, string value, bool response, bool setState)
    {
      checkState (response);
      action (name, value);
      if (setState && _state == HttpHeaderType.Unspecified)
        _state = response ? HttpHeaderType.Response : HttpHeaderType.Request;
    }

    private void doWithoutCheckingName (Action <string, string> action, string name, string value)
    {
      checkRestricted (name);
      action (name, checkValue (value));
    }

    private static HttpHeaderInfo getHeaderInfo (string name)
    {
      foreach (var info in _headers.Values)
        if (info.Name.Equals (name, StringComparison.InvariantCultureIgnoreCase))
          return info;

      return null;
    }

    private static bool isRestricted (string name, bool response)
    {
      var info = getHeaderInfo (name);
      return info != null && info.IsRestricted (response);
    }

    private void removeWithoutCheckingName (string name, string unuse)
    {
      checkRestricted (name);
      base.Remove (name);
    }

    private void setWithoutCheckingName (string name, string value)
    {
      doWithoutCheckingName (base.Set, name, value);
    }

    #endregion

    #region Internal Methods

    internal static string Convert (HttpRequestHeader header)
    {
      return convert (header.ToString ());
    }

    internal static string Convert (HttpResponseHeader header)
    {
      return convert (header.ToString ());
    }

    internal void InternalRemove (string name)
    {
      base.Remove (name);
    }

    internal void InternalSet (string header, bool response)
    {
      var pos = checkColonSeparated (header);
      InternalSet (header.Substring (0, pos), header.Substring (pos + 1), response);
    }

    internal void InternalSet (string name, string value, bool response)
    {
      value = checkValue (value);
      if (IsMultiValue (name, response))
        base.Add (name, value);
      else
        base.Set (name, value);
    }

    internal static bool IsHeaderName (string name)
    {
      return name != null && name.Length > 0 && name.IsToken ();
    }

    internal static bool IsHeaderValue (string value)
    {
      return value.IsText ();
    }

    internal static bool IsMultiValue (string headerName, bool response)
    {
      if (headerName == null || headerName.Length == 0)
        return false;

      var info = getHeaderInfo (headerName);
      return info != null && info.IsMultiValue (response);
    }

    internal string ToStringMultiValue (bool response)
    {
      var buff = new StringBuilder ();
      Count.Times (
        i => {
          var key = GetKey (i);
          if (IsMultiValue (key, response))
            foreach (var val in GetValues (i))
              buff.AppendFormat ("{0}: {1}\r\n", key, val);
          else
            buff.AppendFormat ("{0}: {1}\r\n", key, Get (i));
        });

      return buff.Append ("\r\n").ToString ();
    }

    #endregion

    #region Protected Methods

    /// <summary>
    /// Adds a header to the collection without checking if the header is on
    /// the restricted header list.
    /// </summary>
    /// <param name="headerName">
    /// A <see cref="string"/> that represents the name of the header to add.
    /// </param>
    /// <param name="headerValue">
    /// A <see cref="string"/> that represents the value of the header to add.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="headerName"/> is <see langword="null"/> or empty.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="headerName"/> or <paramref name="headerValue"/> contains invalid characters.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The length of <paramref name="headerValue"/> is greater than 65,535 characters.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The current <see cref="WebHeaderCollection"/> instance doesn't allow
    /// the <paramref name="headerName"/>.
    /// </exception>
    protected void AddWithoutValidate (string headerName, string headerValue)
    {
      add (headerName, headerValue, true);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Adds the specified <paramref name="header"/> to the collection.
    /// </summary>
    /// <param name="header">
    /// A <see cref="string"/> that represents the header with the name and value separated by
    /// a colon (<c>':'</c>).
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="header"/> is <see langword="null"/>, empty, or the name part of
    /// <paramref name="header"/> is empty.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="header"/> doesn't contain a colon.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="header"/> is a restricted header.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   The name or value part of <paramref name="header"/> contains invalid characters.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The length of the value part of <paramref name="header"/> is greater than 65,535 characters.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The current <see cref="WebHeaderCollection"/> instance doesn't allow
    /// the <paramref name="header"/>.
    /// </exception>
    public void Add (string header)
    {
      if (header == null || header.Length == 0)
        throw new ArgumentNullException ("header");

      var pos = checkColonSeparated (header);
      add (header.Substring (0, pos), header.Substring (pos + 1), false);
    }

    /// <summary>
    /// Adds the specified request <paramref name="header"/> with
    /// the specified <paramref name="value"/> to the collection.
    /// </summary>
    /// <param name="header">
    /// One of the <see cref="HttpRequestHeader"/> enum values, represents
    /// the request header to add.
    /// </param>
    /// <param name="value">
    /// A <see cref="string"/> that represents the value of the header to add.
    /// </param>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="header"/> is a restricted header.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="value"/> contains invalid characters.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The length of <paramref name="value"/> is greater than 65,535 characters.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The current <see cref="WebHeaderCollection"/> instance doesn't allow
    /// the request <paramref name="header"/>.
    /// </exception>
    public void Add (HttpRequestHeader header, string value)
    {
      doWithCheckingState (addWithoutCheckingName, Convert (header), value, false, true);
    }

    /// <summary>
    /// Adds the specified response <paramref name="header"/> with
    /// the specified <paramref name="value"/> to the collection.
    /// </summary>
    /// <param name="header">
    /// One of the <see cref="HttpResponseHeader"/> enum values, represents
    /// the response header to add.
    /// </param>
    /// <param name="value">
    /// A <see cref="string"/> that represents the value of the header to add.
    /// </param>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="header"/> is a restricted header.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="value"/> contains invalid characters.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The length of <paramref name="value"/> is greater than 65,535 characters.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The current <see cref="WebHeaderCollection"/> instance doesn't allow
    /// the response <paramref name="header"/>.
    /// </exception>
    public void Add (HttpResponseHeader header, string value)
    {
      doWithCheckingState (addWithoutCheckingName, Convert (header), value, true, true);
    }

    /// <summary>
    /// Adds a header with the specified <paramref name="name"/> and
    /// <paramref name="value"/> to the collection.
    /// </summary>
    /// <param name="name">
    /// A <see cref="string"/> that represents the name of the header to add.
    /// </param>
    /// <param name="value">
    /// A <see cref="string"/> that represents the value of the header to add.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> is <see langword="null"/> or empty.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="name"/> or <paramref name="value"/> contains invalid characters.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="name"/> is a restricted header name.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The length of <paramref name="value"/> is greater than 65,535 characters.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The current <see cref="WebHeaderCollection"/> instance doesn't allow
    /// the header <paramref name="name"/>.
    /// </exception>
    public override void Add (string name, string value)
    {
      add (name, value, false);
    }

    /// <summary>
    /// Removes all headers from the collection.
    /// </summary>
    public override void Clear ()
    {
      base.Clear ();
      _state = HttpHeaderType.Unspecified;
    }

    /// <summary>
    /// Get the value of the header at the specified <paramref name="index"/> in the collection.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that receives the value of the header.
    /// </returns>
    /// <param name="index">
    /// An <see cref="int"/> that represents the zero-based index of the header to find.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is out of allowable range of indexes for the collection.
    /// </exception>
    public override string Get (int index)
    {
      return base.Get (index);
    }

    /// <summary>
    /// Get the value of the header with the specified <paramref name="name"/> in the collection.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that receives the value of the header if found;
    /// otherwise, <see langword="null"/>.
    /// </returns>
    /// <param name="name">
    /// A <see cref="string"/> that represents the name of the header to find.
    /// </param>
    public override string Get (string name)
    {
      return base.Get (name);
    }

    /// <summary>
    /// Gets the enumerator used to iterate through the collection.
    /// </summary>
    /// <returns>
    /// An <see cref="IEnumerator"/> instance used to iterate through the collection.
    /// </returns>
    public override IEnumerator GetEnumerator ()
    {
      return base.GetEnumerator ();
    }

    /// <summary>
    /// Get the name of the header at the specified <paramref name="index"/> in the collection.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that receives the header name.
    /// </returns>
    /// <param name="index">
    /// An <see cref="int"/> that represents the zero-based index of the header to find.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is out of allowable range of indexes for the collection.
    /// </exception>
    public override string GetKey (int index)
    {
      return base.GetKey (index);
    }

    /// <summary>
    /// Gets an array of header values stored in the specified <paramref name="index"/> position of
    /// the collection.
    /// </summary>
    /// <returns>
    /// An array of <see cref="string"/> that receives the header values if found;
    /// otherwise, <see langword="null"/>.
    /// </returns>
    /// <param name="index">
    /// An <see cref="int"/> that represents the zero-based index of the header to find.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is out of allowable range of indexes for the collection.
    /// </exception>
    public override string[] GetValues (int index)
    {
      var vals = base.GetValues (index);
      return vals != null && vals.Length > 0 ? vals : null;
    }

    /// <summary>
    /// Gets an array of header values stored in the specified <paramref name="header"/>.
    /// </summary>
    /// <returns>
    /// An array of <see cref="string"/> that receives the header values if found;
    /// otherwise, <see langword="null"/>.
    /// </returns>
    /// <param name="header">
    /// A <see cref="string"/> that represents the name of the header to find.
    /// </param>
    public override string[] GetValues (string header)
    {
      var vals = base.GetValues (header);
      return vals != null && vals.Length > 0 ? vals : null;
    }

    /// <summary>
    /// Populates the specified <see cref="SerializationInfo"/> with the data needed to serialize
    /// the <see cref="WebHeaderCollection"/>.
    /// </summary>
    /// <param name="serializationInfo">
    /// A <see cref="SerializationInfo"/> that holds the serialized object data.
    /// </param>
    /// <param name="streamingContext">
    /// A <see cref="StreamingContext"/> that specifies the destination for the serialization.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="serializationInfo"/> is <see langword="null"/>.
    /// </exception>
    [SecurityPermission (
      SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.SerializationFormatter)]
    public override void GetObjectData (
      SerializationInfo serializationInfo, StreamingContext streamingContext)
    {
      if (serializationInfo == null)
        throw new ArgumentNullException ("serializationInfo");

      serializationInfo.AddValue ("InternallyUsed", _internallyUsed);
      serializationInfo.AddValue ("State", (int) _state);

      var cnt = Count;
      serializationInfo.AddValue ("Count", cnt);
      cnt.Times (
        i => {
          serializationInfo.AddValue (i.ToString (), GetKey (i));
          serializationInfo.AddValue ((cnt + i).ToString (), Get (i));
        });
    }

    /// <summary>
    /// Determines whether the specified header can be set for the request.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the header is restricted; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="headerName">
    /// A <see cref="string"/> that represents the name of the header to test.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="headerName"/> is <see langword="null"/> or empty.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="headerName"/> contains invalid characters.
    /// </exception>
    public static bool IsRestricted (string headerName)
    {
      return isRestricted (checkName (headerName), false);
    }

    /// <summary>
    /// Determines whether the specified header can be set for the request or the response.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the header is restricted; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="headerName">
    /// A <see cref="string"/> that represents the name of the header to test.
    /// </param>
    /// <param name="response">
    /// <c>true</c> if does the test for the response; for the request, <c>false</c>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="headerName"/> is <see langword="null"/> or empty.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="headerName"/> contains invalid characters.
    /// </exception>
    public static bool IsRestricted (string headerName, bool response)
    {
      return isRestricted (checkName (headerName), response);
    }

    /// <summary>
    /// Implements the <see cref="ISerializable"/> interface and raises the deserialization event
    /// when the deserialization is complete.
    /// </summary>
    /// <param name="sender">
    /// An <see cref="object"/> that represents the source of the deserialization event.
    /// </param>
    public override void OnDeserialization (object sender)
    {
    }

    /// <summary>
    /// Removes the specified request <paramref name="header"/> from the collection.
    /// </summary>
    /// <param name="header">
    /// One of the <see cref="HttpRequestHeader"/> enum values, represents
    /// the request header to remove.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="header"/> is a restricted header.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The current <see cref="WebHeaderCollection"/> instance doesn't allow
    /// the request <paramref name="header"/>.
    /// </exception>
    public void Remove (HttpRequestHeader header)
    {
      doWithCheckingState (removeWithoutCheckingName, Convert (header), null, false, false);
    }

    /// <summary>
    /// Removes the specified response <paramref name="header"/> from the collection.
    /// </summary>
    /// <param name="header">
    /// One of the <see cref="HttpResponseHeader"/> enum values, represents
    /// the response header to remove.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="header"/> is a restricted header.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The current <see cref="WebHeaderCollection"/> instance doesn't allow
    /// the response <paramref name="header"/>.
    /// </exception>
    public void Remove (HttpResponseHeader header)
    {
      doWithCheckingState (removeWithoutCheckingName, Convert (header), null, true, false);
    }

    /// <summary>
    /// Removes the specified header from the collection.
    /// </summary>
    /// <param name="name">
    /// A <see cref="string"/> that represents the name of the header to remove.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> is <see langword="null"/> or empty.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="name"/> contains invalid characters.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="name"/> is a restricted header name.
    ///   </para>
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The current <see cref="WebHeaderCollection"/> instance doesn't allow
    /// the header <paramref name="name"/>.
    /// </exception>
    public override void Remove (string name)
    {
      doWithCheckingState (removeWithoutCheckingName, checkName (name), null, false);
    }

    /// <summary>
    /// Sets the specified request <paramref name="header"/> to the specified value.
    /// </summary>
    /// <param name="header">
    /// One of the <see cref="HttpRequestHeader"/> enum values, represents
    /// the request header to set.
    /// </param>
    /// <param name="value">
    /// A <see cref="string"/> that represents the value of the request header to set.
    /// </param>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="header"/> is a restricted header.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="value"/> contains invalid characters.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The length of <paramref name="value"/> is greater than 65,535 characters.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The current <see cref="WebHeaderCollection"/> instance doesn't allow
    /// the request <paramref name="header"/>.
    /// </exception>
    public void Set (HttpRequestHeader header, string value)
    {
      doWithCheckingState (setWithoutCheckingName, Convert (header), value, false, true);
    }

    /// <summary>
    /// Sets the specified response <paramref name="header"/> to the specified value.
    /// </summary>
    /// <param name="header">
    /// One of the <see cref="HttpResponseHeader"/> enum values, represents
    /// the response header to set.
    /// </param>
    /// <param name="value">
    /// A <see cref="string"/> that represents the value of the response header to set.
    /// </param>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="header"/> is a restricted header.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="value"/> contains invalid characters.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The length of <paramref name="value"/> is greater than 65,535 characters.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The current <see cref="WebHeaderCollection"/> instance doesn't allow
    /// the response <paramref name="header"/>.
    /// </exception>
    public void Set (HttpResponseHeader header, string value)
    {
      doWithCheckingState (setWithoutCheckingName, Convert (header), value, true, true);
    }

    /// <summary>
    /// Sets the specified header to the specified value.
    /// </summary>
    /// <param name="name">
    /// A <see cref="string"/> that represents the name of the header to set.
    /// </param>
    /// <param name="value">
    /// A <see cref="string"/> that represents the value of the header to set.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> is <see langword="null"/> or empty.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="name"/> or <paramref name="value"/> contains invalid characters.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="name"/> is a restricted header name.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The length of <paramref name="value"/> is greater than 65,535 characters.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The current <see cref="WebHeaderCollection"/> instance doesn't allow
    /// the header <paramref name="name"/>.
    /// </exception>
    public override void Set (string name, string value)
    {
      doWithCheckingState (setWithoutCheckingName, checkName (name), value, true);
    }

    /// <summary>
    /// Converts the current <see cref="WebHeaderCollection"/> to an array of <see cref="byte"/>.
    /// </summary>
    /// <returns>
    /// An array of <see cref="byte"/> that receives the converted current
    /// <see cref="WebHeaderCollection"/>.
    /// </returns>
    public byte[] ToByteArray ()
    {
      return Encoding.UTF8.GetBytes (ToString ());
    }

    /// <summary>
    /// Returns a <see cref="string"/> that represents the current
    /// <see cref="WebHeaderCollection"/>.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that represents the current <see cref="WebHeaderCollection"/>.
    /// </returns>
    public override string ToString ()
    {
      var buff = new StringBuilder ();
      Count.Times (i => buff.AppendFormat ("{0}: {1}\r\n", GetKey (i), Get (i)));

      return buff.Append ("\r\n").ToString ();
    }

    #endregion

    #region Explicit Interface Implementations

    /// <summary>
    /// Populates the specified <see cref="SerializationInfo"/> with the data needed to serialize
    /// the current <see cref="WebHeaderCollection"/>.
    /// </summary>
    /// <param name="serializationInfo">
    /// A <see cref="SerializationInfo"/> that holds the serialized object data.
    /// </param>
    /// <param name="streamingContext">
    /// A <see cref="StreamingContext"/> that specifies the destination for the serialization.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="serializationInfo"/> is <see langword="null"/>.
    /// </exception>
    [SecurityPermission (
      SecurityAction.LinkDemand,
      Flags = SecurityPermissionFlag.SerializationFormatter,
      SerializationFormatter = true)]
    void ISerializable.GetObjectData (
      SerializationInfo serializationInfo, StreamingContext streamingContext)
    {
      GetObjectData (serializationInfo, streamingContext);
    }

    #endregion
  }
}
