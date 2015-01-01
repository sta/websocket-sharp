#region License
/*
 * QueryStringCollection.cs
 *
 * This code is derived from System.Net.HttpUtility.cs of Mono
 * (http://www.mono-project.com).
 *
 * The MIT License
 *
 * Copyright (c) 2005-2009 Novell, Inc. (http://www.novell.com)
 * Copyright (c) 2014 sta.blockhead
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

namespace WebSocketSharp.Net
{
  internal sealed class QueryStringCollection : NameValueCollection
  {
    public override string ToString ()
    {
      var cnt = Count;
      if (cnt == 0)
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
}
