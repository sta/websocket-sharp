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
  /// Provides a collection of the HTTP headers associated with a request or
  /// response.
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
        new Dictionary<string, HttpHeaderInfo> (
          StringComparer.InvariantCultureIgnoreCase
        )
        {
          {
            "Accept",
            new HttpHeaderInfo (
              "Accept",
              HttpHeaderType.Request
              | HttpHeaderType.Restricted
              | HttpHeaderType.MultiValue
            )
          },
          {
            "AcceptCharset",
            new HttpHeaderInfo (
              "Accept-Charset",
              HttpHeaderType.Request | HttpHeaderType.MultiValue
            )
          },
          {
            "AcceptEncoding",
            new HttpHeaderInfo (
              "Accept-Encoding",
              HttpHeaderType.Request | HttpHeaderType.MultiValue
            )
          },
          {
            "AcceptLanguage",
            new HttpHeaderInfo (
              "Accept-Language",
              HttpHeaderType.Request | HttpHeaderType.MultiValue
            )
          },
          {
            "AcceptRanges",
            new HttpHeaderInfo (
              "Accept-Ranges",
              HttpHeaderType.Response | HttpHeaderType.MultiValue
            )
          },
          {
            "Age",
            new HttpHeaderInfo (
              "Age",
              HttpHeaderType.Response
            )
          },
          {
            "Allow",
            new HttpHeaderInfo (
              "Allow",
              HttpHeaderType.Request
              | HttpHeaderType.Response
              | HttpHeaderType.MultiValue
            )
          },
          {
            "Authorization",
            new HttpHeaderInfo (
              "Authorization",
              HttpHeaderType.Request | HttpHeaderType.MultiValue
            )
          },
          {
            "CacheControl",
            new HttpHeaderInfo (
              "Cache-Control",
              HttpHeaderType.Request
              | HttpHeaderType.Response
              | HttpHeaderType.MultiValue
            )
          },
          {
            "Connection",
            new HttpHeaderInfo (
              "Connection",
              HttpHeaderType.Request
              | HttpHeaderType.Response
              | HttpHeaderType.Restricted
              | HttpHeaderType.MultiValue
            )
          },
          {
            "ContentEncoding",
            new HttpHeaderInfo (
              "Content-Encoding",
              HttpHeaderType.Request
              | HttpHeaderType.Response
              | HttpHeaderType.MultiValue
            )
          },
          {
            "ContentLanguage",
            new HttpHeaderInfo (
              "Content-Language",
              HttpHeaderType.Request
              | HttpHeaderType.Response
              | HttpHeaderType.MultiValue
            )
          },
          {
            "ContentLength",
            new HttpHeaderInfo (
              "Content-Length",
              HttpHeaderType.Request
              | HttpHeaderType.Response
              | HttpHeaderType.Restricted
            )
          },
          {
            "ContentLocation",
            new HttpHeaderInfo (
              "Content-Location",
              HttpHeaderType.Request | HttpHeaderType.Response
            )
          },
          {
            "ContentMd5",
            new HttpHeaderInfo (
              "Content-MD5",
              HttpHeaderType.Request | HttpHeaderType.Response
            )
          },
          {
            "ContentRange",
            new HttpHeaderInfo (
              "Content-Range",
              HttpHeaderType.Request | HttpHeaderType.Response
            )
          },
          {
            "ContentType",
            new HttpHeaderInfo (
              "Content-Type",
              HttpHeaderType.Request
              | HttpHeaderType.Response
              | HttpHeaderType.Restricted
            )
          },
          {
            "Cookie",
            new HttpHeaderInfo (
              "Cookie",
              HttpHeaderType.Request
            )
          },
          {
            "Cookie2",
            new HttpHeaderInfo (
              "Cookie2",
              HttpHeaderType.Request
            )
          },
          {
            "Date",
            new HttpHeaderInfo (
              "Date",
              HttpHeaderType.Request
              | HttpHeaderType.Response
              | HttpHeaderType.Restricted
            )
          },
          {
            "Expect",
            new HttpHeaderInfo (
              "Expect",
              HttpHeaderType.Request
              | HttpHeaderType.Restricted
              | HttpHeaderType.MultiValue
            )
          },
          {
            "Expires",
            new HttpHeaderInfo (
              "Expires",
              HttpHeaderType.Request | HttpHeaderType.Response
            )
          },
          {
            "ETag",
            new HttpHeaderInfo (
              "ETag",
              HttpHeaderType.Response
            )
          },
          {
            "From",
            new HttpHeaderInfo (
              "From",
              HttpHeaderType.Request
            )
          },
          {
            "Host",
            new HttpHeaderInfo (
              "Host",
              HttpHeaderType.Request | HttpHeaderType.Restricted
            )
          },
          {
            "IfMatch",
            new HttpHeaderInfo (
              "If-Match",
              HttpHeaderType.Request | HttpHeaderType.MultiValue
            )
          },
          {
            "IfModifiedSince",
            new HttpHeaderInfo (
              "If-Modified-Since",
              HttpHeaderType.Request | HttpHeaderType.Restricted
            )
          },
          {
            "IfNoneMatch",
            new HttpHeaderInfo (
              "If-None-Match",
              HttpHeaderType.Request | HttpHeaderType.MultiValue
            )
          },
          {
            "IfRange",
            new HttpHeaderInfo (
              "If-Range",
              HttpHeaderType.Request
            )
          },
          {
            "IfUnmodifiedSince",
            new HttpHeaderInfo (
              "If-Unmodified-Since",
              HttpHeaderType.Request
            )
          },
          {
            "KeepAlive",
            new HttpHeaderInfo (
              "Keep-Alive",
              HttpHeaderType.Request
              | HttpHeaderType.Response
              | HttpHeaderType.MultiValue
            )
          },
          {
            "LastModified",
            new HttpHeaderInfo (
              "Last-Modified",
              HttpHeaderType.Request | HttpHeaderType.Response
            )
          },
          {
            "Location",
            new HttpHeaderInfo (
              "Location",
              HttpHeaderType.Response
            )
          },
          {
            "MaxForwards",
            new HttpHeaderInfo (
              "Max-Forwards",
              HttpHeaderType.Request
            )
          },
          {
            "Pragma",
            new HttpHeaderInfo (
              "Pragma",
              HttpHeaderType.Request | HttpHeaderType.Response
            )
          },
          {
            "ProxyAuthenticate",
            new HttpHeaderInfo (
              "Proxy-Authenticate",
              HttpHeaderType.Response | HttpHeaderType.MultiValue
            )
          },
          {
            "ProxyAuthorization",
            new HttpHeaderInfo (
              "Proxy-Authorization",
              HttpHeaderType.Request
            )
          },
          {
            "ProxyConnection",
            new HttpHeaderInfo (
              "Proxy-Connection",
              HttpHeaderType.Request
              | HttpHeaderType.Response
              | HttpHeaderType.Restricted
            )
          },
          {
            "Public",
            new HttpHeaderInfo (
              "Public",
              HttpHeaderType.Response | HttpHeaderType.MultiValue
            )
          },
          {
            "Range",
            new HttpHeaderInfo (
              "Range",
              HttpHeaderType.Request
              | HttpHeaderType.Restricted
              | HttpHeaderType.MultiValue
            )
          },
          {
            "Referer",
            new HttpHeaderInfo (
              "Referer",
              HttpHeaderType.Request | HttpHeaderType.Restricted
            )
          },
          {
            "RetryAfter",
            new HttpHeaderInfo (
              "Retry-After",
              HttpHeaderType.Response
            )
          },
          {
            "SecWebSocketAccept",
            new HttpHeaderInfo (
              "Sec-WebSocket-Accept",
              HttpHeaderType.Response | HttpHeaderType.Restricted
            )
          },
          {
            "SecWebSocketExtensions",
            new HttpHeaderInfo (
              "Sec-WebSocket-Extensions",
              HttpHeaderType.Request
              | HttpHeaderType.Response
              | HttpHeaderType.Restricted
              | HttpHeaderType.MultiValueInRequest
            )
          },
          {
            "SecWebSocketKey",
            new HttpHeaderInfo (
              "Sec-WebSocket-Key",
              HttpHeaderType.Request | HttpHeaderType.Restricted
            )
          },
          {
            "SecWebSocketProtocol",
            new HttpHeaderInfo (
              "Sec-WebSocket-Protocol",
              HttpHeaderType.Request
              | HttpHeaderType.Response
              | HttpHeaderType.MultiValueInRequest
            )
          },
          {
            "SecWebSocketVersion",
            new HttpHeaderInfo (
              "Sec-WebSocket-Version",
              HttpHeaderType.Request
              | HttpHeaderType.Response
              | HttpHeaderType.Restricted
              | HttpHeaderType.MultiValueInResponse
            )
          },
          {
            "Server",
            new HttpHeaderInfo (
              "Server",
              HttpHeaderType.Response
            )
          },
          {
            "SetCookie",
            new HttpHeaderInfo (
              "Set-Cookie",
              HttpHeaderType.Response | HttpHeaderType.MultiValue
            )
          },
          {
            "SetCookie2",
            new HttpHeaderInfo (
              "Set-Cookie2",
              HttpHeaderType.Response | HttpHeaderType.MultiValue
            )
          },
          {
            "Te",
            new HttpHeaderInfo (
              "TE",
              HttpHeaderType.Request
            )
          },
          {
            "Trailer",
            new HttpHeaderInfo (
              "Trailer",
              HttpHeaderType.Request | HttpHeaderType.Response
            )
          },
          {
            "TransferEncoding",
            new HttpHeaderInfo (
              "Transfer-Encoding",
              HttpHeaderType.Request
              | HttpHeaderType.Response
              | HttpHeaderType.Restricted
              | HttpHeaderType.MultiValue
            )
          },
          {
            "Translate",
            new HttpHeaderInfo (
              "Translate",
              HttpHeaderType.Request
            )
          },
          {
            "Upgrade",
            new HttpHeaderInfo (
              "Upgrade",
              HttpHeaderType.Request
              | HttpHeaderType.Response
              | HttpHeaderType.MultiValue
            )
          },
          {
            "UserAgent",
            new HttpHeaderInfo (
              "User-Agent",
              HttpHeaderType.Request | HttpHeaderType.Restricted
            )
          },
          {
            "Vary",
            new HttpHeaderInfo (
              "Vary",
              HttpHeaderType.Response | HttpHeaderType.MultiValue
            )
          },
          {
            "Via",
            new HttpHeaderInfo (
              "Via",
              HttpHeaderType.Request
              | HttpHeaderType.Response
              | HttpHeaderType.MultiValue
            )
          },
          {
            "Warning",
            new HttpHeaderInfo (
              "Warning",
              HttpHeaderType.Request
              | HttpHeaderType.Response
              | HttpHeaderType.MultiValue
            )
          },
          {
            "WwwAuthenticate",
            new HttpHeaderInfo (
              "WWW-Authenticate",
              HttpHeaderType.Response
              | HttpHeaderType.Restricted
              | HttpHeaderType.MultiValue
            )
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
    /// Initializes a new instance of the <see cref="WebHeaderCollection"/> class
    /// from the specified instances of the <see cref="SerializationInfo"/> and
    /// <see cref="StreamingContext"/> classes.
    /// </summary>
    /// <param name="serializationInfo">
    /// A <see cref="SerializationInfo"/> that contains the serialized
    /// object data.
    /// </param>
    /// <param name="streamingContext">
    /// A <see cref="StreamingContext"/> that specifies the source for
    /// the deserialization.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="serializationInfo"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// An element with the specified name is not found in
    /// <paramref name="serializationInfo"/>.
    /// </exception>
    protected WebHeaderCollection (
      SerializationInfo serializationInfo, StreamingContext streamingContext
    )
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
            serializationInfo.GetString ((cnt + i).ToString ())
          );
        }
      }
      catch (SerializationException ex) {
        throw new ArgumentException (ex.Message, "serializationInfo", ex);
      }
    }

    #endregion

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="WebHeaderCollection"/>
    /// class.
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
    /// An array of <see cref="string"/> that contains all header names in
    /// the collection.
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
    /// An <see cref="int"/> that represents the number of headers in
    /// the collection.
    /// </value>
    public override int Count {
      get {
        return base.Count;
      }
    }

    /// <summary>
    /// Gets or sets the specified request header.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the value of the request header.
    /// </value>
    /// <param name="header">
    ///   <para>
    ///   One of the <see cref="HttpRequestHeader"/> enum values.
    ///   </para>
    ///   <para>
    ///   It represents the request header to get or set.
    ///   </para>
    /// </param>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="header"/> is a restricted header.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="value"/> contains an invalid character.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The length of <paramref name="value"/> is greater than 65,535
    /// characters.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// This instance does not allow the request header.
    /// </exception>
    public string this[HttpRequestHeader header] {
      get {
        var key = header.ToString ();
        var name = getHeaderName (key);

        return Get (name);
      }

      set {
        Add (header, value);
      }
    }

    /// <summary>
    /// Gets or sets the specified response header.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the value of the response header.
    /// </value>
    /// <param name="header">
    ///   <para>
    ///   One of the <see cref="HttpResponseHeader"/> enum values.
    ///   </para>
    ///   <para>
    ///   It represents the response header to get or set.
    ///   </para>
    /// </param>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="header"/> is a restricted header.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="value"/> contains an invalid character.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The length of <paramref name="value"/> is greater than 65,535
    /// characters.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// This instance does not allow the response header.
    /// </exception>
    public string this[HttpResponseHeader header] {
      get {
        var key = header.ToString ();
        var name = getHeaderName (key);

        return Get (name);
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
      if (name == null)
        throw new ArgumentNullException ("name");

      if (name.Length == 0)
        throw new ArgumentException ("An empty string.", "name");

      name = name.Trim ();

      if (name.Length == 0)
        throw new ArgumentException ("A string of spaces.", "name");

      if (!name.IsToken ()) {
        var msg = "It contains an invalid character.";

        throw new ArgumentException (msg, "name");
      }

      return name;
    }

    private void checkRestricted (string name)
    {
      if (_internallyUsed)
        return;

      if (isRestricted (name, true)) {
        var msg = "This header must be modified with the appropiate property.";

        throw new ArgumentException (msg);
      }
    }

    private void checkState (bool response)
    {
      if (_state == HttpHeaderType.Unspecified)
        return;

      if (response) {
        if (_state == HttpHeaderType.Response)
          return;

        var msg = "This collection is already in use for the request headers.";

        throw new InvalidOperationException (msg);
      }

      if (_state == HttpHeaderType.Response) {
        var msg = "This collection is already in use for the response headers.";

        throw new InvalidOperationException (msg);
      }
    }

    private static string checkValue (string value)
    {
      if (value == null)
        return String.Empty;

      value = value.Trim ();

      var len = value.Length;

      if (len == 0)
        return value;

      if (len > 65535) {
        var msg = "The length is greater than 65,535 characters.";

        throw new ArgumentOutOfRangeException ("value", msg);
      }

      if (!value.IsText ()) {
        var msg = "It contains an invalid character.";

        throw new ArgumentException (msg, "value");
      }

      return value;
    }

    private void doWithCheckingState (
      Action <string, string> action, string name, string value, bool setState
    )
    {
      var headerType = checkHeaderType (name);

      if (headerType == HttpHeaderType.Response) {
        doWithCheckingState (action, name, value, true, setState);

        return;
      }

      if (headerType == HttpHeaderType.Request) {
        doWithCheckingState (action, name, value, false, setState);

        return;
      }

      action (name, value);
    }

    private void doWithCheckingState (
      Action <string, string> action,
      string name,
      string value,
      bool response,
      bool setState
    )
    {
      checkState (response);
      action (name, value);

      setState = setState && _state == HttpHeaderType.Unspecified;

      if (!setState)
        return;

      _state = response ? HttpHeaderType.Response : HttpHeaderType.Request;
    }

    private void doWithoutCheckingName (
      Action <string, string> action, string name, string value
    )
    {
      checkRestricted (name);
      value = checkValue (value);
      action (name, value);
    }

    private static HttpHeaderInfo getHeaderInfo (string name)
    {
      var comparison = StringComparison.InvariantCultureIgnoreCase;

      foreach (var headerInfo in _headers.Values) {
        if (headerInfo.Name.Equals (name, comparison))
          return headerInfo;
      }

      return null;
    }

    private static string getHeaderName (string key)
    {
      HttpHeaderInfo headerInfo;

      return _headers.TryGetValue (key, out headerInfo)
             ? headerInfo.Name
             : null;
    }

    private static bool isMultiValue (string name, bool response)
    {
      var headerInfo = getHeaderInfo (name);

      return headerInfo != null && headerInfo.IsMultiValue (response);
    }

    private static bool isRestricted (string name, bool response)
    {
      var headerInfo = getHeaderInfo (name);

      return headerInfo != null && headerInfo.IsRestricted (response);
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

    internal void InternalRemove (string name)
    {
      base.Remove (name);
    }

    internal void InternalSet (string header, bool response)
    {
      var idx = header.IndexOf (':');

      if (idx == -1) {
        var msg = "It does not contain a colon character.";

        throw new ArgumentException (msg, "header");
      }

      var name = header.Substring (0, idx);
      var val = header.Substring (idx + 1);

      InternalSet (name, val, response);
    }

    internal void InternalSet (string name, string value, bool response)
    {
      value = checkValue (value);

      if (isMultiValue (name, response)) {
        base.Add (name, value);

        return;
      }

      base.Set (name, value);
    }

    internal string ToStringMultiValue (bool response)
    {
      var cnt = Count;

      if (cnt == 0)
        return "\r\n";

      var buff = new StringBuilder ();

      for (var i = 0; i < cnt; i++) {
        var name = GetKey (i);

        if (isMultiValue (name, response)) {
          foreach (var val in GetValues (i))
            buff.AppendFormat ("{0}: {1}\r\n", name, val);

          continue;
        }

        buff.AppendFormat ("{0}: {1}\r\n", name, Get (i));
      }

      buff.Append ("\r\n");

      return buff.ToString ();
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
    /// Adds the specified header to the collection.
    /// </summary>
    /// <param name="header">
    /// A <see cref="string"/> that specifies the header to add,
    /// with the name and value separated by a colon character (':').
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="header"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="header"/> is an empty string.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="header"/> does not contain a colon character.
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
    ///   The name part of <paramref name="header"/> is an empty string.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   The name part of <paramref name="header"/> contains an invalid
    ///   character.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   The value part of <paramref name="header"/> contains an invalid
    ///   character.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The length of the value part of <paramref name="header"/> is greater
    /// than 65,535 characters.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// This instance does not allow the header.
    /// </exception>
    public void Add (string header)
    {
      if (header == null)
        throw new ArgumentNullException ("header");

      if (header.Length == 0) {
        var msg = "An empty string.";

        throw new ArgumentException (msg, "header");
      }

      var idx = header.IndexOf (':');

      if (idx == -1) {
        var msg = "It does not contain a colon character.";

        throw new ArgumentException (msg, "header");
      }

      var name = header.Substring (0, idx);
      var val = header.Substring (idx + 1);

      add (name, val, false);
    }

    /// <summary>
    /// Adds the specified request header with the specified value to
    /// the collection.
    /// </summary>
    /// <param name="header">
    ///   <para>
    ///   One of the <see cref="HttpRequestHeader"/> enum values.
    ///   </para>
    ///   <para>
    ///   It specifies the request header to add.
    ///   </para>
    /// </param>
    /// <param name="value">
    /// A <see cref="string"/> that specifies the value of the header to add.
    /// </param>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="header"/> is a restricted header.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="value"/> contains an invalid character.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The length of <paramref name="value"/> is greater than 65,535
    /// characters.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// This instance does not allow the request header.
    /// </exception>
    public void Add (HttpRequestHeader header, string value)
    {
      var key = header.ToString ();
      var name = getHeaderName (key);

      doWithCheckingState (addWithoutCheckingName, name, value, false, true);
    }

    /// <summary>
    /// Adds the specified response header with the specified value to
    /// the collection.
    /// </summary>
    /// <param name="header">
    ///   <para>
    ///   One of the <see cref="HttpResponseHeader"/> enum values.
    ///   </para>
    ///   <para>
    ///   It specifies the response header to add.
    ///   </para>
    /// </param>
    /// <param name="value">
    /// A <see cref="string"/> that specifies the value of the header to add.
    /// </param>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="header"/> is a restricted header.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="value"/> contains an invalid character.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The length of <paramref name="value"/> is greater than 65,535
    /// characters.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// This instance does not allow the response header.
    /// </exception>
    public void Add (HttpResponseHeader header, string value)
    {
      var key = header.ToString ();
      var name = getHeaderName (key);

      doWithCheckingState (addWithoutCheckingName, name, value, true, true);
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
    /// Populates a <see cref="SerializationInfo"/> instance with the data
    /// needed to serialize this instance.
    /// </summary>
    /// <param name="serializationInfo">
    /// A <see cref="SerializationInfo"/> to populate with the data.
    /// </param>
    /// <param name="streamingContext">
    /// A <see cref="StreamingContext"/> that specifies the destination for
    /// the serialization.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="serializationInfo"/> is <see langword="null"/>.
    /// </exception>
    [
      SecurityPermission (
        SecurityAction.LinkDemand,
        Flags = SecurityPermissionFlag.SerializationFormatter
      )
    ]
    public override void GetObjectData (
      SerializationInfo serializationInfo, StreamingContext streamingContext
    )
    {
      if (serializationInfo == null)
        throw new ArgumentNullException ("serializationInfo");

      serializationInfo.AddValue ("InternallyUsed", _internallyUsed);
      serializationInfo.AddValue ("State", (int) _state);

      var cnt = Count;

      serializationInfo.AddValue ("Count", cnt);

      for (var i = 0; i < cnt; i++) {
        serializationInfo.AddValue (i.ToString (), GetKey (i));
        serializationInfo.AddValue ((cnt + i).ToString (), Get (i));
      }
    }

    /// <summary>
    /// Determines whether the specified HTTP header can be set for the request.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the header cannot be set; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="headerName">
    /// A <see cref="string"/> that specifies the name of the header to test.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="headerName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="headerName"/> is an empty string.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="headerName"/> is a string of spaces.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="headerName"/> contains an invalid character.
    ///   </para>
    /// </exception>
    public static bool IsRestricted (string headerName)
    {
      return IsRestricted (headerName, false);
    }

    /// <summary>
    /// Determines whether the specified HTTP header can be set for the request
    /// or the response.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the header cannot be set; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="headerName">
    /// A <see cref="string"/> that specifies the name of the header to test.
    /// </param>
    /// <param name="response">
    /// A <see cref="bool"/>: <c>true</c> if the test is for the response;
    /// otherwise, <c>false</c>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="headerName"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="headerName"/> is an empty string.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="headerName"/> is a string of spaces.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="headerName"/> contains an invalid character.
    ///   </para>
    /// </exception>
    public static bool IsRestricted (string headerName, bool response)
    {
      if (headerName == null)
        throw new ArgumentNullException ("headerName");

      if (headerName.Length == 0)
        throw new ArgumentException ("An empty string.", "headerName");

      headerName = headerName.Trim ();

      if (headerName.Length == 0)
        throw new ArgumentException ("A string of spaces.", "headerName");

      if (!headerName.IsToken ()) {
        var msg = "It contains an invalid character.";

        throw new ArgumentException (msg, "headerName");
      }

      return isRestricted (headerName, response);
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
    /// Removes the specified request header from the collection.
    /// </summary>
    /// <param name="header">
    ///   <para>
    ///   One of the <see cref="HttpRequestHeader"/> enum values.
    ///   </para>
    ///   <para>
    ///   It specifies the request header to remove.
    ///   </para>
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="header"/> is a restricted header.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// This instance does not allow the request header.
    /// </exception>
    public void Remove (HttpRequestHeader header)
    {
      var key = header.ToString ();
      var name = getHeaderName (key);

      doWithCheckingState (removeWithoutCheckingName, name, null, false, false);
    }

    /// <summary>
    /// Removes the specified response header from the collection.
    /// </summary>
    /// <param name="header">
    ///   <para>
    ///   One of the <see cref="HttpResponseHeader"/> enum values.
    ///   </para>
    ///   <para>
    ///   It specifies the response header to remove.
    ///   </para>
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="header"/> is a restricted header.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// This instance does not allow the response header.
    /// </exception>
    public void Remove (HttpResponseHeader header)
    {
      var key = header.ToString ();
      var name = getHeaderName (key);

      doWithCheckingState (removeWithoutCheckingName, name, null, true, false);
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
    /// Sets the specified request header to the specified value.
    /// </summary>
    /// <param name="header">
    ///   <para>
    ///   One of the <see cref="HttpRequestHeader"/> enum values.
    ///   </para>
    ///   <para>
    ///   It specifies the request header to set.
    ///   </para>
    /// </param>
    /// <param name="value">
    /// A <see cref="string"/> that specifies the value of the request header
    /// to set.
    /// </param>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="header"/> is a restricted header.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="value"/> contains an invalid character.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The length of <paramref name="value"/> is greater than 65,535
    /// characters.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// This instance does not allow the request header.
    /// </exception>
    public void Set (HttpRequestHeader header, string value)
    {
      var key = header.ToString ();
      var name = getHeaderName (key);

      doWithCheckingState (setWithoutCheckingName, name, value, false, true);
    }

    /// <summary>
    /// Sets the specified response header to the specified value.
    /// </summary>
    /// <param name="header">
    ///   <para>
    ///   One of the <see cref="HttpResponseHeader"/> enum values.
    ///   </para>
    ///   <para>
    ///   It specifies the response header to set.
    ///   </para>
    /// </param>
    /// <param name="value">
    /// A <see cref="string"/> that specifies the value of the response header
    /// to set.
    /// </param>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="header"/> is a restricted header.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="value"/> contains an invalid character.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The length of <paramref name="value"/> is greater than 65,535
    /// characters.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// This instance does not allow the response header.
    /// </exception>
    public void Set (HttpResponseHeader header, string value)
    {
      var key = header.ToString ();
      var name = getHeaderName (key);

      doWithCheckingState (setWithoutCheckingName, name, value, true, true);
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
    /// Converts the current instance to an array of byte.
    /// </summary>
    /// <returns>
    /// An array of <see cref="byte"/> converted from a string that represents
    /// the current instance.
    /// </returns>
    public byte[] ToByteArray ()
    {
      return Encoding.UTF8.GetBytes (ToString ());
    }

    /// <summary>
    /// Returns a string that represents the current instance.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that represents all headers in the collection.
    /// </returns>
    public override string ToString ()
    {
      var cnt = Count;

      if (cnt == 0)
        return "\r\n";

      var buff = new StringBuilder ();

      for (var i = 0; i < cnt; i++)
        buff.AppendFormat ("{0}: {1}\r\n", GetKey (i), Get (i));

      buff.Append ("\r\n");

      return buff.ToString ();
    }

    #endregion

    #region Explicit Interface Implementations

    /// <summary>
    /// Populates a <see cref="SerializationInfo"/> instance with the data
    /// needed to serialize this instance.
    /// </summary>
    /// <param name="serializationInfo">
    /// A <see cref="SerializationInfo"/> to populate with the data.
    /// </param>
    /// <param name="streamingContext">
    /// A <see cref="StreamingContext"/> that specifies the destination for
    /// the serialization.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="serializationInfo"/> is <see langword="null"/>.
    /// </exception>
    [
      SecurityPermission (
        SecurityAction.LinkDemand,
        Flags = SecurityPermissionFlag.SerializationFormatter,
        SerializationFormatter = true
      )
    ]
    void ISerializable.GetObjectData (
      SerializationInfo serializationInfo, StreamingContext streamingContext
    )
    {
      GetObjectData (serializationInfo, streamingContext);
    }

    #endregion
  }
}
