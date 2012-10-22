#region MIT License
/**
 * HttpListenerWebSocketContext.cs
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

  public class HttpListenerWebSocketContext : WebSocketContext
  {
    private HttpListenerContext _context;
    private WebSocket           _socket;
    private WsStream            _stream;

    internal HttpListenerWebSocketContext(HttpListenerContext context)
    {
      _context = context;
      _stream  = WsStream.CreateServerStream(context);
      _socket  = new WebSocket(this);
    }

    internal HttpListenerContext BaseContext {
      get {
        return _context;
      }
    }

    internal WsStream Stream {
      get {
        return _stream;
      }
    }

    public override CookieCollection CookieCollection {
      get {
        return _context.Request.Cookies;
      }
    }

    public override NameValueCollection Headers {
      get {
        return _context.Request.Headers;
      }
    }

    public override bool IsAuthenticated {
      get {
        return _context.Request.IsAuthenticated;
      }
    }

    public override bool IsSecureConnection {
      get {
        return _context.Request.IsSecureConnection;
      }
    }

    public override bool IsLocal {
      get {
        return _context.Request.IsLocal;
      }
    }

    public override string Origin {
      get {
        return Headers["Origin"];
      }
    }

    public virtual string Path {
      get {
        return RequestUri.GetAbsolutePath();
      }
    }

    public override Uri RequestUri {
      get {
        return _context.Request.RawUrl.ToUri();
      }
    }

    public override string SecWebSocketKey {
      get {
        return Headers["Sec-WebSocket-Key"];
      }
    }

    public override IEnumerable<string> SecWebSocketProtocols {
      get {
        return Headers.GetValues("Sec-WebSocket-Protocol");
      }
    }

    public override string SecWebSocketVersion {
      get {
        return Headers["Sec-WebSocket-Version"];
      }
    }

    public virtual System.Net.IPEndPoint ServerEndPoint {
      get {
        return _context.Connection.LocalEndPoint;
      }
    }

    public override IPrincipal User {
      get {
        return _context.User;
      }
    }

    public virtual System.Net.IPEndPoint UserEndPoint {
      get {
        return _context.Connection.RemoteEndPoint;
      }
    }

    public override WebSocket WebSocket {
      get {
        return _socket;
      }
    }
  }
}
