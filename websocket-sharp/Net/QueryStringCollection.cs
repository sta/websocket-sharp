#region License
/*
 * QueryStringCollection.cs
 *
 * This code is derived from HttpUtility.cs (System.Net) of Mono
 * (http://www.mono-project.com).
 *
 * The MIT License
 *
 * Copyright (c) 2005-2009 Novell, Inc. (http://www.novell.com)
 * Copyright (c) 2018 sta.blockhead
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
using System.Collections.Specialized;
using System.Text;

namespace WebSocketSharp.Net {
    internal sealed class QueryStringCollection : NameValueCollection {
        #region Public Constructors

        public QueryStringCollection() {
        }

        public QueryStringCollection(int capacity)
          : base(capacity) {
        }

        #endregion

        #region Private Methods

        private static string urlDecode(string s, Encoding encoding) {
            return s.IndexOfAny(new[] { '%', '+' }) > -1
                   ? HttpUtility.UrlDecode(s, encoding)
                   : s;
        }

        #endregion

        #region Public Methods

        public static QueryStringCollection Parse(string query) {
            return Parse(query, Encoding.UTF8);
        }

        public static QueryStringCollection Parse(string query, Encoding encoding) {
            if (query == null)
                return new QueryStringCollection(1);

            var len = query.Length;
            if (len == 0)
                return new QueryStringCollection(1);

            if (query == "?")
                return new QueryStringCollection(1);

            if (query[0] == '?')
                query = query.Substring(1);

            if (encoding == null)
                encoding = Encoding.UTF8;

            var ret = new QueryStringCollection();

            var components = query.Split('&');
            foreach (var component in components) {
                len = component.Length;
                if (len == 0)
                    continue;

                if (component == "=")
                    continue;

                var i = component.IndexOf('=');
                if (i < 0) {
                    ret.Add(null, urlDecode(component, encoding));
                    continue;
                }

                if (i == 0) {
                    ret.Add(null, urlDecode(component.Substring(1), encoding));
                    continue;
                }

                var name = urlDecode(component.Substring(0, i), encoding);

                var start = i + 1;
                var val = start < len
                          ? urlDecode(component.Substring(start), encoding)
                          : String.Empty;

                ret.Add(name, val);
            }

            return ret;
        }

        public override string ToString() {
            var buff = new StringBuilder();

            foreach (var key in AllKeys)
                buff.AppendFormat("{0}={1}&", key, this[key]);

            if (buff.Length > 0)
                buff.Length--;

            return buff.ToString();
        }

        #endregion
    }
}
