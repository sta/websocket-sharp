#region MIT License
/**
 * WebSocketContext.cs
 *
 * The MIT License
 *
 * Copyright (c) 2012 sta.blockhead
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
using System.Collections.Specialized;
using System.Security.Principal;

namespace WebSocketSharp.Net {

  public abstract class WebSocketContext {

    protected WebSocketContext()
    {
    }

    public abstract CookieCollection    CookieCollection      { get; }
    public abstract NameValueCollection Headers               { get; }
    public abstract bool                IsAuthenticated       { get; }
    public abstract bool                IsSecureConnection    { get; }
    public abstract bool                IsLocal               { get; }
    public abstract string              Origin                { get; }
    public abstract Uri                 RequestUri            { get; }
    public abstract string              SecWebSocketKey       { get; }
    public abstract IEnumerable<string> SecWebSocketProtocols { get; }
    public abstract string              SecWebSocketVersion   { get; }
    public abstract IPrincipal          User                  { get; }
    public abstract WebSocket           WebSocket             { get; }
  }
}
