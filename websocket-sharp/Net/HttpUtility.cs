#region License
//
// HttpUtility.cs
//	Copied from System.Net.HttpUtility.cs
//
// Authors:
//	Patrik Torstensson (Patrik.Torstensson@labs2.com)
//	Wictor Wilén (decode/encode functions) (wictor@ibizkit.se)
//	Tim Coleman (tim@timcoleman.com)
//	Gonzalo Paniagua Javier (gonzalo@ximian.com)
//
// Copyright (C) 2005-2009 Novell, Inc (http://www.novell.com)
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
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Text;

namespace WebSocketSharp.Net {

	internal sealed class HttpUtility {

		sealed class HttpQSCollection : NameValueCollection
		{
			public override string ToString ()
			{
				var count = Count;
				if (count == 0)
					return String.Empty;

				var output = new StringBuilder ();
				var keys = AllKeys;
				foreach (var key in keys)
					output.AppendFormat ("{0}={1}&", key, this [key]);

				if (output.Length > 0)
					output.Length--;

				return output.ToString ();
			}
		}

		#region Private Static Fields

		private static Dictionary<string, char> _entities;
		private static char []                  _hexChars = "0123456789abcdef".ToCharArray ();
		private static object                   _sync = new object ();

		#endregion

		#region Private Static Properties

		private static Dictionary<string, char> Entities {
			get {
				lock (_sync) {
					if (_entities == null)
						InitEntities ();

					return _entities;
				}
			}
		}

		#endregion

		#region Private Methods

		private static int GetChar (byte [] bytes, int offset, int length)
		{
			var value = 0;
			var end = length + offset;
			int current;
			for (int i = offset; i < end; i++) {
				current = GetInt (bytes [i]);
				if (current == -1)
					return -1;

				value = (value << 4) + current;
			}

			return value;
		}

		private static int GetChar (string s, int offset, int length)
		{
			var value = 0;
			var end = length + offset;
			char c;
			int current;
			for (int i = offset; i < end; i++) {
				c = s [i];
				if (c > 127)
					return -1;

				current = GetInt ((byte) c);
				if (current == -1)
					return -1;

				value = (value << 4) + current;
			}

			return value;
		}

		private static char [] GetChars (MemoryStream buffer, Encoding encoding)
		{
			return encoding.GetChars (buffer.GetBuffer (), 0, (int) buffer.Length);
		}

		private static int GetInt (byte b)
		{
			var c = (char) b;
			return c >= '0' && c <= '9'
			       ? c - '0'
			       : c >= 'a' && c <= 'f'
			         ? c - 'a' + 10
			         : c >= 'A' && c <= 'F'
			           ? c - 'A' + 10
			           : -1;
		}

		private static void InitEntities ()
		{
			// Build the dictionary of HTML entity references.
			// This list comes from the HTML 4.01 W3C recommendation.
			_entities = new Dictionary<string, char> ();
			_entities.Add ("nbsp", '\u00A0');
			_entities.Add ("iexcl", '\u00A1');
			_entities.Add ("cent", '\u00A2');
			_entities.Add ("pound", '\u00A3');
			_entities.Add ("curren", '\u00A4');
			_entities.Add ("yen", '\u00A5');
			_entities.Add ("brvbar", '\u00A6');
			_entities.Add ("sect", '\u00A7');
			_entities.Add ("uml", '\u00A8');
			_entities.Add ("copy", '\u00A9');
			_entities.Add ("ordf", '\u00AA');
			_entities.Add ("laquo", '\u00AB');
			_entities.Add ("not", '\u00AC');
			_entities.Add ("shy", '\u00AD');
			_entities.Add ("reg", '\u00AE');
			_entities.Add ("macr", '\u00AF');
			_entities.Add ("deg", '\u00B0');
			_entities.Add ("plusmn", '\u00B1');
			_entities.Add ("sup2", '\u00B2');
			_entities.Add ("sup3", '\u00B3');
			_entities.Add ("acute", '\u00B4');
			_entities.Add ("micro", '\u00B5');
			_entities.Add ("para", '\u00B6');
			_entities.Add ("middot", '\u00B7');
			_entities.Add ("cedil", '\u00B8');
			_entities.Add ("sup1", '\u00B9');
			_entities.Add ("ordm", '\u00BA');
			_entities.Add ("raquo", '\u00BB');
			_entities.Add ("frac14", '\u00BC');
			_entities.Add ("frac12", '\u00BD');
			_entities.Add ("frac34", '\u00BE');
			_entities.Add ("iquest", '\u00BF');
			_entities.Add ("Agrave", '\u00C0');
			_entities.Add ("Aacute", '\u00C1');
			_entities.Add ("Acirc", '\u00C2');
			_entities.Add ("Atilde", '\u00C3');
			_entities.Add ("Auml", '\u00C4');
			_entities.Add ("Aring", '\u00C5');
			_entities.Add ("AElig", '\u00C6');
			_entities.Add ("Ccedil", '\u00C7');
			_entities.Add ("Egrave", '\u00C8');
			_entities.Add ("Eacute", '\u00C9');
			_entities.Add ("Ecirc", '\u00CA');
			_entities.Add ("Euml", '\u00CB');
			_entities.Add ("Igrave", '\u00CC');
			_entities.Add ("Iacute", '\u00CD');
			_entities.Add ("Icirc", '\u00CE');
			_entities.Add ("Iuml", '\u00CF');
			_entities.Add ("ETH", '\u00D0');
			_entities.Add ("Ntilde", '\u00D1');
			_entities.Add ("Ograve", '\u00D2');
			_entities.Add ("Oacute", '\u00D3');
			_entities.Add ("Ocirc", '\u00D4');
			_entities.Add ("Otilde", '\u00D5');
			_entities.Add ("Ouml", '\u00D6');
			_entities.Add ("times", '\u00D7');
			_entities.Add ("Oslash", '\u00D8');
			_entities.Add ("Ugrave", '\u00D9');
			_entities.Add ("Uacute", '\u00DA');
			_entities.Add ("Ucirc", '\u00DB');
			_entities.Add ("Uuml", '\u00DC');
			_entities.Add ("Yacute", '\u00DD');
			_entities.Add ("THORN", '\u00DE');
			_entities.Add ("szlig", '\u00DF');
			_entities.Add ("agrave", '\u00E0');
			_entities.Add ("aacute", '\u00E1');
			_entities.Add ("acirc", '\u00E2');
			_entities.Add ("atilde", '\u00E3');
			_entities.Add ("auml", '\u00E4');
			_entities.Add ("aring", '\u00E5');
			_entities.Add ("aelig", '\u00E6');
			_entities.Add ("ccedil", '\u00E7');
			_entities.Add ("egrave", '\u00E8');
			_entities.Add ("eacute", '\u00E9');
			_entities.Add ("ecirc", '\u00EA');
			_entities.Add ("euml", '\u00EB');
			_entities.Add ("igrave", '\u00EC');
			_entities.Add ("iacute", '\u00ED');
			_entities.Add ("icirc", '\u00EE');
			_entities.Add ("iuml", '\u00EF');
			_entities.Add ("eth", '\u00F0');
			_entities.Add ("ntilde", '\u00F1');
			_entities.Add ("ograve", '\u00F2');
			_entities.Add ("oacute", '\u00F3');
			_entities.Add ("ocirc", '\u00F4');
			_entities.Add ("otilde", '\u00F5');
			_entities.Add ("ouml", '\u00F6');
			_entities.Add ("divide", '\u00F7');
			_entities.Add ("oslash", '\u00F8');
			_entities.Add ("ugrave", '\u00F9');
			_entities.Add ("uacute", '\u00FA');
			_entities.Add ("ucirc", '\u00FB');
			_entities.Add ("uuml", '\u00FC');
			_entities.Add ("yacute", '\u00FD');
			_entities.Add ("thorn", '\u00FE');
			_entities.Add ("yuml", '\u00FF');
			_entities.Add ("fnof", '\u0192');
			_entities.Add ("Alpha", '\u0391');
			_entities.Add ("Beta", '\u0392');
			_entities.Add ("Gamma", '\u0393');
			_entities.Add ("Delta", '\u0394');
			_entities.Add ("Epsilon", '\u0395');
			_entities.Add ("Zeta", '\u0396');
			_entities.Add ("Eta", '\u0397');
			_entities.Add ("Theta", '\u0398');
			_entities.Add ("Iota", '\u0399');
			_entities.Add ("Kappa", '\u039A');
			_entities.Add ("Lambda", '\u039B');
			_entities.Add ("Mu", '\u039C');
			_entities.Add ("Nu", '\u039D');
			_entities.Add ("Xi", '\u039E');
			_entities.Add ("Omicron", '\u039F');
			_entities.Add ("Pi", '\u03A0');
			_entities.Add ("Rho", '\u03A1');
			_entities.Add ("Sigma", '\u03A3');
			_entities.Add ("Tau", '\u03A4');
			_entities.Add ("Upsilon", '\u03A5');
			_entities.Add ("Phi", '\u03A6');
			_entities.Add ("Chi", '\u03A7');
			_entities.Add ("Psi", '\u03A8');
			_entities.Add ("Omega", '\u03A9');
			_entities.Add ("alpha", '\u03B1');
			_entities.Add ("beta", '\u03B2');
			_entities.Add ("gamma", '\u03B3');
			_entities.Add ("delta", '\u03B4');
			_entities.Add ("epsilon", '\u03B5');
			_entities.Add ("zeta", '\u03B6');
			_entities.Add ("eta", '\u03B7');
			_entities.Add ("theta", '\u03B8');
			_entities.Add ("iota", '\u03B9');
			_entities.Add ("kappa", '\u03BA');
			_entities.Add ("lambda", '\u03BB');
			_entities.Add ("mu", '\u03BC');
			_entities.Add ("nu", '\u03BD');
			_entities.Add ("xi", '\u03BE');
			_entities.Add ("omicron", '\u03BF');
			_entities.Add ("pi", '\u03C0');
			_entities.Add ("rho", '\u03C1');
			_entities.Add ("sigmaf", '\u03C2');
			_entities.Add ("sigma", '\u03C3');
			_entities.Add ("tau", '\u03C4');
			_entities.Add ("upsilon", '\u03C5');
			_entities.Add ("phi", '\u03C6');
			_entities.Add ("chi", '\u03C7');
			_entities.Add ("psi", '\u03C8');
			_entities.Add ("omega", '\u03C9');
			_entities.Add ("thetasym", '\u03D1');
			_entities.Add ("upsih", '\u03D2');
			_entities.Add ("piv", '\u03D6');
			_entities.Add ("bull", '\u2022');
			_entities.Add ("hellip", '\u2026');
			_entities.Add ("prime", '\u2032');
			_entities.Add ("Prime", '\u2033');
			_entities.Add ("oline", '\u203E');
			_entities.Add ("frasl", '\u2044');
			_entities.Add ("weierp", '\u2118');
			_entities.Add ("image", '\u2111');
			_entities.Add ("real", '\u211C');
			_entities.Add ("trade", '\u2122');
			_entities.Add ("alefsym", '\u2135');
			_entities.Add ("larr", '\u2190');
			_entities.Add ("uarr", '\u2191');
			_entities.Add ("rarr", '\u2192');
			_entities.Add ("darr", '\u2193');
			_entities.Add ("harr", '\u2194');
			_entities.Add ("crarr", '\u21B5');
			_entities.Add ("lArr", '\u21D0');
			_entities.Add ("uArr", '\u21D1');
			_entities.Add ("rArr", '\u21D2');
			_entities.Add ("dArr", '\u21D3');
			_entities.Add ("hArr", '\u21D4');
			_entities.Add ("forall", '\u2200');
			_entities.Add ("part", '\u2202');
			_entities.Add ("exist", '\u2203');
			_entities.Add ("empty", '\u2205');
			_entities.Add ("nabla", '\u2207');
			_entities.Add ("isin", '\u2208');
			_entities.Add ("notin", '\u2209');
			_entities.Add ("ni", '\u220B');
			_entities.Add ("prod", '\u220F');
			_entities.Add ("sum", '\u2211');
			_entities.Add ("minus", '\u2212');
			_entities.Add ("lowast", '\u2217');
			_entities.Add ("radic", '\u221A');
			_entities.Add ("prop", '\u221D');
			_entities.Add ("infin", '\u221E');
			_entities.Add ("ang", '\u2220');
			_entities.Add ("and", '\u2227');
			_entities.Add ("or", '\u2228');
			_entities.Add ("cap", '\u2229');
			_entities.Add ("cup", '\u222A');
			_entities.Add ("int", '\u222B');
			_entities.Add ("there4", '\u2234');
			_entities.Add ("sim", '\u223C');
			_entities.Add ("cong", '\u2245');
			_entities.Add ("asymp", '\u2248');
			_entities.Add ("ne", '\u2260');
			_entities.Add ("equiv", '\u2261');
			_entities.Add ("le", '\u2264');
			_entities.Add ("ge", '\u2265');
			_entities.Add ("sub", '\u2282');
			_entities.Add ("sup", '\u2283');
			_entities.Add ("nsub", '\u2284');
			_entities.Add ("sube", '\u2286');
			_entities.Add ("supe", '\u2287');
			_entities.Add ("oplus", '\u2295');
			_entities.Add ("otimes", '\u2297');
			_entities.Add ("perp", '\u22A5');
			_entities.Add ("sdot", '\u22C5');
			_entities.Add ("lceil", '\u2308');
			_entities.Add ("rceil", '\u2309');
			_entities.Add ("lfloor", '\u230A');
			_entities.Add ("rfloor", '\u230B');
			_entities.Add ("lang", '\u2329');
			_entities.Add ("rang", '\u232A');
			_entities.Add ("loz", '\u25CA');
			_entities.Add ("spades", '\u2660');
			_entities.Add ("clubs", '\u2663');
			_entities.Add ("hearts", '\u2665');
			_entities.Add ("diams", '\u2666');
			_entities.Add ("quot", '\u0022');
			_entities.Add ("amp", '\u0026');
			_entities.Add ("lt", '\u003C');
			_entities.Add ("gt", '\u003E');
			_entities.Add ("OElig", '\u0152');
			_entities.Add ("oelig", '\u0153');
			_entities.Add ("Scaron", '\u0160');
			_entities.Add ("scaron", '\u0161');
			_entities.Add ("Yuml", '\u0178');
			_entities.Add ("circ", '\u02C6');
			_entities.Add ("tilde", '\u02DC');
			_entities.Add ("ensp", '\u2002');
			_entities.Add ("emsp", '\u2003');
			_entities.Add ("thinsp", '\u2009');
			_entities.Add ("zwnj", '\u200C');
			_entities.Add ("zwj", '\u200D');
			_entities.Add ("lrm", '\u200E');
			_entities.Add ("rlm", '\u200F');
			_entities.Add ("ndash", '\u2013');
			_entities.Add ("mdash", '\u2014');
			_entities.Add ("lsquo", '\u2018');
			_entities.Add ("rsquo", '\u2019');
			_entities.Add ("sbquo", '\u201A');
			_entities.Add ("ldquo", '\u201C');
			_entities.Add ("rdquo", '\u201D');
			_entities.Add ("bdquo", '\u201E');
			_entities.Add ("dagger", '\u2020');
			_entities.Add ("Dagger", '\u2021');
			_entities.Add ("permil", '\u2030');
			_entities.Add ("lsaquo", '\u2039');
			_entities.Add ("rsaquo", '\u203A');
			_entities.Add ("euro", '\u20AC');
		}

		private static bool NotEncoded (char c)
		{
			return c == '!' ||
			       c == '\'' ||
			       c == '(' ||
			       c == ')' ||
			       c == '*' ||
			       c == '-' ||
			       c == '.' ||
			       c == '_';
		}

		private static void UrlEncodeChar (char c, Stream result, bool isUnicode)
		{
			if (c > 255) {
				// FIXME: What happens when there is an internal error ?
				//if (!isUnicode)
				//	throw new ArgumentOutOfRangeException ("c", c, "c must be less than 256.");
				result.WriteByte ((byte) '%');
				result.WriteByte ((byte) 'u');
				var i = (int) c;
				var idx = i >> 12;
				result.WriteByte ((byte) _hexChars [idx]);
				idx = (i >> 8) & 0x0F;
				result.WriteByte ((byte) _hexChars [idx]);
				idx = (i >> 4) & 0x0F;
				result.WriteByte ((byte) _hexChars [idx]);
				idx = i & 0x0F;
				result.WriteByte ((byte) _hexChars [idx]);

				return;
			}

			if (c > ' ' && NotEncoded (c)) {
				result.WriteByte ((byte) c);
				return;
			}

			if (c == ' ') {
				result.WriteByte ((byte) '+');
				return;
			}

			if ((c < '0') ||
				(c < 'A' && c > '9') ||
				(c > 'Z' && c < 'a') ||
				(c > 'z')) {
				if (isUnicode && c > 127) {
					result.WriteByte ((byte) '%');
					result.WriteByte ((byte) 'u');
					result.WriteByte ((byte) '0');
					result.WriteByte ((byte) '0');
				}
				else
					result.WriteByte ((byte) '%');

				var idx = ((int) c) >> 4;
				result.WriteByte ((byte) _hexChars [idx]);
				idx = ((int) c) & 0x0F;
				result.WriteByte ((byte) _hexChars [idx]);
			}
			else
				result.WriteByte ((byte) c);
		}

		private static void UrlPathEncodeChar (char c, Stream result)
		{
			if (c < 33 || c > 126) {
				var bytes = Encoding.UTF8.GetBytes (c.ToString ());
				int i;
				foreach (var b in bytes) {
					result.WriteByte ((byte) '%');
					i = ((int) b) >> 4;
					result.WriteByte ((byte) _hexChars [i]);
					i = ((int) b) & 0x0F;
					result.WriteByte ((byte) _hexChars [i]);
				}
			}
			else if (c == ' ') {
				result.WriteByte ((byte) '%');
				result.WriteByte ((byte) '2');
				result.WriteByte ((byte) '0');
			}
			else
				result.WriteByte ((byte) c);
		}

		private static void WriteCharBytes (IList buffer, char c, Encoding encoding)
		{
			if (c > 255)
				foreach (var b in encoding.GetBytes (new char[] { c }))
					buffer.Add (b);
			else
				buffer.Add ((byte) c);
		}

		#endregion

		#region Internal Methods

		internal static void ParseQueryString (string query, Encoding encoding, NameValueCollection result)
		{
			if (query.Length == 0)
				return;

			var decoded = HtmlDecode (query);
			var decodedLength = decoded.Length;
			var namePos = 0;
			var first = true;
			while (namePos <= decodedLength) {
				int valuePos = -1, valueEnd = -1;
				for (int q = namePos; q < decodedLength; q++) {
					if (valuePos == -1 && decoded [q] == '=') {
						valuePos = q + 1;
					}
					else if (decoded [q] == '&') {
						valueEnd = q;
						break;
					}
				}

				if (first) {
					first = false;
					if (decoded [namePos] == '?')
						namePos++;
				}

				string name, value;
				if (valuePos == -1) {
					name = null;
					valuePos = namePos;
				}
				else {
					name = UrlDecode (decoded.Substring (namePos, valuePos - namePos - 1), encoding);
				}

				if (valueEnd < 0) {
					namePos = -1;
					valueEnd = decoded.Length;
				}
				else {
					namePos = valueEnd + 1;
				}

				value = UrlDecode (decoded.Substring (valuePos, valueEnd - valuePos), encoding);
				result.Add (name, value);
				if (namePos == -1)
					break;
			}
		}

		internal static string UrlDecodeInternal (byte [] bytes, int offset, int count, Encoding encoding)
		{
			var output = new StringBuilder ();
			var acc = new MemoryStream ();
			var end = count + offset;
			int xchar;
			for (int i = offset; i < end; i++) {
				if (bytes [i] == '%' && i + 2 < count && bytes [i + 1] != '%') {
					if (bytes [i + 1] == (byte) 'u' && i + 5 < end) {
						if (acc.Length > 0) {
							output.Append (GetChars (acc, encoding));
							acc.SetLength (0);
						}

						xchar = GetChar (bytes, i + 2, 4);
						if (xchar != -1) {
							output.Append ((char) xchar);
							i += 5;

							continue;
						}
					}
					else if ((xchar = GetChar (bytes, i + 1, 2)) != -1) {
						acc.WriteByte ((byte) xchar);
						i += 2;

						continue;
					}
				}

				if (acc.Length > 0) {
					output.Append (GetChars (acc, encoding));
					acc.SetLength (0);
				}

				if (bytes [i] == '+') {
					output.Append (' ');
				}
				else {
					output.Append ((char) bytes [i]);
				}
			}

			if (acc.Length > 0) {
				output.Append (GetChars (acc, encoding));
			}

			acc = null;
			return output.ToString ();
		}

		internal static byte [] UrlDecodeToBytesInternal (byte [] bytes, int offset, int count)
		{
			var result = new MemoryStream ();
			var end = offset + count;
			char c;
			int xchar;
			for (int i = offset; i < end; i++) {
				c = (char) bytes [i];
				if (c == '+') {
					c = ' ';
				}
				else if (c == '%' && i < end - 2) {
					xchar = GetChar (bytes, i + 1, 2);
					if (xchar != -1) {
						c = (char) xchar;
						i += 2;
					}
				}

				result.WriteByte ((byte) c);
			}

			return result.ToArray ();
		}

		internal static byte [] UrlEncodeToBytesInternal (byte [] bytes, int offset, int count)
		{
			var result = new MemoryStream (count);
			var end = offset + count;
			for (int i = offset; i < end; i++)
				UrlEncodeChar ((char) bytes [i], result, false);

			return result.ToArray ();
		}

		internal static byte [] UrlEncodeUnicodeToBytesInternal (string s)
		{
			var result = new MemoryStream (s.Length);
			foreach (var c in s)
				UrlEncodeChar (c, result, true);

			return result.ToArray ();
		}

		#endregion

		#region Public Methods

		public static string HtmlAttributeEncode (string s)
		{
			if (s == null || s.Length == 0 || !s.Contains ('&', '"', '<', '>'))
				return s;

			var output = new StringBuilder ();
			foreach (var c in s)
				output.Append (
					c == '&'
					? "&amp;"
					: c == '"'
					  ? "&quot;"
					  : c == '<'
					    ? "&lt;"
					    : c == '>'
					      ? "&gt;"
					      : c.ToString ()
				);

			return output.ToString ();
		}

		public static void HtmlAttributeEncode (string s, TextWriter output)
		{
			if (output == null)
				throw new ArgumentNullException ("output");

			output.Write (HtmlAttributeEncode (s));
		}

		/// <summary>
		/// Decodes an HTML-encoded <see cref="string"/> and returns the decoded <see cref="string"/>.
		/// </summary>
		/// <returns>
		/// A <see cref="string"/> that contains the decoded string.
		/// </returns>
		/// <param name="s">
		/// A <see cref="string"/> to decode.
		/// </param>
		public static string HtmlDecode (string s) 
		{
			if (s == null || s.Length == 0 || !s.Contains ('&'))
				return s;

			var entity = new StringBuilder ();
			var output = new StringBuilder ();
			// 0 -> nothing,
			// 1 -> right after '&'
			// 2 -> between '&' and ';' but no '#'
			// 3 -> '#' found after '&' and getting numbers
			var state = 0;
			var number = 0;
			var haveTrailingDigits = false;
			foreach (var c in s) {
				if (state == 0) {
					if (c == '&') {
						entity.Append (c);
						state = 1;
					}
					else {
						output.Append (c);
					}

					continue;
				}

				if (c == '&') {
					state = 1;
					if (haveTrailingDigits) {
						entity.Append (number.ToString (CultureInfo.InvariantCulture));
						haveTrailingDigits = false;
					}

					output.Append (entity.ToString ());
					entity.Length = 0;
					entity.Append ('&');

					continue;
				}

				if (state == 1) {
					if (c == ';') {
						state = 0;
						output.Append (entity.ToString ());
						output.Append (c);
						entity.Length = 0;
					}
					else {
						number = 0;
						if (c != '#') {
							state = 2;
						}
						else {
							state = 3;
						}

						entity.Append (c);
					}
				}
				else if (state == 2) {
					entity.Append (c);
					if (c == ';') {
						var key = entity.ToString ();
						if (key.Length > 1 && Entities.ContainsKey (key.Substring (1, key.Length - 2)))
							key = Entities [key.Substring (1, key.Length - 2)].ToString ();

						output.Append (key);
						state = 0;
						entity.Length = 0;
					}
				}
				else if (state == 3) {
					if (c == ';') {
						if (number > 65535) {
							output.Append ("&#");
							output.Append (number.ToString (CultureInfo.InvariantCulture));
							output.Append (";");
						}
						else {
							output.Append ((char) number);
						}

						state = 0;
						entity.Length = 0;
						haveTrailingDigits = false;
					}
					else if (Char.IsDigit (c)) {
						number = number * 10 + ((int) c - '0');
						haveTrailingDigits = true;
					}
					else {
						state = 2;
						if (haveTrailingDigits) {
							entity.Append (number.ToString (CultureInfo.InvariantCulture));
							haveTrailingDigits = false;
						}

						entity.Append (c);
					}
				}
			}

			if (entity.Length > 0) {
				output.Append (entity.ToString ());
			}
			else if (haveTrailingDigits) {
				output.Append (number.ToString (CultureInfo.InvariantCulture));
			}

			return output.ToString ();
		}

		/// <summary>
		/// Decodes an HTML-encoded <see cref="string"/> and sends the decoded <see cref="string"/>
		/// to a <see cref="TextWriter"/>.
		/// </summary>
		/// <param name="s">
		/// A <see cref="string"/> to decode.
		/// </param>
		/// <param name="output">
		/// A <see cref="TextWriter"/> that receives the decoded <see cref="string"/>.
		/// </param>
		public static void HtmlDecode (string s, TextWriter output)
		{
			if (output == null)
				throw new ArgumentNullException ("output");

			output.Write (HtmlDecode (s));
		}

		/// <summary>
		/// HTML-encodes a <see cref="string"/> and returns the encoded <see cref="string"/>.
		/// </summary>
		/// <returns>
		/// A <see cref="string"/> that contains the encoded string.
		/// </returns>
		/// <param name="s">
		/// A <see cref="string"/> to encode.
		/// </param>
		public static string HtmlEncode (string s)
		{
			if (s == null || s.Length == 0)
				return s;

			var needEncode = false;
			foreach (var c in s) {
				if (c == '&' || c == '"' || c == '<' || c == '>' || c > 159) {
					needEncode = true;
					break;
				}
			}

			if (!needEncode)
				return s;

			var output = new StringBuilder ();
			foreach (var c in s) {
				if (c == '&')
					output.Append ("&amp;");
				else if (c == '"')
					output.Append ("&quot;");
				else if (c == '<')
					output.Append ("&lt;");
				else if (c == '>')
					output.Append ("&gt;");
				else {
					// MS starts encoding with &# from 160 and stops at 255.
					// We don't do that. One reason is the 65308/65310 unicode
					// characters that look like '<' and '>'.
					if (c > 159) {
						output.Append ("&#");
						output.Append (((int) c).ToString (CultureInfo.InvariantCulture));
						output.Append (";");
					}
					else {
						output.Append (c);
					}
				}
			}

			return output.ToString ();
		}

		/// <summary>
		/// HTML-encodes a <see cref="string"/> and sends the encoded <see cref="string"/>
		/// to a <see cref="TextWriter"/>.
		/// </summary>
		/// <param name="s">
		/// A <see cref="string"/> to encode.
		/// </param>
		/// <param name="output">
		/// A <see cref="TextWriter"/> that receives the encoded <see cref="string"/>.
		/// </param>
		public static void HtmlEncode (string s, TextWriter output)
		{
			if (output == null)
				throw new ArgumentNullException ("output");

			output.Write (HtmlEncode (s));
		}

		public static NameValueCollection ParseQueryString (string query)
		{
			return ParseQueryString (query, Encoding.UTF8);
		}

		public static NameValueCollection ParseQueryString (string query, Encoding encoding)
		{
			if (query == null)
				throw new ArgumentNullException ("query");

			var length = query.Length;
			if (length == 0 || (length == 1 && query [0] == '?'))
				return new NameValueCollection ();

			if (query [0] == '?')
				query = query.Substring (1);

			if (encoding == null)
				encoding = Encoding.UTF8;

			var result = new HttpQSCollection ();
			ParseQueryString (query, encoding, result);

			return result;
		}

		public static string UrlDecode (string s)
		{
			return UrlDecode (s, Encoding.UTF8);
		}

		public static string UrlDecode (string s, Encoding encoding)
		{
			if (s == null || s.Length == 0 || !s.Contains ('%', '+'))
				return s;

			if (encoding == null)
				encoding = Encoding.UTF8;

			var length = s.Length;
			var bytes = new List<byte> ();
			char c;
			int xchar;
			for (int i = 0; i < length; i++) {
				c = s [i];
				if (c == '%' && i + 2 < length && s [i + 1] != '%') {
					if (s [i + 1] == 'u' && i + 5 < length) {
						// Unicode hex sequence.
						xchar = GetChar (s, i + 2, 4);
						if (xchar != -1) {
							WriteCharBytes (bytes, (char) xchar, encoding);
							i += 5;
						}
						else
							WriteCharBytes (bytes, '%', encoding);
					}
					else if ((xchar = GetChar (s, i + 1, 2)) != -1) {
						WriteCharBytes (bytes, (char) xchar, encoding);
						i += 2;
					}
					else {
						WriteCharBytes (bytes, '%', encoding);
					}

					continue;
				}

				if (c == '+')
					WriteCharBytes (bytes, ' ', encoding);
				else
					WriteCharBytes (bytes, c, encoding);
			}

			var buffer = bytes.ToArray ();
			return encoding.GetString (buffer);
		}

		public static string UrlDecode (byte [] bytes, Encoding encoding)
		{
			if (encoding == null)
				encoding = Encoding.UTF8;

			int length;
			return bytes == null
			       ? null
			       : (length = bytes.Length) == 0
			         ? String.Empty
			         : UrlDecodeInternal (bytes, 0, length, encoding);
		}

		public static string UrlDecode (byte [] bytes, int offset, int count, Encoding encoding)
		{
			if (bytes == null)
				return null;

			var length = bytes.Length;
			if (length == 0 || count == 0)
				return String.Empty;

			if (offset < 0 || offset >= length)
				throw new ArgumentOutOfRangeException ("offset");

			if (count < 0 || count > length - offset)
				throw new ArgumentOutOfRangeException ("count");

			if (encoding == null)
				encoding = Encoding.UTF8;

			return UrlDecodeInternal (bytes, offset, count, encoding);
		}

		public static byte [] UrlDecodeToBytes (byte [] bytes)
		{
			int length;
			return bytes == null || (length = bytes.Length) == 0
			       ? bytes
			       : UrlDecodeToBytesInternal (bytes, 0, length);
		}

		public static byte [] UrlDecodeToBytes (string s)
		{
			return UrlDecodeToBytes (s, Encoding.UTF8);
		}

		public static byte [] UrlDecodeToBytes (string s, Encoding encoding)
		{
			if (s == null)
				return null;

			if (s.Length == 0)
				return new byte [0];

			if (encoding == null)
				encoding = Encoding.UTF8;

			var bytes = encoding.GetBytes (s);
			return UrlDecodeToBytesInternal (bytes, 0, bytes.Length);
		}

		public static byte [] UrlDecodeToBytes (byte [] bytes, int offset, int count)
		{
			int length;
			if (bytes == null || (length = bytes.Length) == 0)
				return bytes;

			if (count == 0)
				return new byte [0];

			if (offset < 0 || offset >= length)
				throw new ArgumentOutOfRangeException ("offset");

			if (count < 0 || count > length - offset )
				throw new ArgumentOutOfRangeException ("count");

			return UrlDecodeToBytesInternal (bytes, offset, count);
		}

		public static string UrlEncode (byte [] bytes)
		{
			int length;
			return bytes == null
			       ? null
			       : (length = bytes.Length) == 0
			         ? String.Empty
			         : Encoding.ASCII.GetString (UrlEncodeToBytesInternal (bytes, 0, length));
		}

		public static string UrlEncode (string s)
		{
			return UrlEncode (s, Encoding.UTF8);
		}
	
		public static string UrlEncode (string s, Encoding encoding)
		{
			int length;
			if (s == null || (length = s.Length) == 0)
				return s;

			var needEncode = false;
			foreach (var c in s) {
				if ((c < '0') || (c < 'A' && c > '9') || (c > 'Z' && c < 'a') || (c > 'z')) {
					if (NotEncoded (c))
						continue;

					needEncode = true;
					break;
				}
			}

			if (!needEncode)
				return s;

			if (encoding == null)
				encoding = Encoding.UTF8;

			// Avoided GetByteCount call.
			var bytes = new byte [encoding.GetMaxByteCount (length)];
			var realLen = encoding.GetBytes (s, 0, length, bytes, 0);

			return Encoding.ASCII.GetString (UrlEncodeToBytesInternal (bytes, 0, realLen));
		}
  
		public static string UrlEncode (byte [] bytes, int offset, int count)
		{
			var encoded = UrlEncodeToBytes (bytes, offset, count);
			return encoded == null
			       ? null
			       : encoded.Length == 0
			         ? String.Empty
			         : Encoding.ASCII.GetString (encoded);
		}

		public static byte [] UrlEncodeToBytes (byte [] bytes)
		{
			int length;
			return bytes == null || (length = bytes.Length) == 0
			       ? bytes
			       : UrlEncodeToBytesInternal (bytes, 0, length);
		}

		public static byte [] UrlEncodeToBytes (string s)
		{
			return UrlEncodeToBytes (s, Encoding.UTF8);
		}

		public static byte [] UrlEncodeToBytes (string s, Encoding encoding)
		{
			if (s == null)
				return null;

			if (s.Length == 0)
				return new byte [0];

			if (encoding == null)
				encoding = Encoding.UTF8;

			var bytes = encoding.GetBytes (s);
			return UrlEncodeToBytesInternal (bytes, 0, bytes.Length);
		}

		public static byte [] UrlEncodeToBytes (byte [] bytes, int offset, int count)
		{
			int length;
			if (bytes == null || (length = bytes.Length) == 0)
				return bytes;

			if (count == 0)
				return new byte [0];

			if (offset < 0 || offset >= length)
				throw new ArgumentOutOfRangeException ("offset");

			if (count < 0 || count > length - offset)
				throw new ArgumentOutOfRangeException ("count");

			return UrlEncodeToBytesInternal (bytes, offset, count);
		}

		public static string UrlEncodeUnicode (string s)
		{
			return s == null || s.Length == 0
			       ? s
			       : Encoding.ASCII.GetString (UrlEncodeUnicodeToBytesInternal (s));
		}

		public static byte [] UrlEncodeUnicodeToBytes (string s)
		{
			if (s == null)
				return null;

			if (s.Length == 0)
				return new byte [0];

			return UrlEncodeUnicodeToBytesInternal (s);
		}

		public static string UrlPathEncode (string s)
		{
			if (s == null || s.Length == 0)
				return s;

			var result = new MemoryStream ();
			foreach (var c in s)
				UrlPathEncodeChar (c, result);

			return Encoding.ASCII.GetString (result.ToArray ());
		}

		#endregion
	}
}
