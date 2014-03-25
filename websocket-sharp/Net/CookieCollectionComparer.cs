#region License
/*
 * CookieCollectionComparer.cs
 *
 * This code is a separation from CookieCollection.cs.
 *
 * The MIT License
 *
 * Copyright (c) 2004,2009 Novell, Inc. (http://www.novell.com)
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

using System;
using System.Collections.Generic;

namespace WebSocketSharp.Net
{
  internal sealed class CookieCollectionComparer : IComparer<Cookie>
  {
    public int Compare (Cookie x, Cookie y)
    {
      if (x == null && y == null)
        return 0;

      if (x == null)
        return -1;

      if (y == null)
        return 1;

      var c1 = x.Name.Length + x.Value.Length;
      var c2 = y.Name.Length + y.Value.Length;

      return c1 - c2;
    }
  }
}
