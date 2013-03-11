//
// Cookie.cs
//	Copied from System.Net.Cookie.cs
//
// Authors:
//	Lawrence Pit (loz@cable.a2000.nl)
//	Gonzalo Paniagua Javier (gonzalo@ximian.com)
//	Daniel Nauck (dna@mono-project.de)
//	Sebastien Pouliot (sebastien@ximian.com)
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
using System.Text;
using System.Globalization;
using System.Collections;

namespace WebSocketSharp.Net {

	/// <summary>
	/// Provides a set of properties and methods to use to manage the HTTP Cookie.
	/// </summary>
	/// <remarks>
	/// The Cookie class cannot be inherited.
	/// </remarks>
	[Serializable]
	public sealed class Cookie 
	{
		// Supported cookie formats are:
		// Netscape: http://home.netscape.com/newsref/std/cookie_spec.html
		// RFC 2109: http://www.ietf.org/rfc/rfc2109.txt
		// RFC 2965: http://www.ietf.org/rfc/rfc2965.txt

		#region Static Private Fields

		static char [] reservedCharsForName = new char [] {' ', '=', ';', ',', '\n', '\r', '\t'};
		static char [] reservedCharsForValue = new char [] {';', ','};

		#endregion

		#region Private Fields

		string   comment;
		Uri      commentUri;
		bool     discard;
		string   domain;
		DateTime expires;
		bool     httpOnly;
		string   name;
		string   path;
		string   port;
		int []   ports;
		bool     secure;
		DateTime timestamp;
		string   val;
		int      version;

		#endregion

		#region Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="Cookie"/> class.
		/// </summary>
		public Cookie ()
		{
			comment = String.Empty;
			domain = String.Empty;
			expires = DateTime.MinValue;
			name = String.Empty;
			path = String.Empty;
			port = String.Empty;
			ports = new int [] {};
			timestamp = DateTime.Now;
			val = String.Empty;
			version = 0;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Cookie"/> class
		/// with the specified <paramref name="name"/> and <paramref name="value"/>.
		/// </summary>
		/// <param name="name">
		/// A <see cref="string"/> that contains the Name of the cookie.
		/// </param>
		/// <param name="value">
		/// A <see cref="string"/> that contains the Value of the cookie.
		/// </param>
		/// <exception cref="CookieException">
		///   <para>
		///   <paramref name="name"/> is <see langword="null"/> or <see cref="String.Empty"/>.
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
			string msg;
			if (!CanSetName (name, out msg))
				throw new CookieException (msg);

			if (!CanSetValue (value, out msg))
				throw new CookieException (msg);

			this.name = name;
			this.val = value;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Cookie"/> class
		/// with the specified <paramref name="name"/>, <paramref name="value"/> and <paramref name="path"/>.
		/// </summary>
		/// <param name="name">
		/// A <see cref="string"/> that contains the Name of the cookie.
		/// </param>
		/// <param name="value">
		/// A <see cref="string"/> that contains the Value of the cookie.
		/// </param>
		/// <param name="path">
		/// A <see cref="string"/> that contains the value of the Path attribute of the cookie.
		/// </param>
		/// <exception cref="CookieException">
		///   <para>
		///   <paramref name="name"/> is <see langword="null"/> or <see cref="String.Empty"/>.
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
		/// Initializes a new instance of the <see cref="Cookie"/> class
		/// with the specified <paramref name="name"/>, <paramref name="value"/>,
		/// <paramref name="path"/> and <paramref name="domain"/>.
		/// </summary>
		/// <param name="name">
		/// A <see cref="string"/> that contains the Name of the cookie.
		/// </param>
		/// <param name="value">
		/// A <see cref="string"/> that contains the Value of the cookie.
		/// </param>
		/// <param name="path">
		/// A <see cref="string"/> that contains the value of the Path attribute of the cookie.
		/// </param>
		/// <param name="domain">
		/// A <see cref="string"/> that contains the value of the Domain attribute of the cookie.
		/// </param>
		/// <exception cref="CookieException">
		///   <para>
		///   <paramref name="name"/> is <see langword="null"/> or <see cref="String.Empty"/>.
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

		internal bool ExactDomain { get; set; }

		internal int [] Ports {
			get { return ports; }
		}

		#endregion

		#region Public Properties

		/// <summary>
		/// Gets or sets the value of the Comment attribute of the cookie.
		/// </summary>
		/// <value>
		/// A <see cref="string"/> that contains a comment to document intended use of the cookie.
		/// </value>
		public string Comment {
			get { return comment; }
			set { comment = value.IsNull () ? String.Empty : value; }
		}

		/// <summary>
		/// Gets or sets the value of the CommentURL attribute of the cookie.
		/// </summary>
		/// <value>
		/// A <see cref="Uri"/> that contains a URI that provides the comment
		/// to document intended use of the cookie.
		/// </value>
		public Uri CommentUri {
			get { return commentUri; }
			set { commentUri = value; }
		}

		/// <summary>
		/// Gets or sets a value indicating whether the client discards the cookie unconditionally
		/// when the client terminates.
		/// </summary>
		/// <value>
		/// <c>true</c> if the client discards the cookie unconditionally when the client terminates;
		/// otherwise, <c>false</c>. The default is <c>false</c>.
		/// </value>
		public bool Discard {
			get { return discard; }
			set { discard = value; }
		}

		/// <summary>
		/// Gets or sets the value of the Domain attribute of the cookie.
		/// </summary>
		/// <value>
		/// A <see cref="string"/> that contains a URI for which the cookie is valid.
		/// </value>
		public string Domain {
			get { return domain; }
			set {
				if (value.IsNullOrEmpty ()) {
					domain = String.Empty;
					ExactDomain = true;
				} else {
					domain = value;
					ExactDomain = value [0] != '.';
				}
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether the cookie has expired.
		/// </summary>
		/// <value>
		/// <c>true</c> if the cookie has expired; otherwise, <c>false</c>.
		/// </value>
		public bool Expired {
			get { 
				return expires <= DateTime.Now && 
				       expires != DateTime.MinValue;
			}
			set {  
				if (value)
					expires = DateTime.Now;
			}
		}

		/// <summary>
		/// Gets or sets the value of the Expires attribute of the cookie.
		/// </summary>
		/// <value>
		/// A <see cref="DateTime"/> that contains the date and time at which the cookie expires.
		/// </value>
		public DateTime Expires {
			get { return expires; }
			set { expires = value; }
		}

		/// <summary>
		/// Gets or sets a value indicating non-HTTP APIs can access the cookie.
		/// </summary>
		/// <value>
		/// <c>true</c> if non-HTTP APIs can not access the cookie; otherwise, <c>false</c>.
		/// </value>
		public bool HttpOnly {
			get { return httpOnly; }
			set { httpOnly = value; }
		}

		/// <summary>
		/// Gets or sets the Name of the cookie.
		/// </summary>
		/// <value>
		/// A <see cref="string"/> that contains the Name of the cookie.
		/// </value>
		/// <exception cref="CookieException">
		///   <para>
		///   The value specified for a set operation is <see langword="null"/> or <see cref="String.Empty"/>.
		///   </para>
		///   <para>
		///   - or -
		///   </para>
		///   <para>
		///   The value specified for a set operation contains an invalid character.
		///   </para>
		/// </exception>
		public string Name {
			get { return name; }
			set {
				string msg;
				if (!CanSetName (value, out msg))
					throw new CookieException (msg);

				name = value;
			}
		}

		/// <summary>
		/// Gets or sets the value of the Path attribute of the cookie.
		/// </summary>
		/// <value>
		/// A <see cref="string"/> that contains a subset of URI on the origin server
		/// to which the cookie applies.
		/// </value>
		public string Path {
			get { return path; }
			set { path = value.IsNull () ? String.Empty : value; }
		}

		/// <summary>
		/// Gets or sets the value of the Port attribute of the cookie.
		/// </summary>
		/// <value>
		/// A <see cref="string"/> that contains a list of the TCP ports to which the cookie applies.
		/// </value>
		/// <exception cref="CookieException">
		/// The value specified for a set operation could not be parsed or is not enclosed in double quotes.
		/// </exception>
		public string Port {
			get { return port; }
			set { 
				if (value.IsNullOrEmpty ()) {
					port = String.Empty;
					ports = new int [] {};
					return;
				}

				if (!value.IsEnclosedIn ('"'))
					throw new CookieException ("The 'Port'='" + value + "' attribute of the cookie is invalid. Port must be enclosed in double quotes.");

				string error;
				if (!TryCreatePorts (value, out ports, out error))
					throw new CookieException ("The 'Port'='" + value + "' attribute of the cookie is invalid. Invalid value: " + error);

				port = value;
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
		/// The default is <c>false</c>.
		/// </value>
		public bool Secure {
			get { return secure; }
			set { secure = value; }
		}

		/// <summary>
		/// Gets the time when the cookie was issued.
		/// </summary>
		/// <value>
		/// A <see cref="DateTime"/> that contains the time when the cookie was issued.
		/// </value>
		public DateTime TimeStamp {
			get { return timestamp; }
		}

		/// <summary>
		/// Gets or sets the Value of the cookie.
		/// </summary>
		/// <value>
		/// A <see cref="string"/> that contains the Value of the cookie.
		/// </value>
		public string Value {
			get { return val; }
			set { 
				if (value == null) {
					val = String.Empty;
					return;
				}

				// LAMESPEC: According to .Net specs the Value property should not accept 
				// the semicolon and comma characters, yet it does. For now we'll follow
				// the behaviour of MS.Net instead of the specs.
				/*
				if (value.IndexOfAny(reservedCharsForValue) != -1)
					throw new CookieException("Invalid value. Value cannot contain semicolon or comma characters.");
				*/

				val = value; 
			}
		}

		/// <summary>
		/// Gets or sets the value of the Version attribute of the cookie.
		/// </summary>
		/// <value>
		/// An <see cref="int"/> that contains the version of the HTTP state management
		/// to which the cookie conforms.
		/// </value>
		/// <exception cref="ArgumentOutOfRangeException">
		/// The value specified for a set operation is less than zero.
		/// </exception>
		public int Version {
			get { return version; }
			set { 
				if (value < 0)
					throw new ArgumentOutOfRangeException();
				else
					version = value; 
			}
		}

		#endregion

		#region Private Methods

		static bool CanSetName (string name, out string message)
		{
			if (name.IsNullOrEmpty ()) {
				message = "Name can not be null or empty.";
				return false;
			}

			if (name [0] == '$' || name.Contains (reservedCharsForName)) {
				message = "Name contains an invalid character.";
				return false;
			}

			message = String.Empty;
			return true;
		}

		static bool CanSetValue (string value, out string message)
		{
			if (value.IsNull ()) {
				message = "Value can not be null.";
				return false;
			}

			if (value.Contains (reservedCharsForValue)) {
				if (!value.IsEnclosedIn ('"')) {
					message = "Value contains an invalid character.";
					return false;
				}
			}

			message = String.Empty;
			return true;
		}

		static int Hash (int i, int j, int k, int l, int m)
		{
			return i ^ (j << 13 | j >> 19) ^ (k << 26 | k >> 6) ^ (l << 7 | l >> 25) ^ (m << 20 | m >> 12);
		}

		// See par 3.6 of RFC 2616
		string Quote (string value)
		{
			if (version == 0 || value.IsToken ())
				return value;
			else 
				return "\"" + value.Replace("\"", "\\\"") + "\"";
		}

		static bool TryCreatePorts (string value, out int [] result, out string parseError)
		{
			var values = value.Trim ('"').Split (',');
			var tmp = new int [values.Length];
			for (int i = 0; i < values.Length; i++) {
				tmp [i] = int.MinValue;
				var v = values [i].Trim ();
				if (v.IsEmpty ())
					continue;

				if (!int.TryParse (v, out tmp [i])) {
					result = new int [] {};
					parseError = v;
					return false;
				}
			}

			result = tmp;
			parseError = String.Empty;
			return true;
		}

		#endregion

		#region Internal Methods

		// From server to client
		internal string ToClientString ()
		{
			if (name.IsEmpty ())
				return String.Empty;

			var result = new StringBuilder (64);
			if (version > 0) 
				result.Append ("Version=").Append (version).Append ("; ");

			result.Append (name).Append ("=").Append (Quote (val));
			if (!path.IsNullOrEmpty ())
				result.Append ("; Path=").Append (path);

			if (!domain.IsNullOrEmpty ())
				result.Append ("; Domain=").Append (domain);

			if (!port.IsNullOrEmpty ())
				result.Append ("; Port=").Append (port);

			return result.ToString ();
		}

		// From client to server
		internal string ToString (Uri uri)
		{
			if (name.IsEmpty ())
				return String.Empty;

			var result = new StringBuilder (64);
			if (version > 0)
				result.Append ("$Version=").Append (version).Append ("; ");

			result.Append (name).Append ("=").Append (val);
			if (version == 0)
				return result.ToString ();

			if (!path.IsNullOrEmpty ())
				result.Append ("; $Path=").Append (path);
			else if (!uri.IsNull ())
				result.Append ("; $Path=").Append (uri.GetAbsolutePath ());
			else
				result.Append ("; $Path=/");

			bool append_domain = uri.IsNull () || uri.Host != domain;
			if (append_domain && !domain.IsNullOrEmpty ())
				result.Append ("; $Domain=").Append (domain);

			if (!port.IsNullOrEmpty ())
				result.Append ("; $Port=").Append (port);

			return result.ToString ();
		}

		#endregion

		#region Public Methods

		/// <summary>
		/// Determines whether the specified <see cref="Object"/> is equal to the current <see cref="Cookie"/>.
		/// </summary>
		/// <param name="comparand">
		/// An <see cref="Object"/> to compare with the current <see cref="Cookie"/>.
		/// </param>
		/// <returns>
		/// <c>true</c> if the specified <see cref="Object"/> is equal to the current <see cref="Cookie"/>;
		/// otherwise, <c>false</c>.
		/// </returns>
		public override bool Equals (Object comparand)
		{
			var cookie = comparand as Cookie;
			return !cookie.IsNull() &&
				String.Compare (this.name, cookie.Name, true, CultureInfo.InvariantCulture) == 0 &&
				String.Compare (this.val, cookie.Value, false, CultureInfo.InvariantCulture) == 0 &&
				String.Compare (this.path, cookie.Path, false, CultureInfo.InvariantCulture) == 0 &&
				String.Compare (this.domain, cookie.Domain, true, CultureInfo.InvariantCulture) == 0 &&
				this.version == cookie.Version;
		}

		/// <summary>
		/// Serves as a hash function for a <see cref="Cookie"/> object.
		/// </summary>
		/// <returns>
		/// An <see cref="int"/> that contains a hash code for this instance.
		/// </returns>
		public override int GetHashCode ()
		{
			return Hash (
				StringComparer.InvariantCultureIgnoreCase.GetHashCode (name),
				val.GetHashCode (),
				path.GetHashCode (),
				StringComparer.InvariantCultureIgnoreCase.GetHashCode (domain),
				version);
		}

		/// <summary>
		/// Returns a <see cref="string"/> that represents the current <see cref="Cookie"/>.
		/// </summary>
		/// <remarks>
		/// This method returns a <see cref="string"/> to use to send an HTTP Cookie to an origin server.
		/// </remarks>
		/// <returns>
		/// A <see cref="string"/> that represents the current <see cref="Cookie"/>.
		/// </returns>
		public override string ToString ()
		{
			// i.e., only used for clients
			// see para 4.2.2 of RFC 2109 and para 3.3.4 of RFC 2965
			// see also bug #316017
			return ToString (null);
		}

		#endregion
	}
}
