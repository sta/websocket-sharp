//
// HttpListenerPrefixCollection.cs
//	Copied from System.Net.HttpListenerPrefixCollection.cs
//
// Author:
//	Gonzalo Paniagua Javier (gonzalo@novell.com)
//
// Copyright (c) 2005 Novell, Inc. (http://www.novell.com)
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

namespace WebSocketSharp.Net
{
	/// <summary>
	/// Provides the collection used to store the URI prefixes for the <see cref="HttpListener"/>.
	/// </summary>
	public class HttpListenerPrefixCollection : ICollection<string>, IEnumerable<string>, IEnumerable
	{
		#region Private Fields

		private HttpListener _listener;
		private List<string> _prefixes;

		#endregion

		#region Private Constructors

		private HttpListenerPrefixCollection ()
		{
			_prefixes = new List<string> ();
		}

		#endregion

		#region Internal Constructors

		internal HttpListenerPrefixCollection (HttpListener listener)
			: this ()
		{
			_listener = listener;
		}

		#endregion

		#region Public Properties

		/// <summary>
		/// Gets the number of prefixes contained in the <see cref="HttpListenerPrefixCollection"/>.
		/// </summary>
		/// <value>
		/// A <see cref="int"/> that contains the number of prefixes.
		/// </value>
		public int Count {
			get {
				return _prefixes.Count;
			}
		}

		/// <summary>
		/// Gets a value indicating whether access to the <see cref="HttpListenerPrefixCollection"/>
		/// is read-only.
		/// </summary>
		/// <value>
		/// Always returns <c>false</c>.
		/// </value>
		public bool IsReadOnly {
			get {
				return false;
			}
		}

		/// <summary>
		/// Gets a value indicating whether access to the <see cref="HttpListenerPrefixCollection"/>
		/// is synchronized.
		/// </summary>
		/// <value>
		/// Always returns <c>false</c>.
		/// </value>
		public bool IsSynchronized {
			get {
				return false;
			}
		}

		#endregion

		#region Public Methods

		/// <summary>
		/// Adds the specified <paramref name="uriPrefix"/> to the <see cref="HttpListenerPrefixCollection"/>.
		/// </summary>
		/// <param name="uriPrefix">
		/// A <see cref="string"/> that contains a URI prefix to add.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="uriPrefix"/> is <see langword="null"/>.
		/// </exception>
		/// <exception cref="ArgumentException">
		/// <paramref name="uriPrefix"/> is invalid.
		/// </exception>
		/// <exception cref="ObjectDisposedException">
		/// The <see cref="HttpListener"/> associated with this <see cref="HttpListenerPrefixCollection"/>
		/// is closed.
		/// </exception>
		public void Add (string uriPrefix)
		{
			_listener.CheckDisposed ();
			ListenerPrefix.CheckUriPrefix (uriPrefix);
			if (_prefixes.Contains (uriPrefix))
				return;

			_prefixes.Add (uriPrefix);
			if (_listener.IsListening)
				EndPointManager.AddPrefix (uriPrefix, _listener);
		}

		/// <summary>
		/// Removes all URI prefixes from the <see cref="HttpListenerPrefixCollection"/>.
		/// </summary>
		/// <exception cref="ObjectDisposedException">
		/// The <see cref="HttpListener"/> associated with this <see cref="HttpListenerPrefixCollection"/>
		/// is closed.
		/// </exception>
		public void Clear ()
		{
			_listener.CheckDisposed ();
			_prefixes.Clear ();
			if (_listener.IsListening)
				EndPointManager.RemoveListener (_listener);
		}

		/// <summary>
		/// Returns a value indicating whether the <see cref="HttpListenerPrefixCollection"/> contains
		/// the specified <paramref name="uriPrefix"/>.
		/// </summary>
		/// <returns>
		/// <c>true</c> if the <see cref="HttpListenerPrefixCollection"/> contains <paramref name="uriPrefix"/>;
		/// otherwise, <c>false</c>.
		/// </returns>
		/// <param name="uriPrefix">
		/// A <see cref="string"/> that contains a URI prefix to test.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="uriPrefix"/> is <see langword="null"/>.
		/// </exception>
		/// <exception cref="ObjectDisposedException">
		/// The <see cref="HttpListener"/> associated with this <see cref="HttpListenerPrefixCollection"/>
		/// is closed.
		/// </exception>
		public bool Contains (string uriPrefix)
		{
			_listener.CheckDisposed ();
			if (uriPrefix == null)
				throw new ArgumentNullException ("uriPrefix");

			return _prefixes.Contains (uriPrefix);
		}

		/// <summary>
		/// Copies the contents of the <see cref="HttpListenerPrefixCollection"/> to
		/// the specified <see cref="Array"/>.
		/// </summary>
		/// <param name="array">
		/// An <see cref="Array"/> that receives the URI prefix strings
		/// in the <see cref="HttpListenerPrefixCollection"/>.
		/// </param>
		/// <param name="offset">
		/// An <see cref="int"/> that contains the zero-based index in <paramref name="array"/>
		/// at which copying begins.
		/// </param>
		/// <exception cref="ObjectDisposedException">
		/// The <see cref="HttpListener"/> associated with this <see cref="HttpListenerPrefixCollection"/>
		/// is closed.
		/// </exception>
		public void CopyTo (Array array, int offset)
		{
			_listener.CheckDisposed ();
			((ICollection) _prefixes).CopyTo (array, offset);
		}

		/// <summary>
		/// Copies the contents of the <see cref="HttpListenerPrefixCollection"/> to
		/// the specified array of <see cref="string"/>.
		/// </summary>
		/// <param name="array">
		/// An array of <see cref="string"/> that receives the URI prefix strings
		/// in the <see cref="HttpListenerPrefixCollection"/>.
		/// </param>
		/// <param name="offset">
		/// An <see cref="int"/> that contains the zero-based index in <paramref name="array"/>
		/// at which copying begins.
		/// </param>
		/// <exception cref="ObjectDisposedException">
		/// The <see cref="HttpListener"/> associated with this <see cref="HttpListenerPrefixCollection"/>
		/// is closed.
		/// </exception>
		public void CopyTo (string [] array, int offset)
		{
			_listener.CheckDisposed ();
			_prefixes.CopyTo (array, offset);
		}

		/// <summary>
		/// Gets an object that can be used to iterate through the <see cref="HttpListenerPrefixCollection"/>.
		/// </summary>
		/// <returns>
		/// An object that implements the IEnumerator&lt;string&gt; interface and provides access to
		/// the URI prefix strings in the <see cref="HttpListenerPrefixCollection"/>.
		/// </returns>
		public IEnumerator<string> GetEnumerator ()
		{
			return _prefixes.GetEnumerator ();
		}

		/// <summary>
		/// Removes the specified <paramref name="uriPrefix"/> from the list of prefixes
		/// in the <see cref="HttpListenerPrefixCollection"/>.
		/// </summary>
		/// <returns>
		/// <c>true</c> if <paramref name="uriPrefix"/> is successfully found and removed;
		/// otherwise, <c>false</c>.
		/// </returns>
		/// <param name="uriPrefix">
		/// A <see cref="string"/> that contains a URI prefix to remove.
		/// </param>
		/// <exception cref="ArgumentNullException">
		/// <paramref name="uriPrefix"/> is <see langword="null"/>.
		/// </exception>
		/// <exception cref="ObjectDisposedException">
		/// The <see cref="HttpListener"/> associated with this <see cref="HttpListenerPrefixCollection"/>
		/// is closed.
		/// </exception>
		public bool Remove (string uriPrefix)
		{
			_listener.CheckDisposed ();
			if (uriPrefix == null)
				throw new ArgumentNullException ("uriPrefix");

			var result = _prefixes.Remove (uriPrefix);
			if (result && _listener.IsListening)
				EndPointManager.RemovePrefix (uriPrefix, _listener);

			return result;
		}

		#endregion

		#region Explicit Interface Implementation

		/// <summary>
		/// Gets an object that can be used to iterate through the <see cref="HttpListenerPrefixCollection"/>.
		/// </summary>
		/// <returns>
		/// An object that implements the <see cref="IEnumerator"/> interface and provides access to
		/// the URI prefix strings in the <see cref="HttpListenerPrefixCollection"/>.
		/// </returns>
		IEnumerator IEnumerable.GetEnumerator ()
		{
			return _prefixes.GetEnumerator ();
		}

		#endregion

	}
}
