//
// WebHeaderCollection.cs
//	Copied from System.Net.WebHeaderCollection.cs
//
// Authors:
//	Lawrence Pit (loz@cable.a2000.nl)
//	Gonzalo Paniagua Javier (gonzalo@ximian.com)
//	Miguel de Icaza (miguel@novell.com)
//
// Copyright (c) 2003 Ximian, Inc. (http://www.ximian.com)
// Copyright (c) 2007 Novell, Inc. (http://www.novell.com)
// Copyright (c) 2012-2013 sta.blockhead (sta.blockhead@gmail.com)
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
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Text;
        
namespace WebSocketSharp.Net {

	/// <summary>
	/// Provides a collection of the HTTP headers associated with a request or response.
	/// </summary>
	[Serializable]
	[ComVisible (true)]
	public class WebHeaderCollection : NameValueCollection, ISerializable
	{
		#region Fields

		private static readonly Dictionary<string, bool> multiValue;
		private static readonly Dictionary<string, bool> restricted;
		private static readonly Dictionary<string, bool> restricted_response;

		private bool internallyCreated = false;

		#endregion

		#region Constructors

		static WebHeaderCollection () 
		{
			// the list of restricted header names as defined 
			// by the ms.net spec
			restricted = new Dictionary<string, bool> (StringComparer.InvariantCultureIgnoreCase);
			restricted.Add ("accept", true);
			restricted.Add ("connection", true);
			restricted.Add ("content-length", true);
			restricted.Add ("content-type", true);
			restricted.Add ("date", true);
			restricted.Add ("expect", true);
			restricted.Add ("host", true);
			restricted.Add ("if-modified-since", true);
			restricted.Add ("range", true);
			restricted.Add ("referer", true);
			restricted.Add ("transfer-encoding", true);
			restricted.Add ("user-agent", true);
			restricted.Add ("proxy-connection", true);

			//
			restricted_response = new Dictionary<string, bool> (StringComparer.InvariantCultureIgnoreCase);
			restricted_response.Add ("Content-Length", true);
			restricted_response.Add ("Transfer-Encoding", true);
			restricted_response.Add ("WWW-Authenticate", true);

			// see par 14 of RFC 2068 to see which header names
			// accept multiple values each separated by a comma
			multiValue = new Dictionary<string, bool> (StringComparer.InvariantCultureIgnoreCase);
			multiValue.Add ("accept", true);
			multiValue.Add ("accept-charset", true);
			multiValue.Add ("accept-encoding", true);
			multiValue.Add ("accept-language", true);
			multiValue.Add ("accept-ranges", true);
			multiValue.Add ("allow", true);
			multiValue.Add ("authorization", true);
			multiValue.Add ("cache-control", true);
			multiValue.Add ("connection", true);
			multiValue.Add ("content-encoding", true);
			multiValue.Add ("content-language", true);
			multiValue.Add ("expect", true);
			multiValue.Add ("if-match", true);
			multiValue.Add ("if-none-match", true);
			multiValue.Add ("proxy-authenticate", true);
			multiValue.Add ("public", true);
			multiValue.Add ("range", true);
			multiValue.Add ("transfer-encoding", true);
			multiValue.Add ("upgrade", true);
			multiValue.Add ("vary", true);
			multiValue.Add ("via", true);
			multiValue.Add ("warning", true);
			multiValue.Add ("www-authenticate", true);

			// Extra
			multiValue.Add ("set-cookie", true);
			multiValue.Add ("set-cookie2", true);
		}

		internal WebHeaderCollection (bool internallyCreated)
		{
			this.internallyCreated = internallyCreated;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="WebHeaderCollection"/> class
		/// with the specified <see cref="SerializationInfo"/> and <see cref="StreamingContext"/>.
		/// </summary>
		/// <param name="serializationInfo">
		/// A <see cref="SerializationInfo"/> that holds the serialized object data.
		/// </param>
		/// <param name="streamingContext">
		/// A <see cref="StreamingContext"/> that contains the contextual information about the source or destination.
		/// </param>
		protected WebHeaderCollection (
			SerializationInfo serializationInfo, StreamingContext streamingContext)
		{
			int count;
			try {
				count = serializationInfo.GetInt32("Count");
				for (int i = 0; i < count; i++)
					this.Add (
						serializationInfo.GetString (i.ToString ()),
						serializationInfo.GetString ((count + i).ToString ()));
			} catch (SerializationException) {
				count = serializationInfo.GetInt32("count");
				for (int i = 0; i < count; i++)
					this.Add (
						serializationInfo.GetString ("k" + i),
						serializationInfo.GetString ("v" + i));
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="WebHeaderCollection"/> class.
		/// </summary>
		public WebHeaderCollection ()
		{
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
		/// A <see cref="HttpRequestHeader"/> that contains a request header name.
		/// </param>
		public string this [HttpRequestHeader header]
		{
			get {
				return Get (RequestHeaderToString (header));
			}

			set {
				// TODO: Support to throw InvalidOperationException.

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
		/// A <see cref="HttpResponseHeader"/> that contains a response header name.
		/// </param>
		public string this [HttpResponseHeader header]
		{
			get {
				return Get (ResponseHeaderToString (header));
			}

			set {
				// TODO: Support to throw InvalidOperationException.

				Add (header, value);
			}
		}

		/// <summary>
		/// Gets a collection of header names in the collection.
		/// </summary>
		/// <value>
		/// A <see cref="KeysCollection"/> that contains a collection of header names in the collection.
		/// </value>
		public override KeysCollection Keys
		{
			get {
				return base.Keys;
			}
		}

		#endregion

		#region Private Methods

		static string RequestHeaderToString (HttpRequestHeader value)
		{
			switch (value){
			case HttpRequestHeader.CacheControl:
				return "Cache-Control";
			case HttpRequestHeader.Connection:
				return "Connection";
			case HttpRequestHeader.Date:
				return "Date";
			case HttpRequestHeader.KeepAlive:
				return "Keep-Alive";
			case HttpRequestHeader.Pragma:
				return "Pragma";
			case HttpRequestHeader.Trailer:
				return "Trailer";
			case HttpRequestHeader.TransferEncoding:
				return "Transfer-Encoding";
			case HttpRequestHeader.Upgrade:
				return "Upgrade";
			case HttpRequestHeader.Via:
				return "Via";
			case HttpRequestHeader.Warning:
				return "Warning";
			case HttpRequestHeader.Allow:
				return "Allow";
			case HttpRequestHeader.ContentLength:
				return "Content-Length";
			case HttpRequestHeader.ContentType:
				return "Content-Type";
			case HttpRequestHeader.ContentEncoding:
				return "Content-Encoding";
			case HttpRequestHeader.ContentLanguage:
				return "Content-Language";
			case HttpRequestHeader.ContentLocation:
				return "Content-Location";
			case HttpRequestHeader.ContentMd5:
				return "Content-MD5";
			case HttpRequestHeader.ContentRange:
				return "Content-Range";
			case HttpRequestHeader.Expires:
				return "Expires";
			case HttpRequestHeader.LastModified:
				return "Last-Modified";
			case HttpRequestHeader.Accept:
				return "Accept";
			case HttpRequestHeader.AcceptCharset:
				return "Accept-Charset";
			case HttpRequestHeader.AcceptEncoding:
				return "Accept-Encoding";
			case HttpRequestHeader.AcceptLanguage:
				return "accept-language";
			case HttpRequestHeader.Authorization:
				return "Authorization";
			case HttpRequestHeader.Cookie:
				return "Cookie";
			case HttpRequestHeader.Expect:
				return "Expect";
			case HttpRequestHeader.From:
				return "From";
			case HttpRequestHeader.Host:
				return "Host";
			case HttpRequestHeader.IfMatch:
				return "If-Match";
			case HttpRequestHeader.IfModifiedSince:
				return "If-Modified-Since";
			case HttpRequestHeader.IfNoneMatch:
				return "If-None-Match";
			case HttpRequestHeader.IfRange:
				return "If-Range";
			case HttpRequestHeader.IfUnmodifiedSince:
				return "If-Unmodified-Since";
			case HttpRequestHeader.MaxForwards:
				return "Max-Forwards";
			case HttpRequestHeader.ProxyAuthorization:
				return "Proxy-Authorization";
			case HttpRequestHeader.Referer:
				return "Referer";
			case HttpRequestHeader.Range:
				return "Range";
			case HttpRequestHeader.Te:
				return "TE";
			case HttpRequestHeader.Translate:
				return "Translate";
			case HttpRequestHeader.UserAgent:
				return "User-Agent";
			default:
				throw new InvalidOperationException ();
			}
		}

		static string ResponseHeaderToString (HttpResponseHeader value)
		{
			switch (value){
			case HttpResponseHeader.CacheControl:
				return "Cache-Control";
			case HttpResponseHeader.Connection:
				return "Connection";
			case HttpResponseHeader.Date:
				return "Date";
			case HttpResponseHeader.KeepAlive:
				return "Keep-Alive";
			case HttpResponseHeader.Pragma:
				return "Pragma";
			case HttpResponseHeader.Trailer:
				return "Trailer";
			case HttpResponseHeader.TransferEncoding:
				return "Transfer-Encoding";
			case HttpResponseHeader.Upgrade:
				return "Upgrade";
			case HttpResponseHeader.Via:
				return "Via";
			case HttpResponseHeader.Warning:
				return "Warning";
			case HttpResponseHeader.Allow:
				return "Allow";
			case HttpResponseHeader.ContentLength:
				return "Content-Length";
			case HttpResponseHeader.ContentType:
				return "Content-Type";
			case HttpResponseHeader.ContentEncoding:
				return "Content-Encoding";
			case HttpResponseHeader.ContentLanguage:
				return "Content-Language";
			case HttpResponseHeader.ContentLocation:
				return "Content-Location";
			case HttpResponseHeader.ContentMd5:
				return "Content-MD5";
			case HttpResponseHeader.ContentRange:
				return "Content-Range";
			case HttpResponseHeader.Expires:
				return "Expires";
			case HttpResponseHeader.LastModified:
				return "Last-Modified";
			case HttpResponseHeader.AcceptRanges:
				return "Accept-Ranges";
			case HttpResponseHeader.Age:
				return "Age";
			case HttpResponseHeader.ETag:
				return "ETag";
			case HttpResponseHeader.Location:
				return "Location";
			case HttpResponseHeader.ProxyAuthenticate:
				return "Proxy-Authenticate";
			case HttpResponseHeader.RetryAfter:
				return "Retry-After";
			case HttpResponseHeader.Server:
				return "Server";
			case HttpResponseHeader.SetCookie:
				return "Set-Cookie";
			case HttpResponseHeader.Vary:
				return "Vary";
			case HttpResponseHeader.WwwAuthenticate:
				return "WWW-Authenticate";
			default:
				throw new InvalidOperationException ();
			}
		}

		static string Trim (string value)
		{
			return value.IsNullOrEmpty ()
			       ? String.Empty
			       : value.Trim ();
		}

		#endregion

		#region Internal Methods

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

		internal static bool IsMultiValue (string headerName)
		{
			return headerName.IsNullOrEmpty ()
			       ? false
			       : multiValue.ContainsKey (headerName);
		}

		internal string ToStringMultiValue ()
		{
			var sb = new StringBuilder();
			int count = base.Count;
			for (int i = 0; i < count ; i++) {
				string key = GetKey (i);
				if (IsMultiValue (key)) {
					foreach (string v in GetValues (i)) {
						sb.Append (key)
						  .Append (": ")
						  .Append (v)
						  .Append ("\r\n");
					}
				} else {
					sb.Append (key)
					  .Append (": ")
					  .Append (Get (i))
					  .Append ("\r\n");
				}
			}

			return sb.Append("\r\n").ToString();
		}

		// With this we don't check for invalid characters in header. See bug #55994.
		internal void SetInternal (string header)
		{
			int pos = header.IndexOf (':');
			if (pos == -1)
				throw new ArgumentException ("No colon found", "header");

			SetInternal (header.Substring (0, pos), header.Substring (pos + 1));
		}

		internal void RemoveAndAdd (string name, string value)
		{
			value = Trim (value);
			base.Remove (name);
			base.Set (name, value);
		}

		internal void RemoveInternal (string name)
		{
			if (name.IsNull ())
				throw new ArgumentNullException ("name");

			base.Remove (name);
		}

		internal void SetInternal (string name, string value)
		{
			value = Trim (value);
			if (!IsHeaderValue (value))
				throw new ArgumentException ("Invalid header value.");

			if (IsMultiValue (name)) {
				base.Add (name, value);
			} else {
				base.Remove (name);
				base.Set (name, value);
			}
		}

		#endregion

		#region Explicit Interface Implementation

		/// <summary>
		/// Populates the specified <see cref="SerializationInfo"/> with the data needed to serialize the <see cref="WebHeaderCollection"/>.
		/// </summary>
		/// <param name="serializationInfo">
		/// A <see cref="SerializationInfo"/> that holds the serialized object data.
		/// </param>
		/// <param name="streamingContext">
		/// A <see cref="StreamingContext"/> that specifies the destination for the serialization.
		/// </param>
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
		/// <exception cref="ArgumentException">
		///   <para>
		///   <paramref name="headerName"/> is <see langword="null"/>, <see cref="String.Empty"/>, or
		///   contains invalid characters.
		///   </para>
		///   <para>
		///   -or-
		///   </para>
		///   <para>
		///   <paramref name="headerValue"/> contains invalid characters.
		///   </para>
		/// </exception>
		protected void AddWithoutValidate (string headerName, string headerValue)
		{
			var name = Trim (headerName);
			if (!IsHeaderName (name))
				throw new ArgumentException ("Invalid header name: " + name, "headerName");

			var value = Trim (headerValue);
			if (!IsHeaderValue (value))
				throw new ArgumentException ("Invalid header value: " + value, "headerValue");

			base.Add (name, value);
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
		/// <paramref name="header"/> is <see langword="null"/> or a <see cref="String.Empty"/>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="header"/> does not contain a colon.
		/// </exception>
		public void Add (string header)
		{
			if (header.IsNullOrEmpty ())
				throw new ArgumentNullException ("header");

			int pos = header.IndexOf (':');
			if (pos == -1)
				throw new ArgumentException ("No colon found", "header");

			Add (header.Substring (0, pos), header.Substring (pos + 1));
		}

		/// <summary>
		/// Adds the specified request <paramref name="header"/> with the specified <paramref name="value"/> to the collection.
		/// </summary>
		/// <param name="header">
		/// A <see cref="HttpRequestHeader"/> that contains the name of the request header to add.
		/// </param>
		/// <param name="value">
		/// A <see cref="string"/> that contains the value of the header to add.
		/// </param>
		public void Add (HttpRequestHeader header, string value)
		{
			Add (RequestHeaderToString (header), value);
		}

		/// <summary>
		/// Adds the specified response <paramref name="header"/> with the specified <paramref name="value"/> to the collection.
		/// </summary>
		/// <param name="header">
		/// A <see cref="HttpResponseHeader"/> that contains the name of the response header to add.
		/// </param>
		/// <param name="value">
		/// A <see cref="string"/> that contains the value of the header to add.
		/// </param>
		public void Add (HttpResponseHeader header, string value)
		{
			Add (ResponseHeaderToString (header), value);
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
		/// <exception cref="ArgumentException">
		///   <para>
		///   <paramref name="name"/> is <see langword="null"/> or a <see cref="String.Empty"/>.
		///   </para>
		///   <para>
		///   -or-
		///   </para>
		///   <para>
		///   <paramref name="name"/> is a restricted header that must be set with a property setting.
		///   </para>
		///   <para>
		///   -or-
		///   </para>
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// The length of <paramref name="value"/> is greater than 65535.
		/// </exception>
		public override void Add (string name, string value)
		{
			if (internallyCreated && IsRestricted (name))
				throw new ArgumentException ("This header must be modified with the appropiate property.");

			if (value.Length > 65535)
				throw new ArgumentOutOfRangeException ("value", "The length must not be greater than 65535.");

			AddWithoutValidate (name, value);
		}

		/// <summary>
		/// Removes all headers from the collection.
		/// </summary>
		public override void Clear ()
		{
			base.Clear ();
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
			return values.IsNull () || values.Length == 0
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
			return values.IsNull () || values.Length == 0
			       ? null
			       : values;
		}

		/// <summary>
		/// Populates the specified <see cref="SerializationInfo"/> with the data needed to serialize the <see cref="WebHeaderCollection"/>.
		/// </summary>
		/// <param name="serializationInfo">
		/// A <see cref="SerializationInfo"/> that holds the serialized object data.
		/// </param>
		/// <param name="streamingContext">
		/// A <see cref="StreamingContext"/> that specifies the destination for the serialization.
		/// </param>
		[SecurityPermission (SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.SerializationFormatter)]
		public override void GetObjectData (
			SerializationInfo serializationInfo, StreamingContext streamingContext)
		{
			int count = base.Count;
			serializationInfo.AddValue ("Count", count);
			for (int i = 0; i < count; i++) {
				serializationInfo.AddValue (i.ToString (), GetKey (i));
				serializationInfo.AddValue ((count + i).ToString (), Get (i));
			}
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
			if (headerName.IsNullOrEmpty ())
				throw new ArgumentNullException ("headerName", "Must not be null or empty.");

			var name = headerName.Trim ();
			if (!IsHeaderName (name))
				throw new ArgumentException ("Invalid character in header.");

			return response
			       ? restricted_response.ContainsKey (name)
			       : restricted.ContainsKey (name);
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
		public void Remove (HttpRequestHeader header)
		{
			// TODO: Support to throw InvalidOperationException.

			Remove (RequestHeaderToString (header));
		}

		/// <summary>
		/// Removes the specified header from the collection.
		/// </summary>
		/// <param name="header">
		/// A <see cref="HttpResponseHeader"/> to remove from the collection.
		/// </param>
		public void Remove (HttpResponseHeader header)
		{
			// TODO: Support to throw InvalidOperationException.

			Remove (ResponseHeaderToString (header));
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
		public override void Remove (string name)
		{
			if (name.IsNullOrEmpty ())
				throw new ArgumentNullException ("name");

			name = name.Trim ();
			if (!IsHeaderName (name))
				throw new ArgumentException ("Invalid characters in header.");

			if (internallyCreated && IsRestricted (name))
				throw new ArgumentException ("Restricted header.");

			base.Remove (name);
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
		public void Set (HttpRequestHeader header, string value)
		{
			// TODO: Support to throw InvalidOperationException.

			Set (RequestHeaderToString (header), value);
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
		public void Set (HttpResponseHeader header, string value)
		{
			// TODO: Support to throw InvalidOperationException.

			Set (ResponseHeaderToString (header), value);
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
		public override void Set (string name, string value)
		{
			if (name.IsNullOrEmpty ())
				throw new ArgumentNullException ("name");

			name = name.Trim ();
			if (!IsHeaderName (name))
				throw new ArgumentException ("Invalid header name.");

			if (internallyCreated && IsRestricted (name))
				throw new ArgumentException ("Restricted header.");

			value = Trim (value);
			if (value.Length > 65535)
				throw new ArgumentOutOfRangeException ("value", "The length must not be greater than 65535.");

			if (!IsHeaderValue (value))
				throw new ArgumentException ("Invalid header value.");

			base.Set (name, value);
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
			for (int i = 0; i < Count ; i++)
				sb.Append (GetKey (i))
				  .Append (": ")
				  .Append (Get (i))
				  .Append ("\r\n");

			return sb.Append("\r\n").ToString();
		}

		#endregion
	}
}
