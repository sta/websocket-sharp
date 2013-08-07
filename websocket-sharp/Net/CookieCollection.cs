//
// CookieCollection.cs
//	Copied from System.Net.CookieCollection.cs
//
// Authors:
//	Lawrence Pit (loz@cable.a2000.nl)
//	Gonzalo Paniagua Javier (gonzalo@ximian.com)
//	Sebastien Pouliot (sebastien@ximian.com)
//	sta (sta.blockhead@gmail.com)
//
// Copyright (c) 2004,2009 Novell, Inc (http://www.novell.com)
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
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace WebSocketSharp.Net {

	/// <summary>
	/// Provides a collection container for instances of the <see cref="Cookie"/> class.
	/// </summary>
	[Serializable]
	public class CookieCollection : ICollection, IEnumerable
	{
		// not 100% identical to MS implementation
		sealed class CookieCollectionComparer : IComparer<Cookie>
		{
			public int Compare (Cookie x, Cookie y)
			{
				if (x == null || y == null)
					return 0;

				int c1 = x.Name.Length + x.Value.Length;
				int c2 = y.Name.Length + y.Value.Length;

				return (c1 - c2);
			}
		}

		#region Private Static Fields

		static CookieCollectionComparer Comparer = new CookieCollectionComparer ();

		#endregion

		#region Private Fields

		List<Cookie> list;
		object       sync;

		#endregion

		#region Public Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="CookieCollection"/> class.
		/// </summary>
		public CookieCollection ()
		{
			list = new List<Cookie> ();
		}

		#endregion

		#region Internal Properties

		internal IList<Cookie> List {
			get { return list; }
		}

		internal IEnumerable<Cookie> Sorted {
			get {
				return from cookie in list
				       orderby cookie.Version,
				               cookie.Name,
				               cookie.Path.Length descending
				       select cookie;
			}
		}

		#endregion

		#region Public Properties

		/// <summary>
		/// Gets the number of cookies contained in the <see cref="CookieCollection"/>.
		/// </summary>
		/// <value>
		/// An <see cref="int"/> that indicates the number of cookies contained in the <see cref="CookieCollection"/>.
		/// </value>
		public int Count {
			get { return list.Count; }
		}

		// LAMESPEC: So how is one supposed to create a writable CookieCollection 
		// instance?? We simply ignore this property, as this collection is always
		// writable.
		//
		/// <summary>
		/// Gets a value indicating whether the <see cref="CookieCollection"/> is read-only.
		/// </summary>
		/// <value>
		/// <c>true</c> if the <see cref="CookieCollection"/> is read-only; otherwise, <c>false</c>.
		/// The default is <c>true</c>.
		/// </value>
		public bool IsReadOnly {
			get { return true; }
		}

		/// <summary>
		/// Gets a value indicating whether access to the <see cref="CookieCollection"/> is thread safe.
		/// </summary>
		/// <value>
		/// <c>true</c> if access to the <see cref="CookieCollection"/> is thread safe; otherwise, <c>false</c>.
		/// The default is <c>false</c>.
		/// </value>
		public bool IsSynchronized {
			get { return false; }
		}

		/// <summary>
		/// Gets the <see cref="Cookie"/> with the specified <paramref name="index"/> from the <see cref="CookieCollection"/>.
		/// </summary>
		/// <value>
		/// A <see cref="Cookie"/> with the specified <paramref name="index"/> in the <see cref="CookieCollection"/>.
		/// </value>
		/// <param name="index">
		/// An <see cref="int"/> is the zero-based index of the <see cref="Cookie"/> to find.
		/// </param>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <paramref name="index"/> is less than zero or <paramref name="index"/> is greater than or
		/// equal to <see cref="Count"/>.
		/// </exception>
		public Cookie this [int index] {
			get {
				if (index < 0 || index >= list.Count)
					throw new ArgumentOutOfRangeException ("index");

				return list [index];
			}
		}

		/// <summary>
		/// Gets the <see cref="Cookie"/> with the specified <paramref name="name"/> from the <see cref="CookieCollection"/>.
		/// </summary>
		/// <value>
		/// A <see cref="Cookie"/> with the specified <paramref name="name"/> in the <see cref="CookieCollection"/>.
		/// </value>
		/// <param name="name">
		/// A <see cref="string"/> is the name of the <see cref="Cookie"/> to find.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="name"/> is <see langword="null"/>.
		/// </exception>
		public Cookie this [string name] {
			get {
				if (name == null)
					throw new ArgumentNullException ("name");

				foreach (var cookie in Sorted) {
					if (cookie.Name.Equals (name, StringComparison.InvariantCultureIgnoreCase))
						return cookie;
				}

				return null;
			}
		}

		/// <summary>
		/// Gets an object to use to synchronize access to the <see cref="CookieCollection"/>.
		/// </summary>
		/// <value>
		/// An <see cref="Object"/> to use to synchronize access to the <see cref="CookieCollection"/>.
		/// </value>
		public Object SyncRoot {
			get {
				if (sync == null)
					sync = new object ();

				return sync;
			}
		}

		#endregion

		#region Private Methods

		static CookieCollection ParseRequest (string value)
		{
			var cookies = new CookieCollection ();

			Cookie cookie = null;
			int version = 0;
			string [] pairs = Split(value).ToArray();
			for (int i = 0; i < pairs.Length; i++) {
				string pair = pairs [i].Trim ();
				if (pair.Length == 0)
					continue;

				if (pair.StartsWith ("$version", StringComparison.InvariantCultureIgnoreCase)) {
					version = Int32.Parse (pair.GetValueInternal ("=").Trim ('"'));
				}
				else if (pair.StartsWith ("$path", StringComparison.InvariantCultureIgnoreCase)) {
					if (cookie != null)
						cookie.Path = pair.GetValueInternal ("=");
				}
				else if (pair.StartsWith ("$domain", StringComparison.InvariantCultureIgnoreCase)) {
					if (cookie != null)
						cookie.Domain = pair.GetValueInternal ("=");
				}
				else if (pair.StartsWith ("$port", StringComparison.InvariantCultureIgnoreCase)) {
					var port = pair.Equals ("$port", StringComparison.InvariantCultureIgnoreCase)
					         ? "\"\""
					         : pair.GetValueInternal ("=");

					if (cookie != null)
						cookie.Port = port;
				}
				else {
					if (cookie != null)
						cookies.Add (cookie);

					string name;
					string val = String.Empty;
					int pos = pair.IndexOf ('=');
					if (pos == -1) {
						name = pair;
					}
					else if (pos == pair.Length - 1) {
						name = pair.Substring (0, pos).TrimEnd (' ');
					}
					else {
						name = pair.Substring (0, pos).TrimEnd (' ');
						val = pair.Substring (pos + 1).TrimStart (' ');
					}

					cookie = new Cookie (name, val);
					if (version != 0)
						cookie.Version = version;
				}
			}

			if (cookie != null)
				cookies.Add (cookie);

			return cookies;
		}

		static CookieCollection ParseResponse (string value)
		{
			var cookies = new CookieCollection ();

			Cookie cookie = null;
			string [] pairs = Split(value).ToArray();
			for (int i = 0; i < pairs.Length; i++) {
				string pair = pairs [i].Trim ();
				if (pair.Length == 0)
					continue;

				if (pair.StartsWith ("version", StringComparison.InvariantCultureIgnoreCase)) {
					if (cookie != null)
						cookie.Version = Int32.Parse (pair.GetValueInternal ("=").Trim ('"'));
				}
				else if (pair.StartsWith ("expires", StringComparison.InvariantCultureIgnoreCase)) {
					var buffer = new StringBuilder (pair.GetValueInternal ("="), 32);
					if (i < pairs.Length - 1)
						buffer.AppendFormat (", {0}", pairs [++i].Trim ());

					DateTime expires;
					if (!DateTime.TryParseExact (buffer.ToString (),
						new string [] { "ddd, dd'-'MMM'-'yyyy HH':'mm':'ss 'GMT'", "r" },
						CultureInfo.CreateSpecificCulture("en-US"),
						DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
						out expires))
						expires = DateTime.Now;

					if (cookie != null && cookie.Expires == DateTime.MinValue)
						cookie.Expires = expires.ToLocalTime ();
				}
				else if (pair.StartsWith ("max-age", StringComparison.InvariantCultureIgnoreCase)) {
					int max = Int32.Parse (pair.GetValueInternal ("=").Trim ('"'));
					var expires = DateTime.Now.AddSeconds ((double) max);
					if (cookie != null)
						cookie.Expires = expires;
				}
				else if (pair.StartsWith ("path", StringComparison.InvariantCultureIgnoreCase)) {
					if (cookie != null)
						cookie.Path = pair.GetValueInternal ("=");
				}
				else if (pair.StartsWith ("domain", StringComparison.InvariantCultureIgnoreCase)) {
					if (cookie != null)
						cookie.Domain = pair.GetValueInternal ("=");
				}
				else if (pair.StartsWith ("port", StringComparison.InvariantCultureIgnoreCase)) {
					var port = pair.Equals ("port", StringComparison.InvariantCultureIgnoreCase)
					         ? "\"\""
					         : pair.GetValueInternal ("=");

					if (cookie != null)
						cookie.Port = port;
				}
				else if (pair.StartsWith ("comment", StringComparison.InvariantCultureIgnoreCase)) {
					if (cookie != null)
						cookie.Comment = pair.GetValueInternal ("=").UrlDecode ();
				}
				else if (pair.StartsWith ("commenturl", StringComparison.InvariantCultureIgnoreCase)) {
					if (cookie != null)
						cookie.CommentUri = pair.GetValueInternal ("=").Trim ('"').ToUri ();
				}
				else if (pair.StartsWith ("discard", StringComparison.InvariantCultureIgnoreCase)) {
					if (cookie != null)
						cookie.Discard = true;
				}
				else if (pair.StartsWith ("secure", StringComparison.InvariantCultureIgnoreCase)) {
					if (cookie != null)
						cookie.Secure = true;
				}
				else if (pair.StartsWith ("httponly", StringComparison.InvariantCultureIgnoreCase)) {
					if (cookie != null)
						cookie.HttpOnly = true;
				}
				else {
					if (cookie != null)
						cookies.Add (cookie);

					string name;
					string val = String.Empty;
					int pos = pair.IndexOf ('=');
					if (pos == -1) {
						name = pair;
					}
					else if (pos == pair.Length - 1) {
						name = pair.Substring (0, pos).TrimEnd (' ');
					}
					else {
						name = pair.Substring (0, pos).TrimEnd (' ');
						val = pair.Substring (pos + 1).TrimStart (' ');
					}

					cookie = new Cookie (name, val);
				}
			}

			if (cookie != null)
				cookies.Add (cookie);

			return cookies;
		}

		int SearchCookie (Cookie cookie)
		{
			string name = cookie.Name;
			string path = cookie.Path;
			string domain = cookie.Domain;
			int version = cookie.Version;

			for (int i = list.Count - 1; i >= 0; i--) {
				Cookie c = list [i];
				if (!c.Name.Equals (name, StringComparison.InvariantCultureIgnoreCase))
					continue;

				if (!c.Path.Equals (path, StringComparison.InvariantCulture))
					continue;

				if (!c.Domain.Equals (domain, StringComparison.InvariantCultureIgnoreCase))
					continue;

				if (c.Version != version)
					continue;

				return i;
			}

			return -1;
		}

		static IEnumerable<string> Split (string value)
		{
			return value.SplitHeaderValue (',', ';');
		}

		#endregion

		#region Internal Methods

		internal static CookieCollection Parse (string value, bool response)
		{
			return response
			       ? ParseResponse (value)
			       : ParseRequest (value);
		}

		internal void SetOrRemove (Cookie cookie)
		{
			int pos = SearchCookie (cookie);
			if (pos == -1) {
				if (!cookie.Expired)
					list.Add (cookie);
			}
			else {
				if (!cookie.Expired)
					list [pos] = cookie;
				else
					list.RemoveAt (pos);
			}
		}

		internal void SetOrRemove (CookieCollection cookies)
		{
			foreach (Cookie cookie in cookies)
				SetOrRemove (cookie);
		}

		internal void Sort ()
		{
			if (list.Count > 0)
				list.Sort (Comparer);
		}

		#endregion

		#region Public Methods

		/// <summary>
		/// Add the specified <see cref="Cookie"/> to the <see cref="CookieCollection"/>.
		/// </summary>
		/// <param name="cookie">
		/// A <see cref="Cookie"/> to add to the <see cref="CookieCollection"/>.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="cookie"/> is <see langword="null"/>.
		/// </exception>
		public void Add (Cookie cookie) 
		{
			if (cookie == null)
				throw new ArgumentNullException ("cookie");

			int pos = SearchCookie (cookie);
			if (pos == -1)
				list.Add (cookie);
			else
				list [pos] = cookie;
		}

		/// <summary>
		/// Add the elements of the specified <see cref="CookieCollection"/> to the current <see cref="CookieCollection"/>.
		/// </summary>
		/// <param name="cookies">
		/// A <see cref="CookieCollection"/> to add to the current <see cref="CookieCollection"/>.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="cookies"/> is <see langword="null"/>.
		/// </exception>
		public void Add (CookieCollection cookies) 
		{
			if (cookies == null)
				throw new ArgumentNullException ("cookies");

			foreach (Cookie cookie in cookies)
				Add (cookie);
		}

		/// <summary>
		/// Copies the elements of the <see cref="CookieCollection"/> to the specified <see cref="Array"/>,
		/// starting at the specified <paramref name="index"/> in the <paramref name="array"/>.
		/// </summary>
		/// <param name="array">
		/// An <see cref="Array"/> is the destination of the elements copied from the <see cref="CookieCollection"/>.
		/// </param>
		/// <param name="index">
		/// An <see cref="int"/> that indicates the zero-based index in <paramref name="array"/> at which copying begins.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="array"/> is <see langword="null"/>.
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <paramref name="index"/> is less than zero.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <para>
		/// <paramref name="array"/> is multidimensional.
		/// </para>
		/// <para>
		/// -or-
		/// </para>
		/// <para>
		/// The number of elements in the <see cref="CookieCollection"/> is greater than the available space
		/// from index to the end of the destination <paramref name="array"/>.
		/// </para>
		/// </exception>
		/// <exception cref="InvalidCastException">
		/// The elements in the <see cref="CookieCollection"/> cannot be cast automatically
		/// to the type of the destination <paramref name="array"/>.
		/// </exception>
		public void CopyTo (Array array, int index)
		{
			if (array == null)
				throw new ArgumentNullException ("array");

			if (index < 0)
				throw new ArgumentOutOfRangeException ("index", "Must not be less than zero.");

			if (array.Rank > 1)
				throw new ArgumentException ("Must not be multidimensional.", "array");

			if (array.Length - index < list.Count)
				throw new ArgumentException (
					"The number of elements in this collection is greater than the available space of the destination array.");

			if (!array.GetType ().GetElementType ().IsAssignableFrom (typeof (Cookie)))
				throw new InvalidCastException (
					"The elements in this collection cannot be cast automatically to the type of the destination array.");

			(list as IList).CopyTo (array, index);
		}

		/// <summary>
		/// Copies the elements of the <see cref="CookieCollection"/> to the specified array of <see cref="Cookie"/>,
		/// starting at the specified <paramref name="index"/> in the <paramref name="array"/>.
		/// </summary>
		/// <param name="array">
		/// An array of <see cref="Cookie"/> is the destination of the elements copied from the <see cref="CookieCollection"/>.
		/// </param>
		/// <param name="index">
		/// An <see cref="int"/> that indicates the zero-based index in <paramref name="array"/> at which copying begins.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="array"/> is <see langword="null"/>.
		/// </exception>
		/// <exception cref="ArgumentOutOfRangeException">
		/// <paramref name="index"/> is less than zero.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// The number of elements in the <see cref="CookieCollection"/> is greater than the available space
		/// from index to the end of the destination <paramref name="array"/>.
		/// </exception>
		public void CopyTo (Cookie [] array, int index)
		{
			if (array == null)
				throw new ArgumentNullException ("array");

			if (index < 0)
				throw new ArgumentOutOfRangeException ("index", "Must not be less than zero.");

			if (array.Length - index < list.Count)
				throw new ArgumentException (
					"The number of elements in this collection is greater than the available space of the destination array.");

			list.CopyTo (array, index);
		}

		/// <summary>
		/// Gets the enumerator to use to iterate through the <see cref="CookieCollection"/>.
		/// </summary>
		/// <returns>
		/// An instance of an implementation of the <see cref="IEnumerator"/> interface
		/// to use to iterate through the <see cref="CookieCollection"/>.
		/// </returns>
		public IEnumerator GetEnumerator ()
		{
			return list.GetEnumerator ();
		}

		#endregion
	}
}
