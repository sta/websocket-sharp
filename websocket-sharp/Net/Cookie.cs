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
    private DateTime               _expires;
    private bool                   _httpOnly;
    private string                 _name;
    private string                 _path;
    private string                 _port;
    private int[]                  _ports;
    private static readonly char[] _reservedCharsForName;
    private static readonly char[] _reservedCharsForValue;
    private bool                   _secure;
    private DateTime               _timestamp;
    private string                 _value;
    private int                    _version;

    #endregion

    #region Static Constructor

    static Cookie ()
    {
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
      _comment = String.Empty;
      _domain = String.Empty;
      _expires = DateTime.MinValue;
      _name = String.Empty;
      _path = String.Empty;
      _port = String.Empty;
      _ports = new int[0];
      _timestamp = DateTime.Now;
      _value = String.Empty;
      _version = 0;
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
      : this (name, value, path)
    {
      Domain = domain;
    }

    #endregion

    #region Internal Properties

    internal bool ExactDomain {
      get; set;
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
        return _ports;
      }
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets or sets the value of the Comment attribute of the cookie.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the comment to document intended use of the cookie.
    /// </value>
    public string Comment {
      get {
        return _comment;
      }

      set {
        _comment = value ?? String.Empty;
      }
    }

    /// <summary>
    /// Gets or sets the value of the CommentURL attribute of the cookie.
    /// </summary>
    /// <value>
    /// A <see cref="Uri"/> that represents the URI that provides the comment to document intended
    /// use of the cookie.
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
    /// Gets or sets a value indicating whether the client discards the cookie unconditionally
    /// when the client terminates.
    /// </summary>
    /// <value>
    /// <c>true</c> if the client discards the cookie unconditionally when the client terminates;
    /// otherwise, <c>false</c>. The default value is <c>false</c>.
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
    /// A <see cref="string"/> that represents the URI for which the cookie is valid.
    /// </value>
    public string Domain {
      get {
        return _domain;
      }

      set {
        if (value.IsNullOrEmpty ()) {
          _domain = String.Empty;
          ExactDomain = true;
        }
        else {
          _domain = value;
          ExactDomain = value[0] != '.';
        }
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the cookie has expired.
    /// </summary>
    /// <value>
    /// <c>true</c> if the cookie has expired; otherwise, <c>false</c>.
    /// The default value is <c>false</c>.
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
    /// A <see cref="DateTime"/> that represents the date and time at which the cookie expires.
    /// The default value is <see cref="DateTime.MinValue"/>.
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
    /// Gets or sets a value indicating whether non-HTTP APIs can access the cookie.
    /// </summary>
    /// <value>
    /// <c>true</c> if non-HTTP APIs cannot access the cookie; otherwise, <c>false</c>.
    /// The default value is <c>false</c>.
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
    /// A <see cref="string"/> that represents the subset of URI on the origin server
    /// to which the cookie applies.
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
    /// A <see cref="string"/> that represents the list of TCP ports to which the cookie applies.
    /// </value>
    /// <exception cref="CookieException">
    /// The value specified for a set operation isn't enclosed in double quotes or
    /// couldn't be parsed.
    /// </exception>
    public string Port {
      get {
        return _port;
      }

      set { 
        if (value.IsNullOrEmpty ()) {
          _port = String.Empty;
          _ports = new int[0];

          return;
        }

        if (!value.IsEnclosedIn ('"'))
          throw new CookieException (
            "The value specified for the Port attribute isn't enclosed in double quotes.");

        string err;
        if (!tryCreatePorts (value, out _ports, out err))
          throw new CookieException (
            String.Format (
              "The value specified for the Port attribute contains an invalid value: {0}", err));

        _port = value;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the security level of the cookie is secure.
    /// </summary>
    /// <remarks>
    /// When this property is <c>true</c>, the cookie may be included in the HTTP request
    /// only if the request is transmitted over the HTTPS.
    /// </remarks>
    /// <value>
    /// <c>true</c> if the security level of the cookie is secure; otherwise, <c>false</c>.
    /// The default value is <c>false</c>.
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
    /// A <see cref="DateTime"/> that represents the time when the cookie was issued.
    /// </value>
    public DateTime TimeStamp {
      get {
        return _timestamp;
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
    /// An <see cref="int"/> that represents the version of the HTTP state management
    /// to which the cookie conforms.
    /// </value>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value specified for a set operation isn't 0 or 1.
    /// </exception>
    public int Version {
      get {
        return _version;
      }

      set {
        if (value < 0 || value > 1)
          throw new ArgumentOutOfRangeException ("value", "Not 0 or 1.");

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
      return i ^
             (j << 13 | j >> 19) ^
             (k << 26 | k >>  6) ^
             (l <<  7 | l >> 25) ^
             (m << 20 | m >> 12);
    }

    private string toResponseStringVersion0 ()
    {
      var output = new StringBuilder (64);
      output.AppendFormat ("{0}={1}", _name, _value);

      if (_expires != DateTime.MinValue)
        output.AppendFormat (
          "; Expires={0}",
          _expires.ToUniversalTime ().ToString (
            "ddd, dd'-'MMM'-'yyyy HH':'mm':'ss 'GMT'",
            CultureInfo.CreateSpecificCulture ("en-US")));

      if (!_path.IsNullOrEmpty ())
        output.AppendFormat ("; Path={0}", _path);

      if (!_domain.IsNullOrEmpty ())
        output.AppendFormat ("; Domain={0}", _domain);

      if (_secure)
        output.Append ("; Secure");

      if (_httpOnly)
        output.Append ("; HttpOnly");

      return output.ToString ();
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
        buff.Append (
          _port != "\"\"" ? String.Format ("; Port={0}", _port) : "; Port"
        );
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

    private static bool tryCreatePorts (string value, out int[] result, out string parseError)
    {
      var ports = value.Trim ('"').Split (',');
      var len = ports.Length;
      var res = new int[len];
      for (var i = 0; i < len; i++) {
        res[i] = Int32.MinValue;

        var port = ports[i].Trim ();
        if (port.Length == 0)
          continue;

        if (!Int32.TryParse (port, out res[i])) {
          result = new int[0];
          parseError = port;

          return false;
        }
      }

      result = res;
      parseError = String.Empty;

      return true;
    }

    #endregion

    #region Internal Methods

    // From client to server
    internal string ToRequestString (Uri uri)
    {
      if (_name.Length == 0)
        return String.Empty;

      if (_version == 0)
        return String.Format ("{0}={1}", _name, _value);

      var output = new StringBuilder (64);
      output.AppendFormat ("$Version={0}; {1}={2}", _version, _name, _value);

      if (!_path.IsNullOrEmpty ())
        output.AppendFormat ("; $Path={0}", _path);
      else if (uri != null)
        output.AppendFormat ("; $Path={0}", uri.GetAbsolutePath ());
      else
        output.Append ("; $Path=/");

      var appendDomain = uri == null || uri.Host != _domain;
      if (appendDomain && !_domain.IsNullOrEmpty ())
        output.AppendFormat ("; $Domain={0}", _domain);

      if (!_port.IsNullOrEmpty ()) {
        if (_port == "\"\"")
          output.Append ("; $Port");
        else
          output.AppendFormat ("; $Port={0}", _port);
      }

      return output.ToString ();
    }

    // From server to client
    internal string ToResponseString ()
    {
      return _name.Length > 0
             ? (_version == 0 ? toResponseStringVersion0 () : toResponseStringVersion1 ())
             : String.Empty;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Determines whether the specified <see cref="Object"/> is equal to the current
    /// <see cref="Cookie"/>.
    /// </summary>
    /// <param name="comparand">
    /// An <see cref="Object"/> to compare with the current <see cref="Cookie"/>.
    /// </param>
    /// <returns>
    /// <c>true</c> if <paramref name="comparand"/> is equal to the current <see cref="Cookie"/>;
    /// otherwise, <c>false</c>.
    /// </returns>
    public override bool Equals (Object comparand)
    {
      var cookie = comparand as Cookie;
      return cookie != null &&
             _name.Equals (cookie.Name, StringComparison.InvariantCultureIgnoreCase) &&
             _value.Equals (cookie.Value, StringComparison.InvariantCulture) &&
             _path.Equals (cookie.Path, StringComparison.InvariantCulture) &&
             _domain.Equals (cookie.Domain, StringComparison.InvariantCultureIgnoreCase) &&
             _version == cookie.Version;
    }

    /// <summary>
    /// Serves as a hash function for a <see cref="Cookie"/> object.
    /// </summary>
    /// <returns>
    /// An <see cref="int"/> that represents the hash code for the current <see cref="Cookie"/>.
    /// </returns>
    public override int GetHashCode ()
    {
      return hash (
        StringComparer.InvariantCultureIgnoreCase.GetHashCode (_name),
        _value.GetHashCode (),
        _path.GetHashCode (),
        StringComparer.InvariantCultureIgnoreCase.GetHashCode (_domain),
        _version);
    }

    /// <summary>
    /// Returns a <see cref="string"/> that represents the current <see cref="Cookie"/>.
    /// </summary>
    /// <remarks>
    /// This method returns a <see cref="string"/> to use to send an HTTP Cookie to
    /// an origin server.
    /// </remarks>
    /// <returns>
    /// A <see cref="string"/> that represents the current <see cref="Cookie"/>.
    /// </returns>
    public override string ToString ()
    {
      // i.e., only used for clients
      // See para 4.2.2 of RFC 2109 and para 3.3.4 of RFC 2965
      // See also bug #316017
      return ToRequestString (null);
    }

    #endregion
  }
}
