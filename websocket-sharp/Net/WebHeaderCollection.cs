#region License
/*
 * WebHeaderCollection.cs
 *
 * This code is derived from System.Net.WebHeaderCollection.cs of Mono
 * (http://www.mono-project.com).
 *
 * The MIT License
 *
 * Copyright (c) 2003 Ximian, Inc. (http://www.ximian.com)
 * Copyright (c) 2007 Novell, Inc. (http://www.novell.com)
 * Copyright (c) 2012-2014 sta.blockhead
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
using System.Net;
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
    #region Private Static Fields

    private static readonly Dictionary<string, HttpHeaderInfo> _headers;

    #endregion

    #region Private Fields

    private bool           _internallyCreated;
    private HttpHeaderType _state;

    #endregion

    #region Static Constructor

    static WebHeaderCollection ()
    {
      _headers =
        new Dictionary<string, HttpHeaderInfo> (StringComparer.InvariantCultureIgnoreCase) {
          {
            "Accept",
            new HttpHeaderInfo () {
              Name = "Accept",
              Type = HttpHeaderType.Request | HttpHeaderType.Restricted | HttpHeaderType.MultiValue
            }
          },
          {
            "AcceptCharset",
            new HttpHeaderInfo () {
              Name = "Accept-Charset",
              Type = HttpHeaderType.Request | HttpHeaderType.MultiValue
            }
          },
          {
            "AcceptEncoding",
            new HttpHeaderInfo () {
              Name = "Accept-Encoding",
              Type = HttpHeaderType.Request | HttpHeaderType.MultiValue
            }
          },
          {
            "AcceptLanguage",
            new HttpHeaderInfo () {
              Name = "Accept-language",
              Type = HttpHeaderType.Request | HttpHeaderType.MultiValue
            }
          },
          {
            "AcceptRanges",
            new HttpHeaderInfo () {
              Name = "Accept-Ranges",
              Type = HttpHeaderType.Response | HttpHeaderType.MultiValue
            }
          },
          {
            "Age",
            new HttpHeaderInfo () {
              Name = "Age",
              Type = HttpHeaderType.Response
            }
          },
          {
            "Allow",
            new HttpHeaderInfo () {
              Name = "Allow",
              Type = HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.MultiValue
            }
          },
          {
            "Authorization",
            new HttpHeaderInfo () {
              Name = "Authorization",
              Type = HttpHeaderType.Request | HttpHeaderType.MultiValue
            }
          },
          {
            "CacheControl",
            new HttpHeaderInfo () {
              Name = "Cache-Control",
              Type = HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.MultiValue
            }
          },
          {
            "Connection",
            new HttpHeaderInfo () {
              Name = "Connection",
              Type = HttpHeaderType.Request |
                     HttpHeaderType.Response |
                     HttpHeaderType.Restricted |
                     HttpHeaderType.MultiValue
            }
          },
          {
            "ContentEncoding",
            new HttpHeaderInfo () {
              Name = "Content-Encoding",
              Type = HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.MultiValue
            }
          },
          {
            "ContentLanguage",
            new HttpHeaderInfo () {
              Name = "Content-Language",
              Type = HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.MultiValue
            }
          },
          {
            "ContentLength",
            new HttpHeaderInfo () {
              Name = "Content-Length",
              Type = HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.Restricted
            }
          },
          {
            "ContentLocation",
            new HttpHeaderInfo () {
              Name = "Content-Location",
              Type = HttpHeaderType.Request | HttpHeaderType.Response
            }
          },
          {
            "ContentMd5",
            new HttpHeaderInfo () {
              Name = "Content-MD5",
              Type = HttpHeaderType.Request | HttpHeaderType.Response
            }
          },
          {
            "ContentRange",
            new HttpHeaderInfo () {
              Name = "Content-Range",
              Type = HttpHeaderType.Request | HttpHeaderType.Response
            }
          },
          {
            "ContentType",
            new HttpHeaderInfo () {
              Name = "Content-Type",
              Type = HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.Restricted
            }
          },
          {
            "Cookie",
            new HttpHeaderInfo () {
              Name = "Cookie",
              Type = HttpHeaderType.Request
            }
          },
          {
            "Cookie2",
            new HttpHeaderInfo () {
              Name = "Cookie2",
              Type = HttpHeaderType.Request
            }
          },
          {
            "Date",
            new HttpHeaderInfo () {
              Name = "Date",
              Type = HttpHeaderType.Request |
                     HttpHeaderType.Response |
                     HttpHeaderType.Restricted
            }
          },
          {
            "Expect",
            new HttpHeaderInfo () {
              Name = "Expect",
              Type = HttpHeaderType.Request | HttpHeaderType.Restricted | HttpHeaderType.MultiValue
            }
          },
          {
            "Expires",
            new HttpHeaderInfo () {
              Name = "Expires",
              Type = HttpHeaderType.Request | HttpHeaderType.Response
            }
          },
          {
            "ETag",
            new HttpHeaderInfo () {
              Name = "ETag",
              Type = HttpHeaderType.Response
            }
          },
          {
            "From",
            new HttpHeaderInfo () {
              Name = "From",
              Type = HttpHeaderType.Request
            }
          },
          {
            "Host",
            new HttpHeaderInfo () {
              Name = "Host",
              Type = HttpHeaderType.Request | HttpHeaderType.Restricted
            }
          },
          {
            "IfMatch",
            new HttpHeaderInfo () {
              Name = "If-Match",
              Type = HttpHeaderType.Request | HttpHeaderType.MultiValue
            }
          },
          {
            "IfModifiedSince",
            new HttpHeaderInfo () {
              Name = "If-Modified-Since",
              Type = HttpHeaderType.Request | HttpHeaderType.Restricted
            }
          },
          {
            "IfNoneMatch",
            new HttpHeaderInfo () {
              Name = "If-None-Match",
              Type = HttpHeaderType.Request | HttpHeaderType.MultiValue
            }
          },
          {
            "IfRange",
            new HttpHeaderInfo () {
              Name = "If-Range",
              Type = HttpHeaderType.Request
            }
          },
          {
            "IfUnmodifiedSince",
            new HttpHeaderInfo () {
              Name = "If-Unmodified-Since",
              Type = HttpHeaderType.Request
            }
          },
          {
            "KeepAlive",
            new HttpHeaderInfo () {
              Name = "Keep-Alive",
              Type = HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.MultiValue
            }
          },
          {
            "LastModified",
            new HttpHeaderInfo () {
              Name = "Last-Modified",
              Type = HttpHeaderType.Request | HttpHeaderType.Response
            }
          },
          {
            "Location",
            new HttpHeaderInfo () {
              Name = "Location",
              Type = HttpHeaderType.Response
            }
          },
          {
            "MaxForwards",
            new HttpHeaderInfo () {
              Name = "Max-Forwards",
              Type = HttpHeaderType.Request
            }
          },
          {
            "Pragma",
            new HttpHeaderInfo () {
              Name = "Pragma",
              Type = HttpHeaderType.Request | HttpHeaderType.Response
            }
          },
          {
            "ProxyConnection",
            new HttpHeaderInfo () {
              Name = "Proxy-Connection",
              Type = HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.Restricted
            }
          },
          {
            "ProxyAuthenticate",
            new HttpHeaderInfo () {
              Name = "Proxy-Authenticate",
              Type = HttpHeaderType.Response | HttpHeaderType.MultiValue
            }
          },
          {
            "ProxyAuthorization",
            new HttpHeaderInfo () {
              Name = "Proxy-Authorization",
              Type = HttpHeaderType.Request
            }
          },
          {
            "Public",
            new HttpHeaderInfo () {
              Name = "Public",
              Type = HttpHeaderType.Response | HttpHeaderType.MultiValue
            }
          },
          {
            "Range",
            new HttpHeaderInfo () {
              Name = "Range",
              Type = HttpHeaderType.Request | HttpHeaderType.Restricted | HttpHeaderType.MultiValue
            }
          },
          {
            "Referer",
            new HttpHeaderInfo () {
              Name = "Referer",
              Type = HttpHeaderType.Request | HttpHeaderType.Restricted
            }
          },
          {
            "RetryAfter",
            new HttpHeaderInfo () {
              Name = "Retry-After",
              Type = HttpHeaderType.Response
            }
          },
          {
            "SecWebSocketAccept",
            new HttpHeaderInfo () {
              Name = "Sec-WebSocket-Accept",
              Type = HttpHeaderType.Response | HttpHeaderType.Restricted
            }
          },
          {
            "SecWebSocketExtensions",
            new HttpHeaderInfo () {
              Name = "Sec-WebSocket-Extensions",
              Type = HttpHeaderType.Request |
                     HttpHeaderType.Response |
                     HttpHeaderType.Restricted |
                     HttpHeaderType.MultiValueInRequest
            }
          },
          {
            "SecWebSocketKey",
            new HttpHeaderInfo () {
              Name = "Sec-WebSocket-Key",
              Type = HttpHeaderType.Request | HttpHeaderType.Restricted
            }
          },
          {
            "SecWebSocketProtocol",
            new HttpHeaderInfo () {
              Name = "Sec-WebSocket-Protocol",
              Type = HttpHeaderType.Request |
                     HttpHeaderType.Response |
                     HttpHeaderType.MultiValueInRequest
            }
          },
          {
            "SecWebSocketVersion",
            new HttpHeaderInfo () {
              Name = "Sec-WebSocket-Version",
              Type = HttpHeaderType.Request |
                     HttpHeaderType.Response |
                     HttpHeaderType.Restricted |
                     HttpHeaderType.MultiValueInResponse
            }
          },
          {
            "Server",
            new HttpHeaderInfo () {
              Name = "Server",
              Type = HttpHeaderType.Response
            }
          },
          {
            "SetCookie",
            new HttpHeaderInfo () {
              Name = "Set-Cookie",
              Type = HttpHeaderType.Response | HttpHeaderType.MultiValue
            }
          },
          {
            "SetCookie2",
            new HttpHeaderInfo () {
              Name = "Set-Cookie2",
              Type = HttpHeaderType.Response | HttpHeaderType.MultiValue
            }
          },
          {
            "Te",
            new HttpHeaderInfo () {
              Name = "TE",
              Type = HttpHeaderType.Request
            }
          },
          {
            "Trailer",
            new HttpHeaderInfo () {
              Name = "Trailer",
              Type = HttpHeaderType.Request | HttpHeaderType.Response
            }
          },
          {
            "TransferEncoding",
            new HttpHeaderInfo () {
              Name = "Transfer-Encoding",
              Type = HttpHeaderType.Request |
                     HttpHeaderType.Response |
                     HttpHeaderType.Restricted |
                     HttpHeaderType.MultiValue
            }
          },
          {
            "Translate",
            new HttpHeaderInfo () {
              Name = "Translate",
              Type = HttpHeaderType.Request
            }
          },
          {
            "Upgrade",
            new HttpHeaderInfo () {
              Name = "Upgrade",
              Type = HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.MultiValue
            }
          },
          {
            "UserAgent",
            new HttpHeaderInfo () {
              Name = "User-Agent",
              Type = HttpHeaderType.Request | HttpHeaderType.Restricted
            }
          },
          {
            "Vary",
            new HttpHeaderInfo () {
              Name = "Vary",
              Type = HttpHeaderType.Response | HttpHeaderType.MultiValue
            }
          },
          {
            "Via",
            new HttpHeaderInfo () {
              Name = "Via",
              Type = HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.MultiValue
            }
          },
          {
            "Warning",
            new HttpHeaderInfo () {
              Name = "Warning",
              Type = HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.MultiValue
            }
          },
          {
            "WwwAuthenticate",
            new HttpHeaderInfo () {
              Name = "WWW-Authenticate",
              Type = HttpHeaderType.Response | HttpHeaderType.Restricted | HttpHeaderType.MultiValue
            }
          }
        };
    }

    #endregion

    #region Internal Constructors

    internal WebHeaderCollection (bool internallyCreated)
    {
      _internallyCreated = internallyCreated;
      _state = HttpHeaderType.Unspecified;
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
        _internallyCreated = serializationInfo.GetBoolean ("InternallyCreated");
        _state = (HttpHeaderType) serializationInfo.GetInt32 ("State");

        var count = serializationInfo.GetInt32 ("Count");
        for (int i = 0; i < count; i++) {
          base.Add (
            serializationInfo.GetString (i.ToString ()),
            serializationInfo.GetString ((count + i).ToString ()));
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
      _internallyCreated = false;
      _state = HttpHeaderType.Unspecified;
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets all header names in the collection.
    /// </summary>
    /// <value>
    /// An array of <see cref="string"/> that contains all header names in the collection.
    /// </value>
    public override string [] AllKeys {
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
    /// One of the <see cref="HttpRequestHeader"/> enum values, represents the request header
    /// to get or set.
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
    /// The current <see cref="WebHeaderCollection"/> instance doesn't allow the request
    /// <paramref name="header"/>.
    /// </exception>
    public string this [HttpRequestHeader header] {
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
    /// One of the <see cref="HttpResponseHeader"/> enum values, represents the response header
    /// to get or set.
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
    /// The current <see cref="WebHeaderCollection"/> instance doesn't allow the response
    /// <paramref name="header"/>.
    /// </exception>
    public string this [HttpResponseHeader header] {
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
    /// A <see cref="NameObjectCollectionBase.KeysCollection"/> that contains all header names
    /// in the collection.
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
      Action <string, string> act;
      if (ignoreRestricted)
        act = addWithoutCheckingNameAndRestricted;
      else
        act = addWithoutCheckingName;

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
      var i = header.IndexOf (':');
      if (i == -1)
        throw new ArgumentException ("No colon found.", "header");

      return i;
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
      if (!_internallyCreated && isRestricted (name, true))
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
      return _headers.TryGetValue (key, out info)
             ? info.Name
             : String.Empty;
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

    internal void RemoveInternally (string name)
    {
      base.Remove (name);
    }

    internal void SetInternally (string header, bool response)
    {
      var pos = checkColonSeparated (header);
      SetInternally (header.Substring (0, pos), header.Substring (pos + 1), response);
    }

    internal void SetInternally (string name, string value, bool response)
    {
      value = checkValue (value);
      if (IsMultiValue (name, response))
        base.Add (name, value);
      else
        base.Set (name, value);
    }

    internal string ToStringMultiValue (bool response)
    {
      var buff = new StringBuilder ();
      Count.Times (
        i => {
          var key = GetKey (i);
          if (IsMultiValue (key, response))
            foreach (var value in GetValues (i))
              buff.AppendFormat ("{0}: {1}\r\n", key, value);
          else
            buff.AppendFormat ("{0}: {1}\r\n", key, Get (i));
        });

      return buff.Append ("\r\n").ToString ();
    }

    #endregion

    #region Protected Methods

    /// <summary>
    /// Adds a header to the collection without checking whether the header is on the restricted
    /// header list.
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
    /// a colon (<c>:</c>).
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
      if (header.IsNullOrEmpty ())
        throw new ArgumentNullException ("header");

      var pos = checkColonSeparated (header);
      add (header.Substring (0, pos), header.Substring (pos + 1), false);
    }

    /// <summary>
    /// Adds the specified request <paramref name="header"/> with the specified
    /// <paramref name="value"/> to the collection.
    /// </summary>
    /// <param name="header">
    /// One of the <see cref="HttpRequestHeader"/> enum values, represents the request header
    /// to add.
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
    /// The current <see cref="WebHeaderCollection"/> instance doesn't allow the request
    /// <paramref name="header"/>.
    /// </exception>
    public void Add (HttpRequestHeader header, string value)
    {
      doWithCheckingState (addWithoutCheckingName, Convert (header), value, false, true);
    }

    /// <summary>
    /// Adds the specified response <paramref name="header"/> with the specified
    /// <paramref name="value"/> to the collection.
    /// </summary>
    /// <param name="header">
    /// One of the <see cref="HttpResponseHeader"/> enum values, represents the response header
    /// to add.
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
    /// The current <see cref="WebHeaderCollection"/> instance doesn't allow the response
    /// <paramref name="header"/>.
    /// </exception>
    public void Add (HttpResponseHeader header, string value)
    {
      doWithCheckingState (addWithoutCheckingName, Convert (header), value, true, true);
    }

    /// <summary>
    /// Adds a header with the specified <paramref name="name"/> and <paramref name="value"/>
    /// to the collection.
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
    /// The current <see cref="WebHeaderCollection"/> instance doesn't allow the header
    /// <paramref name="name"/>.
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
    /// A <see cref="string"/> that receives the value of the header if found; otherwise,
    /// <see langword="null"/>.
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
    /// Gets an array of header values stored in the specified <paramref name="index"/> position
    /// of the collection.
    /// </summary>
    /// <returns>
    /// An array of <see cref="string"/> that receives the header values if found; otherwise,
    /// <see langword="null"/>.
    /// </returns>
    /// <param name="index">
    /// An <see cref="int"/> that represents the zero-based index of the header to find.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is out of allowable range of indexes for the collection.
    /// </exception>
    public override string [] GetValues (int index)
    {
      var values = base.GetValues (index);
      return values != null && values.Length > 0
             ? values
             : null;
    }

    /// <summary>
    /// Gets an array of header values stored in the specified <paramref name="header"/>.
    /// </summary>
    /// <returns>
    /// An array of <see cref="string"/> that receives the header values if found; otherwise,
    /// <see langword="null"/>.
    /// </returns>
    /// <param name="header">
    /// A <see cref="string"/> that represents the name of the header to find.
    /// </param>
    public override string [] GetValues (string header)
    {
      var values = base.GetValues (header);
      return values != null && values.Length > 0
             ? values
             : null;
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

      serializationInfo.AddValue ("InternallyCreated", _internallyCreated);
      serializationInfo.AddValue ("State", (int) _state);

      var count = Count;
      serializationInfo.AddValue ("Count", count);
      count.Times (
        i => {
          serializationInfo.AddValue (i.ToString (), GetKey (i));
          serializationInfo.AddValue ((count + i).ToString (), Get (i));
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
    /// One of the <see cref="HttpRequestHeader"/> enum values, represents the request header
    /// to remove.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="header"/> is a restricted header.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The current <see cref="WebHeaderCollection"/> instance doesn't allow the request
    /// <paramref name="header"/>.
    /// </exception>
    public void Remove (HttpRequestHeader header)
    {
      doWithCheckingState (removeWithoutCheckingName, Convert (header), null, false, false);
    }

    /// <summary>
    /// Removes the specified response <paramref name="header"/> from the collection.
    /// </summary>
    /// <param name="header">
    /// One of the <see cref="HttpResponseHeader"/> enum values, represents the response header
    /// to remove.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="header"/> is a restricted header.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The current <see cref="WebHeaderCollection"/> instance doesn't allow the response
    /// <paramref name="header"/>.
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
    /// The current <see cref="WebHeaderCollection"/> instance doesn't allow the header
    /// <paramref name="name"/>.
    /// </exception>
    public override void Remove (string name)
    {
      doWithCheckingState (removeWithoutCheckingName, checkName (name), null, false);
    }

    /// <summary>
    /// Sets the specified request <paramref name="header"/> to the specified value.
    /// </summary>
    /// <param name="header">
    /// One of the <see cref="HttpRequestHeader"/> enum values, represents the request header
    /// to set.
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
    /// The current <see cref="WebHeaderCollection"/> instance doesn't allow the request
    /// <paramref name="header"/>.
    /// </exception>
    public void Set (HttpRequestHeader header, string value)
    {
      doWithCheckingState (setWithoutCheckingName, Convert (header), value, false, true);
    }

    /// <summary>
    /// Sets the specified response <paramref name="header"/> to the specified value.
    /// </summary>
    /// <param name="header">
    /// One of the <see cref="HttpResponseHeader"/> enum values, represents the response header
    /// to set.
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
    /// The current <see cref="WebHeaderCollection"/> instance doesn't allow the response
    /// <paramref name="header"/>.
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
    /// The current <see cref="WebHeaderCollection"/> instance doesn't allow the header
    /// <paramref name="name"/>.
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
    public byte [] ToByteArray ()
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

    #region Explicit Interface Implementation

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
