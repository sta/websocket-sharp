#region License
/*
 * CookieCollection.cs
 *
 * This code is derived from System.Net.CookieCollection.cs of Mono
 * (http://www.mono-project.com).
 *
 * The MIT License
 *
 * Copyright (c) 2004,2009 Novell, Inc. (http://www.novell.com)
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
 * - Sebastien Pouliot <sebastien@ximian.com>
 */
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace WebSocketSharp.Net
{
  /// <summary>
  /// Provides a collection container for instances of the <see cref="Cookie"/> class.
  /// </summary>
  [Serializable]
  public class CookieCollection : ICollection, IEnumerable
  {
    #region Private Fields

    private List<Cookie> _list;
    private object       _sync;

    #endregion

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="CookieCollection"/> class.
    /// </summary>
    public CookieCollection ()
    {
      _list = new List<Cookie> ();
    }

    #endregion

    #region Internal Properties

    internal IList<Cookie> List {
      get {
        return _list;
      }
    }

    internal IEnumerable<Cookie> Sorted {
      get {
        var list = new List<Cookie> (_list);
        if (list.Count > 1)
          list.Sort (compareCookieWithinSorted);

        return list;
      }
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the number of cookies in the collection.
    /// </summary>
    /// <value>
    /// An <see cref="int"/> that represents the number of cookies in the collection.
    /// </value>
    public int Count {
      get {
        return _list.Count;
      }
    }

    /// <summary>
    /// Gets a value indicating whether the collection is read-only.
    /// </summary>
    /// <value>
    /// <c>true</c> if the collection is read-only; otherwise, <c>false</c>.
    /// The default value is <c>true</c>.
    /// </value>
    public bool IsReadOnly {
      // LAMESPEC: So how is one supposed to create a writable CookieCollection instance?
      // We simply ignore this property, as this collection is always writable.
      get {
        return true;
      }
    }

    /// <summary>
    /// Gets a value indicating whether the access to the collection is thread safe.
    /// </summary>
    /// <value>
    /// <c>true</c> if the access to the collection is thread safe; otherwise, <c>false</c>.
    /// The default value is <c>false</c>.
    /// </value>
    public bool IsSynchronized {
      get {
        return false;
      }
    }

    /// <summary>
    /// Gets the <see cref="Cookie"/> at the specified <paramref name="index"/> from
    /// the collection.
    /// </summary>
    /// <value>
    /// A <see cref="Cookie"/> at the specified <paramref name="index"/> in the collection.
    /// </value>
    /// <param name="index">
    /// An <see cref="int"/> that represents the zero-based index of the <see cref="Cookie"/>
    /// to find.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is out of allowable range of indexes for the collection.
    /// </exception>
    public Cookie this[int index] {
      get {
        if (index < 0 || index >= _list.Count)
          throw new ArgumentOutOfRangeException ("index");

        return _list[index];
      }
    }

    /// <summary>
    /// Gets the <see cref="Cookie"/> with the specified <paramref name="name"/> from
    /// the collection.
    /// </summary>
    /// <value>
    /// A <see cref="Cookie"/> with the specified <paramref name="name"/> in the collection.
    /// </value>
    /// <param name="name">
    /// A <see cref="string"/> that represents the name of the <see cref="Cookie"/> to find.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> is <see langword="null"/>.
    /// </exception>
    public Cookie this[string name] {
      get {
        if (name == null)
          throw new ArgumentNullException ("name");

        foreach (var cookie in Sorted)
          if (cookie.Name.Equals (name, StringComparison.InvariantCultureIgnoreCase))
            return cookie;

        return null;
      }
    }

    /// <summary>
    /// Gets an object used to synchronize access to the collection.
    /// </summary>
    /// <value>
    /// An <see cref="Object"/> used to synchronize access to the collection.
    /// </value>
    public Object SyncRoot {
      get {
        return _sync ?? (_sync = ((ICollection) _list).SyncRoot);
      }
    }

    #endregion

    #region Private Methods

    private static int compareCookieWithinSort (Cookie x, Cookie y)
    {
      return (x.Name.Length + x.Value.Length) - (y.Name.Length + y.Value.Length);
    }

    private static int compareCookieWithinSorted (Cookie x, Cookie y)
    {
      var ret = 0;
      return (ret = x.Version - y.Version) != 0
             ? ret
             : (ret = x.Name.CompareTo (y.Name)) != 0
               ? ret
               : y.Path.Length - x.Path.Length;
    }

    private static CookieCollection parseRequest (string value)
    {
      var cookies = new CookieCollection ();

      Cookie cookie = null;
      var ver = 0;
      var pairs = splitCookieHeaderValue (value);
      for (var i = 0; i < pairs.Length; i++) {
        var pair = pairs[i].Trim ();
        if (pair.Length == 0)
          continue;

        if (pair.StartsWith ("$version", StringComparison.InvariantCultureIgnoreCase)) {
          ver = Int32.Parse (pair.GetValue ('=', true));
        }
        else if (pair.StartsWith ("$path", StringComparison.InvariantCultureIgnoreCase)) {
          if (cookie != null)
            cookie.Path = pair.GetValue ('=');
        }
        else if (pair.StartsWith ("$domain", StringComparison.InvariantCultureIgnoreCase)) {
          if (cookie != null)
            cookie.Domain = pair.GetValue ('=');
        }
        else if (pair.StartsWith ("$port", StringComparison.InvariantCultureIgnoreCase)) {
          var port = pair.Equals ("$port", StringComparison.InvariantCultureIgnoreCase)
                     ? "\"\""
                     : pair.GetValue ('=');

          if (cookie != null)
            cookie.Port = port;
        }
        else {
          if (cookie != null)
            cookies.Add (cookie);

          string name;
          string val = String.Empty;

          var pos = pair.IndexOf ('=');
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
          if (ver != 0)
            cookie.Version = ver;
        }
      }

      if (cookie != null)
        cookies.Add (cookie);

      return cookies;
    }

    private static CookieCollection parseResponse (string value)
    {
      var cookies = new CookieCollection ();

      Cookie cookie = null;
      var pairs = splitCookieHeaderValue (value);
      for (var i = 0; i < pairs.Length; i++) {
        var pair = pairs[i].Trim ();
        if (pair.Length == 0)
          continue;

        if (pair.StartsWith ("version", StringComparison.InvariantCultureIgnoreCase)) {
          if (cookie != null)
            cookie.Version = Int32.Parse (pair.GetValue ('=', true));
        }
        else if (pair.StartsWith ("expires", StringComparison.InvariantCultureIgnoreCase)) {
          var buff = new StringBuilder (pair.GetValue ('='), 32);
          if (i < pairs.Length - 1)
            buff.AppendFormat (", {0}", pairs[++i].Trim ());

          DateTime expires;
          if (!DateTime.TryParseExact (
            buff.ToString (),
            new[] { "ddd, dd'-'MMM'-'yyyy HH':'mm':'ss 'GMT'", "r" },
            CultureInfo.CreateSpecificCulture ("en-US"),
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out expires))
            expires = DateTime.Now;

          if (cookie != null && cookie.Expires == DateTime.MinValue)
            cookie.Expires = expires.ToLocalTime ();
        }
        else if (pair.StartsWith ("max-age", StringComparison.InvariantCultureIgnoreCase)) {
          var max = Int32.Parse (pair.GetValue ('=', true));
          var expires = DateTime.Now.AddSeconds ((double) max);
          if (cookie != null)
            cookie.Expires = expires;
        }
        else if (pair.StartsWith ("path", StringComparison.InvariantCultureIgnoreCase)) {
          if (cookie != null)
            cookie.Path = pair.GetValue ('=');
        }
        else if (pair.StartsWith ("domain", StringComparison.InvariantCultureIgnoreCase)) {
          if (cookie != null)
            cookie.Domain = pair.GetValue ('=');
        }
        else if (pair.StartsWith ("port", StringComparison.InvariantCultureIgnoreCase)) {
          var port = pair.Equals ("port", StringComparison.InvariantCultureIgnoreCase)
                     ? "\"\""
                     : pair.GetValue ('=');

          if (cookie != null)
            cookie.Port = port;
        }
        else if (pair.StartsWith ("comment", StringComparison.InvariantCultureIgnoreCase)) {
          if (cookie != null)
            cookie.Comment = pair.GetValue ('=').UrlDecode ();
        }
        else if (pair.StartsWith ("commenturl", StringComparison.InvariantCultureIgnoreCase)) {
          if (cookie != null)
            cookie.CommentUri = pair.GetValue ('=', true).ToUri ();
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

          var pos = pair.IndexOf ('=');
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

    private int searchCookie (Cookie cookie)
    {
      var name = cookie.Name;
      var path = cookie.Path;
      var domain = cookie.Domain;
      var ver = cookie.Version;

      for (var i = _list.Count - 1; i >= 0; i--) {
        var c = _list[i];
        if (c.Name.Equals (name, StringComparison.InvariantCultureIgnoreCase) &&
            c.Path.Equals (path, StringComparison.InvariantCulture) &&
            c.Domain.Equals (domain, StringComparison.InvariantCultureIgnoreCase) &&
            c.Version == ver)
          return i;
      }

      return -1;
    }

    private static string[] splitCookieHeaderValue (string value)
    {
      return new List<string> (value.SplitHeaderValue (',', ';')).ToArray ();
    }

    #endregion

    #region Internal Methods

    internal static CookieCollection Parse (string value, bool response)
    {
      return response
             ? parseResponse (value)
             : parseRequest (value);
    }

    internal void SetOrRemove (Cookie cookie)
    {
      var pos = searchCookie (cookie);
      if (pos == -1) {
        if (!cookie.Expired)
          _list.Add (cookie);

        return;
      }

      if (!cookie.Expired) {
        _list[pos] = cookie;
        return;
      }

      _list.RemoveAt (pos);
    }

    internal void SetOrRemove (CookieCollection cookies)
    {
      foreach (Cookie cookie in cookies)
        SetOrRemove (cookie);
    }

    internal void Sort ()
    {
      if (_list.Count > 1)
        _list.Sort (compareCookieWithinSort);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Adds the specified <paramref name="cookie"/> to the collection.
    /// </summary>
    /// <param name="cookie">
    /// A <see cref="Cookie"/> to add.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="cookie"/> is <see langword="null"/>.
    /// </exception>
    public void Add (Cookie cookie) 
    {
      if (cookie == null)
        throw new ArgumentNullException ("cookie");

      var pos = searchCookie (cookie);
      if (pos == -1) {
        _list.Add (cookie);
        return;
      }

      _list[pos] = cookie;
    }

    /// <summary>
    /// Adds the specified <paramref name="cookies"/> to the collection.
    /// </summary>
    /// <param name="cookies">
    /// A <see cref="CookieCollection"/> that contains the cookies to add.
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
    /// Copies the elements of the collection to the specified <see cref="Array"/>, starting at
    /// the specified <paramref name="index"/> in the <paramref name="array"/>.
    /// </summary>
    /// <param name="array">
    /// An <see cref="Array"/> that represents the destination of the elements copied from
    /// the collection.
    /// </param>
    /// <param name="index">
    /// An <see cref="int"/> that represents the zero-based index in <paramref name="array"/>
    /// at which copying begins.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="array"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is less than zero.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="array"/> is multidimensional.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   The number of elements in the collection is greater than the available space from
    ///   <paramref name="index"/> to the end of the destination <paramref name="array"/>.
    ///   </para>
    /// </exception>
    /// <exception cref="InvalidCastException">
    /// The elements in the collection cannot be cast automatically to the type of the destination
    /// <paramref name="array"/>.
    /// </exception>
    public void CopyTo (Array array, int index)
    {
      if (array == null)
        throw new ArgumentNullException ("array");

      if (index < 0)
        throw new ArgumentOutOfRangeException ("index", "Less than zero.");

      if (array.Rank > 1)
        throw new ArgumentException ("Multidimensional.", "array");

      if (array.Length - index < _list.Count)
        throw new ArgumentException (
          "The number of elements in this collection is greater than the available space of the destination array.");

      if (!array.GetType ().GetElementType ().IsAssignableFrom (typeof (Cookie)))
        throw new InvalidCastException (
          "The elements in this collection cannot be cast automatically to the type of the destination array.");

      ((IList) _list).CopyTo (array, index);
    }

    /// <summary>
    /// Copies the elements of the collection to the specified array of <see cref="Cookie"/>,
    /// starting at the specified <paramref name="index"/> in the <paramref name="array"/>.
    /// </summary>
    /// <param name="array">
    /// An array of <see cref="Cookie"/> that represents the destination of the elements
    /// copied from the collection.
    /// </param>
    /// <param name="index">
    /// An <see cref="int"/> that represents the zero-based index in <paramref name="array"/>
    /// at which copying begins.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="array"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="index"/> is less than zero.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The number of elements in the collection is greater than the available space from
    /// <paramref name="index"/> to the end of the destination <paramref name="array"/>.
    /// </exception>
    public void CopyTo (Cookie[] array, int index)
    {
      if (array == null)
        throw new ArgumentNullException ("array");

      if (index < 0)
        throw new ArgumentOutOfRangeException ("index", "Less than zero.");

      if (array.Length - index < _list.Count)
        throw new ArgumentException (
          "The number of elements in this collection is greater than the available space of the destination array.");

      _list.CopyTo (array, index);
    }

    /// <summary>
    /// Gets the enumerator used to iterate through the collection.
    /// </summary>
    /// <returns>
    /// An <see cref="IEnumerator"/> instance used to iterate through the collection.
    /// </returns>
    public IEnumerator GetEnumerator ()
    {
      return _list.GetEnumerator ();
    }

    #endregion
  }
}
