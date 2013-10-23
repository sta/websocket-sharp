//
// WebHeaderCollection.cs
//	Copied from System.Net.WebHeaderCollection.cs
//
// Authors:
//	Lawrence Pit (loz@cable.a2000.nl)
//	Gonzalo Paniagua Javier (gonzalo@ximian.com)
//	Miguel de Icaza (miguel@novell.com)
//	sta (sta.blockhead@gmail.com)
//
// Copyright (c) 2003 Ximian, Inc. (http://www.ximian.com)
// Copyright (c) 2007 Novell, Inc. (http://www.novell.com)
// Copyright (c) 2012-2013 sta.blockhead
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
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
		#region Fields

		static readonly Dictionary<string, HttpHeaderInfo> headers;

		bool           internallyCreated;
		HttpHeaderType state;

		#endregion

		#region Constructors

		static WebHeaderCollection () 
		{
			headers = new Dictionary<string, HttpHeaderInfo> (StringComparer.InvariantCultureIgnoreCase)
			{
				{ "Accept", new HttpHeaderInfo () {
					Name = "Accept",
					Type = HttpHeaderType.Request | HttpHeaderType.Restricted | HttpHeaderType.MultiValue } },
				{ "AcceptCharset", new HttpHeaderInfo () {
					Name = "Accept-Charset",
					Type = HttpHeaderType.Request | HttpHeaderType.MultiValue } },
				{ "AcceptEncoding", new HttpHeaderInfo () {
					Name = "Accept-Encoding",
					Type = HttpHeaderType.Request | HttpHeaderType.MultiValue } },
				{ "AcceptLanguage", new HttpHeaderInfo () {
					Name = "Accept-language",
					Type = HttpHeaderType.Request | HttpHeaderType.MultiValue } },
				{ "AcceptRanges", new HttpHeaderInfo () {
					Name = "Accept-Ranges",
					Type = HttpHeaderType.Response | HttpHeaderType.MultiValue } },
				{ "Age", new HttpHeaderInfo () {
					Name = "Age",
					Type = HttpHeaderType.Response } },
				{ "Allow", new HttpHeaderInfo () {
					Name = "Allow",
					Type = HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.MultiValue } },
				{ "Authorization", new HttpHeaderInfo () {
					Name = "Authorization",
					Type = HttpHeaderType.Request | HttpHeaderType.MultiValue } },
				{ "CacheControl", new HttpHeaderInfo () {
					Name = "Cache-Control",
					Type = HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.MultiValue } },
				{ "Connection", new HttpHeaderInfo () {
					Name = "Connection",
					Type = HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.Restricted | HttpHeaderType.MultiValue } },
				{ "ContentEncoding", new HttpHeaderInfo () {
					Name = "Content-Encoding",
					Type = HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.MultiValue } },
				{ "ContentLanguage", new HttpHeaderInfo () {
					Name = "Content-Language",
					Type = HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.MultiValue } },
				{ "ContentLength", new HttpHeaderInfo () {
					Name = "Content-Length",
					Type = HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.Restricted } },
				{ "ContentLocation", new HttpHeaderInfo () {
					Name = "Content-Location",
					Type = HttpHeaderType.Request | HttpHeaderType.Response } },
				{ "ContentMd5", new HttpHeaderInfo () {
					Name = "Content-MD5",
					Type = HttpHeaderType.Request | HttpHeaderType.Response } },
				{ "ContentRange", new HttpHeaderInfo () {
					Name = "Content-Range",
					Type = HttpHeaderType.Request | HttpHeaderType.Response } },
				{ "ContentType", new HttpHeaderInfo () {
					Name = "Content-Type",
					Type = HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.Restricted } },
				{ "Cookie", new HttpHeaderInfo () {
					Name = "Cookie",
					Type = HttpHeaderType.Request } },
				{ "Cookie2", new HttpHeaderInfo () {
					Name = "Cookie2",
					Type = HttpHeaderType.Request } },
				{ "Date", new HttpHeaderInfo () {
					Name = "Date",
					Type = HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.Restricted } },
				{ "Expect", new HttpHeaderInfo () {
					Name = "Expect",
					Type = HttpHeaderType.Request | HttpHeaderType.Restricted | HttpHeaderType.MultiValue } },
				{ "Expires", new HttpHeaderInfo () {
					Name = "Expires",
					Type = HttpHeaderType.Request | HttpHeaderType.Response } },
				{ "ETag", new HttpHeaderInfo () {
					Name = "ETag",
					Type = HttpHeaderType.Response } },
				{ "From", new HttpHeaderInfo () {
					Name = "From",
					Type = HttpHeaderType.Request } },
				{ "Host", new HttpHeaderInfo () {
					Name = "Host",
					Type = HttpHeaderType.Request | HttpHeaderType.Restricted } },
				{ "IfMatch", new HttpHeaderInfo () {
					Name = "If-Match",
					Type = HttpHeaderType.Request | HttpHeaderType.MultiValue } },
				{ "IfModifiedSince", new HttpHeaderInfo () {
					Name = "If-Modified-Since",
					Type = HttpHeaderType.Request | HttpHeaderType.Restricted } },
				{ "IfNoneMatch", new HttpHeaderInfo () {
					Name = "If-None-Match",
					Type = HttpHeaderType.Request | HttpHeaderType.MultiValue } },
				{ "IfRange", new HttpHeaderInfo () {
					Name = "If-Range",
					Type = HttpHeaderType.Request } },
				{ "IfUnmodifiedSince", new HttpHeaderInfo () {
					Name = "If-Unmodified-Since",
					Type = HttpHeaderType.Request } },
				{ "KeepAlive", new HttpHeaderInfo () {
					Name = "Keep-Alive",
					Type = HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.MultiValue } },
				{ "LastModified", new HttpHeaderInfo () {
					Name = "Last-Modified",
					Type = HttpHeaderType.Request | HttpHeaderType.Response } },
				{ "Location", new HttpHeaderInfo () {
					Name = "Location",
					Type = HttpHeaderType.Response } },
				{ "MaxForwards", new HttpHeaderInfo () {
					Name = "Max-Forwards",
					Type = HttpHeaderType.Request } },
				{ "Pragma", new HttpHeaderInfo () {
					Name = "Pragma",
					Type = HttpHeaderType.Request | HttpHeaderType.Response } },
				{ "ProxyConnection", new HttpHeaderInfo () {
					Name = "Proxy-Connection",
					Type = HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.Restricted } },
				{ "ProxyAuthenticate", new HttpHeaderInfo () {
					Name = "Proxy-Authenticate",
					Type = HttpHeaderType.Response | HttpHeaderType.MultiValue } },
				{ "ProxyAuthorization", new HttpHeaderInfo () {
					Name = "Proxy-Authorization",
					Type = HttpHeaderType.Request } },
				{ "Public", new HttpHeaderInfo () {
					Name = "Public",
					Type = HttpHeaderType.Response | HttpHeaderType.MultiValue } },
				{ "Range", new HttpHeaderInfo () {
					Name = "Range",
					Type = HttpHeaderType.Request | HttpHeaderType.Restricted | HttpHeaderType.MultiValue } },
				{ "Referer", new HttpHeaderInfo () {
					Name = "Referer",
					Type = HttpHeaderType.Request | HttpHeaderType.Restricted } },
				{ "RetryAfter", new HttpHeaderInfo () {
					Name = "Retry-After",
					Type = HttpHeaderType.Response } },
				{ "SecWebSocketAccept", new HttpHeaderInfo () {
					Name = "Sec-WebSocket-Accept",
					Type = HttpHeaderType.Response | HttpHeaderType.Restricted } },
				{ "SecWebSocketExtensions", new HttpHeaderInfo () {
					Name = "Sec-WebSocket-Extensions",
					Type = HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.Restricted | HttpHeaderType.MultiValueInRequest } },
				{ "SecWebSocketKey", new HttpHeaderInfo () {
					Name = "Sec-WebSocket-Key",
					Type = HttpHeaderType.Request | HttpHeaderType.Restricted } },
				{ "SecWebSocketProtocol", new HttpHeaderInfo () {
					Name = "Sec-WebSocket-Protocol",
					Type = HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.MultiValueInRequest } },
				{ "SecWebSocketVersion", new HttpHeaderInfo () {
					Name = "Sec-WebSocket-Version",
					Type = HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.Restricted | HttpHeaderType.MultiValueInResponse } },
				{ "Server", new HttpHeaderInfo () {
					Name = "Server",
					Type = HttpHeaderType.Response } },
				{ "SetCookie", new HttpHeaderInfo () {
					Name = "Set-Cookie",
					Type = HttpHeaderType.Response | HttpHeaderType.MultiValue } },
				{ "SetCookie2", new HttpHeaderInfo () {
					Name = "Set-Cookie2",
					Type = HttpHeaderType.Response | HttpHeaderType.MultiValue } },
				{ "Te", new HttpHeaderInfo () {
					Name = "TE",
					Type = HttpHeaderType.Request } },
				{ "Trailer", new HttpHeaderInfo () {
					Name = "Trailer",
					Type = HttpHeaderType.Request | HttpHeaderType.Response } },
				{ "TransferEncoding", new HttpHeaderInfo () {
					Name = "Transfer-Encoding",
					Type = HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.Restricted | HttpHeaderType.MultiValue } },
				{ "Translate", new HttpHeaderInfo () {
					Name = "Translate",
					Type = HttpHeaderType.Request } },
				{ "Upgrade", new HttpHeaderInfo () {
					Name = "Upgrade",
					Type = HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.MultiValue } },
				{ "UserAgent", new HttpHeaderInfo () {
					Name = "User-Agent",
					Type = HttpHeaderType.Request | HttpHeaderType.Restricted } },
				{ "Vary", new HttpHeaderInfo () {
					Name = "Vary",
					Type = HttpHeaderType.Response | HttpHeaderType.MultiValue } },
				{ "Via", new HttpHeaderInfo () {
					Name = "Via",
					Type = HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.MultiValue } },
				{ "Warning", new HttpHeaderInfo () {
					Name = "Warning",
					Type = HttpHeaderType.Request | HttpHeaderType.Response | HttpHeaderType.MultiValue } },
				{ "WwwAuthenticate", new HttpHeaderInfo () {
					Name = "WWW-Authenticate",
					Type = HttpHeaderType.Response | HttpHeaderType.Restricted | HttpHeaderType.MultiValue } }
			};
		}

		internal WebHeaderCollection (bool internallyCreated)
		{
			this.internallyCreated = internallyCreated;
			state = HttpHeaderType.Unspecified;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="WebHeaderCollection"/> class
		/// with the specified <see cref="SerializationInfo"/> and <see cref="StreamingContext"/>.
		/// </summary>
		/// <param name="serializationInfo">
		/// A <see cref="SerializationInfo"/> that contains the data to need to serialize the <see cref="WebHeaderCollection"/> object.
		/// </param>
		/// <param name="streamingContext">
		/// A <see cref="StreamingContext"/> that contains the source of the serialized stream associated with the new <see cref="WebHeaderCollection"/>.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="serializationInfo"/> is <see langword="null"/>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// An element with the specified name is not found in <paramref name="serializationInfo"/>.
		/// </exception>
		protected WebHeaderCollection (
			SerializationInfo serializationInfo, StreamingContext streamingContext)
		{
			if (serializationInfo == null)
				throw new ArgumentNullException ("serializationInfo");

			try {
				internallyCreated = serializationInfo.GetBoolean ("InternallyCreated");
				state = (HttpHeaderType) serializationInfo.GetInt32 ("State");

				int count = serializationInfo.GetInt32 ("Count");
				for (int i = 0; i < count; i++) {
					base.Add (
						serializationInfo.GetString (i.ToString ()),
						serializationInfo.GetString ((count + i).ToString ()));
				}
			} catch (SerializationException ex) {
				throw new ArgumentException (ex.Message, "serializationInfo", ex);
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="WebHeaderCollection"/> class.
		/// </summary>
		public WebHeaderCollection ()
		{
			internallyCreated = false;
			state = HttpHeaderType.Unspecified;
		}

		#endregion

		#region Properties

		/// <summary>
		/// Gets all header names in the collection.
		/// </summary>
		/// <value>
		/// An array of <see cref="string"/> that contains all header names in the collection.
		/// </value>
		public override string [] AllKeys
		{
			get {
				return base.AllKeys;
			}
		}

		/// <summary>
		/// Gets the number of headers in the collection.
		/// </summary>
		/// <value>
		/// An <see cref="int"/> that indicates the number of headers in the collection.
		/// </value>
		public override int Count 
		{
			get {
				return base.Count;
			}
		}

		/// <summary>
		/// Gets or sets the specified request <paramref name="header"/> in the collection.
		/// </summary>
		/// <value>
		/// A <see cref="string"/> that contains the value of the specified request <paramref name="header"/>.
		/// </value>
		/// <param name="header">
		/// A <see cref="HttpRequestHeader"/> that indicates a request header.
		/// </param>
		/// <exception cref="InvalidOperationException">
		/// The current <see cref="WebHeaderCollection"/> instance does not allow any of <see cref="HttpRequestHeader"/> values.
		/// </exception>
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
		/// The length of <paramref name="value"/> is greater than 65535.
		/// </exception>
		public string this [HttpRequestHeader header]
		{
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
		/// A <see cref="string"/> that contains the value of the specified response <paramref name="header"/>.
		/// </value>
		/// <param name="header">
		/// A <see cref="HttpResponseHeader"/> that indicates a response header.
		/// </param>
		/// <exception cref="InvalidOperationException">
		/// The current <see cref="WebHeaderCollection"/> instance does not allow any of <see cref="HttpResponseHeader"/> values.
		/// </exception>
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
		/// The length of <paramref name="value"/> is greater than 65535.
		/// </exception>
		public string this [HttpResponseHeader header]
		{
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
		/// A <see cref="NameObjectCollectionBase.KeysCollection"/> that contains a collection
		/// of header names in the collection.
		/// </value>
		public override KeysCollection Keys
		{
			get {
				return base.Keys;
			}
		}

		#endregion

		#region Private Methods

		void Add (string name, string value, bool ignoreRestricted)
		{
			Action <string, string> add;
			if (ignoreRestricted)
				add = AddWithoutCheckingNameAndRestricted;
			else
				add = AddWithoutCheckingName;

			DoWithCheckingState (add, CheckName (name), value, true);
		}

		void AddWithoutCheckingName (string name, string value)
		{
			DoWithoutCheckingName (base.Add, name, value);
		}

		void AddWithoutCheckingNameAndRestricted (string name, string value)
		{
			base.Add (name, CheckValue (value));
		}

		static int CheckColonSeparated (string header)
		{
			int i = header.IndexOf (':');
			if (i == -1)
				throw new ArgumentException ("No colon found.", "header");

			return i;
		}

		static HttpHeaderType CheckHeaderType (string name)
		{
			HttpHeaderInfo info;
			return !TryGetHeaderInfo (name, out info)
			       ? HttpHeaderType.Unspecified
			       : info.IsRequest && !info.IsResponse
			         ? HttpHeaderType.Request
			         : !info.IsRequest && info.IsResponse
			           ? HttpHeaderType.Response
			           : HttpHeaderType.Unspecified;
		}

		static string CheckName (string name)
		{
			if (name.IsNullOrEmpty ())
				throw new ArgumentNullException ("name");

			name = name.Trim ();
			if (!IsHeaderName (name))
				throw new ArgumentException ("Contains invalid characters.", "name");

			return name;
		}

		void CheckRestricted (string name)
		{
			if (!internallyCreated && ContainsInRestricted (name, true))
				throw new ArgumentException ("This header must be modified with the appropiate property.");
		}

		void CheckState (bool response)
		{
			if (state == HttpHeaderType.Unspecified)
				return;

			if (response && state == HttpHeaderType.Request)
				throw new InvalidOperationException ("This collection has already been used to store the request headers.");

			if (!response && state == HttpHeaderType.Response)
				throw new InvalidOperationException ("This collection has already been used to store the response headers.");
		}

		static string CheckValue (string value)
		{
			if (value.IsNullOrEmpty ())
				return String.Empty;

			value = value.Trim ();
			if (value.Length > 65535)
				throw new ArgumentOutOfRangeException ("value", "The length must not be greater than 65535.");

			if (!IsHeaderValue (value))
				throw new ArgumentException ("Contains invalid characters.", "value");

			return value;
		}

		static string Convert (string key)
		{
			HttpHeaderInfo info;
			return headers.TryGetValue (key, out info)
			       ? info.Name
			       : String.Empty;
		}

		static bool ContainsInRestricted (string name, bool response)
		{
			HttpHeaderInfo info;
			return TryGetHeaderInfo (name, out info)
			       ? info.IsRestricted (response)
			       : false;
		}

		void DoWithCheckingState (
			Action <string, string> act, string name, string value, bool setState)
		{
			var type = CheckHeaderType (name);
			if (type == HttpHeaderType.Request)
				DoWithCheckingState (act, name, value, false, setState);
			else if (type == HttpHeaderType.Response)
				DoWithCheckingState (act, name, value, true, setState);
			else
				act (name, value);
		}

		void DoWithCheckingState (
			Action <string, string> act, string name, string value, bool response, bool setState)
		{
			CheckState (response);
			act (name, value);
			if (setState)
				SetState (response);
		}

		void DoWithoutCheckingName (Action <string, string> act, string name, string value)
		{
			CheckRestricted (name);
			act (name, CheckValue (value));
		}

		static HttpHeaderInfo GetHeaderInfo (string name)
		{
			return (from HttpHeaderInfo info in headers.Values
			        where info.Name.Equals (name, StringComparison.InvariantCultureIgnoreCase)
			        select info).FirstOrDefault ();
		}

		void RemoveWithoutCheckingName (string name, string unuse)
		{
			CheckRestricted (name);
			base.Remove (name);
		}

		void SetState (bool response)
		{
			if (state == HttpHeaderType.Unspecified)
				state = response
				        ? HttpHeaderType.Response
				        : HttpHeaderType.Request;
		}

		void SetWithoutCheckingName (string name, string value)
		{
			DoWithoutCheckingName (base.Set, name, value);
		}

		static bool TryGetHeaderInfo (string name, out HttpHeaderInfo info)
		{
			info = GetHeaderInfo (name);
			return info != null;
		}

		#endregion

		#region Internal Methods

		internal static string Convert (HttpRequestHeader header)
		{
			return Convert (header.ToString ());
		}

		internal static string Convert (HttpResponseHeader header)
		{
			return Convert (header.ToString ());
		}

		internal static bool IsHeaderName (string name)
		{
			return name.IsNullOrEmpty ()
			       ? false
			       : name.IsToken ();
		}

		internal static bool IsHeaderValue (string value)
		{
			return value.IsText ();
		}

		internal static bool IsMultiValue (string headerName, bool response)
		{
			if (headerName.IsNullOrEmpty ())
				return false;

			HttpHeaderInfo info;
			return TryGetHeaderInfo (headerName, out info)
			       ? info.IsMultiValue (response)
			       : false;
		}

		internal void RemoveInternal (string name)
		{
			base.Remove (name);
		}

		internal void SetInternal (string header, bool response)
		{
			int pos = CheckColonSeparated (header);
			SetInternal (header.Substring (0, pos), header.Substring (pos + 1), response);
		}

		internal void SetInternal (string name, string value, bool response)
		{
			value = CheckValue (value);
			if (IsMultiValue (name, response))
				base.Add (name, value);
			else
				base.Set (name, value);
		}

		internal string ToStringMultiValue (bool response)
		{
			var sb = new StringBuilder ();
			Count.Times (i => {
				string key = GetKey (i);
				if (IsMultiValue (key, response)) {
					foreach (string value in GetValues (i))
						sb.AppendFormat ("{0}: {1}\r\n", key, value);
				} else {
					sb.AppendFormat ("{0}: {1}\r\n", key, Get (i));
				}
			});

			return sb.Append ("\r\n").ToString ();
		}

		#endregion

		#region Explicit Interface Implementation

		/// <summary>
		/// Populates the specified <see cref="SerializationInfo"/> with the data to need to
		/// serialize the <see cref="WebHeaderCollection"/> object.
		/// </summary>
		/// <param name="serializationInfo">
		/// A <see cref="SerializationInfo"/> that holds the data to need to serialize the <see cref="WebHeaderCollection"/> object.
		/// </param>
		/// <param name="streamingContext">
		/// A <see cref="StreamingContext"/> that specifies the destination for the serialization.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="serializationInfo"/> is <see langword="null"/>.
		/// </exception>
		[SecurityPermission (SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.SerializationFormatter, SerializationFormatter = true)]
		void ISerializable.GetObjectData (
			SerializationInfo serializationInfo, StreamingContext streamingContext)
		{
			GetObjectData (serializationInfo, streamingContext);
		}

		#endregion

		#region Protected Methods

		/// <summary>
		/// Adds a header to the collection without checking whether the header is on the restricted header list.
		/// </summary>
		/// <param name="headerName">
		/// A <see cref="string"/> that contains the name of the header to add.
		/// </param>
		/// <param name="headerValue">
		/// A <see cref="string"/> that contains the value of the header to add.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="headerName"/> is <see langword="null"/> or <see cref="String.Empty"/>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="headerName"/> or <paramref name="headerValue"/> contains invalid characters.
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// The length of <paramref name="headerValue"/> is greater than 65535.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The current <see cref="WebHeaderCollection"/> instance does not allow the <paramref name="headerName"/>.
		/// </exception>
		protected void AddWithoutValidate (string headerName, string headerValue)
		{
			Add (headerName, headerValue, true);
		}

		#endregion

		#region Public Methods

		/// <summary>
		/// Adds the specified <paramref name="header"/> to the collection.
		/// </summary>
		/// <param name="header">
		/// A <see cref="string"/> that contains a header with the name and value separated by a colon (:).
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="header"/> is <see langword="null"/>, <see cref="String.Empty"/>, or
		/// the name part of <paramref name="header"/> is <see cref="String.Empty"/>.
		/// </exception>
		/// <exception cref="ArgumentException">
		///   <para>
		///   <paramref name="header"/> does not contain a colon.
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
		/// The length of the value part of <paramref name="header"/> is greater than 65535.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The current <see cref="WebHeaderCollection"/> instance does not allow the <paramref name="header"/>.
		/// </exception>
		public void Add (string header)
		{
			if (header.IsNullOrEmpty ())
				throw new ArgumentNullException ("header");

			int pos = CheckColonSeparated (header);
			Add (header.Substring (0, pos), header.Substring (pos + 1));
		}

		/// <summary>
		/// Adds the specified request <paramref name="header"/> with the specified <paramref name="value"/> to the collection.
		/// </summary>
		/// <param name="header">
		/// A <see cref="HttpRequestHeader"/> is a request header to add.
		/// </param>
		/// <param name="value">
		/// A <see cref="string"/> that contains the value of the header to add.
		/// </param>
		/// <exception cref="InvalidOperationException">
		/// The current <see cref="WebHeaderCollection"/> instance does not allow any of <see cref="HttpRequestHeader"/> values.
		/// </exception>
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
		/// The length of <paramref name="value"/> is greater than 65535.
		/// </exception>
		public void Add (HttpRequestHeader header, string value)
		{
			DoWithCheckingState (AddWithoutCheckingName, Convert (header), value, false, true);
		}

		/// <summary>
		/// Adds the specified response <paramref name="header"/> with the specified <paramref name="value"/> to the collection.
		/// </summary>
		/// <param name="header">
		/// A <see cref="HttpResponseHeader"/> is a response header to add.
		/// </param>
		/// <param name="value">
		/// A <see cref="string"/> that contains the value of the header to add.
		/// </param>
		/// <exception cref="InvalidOperationException">
		/// The current <see cref="WebHeaderCollection"/> instance does not allow any of <see cref="HttpResponseHeader"/> values.
		/// </exception>
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
		/// The length of <paramref name="value"/> is greater than 65535.
		/// </exception>
		public void Add (HttpResponseHeader header, string value)
		{
			DoWithCheckingState (AddWithoutCheckingName, Convert (header), value, true, true);
		}

		/// <summary>
		/// Adds a header with the specified <paramref name="name"/> and <paramref name="value"/> to the collection.
		/// </summary>
		/// <param name="name">
		/// A <see cref="string"/> that contains the name of the header to add.
		/// </param>
		/// <param name="value">
		/// A <see cref="string"/> that contains the value of the header to add.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="name"/> is <see langword="null"/> or <see cref="String.Empty"/>.
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
		/// The length of <paramref name="value"/> is greater than 65535.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The current <see cref="WebHeaderCollection"/> instance does not allow the header <paramref name="name"/>.
		/// </exception>
		public override void Add (string name, string value)
		{
			Add (name, value, false);
		}

		/// <summary>
		/// Removes all headers from the collection.
		/// </summary>
		public override void Clear ()
		{
			base.Clear ();
			state = HttpHeaderType.Unspecified;
		}

		/// <summary>
		/// Get the value of the header with the specified <paramref name="index"/> in the collection.
		/// </summary>
		/// <returns>
		/// A <see cref="string"/> that receives the value of the header.
		/// </returns>
		/// <param name="index">
		/// An <see cref="int"/> that is the zero-based index of the header to get.
		/// </param>
		public override string Get (int index)
		{
			return base.Get (index);
		}

		/// <summary>
		/// Get the value of the header with the specified <paramref name="name"/> in the collection.
		/// </summary>
		/// <returns>
		/// A <see cref="string"/> that receives the value of the header.
		/// <see langword="null"/> if there is no header with <paramref name="name"/> in the collection.
		/// </returns>
		/// <param name="name">
		/// A <see cref="string"/> that contains the name of the header to get.
		/// </param>
		public override string Get (string name)
		{
			return base.Get (name);
		}

		/// <summary>
		/// Gets the enumerator to use to iterate through the <see cref="WebHeaderCollection"/>.
		/// </summary>
		/// <returns>
		/// An instance of an implementation of the <see cref="IEnumerator"/> interface
		/// to use to iterate through the <see cref="WebHeaderCollection"/>.
		/// </returns>
		public override IEnumerator GetEnumerator ()
		{
			return base.GetEnumerator ();
		}

		/// <summary>
		/// Get the header name at the specified <paramref name="index"/> position in the collection.
		/// </summary>
		/// <returns>
		/// A <see cref="string"/> that receives the header name.
		/// </returns>
		/// <param name="index">
		/// An <see cref="int"/> is the zero-based index of the key to get from the collection.
		/// </param>
		public override string GetKey (int index)
		{
			return base.GetKey (index);
		}

		/// <summary>
		/// Gets an array of header values stored in the specified <paramref name="header"/> name.
		/// </summary>
		/// <returns>
		/// An array of <see cref="string"/> that receives the header values.
		/// </returns>
		/// <param name="header">
		/// A <see cref="string"/> that contains a header name.
		/// </param>
		public override string [] GetValues (string header)
		{
			string [] values = base.GetValues (header);
			return values == null || values.Length == 0
			       ? null
			       : values;
		}

		/// <summary>
		/// Gets an array of header values stored in the specified <paramref name="index"/> position of the header collection.
		/// </summary>
		/// <returns>
		/// An array of <see cref="string"/> that receives the header values.
		/// </returns>
		/// <param name="index">
		/// An <see cref="int"/> is the zero-based index of the header in the collection.
		/// </param>
		public override string [] GetValues (int index)
		{
			string [] values = base.GetValues (index);
			return values == null || values.Length == 0
			       ? null
			       : values;
		}

		/// <summary>
		/// Populates the specified <see cref="SerializationInfo"/> with the data to need to
		/// serialize the <see cref="WebHeaderCollection"/> object.
		/// </summary>
		/// <param name="serializationInfo">
		/// A <see cref="SerializationInfo"/> that holds the data to need to serialize the <see cref="WebHeaderCollection"/> object.
		/// </param>
		/// <param name="streamingContext">
		/// A <see cref="StreamingContext"/> that specifies the destination for the serialization.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="serializationInfo"/> is <see langword="null"/>.
		/// </exception>
		[SecurityPermission (SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.SerializationFormatter)]
		public override void GetObjectData (
			SerializationInfo serializationInfo, StreamingContext streamingContext)
		{
			if (serializationInfo == null)
				throw new ArgumentNullException ("serializationInfo");

			serializationInfo.AddValue ("InternallyCreated", internallyCreated);
			serializationInfo.AddValue ("State", (int) state);

			int count = Count;
			serializationInfo.AddValue ("Count", count);
			count.Times (i => {
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
		/// A <see cref="string"/> that contains the name of the header to test.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="headerName"/> is <see langword="null"/> or <see cref="String.Empty"/>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="headerName"/> contains invalid characters.
		/// </exception>
		public static bool IsRestricted (string headerName)
		{
			return IsRestricted (headerName, false);
		}

		/// <summary>
		/// Determines whether the specified header can be set for the request or the response.
		/// </summary>
		/// <returns>
		/// <c>true</c> if the header is restricted; otherwise, <c>false</c>.
		/// </returns>
		/// <param name="headerName">
		/// A <see cref="string"/> that contains the name of the header to test.
		/// </param>
		/// <param name="response">
		/// <c>true</c> if does the test for the response; for the request, <c>false</c>.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="headerName"/> is <see langword="null"/> or <see cref="String.Empty"/>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="headerName"/> contains invalid characters.
		/// </exception>
		public static bool IsRestricted (string headerName, bool response)
		{
			return ContainsInRestricted (CheckName (headerName), response);
		}

		/// <summary>
		/// Implements the <see cref="ISerializable"/> interface and raises the deserialization event
		/// when the deserialization is complete.
		/// </summary>
		/// <param name="sender">
		/// An <see cref="object"/> that contains the source of the deserialization event.
		/// </param>
		public override void OnDeserialization (object sender)
		{
		}

		/// <summary>
		/// Removes the specified header from the collection.
		/// </summary>
		/// <param name="header">
		/// A <see cref="HttpRequestHeader"/> to remove from the collection.
		/// </param>
		/// <exception cref="InvalidOperationException">
		/// The current <see cref="WebHeaderCollection"/> instance does not allow any of <see cref="HttpRequestHeader"/> values.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="header"/> is a restricted header.
		/// </exception>
		public void Remove (HttpRequestHeader header)
		{
			DoWithCheckingState (RemoveWithoutCheckingName, Convert (header), null, false, false);
		}

		/// <summary>
		/// Removes the specified header from the collection.
		/// </summary>
		/// <param name="header">
		/// A <see cref="HttpResponseHeader"/> to remove from the collection.
		/// </param>
		/// <exception cref="InvalidOperationException">
		/// The current <see cref="WebHeaderCollection"/> instance does not allow any of <see cref="HttpResponseHeader"/> values.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="header"/> is a restricted header.
		/// </exception>
		public void Remove (HttpResponseHeader header)
		{
			DoWithCheckingState (RemoveWithoutCheckingName, Convert (header), null, true, false);
		}

		/// <summary>
		/// Removes the specified header from the collection.
		/// </summary>
		/// <param name="name">
		/// A <see cref="string"/> that contains the name of the header to remove from the collection.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="name"/> is <see langword="null"/> or <see cref="String.Empty"/>.
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
		/// The current <see cref="WebHeaderCollection"/> instance does not allow the header <paramref name="name"/>.
		/// </exception>
		public override void Remove (string name)
		{
			DoWithCheckingState (RemoveWithoutCheckingName, CheckName (name), null, false);
		}

		/// <summary>
		/// Sets the specified header to the specified value.
		/// </summary>
		/// <param name="header">
		/// A <see cref="HttpRequestHeader"/> to set.
		/// </param>
		/// <param name="value">
		/// A <see cref="string"/> that contains the value of the header to set.
		/// </param>
		/// <exception cref="InvalidOperationException">
		/// The current <see cref="WebHeaderCollection"/> instance does not allow any of <see cref="HttpRequestHeader"/> values.
		/// </exception>
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
		/// The length of <paramref name="value"/> is greater than 65535.
		/// </exception>
		public void Set (HttpRequestHeader header, string value)
		{
			DoWithCheckingState (SetWithoutCheckingName, Convert (header), value, false, true);
		}

		/// <summary>
		/// Sets the specified header to the specified value.
		/// </summary>
		/// <param name="header">
		/// A <see cref="HttpResponseHeader"/> to set.
		/// </param>
		/// <param name="value">
		/// A <see cref="string"/> that contains the value of the header to set.
		/// </param>
		/// <exception cref="InvalidOperationException">
		/// The current <see cref="WebHeaderCollection"/> instance does not allow any of <see cref="HttpResponseHeader"/> values.
		/// </exception>
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
		/// The length of <paramref name="value"/> is greater than 65535.
		/// </exception>
		public void Set (HttpResponseHeader header, string value)
		{
			DoWithCheckingState (SetWithoutCheckingName, Convert (header), value, true, true);
		}

		/// <summary>
		/// Sets the specified header to the specified value.
		/// </summary>
		/// <param name="name">
		/// A <see cref="string"/> that contains the name of the header to set.
		/// </param>
		/// <param name="value">
		/// A <see cref="string"/> that contains the value of the header to set.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="name"/> is <see langword="null"/> or <see cref="String.Empty"/>.
		/// </exception>
		/// <exception cref="ArgumentException">
		///   <para>
		///   <paramref name="name"/> or <paramref name="value"/> contain invalid characters.
		///   </para>
		///   <para>
		///   -or-
		///   </para>
		///   <para>
		///   <paramref name="name"/> is a restricted header name.
		///   </para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// The length of <paramref name="value"/> is greater than 65535.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The current <see cref="WebHeaderCollection"/> instance does not allow the header <paramref name="name"/>.
		/// </exception>
		public override void Set (string name, string value)
		{
			DoWithCheckingState (SetWithoutCheckingName, CheckName (name), value, true);
		}

		/// <summary>
		/// Converts the current <see cref="WebHeaderCollection"/> to an array of <see cref="byte"/>.
		/// </summary>
		/// <returns>
		/// An array of <see cref="byte"/> that receives the converted current <see cref="WebHeaderCollection"/>.
		/// </returns>
		public byte [] ToByteArray ()
		{
			return Encoding.UTF8.GetBytes (ToString ());
		}

		/// <summary>
		/// Returns a <see cref="string"/> that represents the current <see cref="WebHeaderCollection"/>.
		/// </summary>
		/// <returns>
		/// A <see cref="string"/> that represents the current <see cref="WebHeaderCollection"/>.
		/// </returns>
		public override string ToString ()
		{
			var sb = new StringBuilder();
			Count.Times (i => {
				sb.AppendFormat ("{0}: {1}\r\n", GetKey (i), Get (i));
			});

			return sb.Append ("\r\n").ToString ();
		}

		#endregion
	}
}
