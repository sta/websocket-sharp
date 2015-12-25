#region License
/*
 * HttpUtility.cs
 *
 * This code is derived from System.Net.HttpUtility.cs of Mono
 * (http://www.mono-project.com).
 *
 * The MIT License
 *
 * Copyright (c) 2005-2009 Novell, Inc. (http://www.novell.com)
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
 * - Patrik Torstensson <Patrik.Torstensson@labs2.com>
 * - Wictor Wilén (decode/encode functions) <wictor@ibizkit.se>
 * - Tim Coleman <tim@timcoleman.com>
 * - Gonzalo Paniagua Javier <gonzalo@ximian.com>
 */
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Text;

namespace WebSocketSharp.Net
{
	internal sealed class HttpUtility
	{
	    private static Dictionary<string, char> _entities;
		private static char[] _hexChars = "0123456789abcdef".ToCharArray();
		private static object _sync = new object();

	    private static int getChar(byte[] bytes, int offset, int length)
		{
			var val = 0;
			var end = length + offset;
			for (var i = offset; i < end; i++)
			{
				var current = getInt(bytes[i]);
				if (current == -1)
					return -1;

				val = (val << 4) + current;
			}

			return val;
		}

		private static int getChar(string s, int offset, int length)
		{
			var val = 0;
			var end = length + offset;
			for (var i = offset; i < end; i++)
			{
				var c = s[i];
				if (c > 127)
					return -1;

				var current = getInt((byte)c);
				if (current == -1)
					return -1;

				val = (val << 4) + current;
			}

			return val;
		}

		private static char[] getChars(MemoryStream buffer, Encoding encoding)
		{
			return encoding.GetChars(buffer.GetBuffer(), 0, (int)buffer.Length);
		}

		private static Dictionary<string, char> getEntities()
		{
			lock (_sync)
			{
				if (_entities == null)
					initEntities();

				return _entities;
			}
		}

		private static int getInt(byte b)
		{
			var c = (char)b;
			return c >= '0' && c <= '9'
				   ? c - '0'
				   : c >= 'a' && c <= 'f'
					 ? c - 'a' + 10
					 : c >= 'A' && c <= 'F'
					   ? c - 'A' + 10
					   : -1;
		}

		private static void initEntities()
		{
			// Build the dictionary of HTML entity references.
			// This list comes from the HTML 4.01 W3C recommendation.
		    _entities = new Dictionary<string, char>
		                    {
		                        { "nbsp", '\u00A0' },
		                        { "iexcl", '\u00A1' },
		                        { "cent", '\u00A2' },
		                        { "pound", '\u00A3' },
		                        { "curren", '\u00A4' },
		                        { "yen", '\u00A5' },
		                        { "brvbar", '\u00A6' },
		                        { "sect", '\u00A7' },
		                        { "uml", '\u00A8' },
		                        { "copy", '\u00A9' },
		                        { "ordf", '\u00AA' },
		                        { "laquo", '\u00AB' },
		                        { "not", '\u00AC' },
		                        { "shy", '\u00AD' },
		                        { "reg", '\u00AE' },
		                        { "macr", '\u00AF' },
		                        { "deg", '\u00B0' },
		                        { "plusmn", '\u00B1' },
		                        { "sup2", '\u00B2' },
		                        { "sup3", '\u00B3' },
		                        { "acute", '\u00B4' },
		                        { "micro", '\u00B5' },
		                        { "para", '\u00B6' },
		                        { "middot", '\u00B7' },
		                        { "cedil", '\u00B8' },
		                        { "sup1", '\u00B9' },
		                        { "ordm", '\u00BA' },
		                        { "raquo", '\u00BB' },
		                        { "frac14", '\u00BC' },
		                        { "frac12", '\u00BD' },
		                        { "frac34", '\u00BE' },
		                        { "iquest", '\u00BF' },
		                        { "Agrave", '\u00C0' },
		                        { "Aacute", '\u00C1' },
		                        { "Acirc", '\u00C2' },
		                        { "Atilde", '\u00C3' },
		                        { "Auml", '\u00C4' },
		                        { "Aring", '\u00C5' },
		                        { "AElig", '\u00C6' },
		                        { "Ccedil", '\u00C7' },
		                        { "Egrave", '\u00C8' },
		                        { "Eacute", '\u00C9' },
		                        { "Ecirc", '\u00CA' },
		                        { "Euml", '\u00CB' },
		                        { "Igrave", '\u00CC' },
		                        { "Iacute", '\u00CD' },
		                        { "Icirc", '\u00CE' },
		                        { "Iuml", '\u00CF' },
		                        { "ETH", '\u00D0' },
		                        { "Ntilde", '\u00D1' },
		                        { "Ograve", '\u00D2' },
		                        { "Oacute", '\u00D3' },
		                        { "Ocirc", '\u00D4' },
		                        { "Otilde", '\u00D5' },
		                        { "Ouml", '\u00D6' },
		                        { "times", '\u00D7' },
		                        { "Oslash", '\u00D8' },
		                        { "Ugrave", '\u00D9' },
		                        { "Uacute", '\u00DA' },
		                        { "Ucirc", '\u00DB' },
		                        { "Uuml", '\u00DC' },
		                        { "Yacute", '\u00DD' },
		                        { "THORN", '\u00DE' },
		                        { "szlig", '\u00DF' },
		                        { "agrave", '\u00E0' },
		                        { "aacute", '\u00E1' },
		                        { "acirc", '\u00E2' },
		                        { "atilde", '\u00E3' },
		                        { "auml", '\u00E4' },
		                        { "aring", '\u00E5' },
		                        { "aelig", '\u00E6' },
		                        { "ccedil", '\u00E7' },
		                        { "egrave", '\u00E8' },
		                        { "eacute", '\u00E9' },
		                        { "ecirc", '\u00EA' },
		                        { "euml", '\u00EB' },
		                        { "igrave", '\u00EC' },
		                        { "iacute", '\u00ED' },
		                        { "icirc", '\u00EE' },
		                        { "iuml", '\u00EF' },
		                        { "eth", '\u00F0' },
		                        { "ntilde", '\u00F1' },
		                        { "ograve", '\u00F2' },
		                        { "oacute", '\u00F3' },
		                        { "ocirc", '\u00F4' },
		                        { "otilde", '\u00F5' },
		                        { "ouml", '\u00F6' },
		                        { "divide", '\u00F7' },
		                        { "oslash", '\u00F8' },
		                        { "ugrave", '\u00F9' },
		                        { "uacute", '\u00FA' },
		                        { "ucirc", '\u00FB' },
		                        { "uuml", '\u00FC' },
		                        { "yacute", '\u00FD' },
		                        { "thorn", '\u00FE' },
		                        { "yuml", '\u00FF' },
		                        { "fnof", '\u0192' },
		                        { "Alpha", '\u0391' },
		                        { "Beta", '\u0392' },
		                        { "Gamma", '\u0393' },
		                        { "Delta", '\u0394' },
		                        { "Epsilon", '\u0395' },
		                        { "Zeta", '\u0396' },
		                        { "Eta", '\u0397' },
		                        { "Theta", '\u0398' },
		                        { "Iota", '\u0399' },
		                        { "Kappa", '\u039A' },
		                        { "Lambda", '\u039B' },
		                        { "Mu", '\u039C' },
		                        { "Nu", '\u039D' },
		                        { "Xi", '\u039E' },
		                        { "Omicron", '\u039F' },
		                        { "Pi", '\u03A0' },
		                        { "Rho", '\u03A1' },
		                        { "Sigma", '\u03A3' },
		                        { "Tau", '\u03A4' },
		                        { "Upsilon", '\u03A5' },
		                        { "Phi", '\u03A6' },
		                        { "Chi", '\u03A7' },
		                        { "Psi", '\u03A8' },
		                        { "Omega", '\u03A9' },
		                        { "alpha", '\u03B1' },
		                        { "beta", '\u03B2' },
		                        { "gamma", '\u03B3' },
		                        { "delta", '\u03B4' },
		                        { "epsilon", '\u03B5' },
		                        { "zeta", '\u03B6' },
		                        { "eta", '\u03B7' },
		                        { "theta", '\u03B8' },
		                        { "iota", '\u03B9' },
		                        { "kappa", '\u03BA' },
		                        { "lambda", '\u03BB' },
		                        { "mu", '\u03BC' },
		                        { "nu", '\u03BD' },
		                        { "xi", '\u03BE' },
		                        { "omicron", '\u03BF' },
		                        { "pi", '\u03C0' },
		                        { "rho", '\u03C1' },
		                        { "sigmaf", '\u03C2' },
		                        { "sigma", '\u03C3' },
		                        { "tau", '\u03C4' },
		                        { "upsilon", '\u03C5' },
		                        { "phi", '\u03C6' },
		                        { "chi", '\u03C7' },
		                        { "psi", '\u03C8' },
		                        { "omega", '\u03C9' },
		                        { "thetasym", '\u03D1' },
		                        { "upsih", '\u03D2' },
		                        { "piv", '\u03D6' },
		                        { "bull", '\u2022' },
		                        { "hellip", '\u2026' },
		                        { "prime", '\u2032' },
		                        { "Prime", '\u2033' },
		                        { "oline", '\u203E' },
		                        { "frasl", '\u2044' },
		                        { "weierp", '\u2118' },
		                        { "image", '\u2111' },
		                        { "real", '\u211C' },
		                        { "trade", '\u2122' },
		                        { "alefsym", '\u2135' },
		                        { "larr", '\u2190' },
		                        { "uarr", '\u2191' },
		                        { "rarr", '\u2192' },
		                        { "darr", '\u2193' },
		                        { "harr", '\u2194' },
		                        { "crarr", '\u21B5' },
		                        { "lArr", '\u21D0' },
		                        { "uArr", '\u21D1' },
		                        { "rArr", '\u21D2' },
		                        { "dArr", '\u21D3' },
		                        { "hArr", '\u21D4' },
		                        { "forall", '\u2200' },
		                        { "part", '\u2202' },
		                        { "exist", '\u2203' },
		                        { "empty", '\u2205' },
		                        { "nabla", '\u2207' },
		                        { "isin", '\u2208' },
		                        { "notin", '\u2209' },
		                        { "ni", '\u220B' },
		                        { "prod", '\u220F' },
		                        { "sum", '\u2211' },
		                        { "minus", '\u2212' },
		                        { "lowast", '\u2217' },
		                        { "radic", '\u221A' },
		                        { "prop", '\u221D' },
		                        { "infin", '\u221E' },
		                        { "ang", '\u2220' },
		                        { "and", '\u2227' },
		                        { "or", '\u2228' },
		                        { "cap", '\u2229' },
		                        { "cup", '\u222A' },
		                        { "int", '\u222B' },
		                        { "there4", '\u2234' },
		                        { "sim", '\u223C' },
		                        { "cong", '\u2245' },
		                        { "asymp", '\u2248' },
		                        { "ne", '\u2260' },
		                        { "equiv", '\u2261' },
		                        { "le", '\u2264' },
		                        { "ge", '\u2265' },
		                        { "sub", '\u2282' },
		                        { "sup", '\u2283' },
		                        { "nsub", '\u2284' },
		                        { "sube", '\u2286' },
		                        { "supe", '\u2287' },
		                        { "oplus", '\u2295' },
		                        { "otimes", '\u2297' },
		                        { "perp", '\u22A5' },
		                        { "sdot", '\u22C5' },
		                        { "lceil", '\u2308' },
		                        { "rceil", '\u2309' },
		                        { "lfloor", '\u230A' },
		                        { "rfloor", '\u230B' },
		                        { "lang", '\u2329' },
		                        { "rang", '\u232A' },
		                        { "loz", '\u25CA' },
		                        { "spades", '\u2660' },
		                        { "clubs", '\u2663' },
		                        { "hearts", '\u2665' },
		                        { "diams", '\u2666' },
		                        { "quot", '\u0022' },
		                        { "amp", '\u0026' },
		                        { "lt", '\u003C' },
		                        { "gt", '\u003E' },
		                        { "OElig", '\u0152' },
		                        { "oelig", '\u0153' },
		                        { "Scaron", '\u0160' },
		                        { "scaron", '\u0161' },
		                        { "Yuml", '\u0178' },
		                        { "circ", '\u02C6' },
		                        { "tilde", '\u02DC' },
		                        { "ensp", '\u2002' },
		                        { "emsp", '\u2003' },
		                        { "thinsp", '\u2009' },
		                        { "zwnj", '\u200C' },
		                        { "zwj", '\u200D' },
		                        { "lrm", '\u200E' },
		                        { "rlm", '\u200F' },
		                        { "ndash", '\u2013' },
		                        { "mdash", '\u2014' },
		                        { "lsquo", '\u2018' },
		                        { "rsquo", '\u2019' },
		                        { "sbquo", '\u201A' },
		                        { "ldquo", '\u201C' },
		                        { "rdquo", '\u201D' },
		                        { "bdquo", '\u201E' },
		                        { "dagger", '\u2020' },
		                        { "Dagger", '\u2021' },
		                        { "permil", '\u2030' },
		                        { "lsaquo", '\u2039' },
		                        { "rsaquo", '\u203A' },
		                        { "euro", '\u20AC' }
		                    };
		}

		private static bool notEncoded(char c)
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

		private static void urlEncode(char c, Stream result, bool unicode)
		{
			if (c > 255)
			{
				// FIXME: What happens when there is an internal error?
				//if (!unicode)
				//  throw new ArgumentOutOfRangeException ("c", c, "Greater than 255.");

				result.WriteByte((byte)'%');
				result.WriteByte((byte)'u');

				var i = (int)c;
				var idx = i >> 12;
				result.WriteByte((byte)_hexChars[idx]);

				idx = (i >> 8) & 0x0F;
				result.WriteByte((byte)_hexChars[idx]);

				idx = (i >> 4) & 0x0F;
				result.WriteByte((byte)_hexChars[idx]);

				idx = i & 0x0F;
				result.WriteByte((byte)_hexChars[idx]);

				return;
			}

			if (c > ' ' && notEncoded(c))
			{
				result.WriteByte((byte)c);
				return;
			}

			if (c == ' ')
			{
				result.WriteByte((byte)'+');
				return;
			}

			if ((c < '0') ||
				(c < 'A' && c > '9') ||
				(c > 'Z' && c < 'a') ||
				(c > 'z'))
			{
				if (unicode && c > 127)
				{
					result.WriteByte((byte)'%');
					result.WriteByte((byte)'u');
					result.WriteByte((byte)'0');
					result.WriteByte((byte)'0');
				}
				else
				{
					result.WriteByte((byte)'%');
				}

				var i = (int)c;
				var idx = i >> 4;
				result.WriteByte((byte)_hexChars[idx]);

				idx = i & 0x0F;
				result.WriteByte((byte)_hexChars[idx]);

				return;
			}

			result.WriteByte((byte)c);
		}

		private static void urlPathEncode(char c, Stream result)
		{
			if (c < 33 || c > 126)
			{
				var bytes = Encoding.UTF8.GetBytes(c.ToString());
				foreach (var b in bytes)
				{
					result.WriteByte((byte)'%');

					var i = (int)b;
					var idx = i >> 4;
					result.WriteByte((byte)_hexChars[idx]);

					idx = i & 0x0F;
					result.WriteByte((byte)_hexChars[idx]);
				}

				return;
			}

			if (c == ' ')
			{
				result.WriteByte((byte)'%');
				result.WriteByte((byte)'2');
				result.WriteByte((byte)'0');

				return;
			}

			result.WriteByte((byte)c);
		}

		private static void writeCharBytes(char c, IList buffer, Encoding encoding)
		{
			if (c > 255)
			{
				foreach (var b in encoding.GetBytes(new[] { c }))
					buffer.Add(b);

				return;
			}

			buffer.Add((byte)c);
		}

	    internal static Uri CreateRequestUrl(
		  string requestUri, string host, bool websocketRequest, bool secure)
		{
			if (string.IsNullOrEmpty(requestUri) || host == null || host.Length == 0)
				return null;

			string schm = null;
			string path = null;
			if (requestUri.StartsWith("/"))
			{
				path = requestUri;
			}
			else if (requestUri.MaybeUri())
			{
				Uri uri;
				var valid = Uri.TryCreate(requestUri, UriKind.Absolute, out uri) &&
							(((schm = uri.Scheme).StartsWith("http") && !websocketRequest) ||
							 (schm.StartsWith("ws") && websocketRequest));

				if (!valid)
					return null;

				host = uri.Authority;
				path = uri.PathAndQuery;
			}
			else if (requestUri == "*")
			{
			}
			else
			{
				// As authority form
				host = requestUri;
			}

			if (schm == null)
				schm = (websocketRequest ? "ws" : "http") + (secure ? "s" : string.Empty);

			var colon = host.IndexOf(':');
			if (colon == -1)
				host = $"{host}:{(schm == "http" || schm == "ws" ? 80 : 443)}";

			var url = $"{schm}://{host}{path}";

			Uri res;
			if (!Uri.TryCreate(url, UriKind.Absolute, out res))
				return null;

			return res;
		}

		internal static Encoding GetEncoding(string contentType)
		{
			var parts = contentType.Split(';');
			foreach (var p in parts)
			{
				var part = p.Trim();
				if (part.StartsWith("charset", StringComparison.OrdinalIgnoreCase))
					return Encoding.GetEncoding(part.GetValue('=', true));
			}

			return null;
		}

		internal static NameValueCollection InternalParseQueryString(string query, Encoding encoding)
		{
			int len;
			if (query == null || (len = query.Length) == 0 || (len == 1 && query[0] == '?'))
				return new NameValueCollection(1);

			if (query[0] == '?')
				query = query.Substring(1);

			var res = new QueryStringCollection();
			var components = query.Split('&');
			foreach (var component in components)
			{
				var i = component.IndexOf('=');
				if (i > -1)
				{
					var name = UrlDecode(component.Substring(0, i), encoding);
					var val = component.Length > i + 1
							  ? UrlDecode(component.Substring(i + 1), encoding)
							  : string.Empty;

					res.Add(name, val);
				}
				else
				{
					res.Add(null, UrlDecode(component, encoding));
				}
			}

			return res;
		}

		internal static string InternalUrlDecode(
		  byte[] bytes, int offset, int count, Encoding encoding)
		{
			var output = new StringBuilder();
			using (var acc = new MemoryStream())
			{
				var end = count + offset;
				for (var i = offset; i < end; i++)
				{
					if (bytes[i] == '%' && i + 2 < count && bytes[i + 1] != '%')
					{
						int xchar;
						if (bytes[i + 1] == (byte)'u' && i + 5 < end)
						{
							if (acc.Length > 0)
							{
								output.Append(getChars(acc, encoding));
								acc.SetLength(0);
							}

							xchar = getChar(bytes, i + 2, 4);
							if (xchar != -1)
							{
								output.Append((char)xchar);
								i += 5;

								continue;
							}
						}
						else if ((xchar = getChar(bytes, i + 1, 2)) != -1)
						{
							acc.WriteByte((byte)xchar);
							i += 2;

							continue;
						}
					}

					if (acc.Length > 0)
					{
						output.Append(getChars(acc, encoding));
						acc.SetLength(0);
					}

					if (bytes[i] == '+')
					{
						output.Append(' ');
						continue;
					}

					output.Append((char)bytes[i]);
				}

				if (acc.Length > 0)
					output.Append(getChars(acc, encoding));
			}

			return output.ToString();
		}

		internal static byte[] InternalUrlDecodeToBytes(byte[] bytes, int offset, int count)
		{
			using (var res = new MemoryStream())
			{
				var end = offset + count;
				for (var i = offset; i < end; i++)
				{
					var c = (char)bytes[i];
					if (c == '+')
					{
						c = ' ';
					}
					else if (c == '%' && i < end - 2)
					{
						var xchar = getChar(bytes, i + 1, 2);
						if (xchar != -1)
						{
							c = (char)xchar;
							i += 2;
						}
					}

					res.WriteByte((byte)c);
				}

				res.Close();
				return res.ToArray();
			}
		}

		internal static byte[] InternalUrlEncodeToBytes(byte[] bytes, int offset, int count)
		{
			using (var res = new MemoryStream())
			{
				var end = offset + count;
				for (var i = offset; i < end; i++)
					urlEncode((char)bytes[i], res, false);

				res.Close();
				return res.ToArray();
			}
		}

		internal static byte[] InternalUrlEncodeUnicodeToBytes(string s)
		{
			using (var res = new MemoryStream())
			{
				foreach (var c in s)
					urlEncode(c, res, true);

				res.Close();
				return res.ToArray();
			}
		}

	    public static string HtmlAttributeEncode(string s)
		{
			if (string.IsNullOrEmpty(s) || !s.Contains('&', '"', '<', '>'))
				return s;

			var output = new StringBuilder();
			foreach (var c in s)
				output.Append(
				  c == '&'
				  ? "&amp;"
				  : c == '"'
					? "&quot;"
					: c == '<'
					  ? "&lt;"
					  : c == '>'
						? "&gt;"
						: c.ToString());

			return output.ToString();
		}

		public static void HtmlAttributeEncode(string s, TextWriter output)
		{
			if (output == null)
				throw new ArgumentNullException(nameof(output));

			output.Write(HtmlAttributeEncode(s));
		}

		/// <summary>
		/// Decodes an HTML-encoded <see cref="string"/> and returns the decoded <see cref="string"/>.
		/// </summary>
		/// <returns>
		/// A <see cref="string"/> that represents the decoded string.
		/// </returns>
		/// <param name="s">
		/// A <see cref="string"/> to decode.
		/// </param>
		public static string HtmlDecode(string s)
		{
			if (string.IsNullOrEmpty(s) || !s.Contains('&'))
				return s;

			var entity = new StringBuilder();
			var output = new StringBuilder();

			// 0 -> nothing,
			// 1 -> right after '&'
			// 2 -> between '&' and ';' but no '#'
			// 3 -> '#' found after '&' and getting numbers
			var state = 0;

			var number = 0;
			var haveTrailingDigits = false;
			foreach (var c in s)
			{
				if (state == 0)
				{
					if (c == '&')
					{
						entity.Append(c);
						state = 1;
					}
					else
					{
						output.Append(c);
					}

					continue;
				}

				if (c == '&')
				{
					state = 1;
					if (haveTrailingDigits)
					{
						entity.Append(number.ToString(CultureInfo.InvariantCulture));
						haveTrailingDigits = false;
					}

					output.Append(entity);
					entity.Length = 0;
					entity.Append('&');

					continue;
				}

				if (state == 1)
				{
					if (c == ';')
					{
						state = 0;
						output.Append(entity);
						output.Append(c);
						entity.Length = 0;
					}
					else
					{
						number = 0;
						if (c != '#')
							state = 2;
						else
							state = 3;

						entity.Append(c);
					}
				}
				else if (state == 2)
				{
					entity.Append(c);
					if (c == ';')
					{
						var key = entity.ToString();
						var entities = getEntities();
						if (key.Length > 1 && entities.ContainsKey(key.Substring(1, key.Length - 2)))
							key = entities[key.Substring(1, key.Length - 2)].ToString();

						output.Append(key);
						state = 0;
						entity.Length = 0;
					}
				}
				else if (state == 3)
				{
					if (c == ';')
					{
						if (number > 65535)
						{
							output.Append("&#");
							output.Append(number.ToString(CultureInfo.InvariantCulture));
							output.Append(";");
						}
						else
						{
							output.Append((char)number);
						}

						state = 0;
						entity.Length = 0;
						haveTrailingDigits = false;
					}
					else if (char.IsDigit(c))
					{
						number = number * 10 + (c - '0');
						haveTrailingDigits = true;
					}
					else
					{
						state = 2;
						if (haveTrailingDigits)
						{
							entity.Append(number.ToString(CultureInfo.InvariantCulture));
							haveTrailingDigits = false;
						}

						entity.Append(c);
					}
				}
			}

			if (entity.Length > 0)
				output.Append(entity);
			else if (haveTrailingDigits)
				output.Append(number.ToString(CultureInfo.InvariantCulture));

			return output.ToString();
		}

		/// <summary>
		/// Decodes an HTML-encoded <see cref="string"/> and sends the decoded <see cref="string"/>
		/// to the specified <see cref="TextWriter"/>.
		/// </summary>
		/// <param name="s">
		/// A <see cref="string"/> to decode.
		/// </param>
		/// <param name="output">
		/// A <see cref="TextWriter"/> that receives the decoded string.
		/// </param>
		public static void HtmlDecode(string s, TextWriter output)
		{
			if (output == null)
				throw new ArgumentNullException(nameof(output));

			output.Write(HtmlDecode(s));
		}

		/// <summary>
		/// HTML-encodes a <see cref="string"/> and returns the encoded <see cref="string"/>.
		/// </summary>
		/// <returns>
		/// A <see cref="string"/> that represents the encoded string.
		/// </returns>
		/// <param name="s">
		/// A <see cref="string"/> to encode.
		/// </param>
		public static string HtmlEncode(string s)
		{
			if (string.IsNullOrEmpty(s))
				return s;

			var needEncode = false;
			foreach (var c in s)
			{
				if (c == '&' || c == '"' || c == '<' || c == '>' || c > 159)
				{
					needEncode = true;
					break;
				}
			}

			if (!needEncode)
				return s;

			var output = new StringBuilder();
			foreach (var c in s)
			{
				if (c == '&')
				{
					output.Append("&amp;");
				}
				else if (c == '"')
				{
					output.Append("&quot;");
				}
				else if (c == '<')
				{
					output.Append("&lt;");
				}
				else if (c == '>')
				{
					output.Append("&gt;");
				}
				else if (c > 159)
				{
					// MS starts encoding with &# from 160 and stops at 255.
					// We don't do that. One reason is the 65308/65310 unicode
					// characters that look like '<' and '>'.
					output.Append("&#");
					output.Append(((int)c).ToString(CultureInfo.InvariantCulture));
					output.Append(";");
				}
				else
				{
					output.Append(c);
				}
			}

			return output.ToString();
		}

		/// <summary>
		/// HTML-encodes a <see cref="string"/> and sends the encoded <see cref="string"/>
		/// to the specified <see cref="TextWriter"/>.
		/// </summary>
		/// <param name="s">
		/// A <see cref="string"/> to encode.
		/// </param>
		/// <param name="output">
		/// A <see cref="TextWriter"/> that receives the encoded string.
		/// </param>
		public static void HtmlEncode(string s, TextWriter output)
		{
			if (output == null)
				throw new ArgumentNullException(nameof(output));

			output.Write(HtmlEncode(s));
		}

		public static NameValueCollection ParseQueryString(string query)
		{
			return ParseQueryString(query, Encoding.UTF8);
		}

		public static NameValueCollection ParseQueryString(string query, Encoding encoding)
		{
			if (query == null)
				throw new ArgumentNullException(nameof(query));

			return InternalParseQueryString(query, encoding ?? Encoding.UTF8);
		}

		public static string UrlDecode(string s)
		{
			return UrlDecode(s, Encoding.UTF8);
		}

		public static string UrlDecode(string s, Encoding encoding)
		{
			if (string.IsNullOrEmpty(s) || !s.Contains('%', '+'))
				return s;

			if (encoding == null)
				encoding = Encoding.UTF8;

			var buff = new List<byte>();
			var len = s.Length;
			for (var i = 0; i < len; i++)
			{
				var c = s[i];
				if (c == '%' && i + 2 < len && s[i + 1] != '%')
				{
					int xchar;
					if (s[i + 1] == 'u' && i + 5 < len)
					{
						// Unicode hex sequence.
						xchar = getChar(s, i + 2, 4);
						if (xchar != -1)
						{
							writeCharBytes((char)xchar, buff, encoding);
							i += 5;
						}
						else
						{
							writeCharBytes('%', buff, encoding);
						}
					}
					else if ((xchar = getChar(s, i + 1, 2)) != -1)
					{
						writeCharBytes((char)xchar, buff, encoding);
						i += 2;
					}
					else
					{
						writeCharBytes('%', buff, encoding);
					}

					continue;
				}

				if (c == '+')
				{
					writeCharBytes(' ', buff, encoding);
					continue;
				}

				writeCharBytes(c, buff, encoding);
			}

			return encoding.GetString(buff.ToArray());
		}

		public static string UrlDecode(byte[] bytes, Encoding encoding)
		{
			int len;
			return bytes == null
				   ? null
				   : (len = bytes.Length) == 0
					 ? string.Empty
					 : InternalUrlDecode(bytes, 0, len, encoding ?? Encoding.UTF8);
		}

		public static string UrlDecode(byte[] bytes, int offset, int count, Encoding encoding)
		{
			if (bytes == null)
				return null;

			var len = bytes.Length;
			if (len == 0 || count == 0)
				return string.Empty;

			if (offset < 0 || offset >= len)
				throw new ArgumentOutOfRangeException(nameof(offset));

			if (count < 0 || count > len - offset)
				throw new ArgumentOutOfRangeException(nameof(count));

			return InternalUrlDecode(bytes, offset, count, encoding ?? Encoding.UTF8);
		}

		public static byte[] UrlDecodeToBytes(byte[] bytes)
		{
			int len;
			return bytes != null && (len = bytes.Length) > 0
				   ? InternalUrlDecodeToBytes(bytes, 0, len)
				   : bytes;
		}

		public static byte[] UrlDecodeToBytes(string s)
		{
			return UrlDecodeToBytes(s, Encoding.UTF8);
		}

		public static byte[] UrlDecodeToBytes(string s, Encoding encoding)
		{
			if (s == null)
				return null;

			if (s.Length == 0)
				return new byte[0];

			var bytes = (encoding ?? Encoding.UTF8).GetBytes(s);
			return InternalUrlDecodeToBytes(bytes, 0, bytes.Length);
		}

		public static byte[] UrlDecodeToBytes(byte[] bytes, int offset, int count)
		{
			int len;
			if (bytes == null || (len = bytes.Length) == 0)
				return bytes;

			if (count == 0)
				return new byte[0];

			if (offset < 0 || offset >= len)
				throw new ArgumentOutOfRangeException(nameof(offset));

			if (count < 0 || count > len - offset)
				throw new ArgumentOutOfRangeException(nameof(count));

			return InternalUrlDecodeToBytes(bytes, offset, count);
		}

		public static string UrlEncode(byte[] bytes)
		{
			int len;
			return bytes == null
				   ? null
				   : (len = bytes.Length) == 0
					 ? string.Empty
					 : Encoding.ASCII.GetString(InternalUrlEncodeToBytes(bytes, 0, len));
		}

		public static string UrlEncode(string s)
		{
			return UrlEncode(s, Encoding.UTF8);
		}

		public static string UrlEncode(string s, Encoding encoding)
		{
			int len;
			if (s == null || (len = s.Length) == 0)
				return s;

			var needEncode = false;
			foreach (var c in s)
			{
				if ((c < '0') || (c < 'A' && c > '9') || (c > 'Z' && c < 'a') || (c > 'z'))
				{
					if (notEncoded(c))
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
			var bytes = new byte[encoding.GetMaxByteCount(len)];
			var realLen = encoding.GetBytes(s, 0, len, bytes, 0);

			return Encoding.ASCII.GetString(InternalUrlEncodeToBytes(bytes, 0, realLen));
		}

		public static string UrlEncode(byte[] bytes, int offset, int count)
		{
			var encoded = UrlEncodeToBytes(bytes, offset, count);
			return encoded == null
				   ? null
				   : encoded.Length == 0
					 ? string.Empty
					 : Encoding.ASCII.GetString(encoded);
		}

		public static byte[] UrlEncodeToBytes(byte[] bytes)
		{
			int len;
			return bytes != null && (len = bytes.Length) > 0
				   ? InternalUrlEncodeToBytes(bytes, 0, len)
				   : bytes;
		}

		public static byte[] UrlEncodeToBytes(string s)
		{
			return UrlEncodeToBytes(s, Encoding.UTF8);
		}

		public static byte[] UrlEncodeToBytes(string s, Encoding encoding)
		{
			if (s == null)
				return null;

			if (s.Length == 0)
				return new byte[0];

			var bytes = (encoding ?? Encoding.UTF8).GetBytes(s);
			return InternalUrlEncodeToBytes(bytes, 0, bytes.Length);
		}

		public static byte[] UrlEncodeToBytes(byte[] bytes, int offset, int count)
		{
			int len;
			if (bytes == null || (len = bytes.Length) == 0)
				return bytes;

			if (count == 0)
				return new byte[0];

			if (offset < 0 || offset >= len)
				throw new ArgumentOutOfRangeException(nameof(offset));

			if (count < 0 || count > len - offset)
				throw new ArgumentOutOfRangeException(nameof(count));

			return InternalUrlEncodeToBytes(bytes, offset, count);
		}

		public static string UrlEncodeUnicode(string s)
		{
			return !string.IsNullOrEmpty(s)
				   ? Encoding.ASCII.GetString(InternalUrlEncodeUnicodeToBytes(s))
				   : s;
		}

		public static byte[] UrlEncodeUnicodeToBytes(string s)
		{
			return s == null
				   ? null
				   : s.Length == 0
					 ? new byte[0]
					 : InternalUrlEncodeUnicodeToBytes(s);
		}

		public static string UrlPathEncode(string s)
		{
			if (string.IsNullOrEmpty(s))
				return s;

			using (var res = new MemoryStream())
			{
				foreach (var c in s)
					urlPathEncode(c, res);

				res.Close();
				return Encoding.ASCII.GetString(res.ToArray());
			}
		}
	}
}
