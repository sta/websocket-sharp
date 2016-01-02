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

/*
 * Authors:
 * - Patrik Torstensson <Patrik.Torstensson@labs2.com>
 * - Wictor Wilén (decode/encode functions) <wictor@ibizkit.se>
 * - Tim Coleman <tim@timcoleman.com>
 * - Gonzalo Paniagua Javier <gonzalo@ximian.com>
 */

namespace WebSocketSharp.Net
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Globalization;
    using System.IO;
    using System.Text;

    internal sealed class HttpUtility
	{
	    private static Dictionary<string, char> entities;
		private static char[] hexChars = "0123456789abcdef".ToCharArray();
		private static object sync = new object();

	    private static int getChar(byte[] bytes, int offset, int length)
		{
			var val = 0;
			var end = length + offset;
			for (var i = offset; i < end; i++)
			{
				var current = GetInt(bytes[i]);
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

				var current = GetInt((byte)c);
				if (current == -1)
					return -1;

				val = (val << 4) + current;
			}

			return val;
		}
        
		private static int GetInt(byte b)
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
        
		private static bool NotEncoded(char c)
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
				result.WriteByte((byte)hexChars[idx]);

				idx = (i >> 8) & 0x0F;
				result.WriteByte((byte)hexChars[idx]);

				idx = (i >> 4) & 0x0F;
				result.WriteByte((byte)hexChars[idx]);

				idx = i & 0x0F;
				result.WriteByte((byte)hexChars[idx]);

				return;
			}

			if (c > ' ' && NotEncoded(c))
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
				result.WriteByte((byte)hexChars[idx]);

				idx = i & 0x0F;
				result.WriteByte((byte)hexChars[idx]);

				return;
			}

			result.WriteByte((byte)c);
		}
        
		private static void WriteCharBytes(char c, IList buffer, Encoding encoding)
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
	        {
	            return null;
	        }

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
				host = string.Format("{0}:{1}", host, schm == "http" || schm == "ws" ? 80 : 443);

			var url = string.Format("{0}://{1}{2}", schm, host, path);

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
        
		public static string UrlDecode(string s)
		{
			return UrlDecode(s, Encoding.UTF8);
		}

		public static string UrlDecode(string s, Encoding encoding)
		{
			if (s == null || s.Length == 0 || !s.Contains('%', '+'))
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
							WriteCharBytes((char)xchar, buff, encoding);
							i += 5;
						}
						else
						{
							WriteCharBytes('%', buff, encoding);
						}
					}
					else if ((xchar = getChar(s, i + 1, 2)) != -1)
					{
						WriteCharBytes((char)xchar, buff, encoding);
						i += 2;
					}
					else
					{
						WriteCharBytes('%', buff, encoding);
					}

					continue;
				}

				if (c == '+')
				{
					WriteCharBytes(' ', buff, encoding);
					continue;
				}

				WriteCharBytes(c, buff, encoding);
			}

			return encoding.GetString(buff.ToArray());
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
					if (NotEncoded(c))
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
	}
}
