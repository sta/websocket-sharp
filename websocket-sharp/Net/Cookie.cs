#region License
/*
 * Cookie.cs
 *
 * This code is derived from System.Net.Cookie.cs of Mono
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
 * - Daniel Nauck <dna@mono-project.de>
 * - Sebastien Pouliot <sebastien@ximian.com>
 */
#endregion

using System;
using System.Globalization;
using System.Text;

namespace WebSocketSharp.Net
{
  /// <summary>
  /// Provides a set of methods and properties used to manage an HTTP Cookie.
  /// </summary>
  /// <remarks>
  ///   <para>
  ///   The Cookie class supports the following cookie formats:
  ///   <see href="http://web.archive.org/web/20020803110822/http://wp.netscape.com/newsref/std/cookie_spec.html">Netscape specification</see>,
  ///   <see href="http://www.ietf.org/rfc/rfc2109.txt">RFC 2109</see>, and
  ///   <see href="http://www.ietf.org/rfc/rfc2965.txt">RFC 2965</see>
  ///   </para>
  ///   <para>
  ///   The Cookie class cannot be inherited.
  ///   </para>
  /// </remarks>
  [Serializable]
  public sealed class Cookie
  {
    #region Private Fields

    private string                 _comment;
    private Uri                    _commentUri;
    private bool                   _discard;
    private string                 _domain;
    private static readonly int[]  _emptyPorts;
    private DateTime               _expires;
    private bool                   _httpOnly;
    private string                 _name;
    private string                 _path;
    private string                 _port;
    private int[]                  _ports;
    private static readonly char[] _reservedCharsForName;
    private static readonly char[] _reservedCharsForValue;
    private bool                   _secure;
    private DateTime               _timeStamp;
    private string                 _value;
    private int                    _version;

    #endregion

    #region Static Constructor

    static Cookie ()
    {
      _emptyPorts = new int[0];
      _reservedCharsForName = new[] { ' ', '=', ';', ',', '\n', '\r', '\t' };
      _reservedCharsForValue = new[] { ';', ',' };
    }

    #endregion

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="Cookie"/> class.
    /// </summary>
    public Cookie ()
    {
      _domain = String.Empty;
      _expires = DateTime.MinValue;
      _name = String.Empty;
      _path = String.Empty;
      _timeStamp = DateTime.Now;
      _value = String.Empty;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Cookie"/> class with the specified
    /// <paramref name="name"/> and <paramref name="value"/>.
    /// </summary>
    /// <param name="name">
    /// A <see cref="string"/> that represents the Name of the cookie.
    /// </param>
    /// <param name="value">
    /// A <see cref="string"/> that represents the Value of the cookie.
    /// </param>
    /// <exception cref="CookieException">
    ///   <para>
    ///   <paramref name="name"/> is <see langword="null"/> or empty.
    ///   </para>
    ///   <para>
    ///   - or -
    ///   </para>
    ///   <para>
    ///   <paramref name="name"/> contains an invalid character.
    ///   </para>
    ///   <para>
    ///   - or -
    ///   </para>
    ///   <para>
    ///   <paramref name="value"/> is <see langword="null"/>.
    ///   </para>
    ///   <para>
    ///   - or -
    ///   </para>
    ///   <para>
    ///   <paramref name="value"/> contains a string not enclosed in double quotes
    ///   that contains an invalid character.
    ///   </para>
    /// </exception>
    public Cookie (string name, string value)
      : this ()
    {
      Name = name;
      Value = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Cookie"/> class with the specified
    /// <paramref name="name"/>, <paramref name="value"/>, and <paramref name="path"/>.
    /// </summary>
    /// <param name="name">
    /// A <see cref="string"/> that represents the Name of the cookie.
    /// </param>
    /// <param name="value">
    /// A <see cref="string"/> that represents the Value of the cookie.
    /// </param>
    /// <param name="path">
    /// A <see cref="string"/> that represents the value of the Path attribute of the cookie.
    /// </param>
    /// <exception cref="CookieException">
    ///   <para>
    ///   <paramref name="name"/> is <see langword="null"/> or empty.
    ///   </para>
    ///   <para>
    ///   - or -
    ///   </para>
    ///   <para>
    ///   <paramref name="name"/> contains an invalid character.
    ///   </para>
    ///   <para>
    ///   - or -
    ///   </para>
    ///   <para>
    ///   <paramref name="value"/> is <see langword="null"/>.
    ///   </para>
    ///   <para>
    ///   - or -
    ///   </para>
    ///   <para>
    ///   <paramref name="value"/> contains a string not enclosed in double quotes
    ///   that contains an invalid character.
    ///   </para>
    /// </exception>
    public Cookie (string name, string value, string path)
      : this (name, value)
    {
      Path = path;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Cookie"/> class with the specified
    /// <paramref name="name"/>, <paramref name="value"/>, <paramref name="path"/>, and
    /// <paramref name="domain"/>.
    /// </summary>
    /// <param name="name">
    /// A <see cref="string"/> that represents the Name of the cookie.
    /// </param>
    /// <param name="value">
    /// A <see cref="string"/> that represents the Value of the cookie.
    /// </param>
    /// <param name="path">
    /// A <see cref="string"/> that represents the value of the Path attribute of the cookie.
    /// </param>
    /// <param name="domain">
    /// A <see cref="string"/> that represents the value of the Domain attribute of the cookie.
    /// </param>
    /// <exception cref="CookieException">
    ///   <para>
    ///   <paramref name="name"/> is <see langword="null"/> or empty.
    ///   </para>
    ///   <para>
    ///   - or -
    ///   </para>
    ///   <para>
    ///   <paramref name="name"/> contains an invalid character.
    ///   </para>
    ///   <para>
    ///   - or -
    ///   </para>
    ///   <para>
    ///   <paramref name="value"/> is <see langword="null"/>.
    ///   </para>
    ///   <para>
    ///   - or -
    ///   </para>
    ///   <para>
    ///   <paramref name="value"/> contains a string not enclosed in double quotes
    ///   that contains an invalid character.
    ///   </para>
    /// </exception>
    public Cookie (string name, string value, string path, string domain)
    {
      if (name == null)
        throw new ArgumentNullException ("name");

      if (value == null)
        throw new ArgumentNullException ("value");

      if (name.Length == 0)
        throw new ArgumentException ("An empty string.", "name");

      if (name[0] == '$' || name.Contains (_reservedCharsForName)) {
        var msg = "It contains an invalid character.";
        throw new ArgumentException (msg, "name");
      }

      if (value.Contains (_reservedCharsForValue)) {
        if (!value.IsEnclosedIn ('"')) {
          var msg = "It contains an invalid character.";
          throw new ArgumentException (msg, "value");
        }
      }

      init (name, value, path ?? String.Empty, domain ?? String.Empty);
    }

    #endregion

    #region Internal Properties

    internal bool ExactDomain {
      get {
        return _domain.Length == 0 || _domain[0] != '.';
      }
    }

    internal int MaxAge {
      get {
        if (_expires == DateTime.MinValue)
          return 0;

        var expires = _expires.Kind != DateTimeKind.Local
                      ? _expires.ToLocalTime ()
                      : _expires;

        var span = expires - DateTime.Now;
        return span > TimeSpan.Zero
               ? (int) span.TotalSeconds
               : 0;
      }
    }

    internal int[] Ports {
      get {
        return _ports ?? _emptyPorts;
      }
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets or sets the value of the Comment attribute of the cookie.
    /// </summary>
    /// <value>
    ///   <para>
    ///   A <see cref="string"/> that represents the comment to document
    ///   intended use of the cookie.
    ///   </para>
    ///   <para>
    ///   An empty string if this attribute is not needed.
    ///   </para>
    ///   <para>
    ///   The default value is an empty string.
    ///   </para>
    /// </value>
    public string Comment {
      get {
        return _comment ?? String.Empty;
      }

      set {
        _comment = value;
      }
    }

    /// <summary>
    /// Gets or sets the value of the CommentURL attribute of the cookie.
    /// </summary>
    /// <value>
    ///   <para>
    ///   A <see cref="Uri"/> that represents the URI that provides
    ///   the comment to document intended use of the cookie.
    ///   </para>
    ///   <para>
    ///   <see langword="null"/> if this attribute is not needed.
    ///   </para>
    ///   <para>
    ///   The default value is <see langword="null"/>.
    ///   </para>
    /// </value>
    public Uri CommentUri {
      get {
        return _commentUri;
      }

      set {
        _commentUri = value;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the client discards the cookie
    /// unconditionally when the client terminates.
    /// </summary>
    /// <value>
    ///   <para>
    ///   <c>true</c> if the client discards the cookie unconditionally
    ///   when the client terminates; otherwise, <c>false</c>.
    ///   </para>
    ///   <para>
    ///   The default value is <c>false</c>.
    ///   </para>
    /// </value>
    public bool Discard {
      get {
        return _discard;
      }

      set {
        _discard = value;
      }
    }

    /// <summary>
    /// Gets or sets the value of the Domain attribute of the cookie.
    /// </summary>
    /// <value>
    ///   <para>
    ///   A <see cref="string"/> that represents the domain name that
    ///   the cookie is valid for.
    ///   </para>
    ///   <para>
    ///   An empty string if this attribute is not needed.
    ///   </para>
    /// </value>
    public string Domain {
      get {
        return _domain;
      }

      set {
        _domain = value ?? String.Empty;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the cookie has expired.
    /// </summary>
    /// <value>
    ///   <para>
    ///   <c>true</c> if the cookie has expired; otherwise, <c>false</c>.
    ///   </para>
    ///   <para>
    ///   The default value is <c>false</c>.
    ///   </para>
    /// </value>
    public bool Expired {
      get {
        return _expires != DateTime.MinValue && _expires <= DateTime.Now;
      }

      set {
        _expires = value ? DateTime.Now : DateTime.MinValue;
      }
    }

    /// <summary>
    /// Gets or sets the value of the Expires attribute of the cookie.
    /// </summary>
    /// <value>
    ///   <para>
    ///   A <see cref="DateTime"/> that represents the date and time that
    ///   the cookie expires on.
    ///   </para>
    ///   <para>
    ///   <see cref="DateTime.MinValue"/> if this attribute is not needed.
    ///   </para>
    ///   <para>
    ///   The default value is <see cref="DateTime.MinValue"/>.
    ///   </para>
    /// </value>
    public DateTime Expires {
      get {
        return _expires;
      }

      set {
        _expires = value;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether non-HTTP APIs can access
    /// the cookie.
    /// </summary>
    /// <value>
    ///   <para>
    ///   <c>true</c> if non-HTTP APIs cannot access the cookie; otherwise,
    ///   <c>false</c>.
    ///   </para>
    ///   <para>
    ///   The default value is <c>false</c>.
    ///   </para>
    /// </value>
    public bool HttpOnly {
      get {
        return _httpOnly;
      }

      set {
        _httpOnly = value;
      }
    }

    /// <summary>
    /// Gets or sets the Name of the cookie.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the Name of the cookie.
    /// </value>
    /// <exception cref="CookieException">
    ///   <para>
    ///   The value specified for a set operation is <see langword="null"/> or empty.
    ///   </para>
    ///   <para>
    ///   - or -
    ///   </para>
    ///   <para>
    ///   The value specified for a set operation contains an invalid character.
    ///   </para>
    /// </exception>
    public string Name {
      get {
        return _name;
      }

      set {
        string msg;
        if (!canSetName (value, out msg))
          throw new CookieException (msg);

        _name = value;
      }
    }

    /// <summary>
    /// Gets or sets the value of the Path attribute of the cookie.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the subset of URI on
    /// the origin server that the cookie applies to.
    /// </value>
    public string Path {
      get {
        return _path;
      }

      set {
        _path = value ?? String.Empty;
      }
    }

    /// <summary>
    /// Gets or sets the value of the Port attribute of the cookie.
    /// </summary>
    /// <value>
    ///   <para>
    ///   A <see cref="string"/> that represents the list of TCP ports
    ///   that the cookie applies to.
    ///   </para>
    ///   <para>
    ///   An empty string if this attribute is not needed.
    ///   </para>
    ///   <para>
    ///   The default value is an empty string.
    ///   </para>
    /// </value>
    /// <exception cref="CookieException">
    ///   <para>
    ///   The value specified for a set operation is not enclosed in
    ///   double quotes.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   The value specified for a set operation could not be parsed.
    ///   </para>
    /// </exception>
    public string Port {
      get {
        return _port ?? String.Empty;
      }

      set { 
        if (value.IsNullOrEmpty ()) {
          _port = value;
          _ports = null;

          return;
        }

        if (!value.IsEnclosedIn ('"')) {
          var msg = "The value is not enclosed in double quotes.";
          throw new CookieException (msg);
        }

        int[] ports;
        if (!tryCreatePorts (value, out ports)) {
          var msg = "The value could not be parsed.";
          throw new CookieException (msg);
        }

        _port = value;
        _ports = ports;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the security level of
    /// the cookie is secure.
    /// </summary>
    /// <remarks>
    /// When this property is <c>true</c>, the cookie may be included in
    /// the request only if the request is transmitted over HTTPS.
    /// </remarks>
    /// <value>
    ///   <para>
    ///   <c>true</c> if the security level of the cookie is secure;
    ///   otherwise, <c>false</c>.
    ///   </para>
    ///   <para>
    ///   The default value is <c>false</c>.
    ///   </para>
    /// </value>
    public bool Secure {
      get {
        return _secure;
      }

      set {
        _secure = value;
      }
    }

    /// <summary>
    /// Gets the time when the cookie was issued.
    /// </summary>
    /// <value>
    /// A <see cref="DateTime"/> that represents the time when
    /// the cookie was issued.
    /// </value>
    public DateTime TimeStamp {
      get {
        return _timeStamp;
      }
    }

    /// <summary>
    /// Gets or sets the Value of the cookie.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the Value of the cookie.
    /// </value>
    /// <exception cref="CookieException">
    ///   <para>
    ///   The value specified for a set operation is <see langword="null"/>.
    ///   </para>
    ///   <para>
    ///   - or -
    ///   </para>
    ///   <para>
    ///   The value specified for a set operation contains a string not enclosed in double quotes
    ///   that contains an invalid character.
    ///   </para>
    /// </exception>
    public string Value {
      get {
        return _value;
      }

      set {
        string msg;
        if (!canSetValue (value, out msg))
          throw new CookieException (msg);

        _value = value.Length > 0 ? value : "\"\"";
      }
    }

    /// <summary>
    /// Gets or sets the value of the Version attribute of the cookie.
    /// </summary>
    /// <value>
    ///   <para>
    ///   An <see cref="int"/> that represents the version of HTTP state
    ///   management that the cookie conforms to.
    ///   </para>
    ///   <para>
    ///   0 or 1.
    ///   </para>
    ///   <para>
    ///   The default value is 0.
    ///   </para>
    /// </value>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value specified for a set operation is not allowed.
    /// </exception>
    public int Version {
      get {
        return _version;
      }

      set {
        if (value < 0 || value > 1)
          throw new ArgumentOutOfRangeException ("value", "Not allowed.");

        _version = value;
      }
    }

    #endregion

    #region Private Methods

    private static bool canSetName (string name, out string message)
    {
      if (name.IsNullOrEmpty ()) {
        message = "The value specified for the Name is null or empty.";
        return false;
      }

      if (name[0] == '$' || name.Contains (_reservedCharsForName)) {
        message = "The value specified for the Name contains an invalid character.";
        return false;
      }

      message = String.Empty;
      return true;
    }

    private static bool canSetValue (string value, out string message)
    {
      if (value == null) {
        message = "The value specified for the Value is null.";
        return false;
      }

      if (value.Contains (_reservedCharsForValue) && !value.IsEnclosedIn ('"')) {
        message = "The value specified for the Value contains an invalid character.";
        return false;
      }

      message = String.Empty;
      return true;
    }

    private static int hash (int i, int j, int k, int l, int m)
    {
      return i
             ^ (j << 13 | j >> 19)
             ^ (k << 26 | k >>  6)
             ^ (l <<  7 | l >> 25)
             ^ (m << 20 | m >> 12);
    }

    private void init (string name, string value, string path, string domain)
    {
      _name = name;
      _value = value;
      _path = path;
      _domain = domain;

      _expires = DateTime.MinValue;
      _timeStamp = DateTime.Now;
    }

    private string toResponseStringVersion0 ()
    {
      var buff = new StringBuilder (64);

      buff.AppendFormat ("{0}={1}", _name, _value);

      if (_expires != DateTime.MinValue) {
        buff.AppendFormat (
          "; Expires={0}",
          _expires.ToUniversalTime ().ToString (
            "ddd, dd'-'MMM'-'yyyy HH':'mm':'ss 'GMT'",
            CultureInfo.CreateSpecificCulture ("en-US")
          )
        );
      }

      if (!_path.IsNullOrEmpty ())
        buff.AppendFormat ("; Path={0}", _path);

      if (!_domain.IsNullOrEmpty ())
        buff.AppendFormat ("; Domain={0}", _domain);

      if (_secure)
        buff.Append ("; Secure");

      if (_httpOnly)
        buff.Append ("; HttpOnly");

      return buff.ToString ();
    }

    private string toResponseStringVersion1 ()
    {
      var buff = new StringBuilder (64);

      buff.AppendFormat ("{0}={1}; Version={2}", _name, _value, _version);

      if (_expires != DateTime.MinValue)
        buff.AppendFormat ("; Max-Age={0}", MaxAge);

      if (!_path.IsNullOrEmpty ())
        buff.AppendFormat ("; Path={0}", _path);

      if (!_domain.IsNullOrEmpty ())
        buff.AppendFormat ("; Domain={0}", _domain);

      if (!_port.IsNullOrEmpty ()) {
        if (_port != "\"\"")
          buff.AppendFormat ("; Port={0}", _port);
        else
          buff.Append ("; Port");
      }

      if (!_comment.IsNullOrEmpty ())
        buff.AppendFormat ("; Comment={0}", HttpUtility.UrlEncode (_comment));

      if (_commentUri != null) {
        var url = _commentUri.OriginalString;
        buff.AppendFormat (
          "; CommentURL={0}", !url.IsToken () ? url.Quote () : url
        );
      }

      if (_discard)
        buff.Append ("; Discard");

      if (_secure)
        buff.Append ("; Secure");

      return buff.ToString ();
    }

    private static bool tryCreatePorts (string value, out int[] result)
    {
      result = null;

      var arr = value.Trim ('"').Split (',');
      var len = arr.Length;
      var res = new int[len];

      for (var i = 0; i < len; i++) {
        var s = arr[i].Trim ();
        if (s.Length == 0) {
          res[i] = Int32.MinValue;
          continue;
        }

        if (!Int32.TryParse (s, out res[i]))
          return false;
      }

      result = res;
      return true;
    }

    #endregion

    #region Internal Methods

    internal string ToRequestString (Uri uri)
    {
      if (_name.Length == 0)
        return String.Empty;

      if (_version == 0)
        return String.Format ("{0}={1}", _name, _value);

      var buff = new StringBuilder (64);

      buff.AppendFormat ("$Version={0}; {1}={2}", _version, _name, _value);

      if (!_path.IsNullOrEmpty ())
        buff.AppendFormat ("; $Path={0}", _path);
      else if (uri != null)
        buff.AppendFormat ("; $Path={0}", uri.GetAbsolutePath ());
      else
        buff.Append ("; $Path=/");

      if (!_domain.IsNullOrEmpty ()) {
        if (uri == null || uri.Host != _domain)
          buff.AppendFormat ("; $Domain={0}", _domain);
      }

      if (!_port.IsNullOrEmpty ()) {
        if (_port != "\"\"")
          buff.AppendFormat ("; $Port={0}", _port);
        else
          buff.Append ("; $Port");
      }

      return buff.ToString ();
    }

    /// <summary>
    /// Returns a string that represents the current cookie instance.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that is suitable for the Set-Cookie response
    /// header.
    /// </returns>
    internal string ToResponseString ()
    {
      return _name.Length == 0
             ? String.Empty
             : _version == 0
               ? toResponseStringVersion0 ()
               : toResponseStringVersion1 ();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Determines whether the current cookie instance is equal to
    /// the specified <see cref="object"/> instance.
    /// </summary>
    /// <param name="comparand">
    ///   <para>
    ///   An <see cref="object"/> instance to compare with
    ///   the current cookie instance.
    ///   </para>
    ///   <para>
    ///   An reference to a <see cref="Cookie"/> instance.
    ///   </para>
    /// </param>
    /// <returns>
    /// <c>true</c> if the current cookie instance is equal to
    /// <paramref name="comparand"/>; otherwise, <c>false</c>.
    /// </returns>
    public override bool Equals (object comparand)
    {
      var cookie = comparand as Cookie;
      return cookie != null
             && _name.Equals (
                  cookie._name, StringComparison.InvariantCultureIgnoreCase
                )
             && _value.Equals (
                  cookie._value, StringComparison.InvariantCulture
                )
             && _path.Equals (
                  cookie._path, StringComparison.InvariantCulture
                )
             && _domain.Equals (
                  cookie._domain, StringComparison.InvariantCultureIgnoreCase
                )
             && _version == cookie._version;
    }

    /// <summary>
    /// Gets a hash code for the current cookie instance.
    /// </summary>
    /// <returns>
    /// An <see cref="int"/> that represents the hash code.
    /// </returns>
    public override int GetHashCode ()
    {
      return hash (
               StringComparer.InvariantCultureIgnoreCase.GetHashCode (_name),
               _value.GetHashCode (),
               _path.GetHashCode (),
               StringComparer.InvariantCultureIgnoreCase.GetHashCode (_domain),
               _version
             );
    }

    /// <summary>
    /// Returns a string that represents the current cookie instance.
    /// </summary>
    /// <returns>
    /// A <see cref="string"/> that is suitable for the Cookie request header.
    /// </returns>
    public override string ToString ()
    {
      return ToRequestString (null);
    }

    #endregion
  }
}
