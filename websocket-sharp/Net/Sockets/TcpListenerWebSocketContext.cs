#region MIT License
/*
 * TcpListenerWebSocketContext.cs
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
using System.Net;
using System.Net.Sockets;
using System.Security.Principal;

namespace WebSocketSharp.Net.Sockets {

  public class TcpListenerWebSocketContext : WebSocketContext
  {
    private TcpClient        _client;
    private bool             _isSecure;
    private RequestHandshake _request;
    private WebSocket        _socket;
    private WsStream         _stream;

    internal TcpListenerWebSocketContext(TcpClient client, bool secure)
    {
      _client   = client;
      _isSecure = secure;
      _stream   = WsStream.CreateServerStream(client, secure);
      _request  = RequestHandshake.Parse(_stream.ReadHandshake());
      _socket   = new WebSocket(this);
    }

    internal TcpClient Client {
      get {
        return _client;
      }
    }

    internal WsStream Stream {
      get {
        return _stream;
      }
    }

    public override CookieCollection CookieCollection {
      get {
        throw new NotImplementedException();
      }
    }

    public override NameValueCollection Headers {
      get {
        return _request.Headers;
      }
    }

    public override bool IsAuthenticated {
      get {
        throw new NotImplementedException();
      }
    }

    public override bool IsSecureConnection {
      get {
        return _isSecure;
      }
    }

    public override bool IsLocal {
      get {
        throw new NotImplementedException();
      }
    }

    public override string Origin {
      get {
        return Headers["Origin"];
      }
    }

    public virtual string Path {
      get {
        return _request.RequestUri.GetAbsolutePath();
      }
    }

    public override Uri RequestUri {
      get {
        return _request.RequestUri;
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

    public virtual IPEndPoint ServerEndPoint {
      get {
        return (IPEndPoint)_client.Client.LocalEndPoint;
      }
    }

    public override IPrincipal User {
      get {
        throw new NotImplementedException();
      }
    }

    public virtual IPEndPoint UserEndPoint {
      get {
        return (IPEndPoint)_client.Client.RemoteEndPoint;
      }
    }

    public override WebSocket WebSocket {
      get {
        return _socket;
      }
    }
  }
}
