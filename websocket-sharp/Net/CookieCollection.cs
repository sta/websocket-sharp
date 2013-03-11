//
// CookieCollection.cs
//	Copied from System.Net.CookieCollection.cs
//
// Authors:
//	Lawrence Pit (loz@cable.a2000.nl)
//	Gonzalo Paniagua Javier (gonzalo@ximian.com)
//	Sebastien Pouliot <sebastien@ximian.com>
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
using System.Runtime.Serialization;

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

		#region Static Field

		static CookieCollectionComparer Comparer = new CookieCollectionComparer ();

		#endregion

		#region Field

		List<Cookie> list;
		object       sync;

		#endregion

		#region Constructor

		/// <summary>
		/// Initializes a new instance of the <see cref="CookieCollection"/> class.
		/// </summary>
		public CookieCollection ()
		{
			list = new List<Cookie> ();
		}

		#endregion

		#region Internal Property

		internal IList<Cookie> List {
			get { return list; }
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
		/// An <see cref="int"/> that is the zero-based index of the <see cref="Cookie"/> to find.
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
		/// A <see cref="string"/> that is the name of the <see cref="Cookie"/> to find.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="name"/> is <see langword="null"/>.
		/// </exception>
		public Cookie this [string name] {
			get {
				if (name.IsNull ())
					throw new ArgumentNullException ("name");

				foreach (var cookie in list) {
					if (0 == String.Compare (cookie.Name, name, true, CultureInfo.InvariantCulture))
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
				if (sync.IsNull ())
					sync = new object ();

				return sync;
			}
		}

		#endregion

		#region Private Method

		int SearchCookie (Cookie cookie)
		{
			string name = cookie.Name;
			string domain = cookie.Domain;
			string path = cookie.Path;

			for (int i = list.Count - 1; i >= 0; i--) {
				Cookie c = list [i];
				if (0 != String.Compare (name, c.Name, true, CultureInfo.InvariantCulture))
					continue;

				if (0 != String.Compare (domain, c.Domain, true, CultureInfo.InvariantCulture))
					continue;

				if (0 != String.Compare (path, c.Path, false, CultureInfo.InvariantCulture))
					continue;

				if (c.Version != cookie.Version)
					continue;

				return i;
			}

			return -1;
		}

		#endregion

		#region Internal Method

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
			if (cookie.IsNull ())
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
			if (cookies.IsNull ())
				throw new ArgumentNullException ("cookies");

			foreach (Cookie cookie in cookies)
				Add (cookie);
		}

		/// <summary>
		/// Copies the elements of the <see cref="CookieCollection"/> to the specified <see cref="Array"/>,
		/// starting at the specified <paramref name="index"/> in the <paramref name="array"/>.
		/// </summary>
		/// <param name="array">
		/// An <see cref="Array"/> that is the destination of the elements copied from the <see cref="CookieCollection"/>.
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
		public void CopyTo (Array array, int index)
		{
			if (array.IsNull ())
				throw new ArgumentNullException ("array");

			if (index < 0)
				throw new ArgumentOutOfRangeException ("index", "Must not be less than zero.");

			// TODO: Support for ArgumentException and InvalidCastException.

			(list as IList).CopyTo (array, index);
		}

		/// <summary>
		/// Copies the elements of the <see cref="CookieCollection"/> to the specified array of <see cref="Cookie"/>,
		/// starting at the specified <paramref name="index"/> in the <paramref name="array"/>.
		/// </summary>
		/// <param name="array">
		/// An array of <see cref="Cookie"/> that is the destination of the elements copied from the <see cref="CookieCollection"/>.
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
		public void CopyTo (Cookie [] array, int index)
		{
			if (array.IsNull ())
				throw new ArgumentNullException ("array");

			if (index < 0)
				throw new ArgumentOutOfRangeException ("index", "Must not be less than zero.");

			// TODO: Support for ArgumentException.

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
