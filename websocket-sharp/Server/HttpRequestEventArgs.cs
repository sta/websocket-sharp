#region License
/*
 * HttpRequestEventArgs.cs
 *
 * The MIT License
 *
 * Copyright (c) 2012-2013 sta.blockhead
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
using WebSocketSharp.Net;

namespace WebSocketSharp.Server {

  /// <summary>
  /// Contains the event data associated with the HTTP request events of the <see cref="HttpServer"/> class.
  /// </summary>
  /// <remarks>
  /// An HTTP request event occurs when a <see cref="HttpServer"/> instance receives an HTTP request.
  /// If you want to get the HTTP request objects, you should access the <see cref="Request"/> property.
  /// If you want to get the HTTP response objects to send, you should access the <see cref="Response"/> property.
  /// </remarks>
  public class HttpRequestEventArgs : EventArgs
  {
    #region Internal Constructors

    internal HttpRequestEventArgs(HttpListenerContext context)
    {
      Request = context.Request;
      Response = context.Response;
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the HTTP request objects sent from a client.
    /// </summary>
    /// <value>
    /// A <see cref="HttpListenerRequest"/> that contains the HTTP request objects.
    /// </value>
    public HttpListenerRequest Request { get; private set; }

    /// <summary>
    /// Gets the HTTP response objects to send to the client in response to the client's request.
    /// </summary>
    /// <value>
    /// A <see cref="HttpListenerResponse"/> that contains the HTTP response objects.
    /// </value>
    public HttpListenerResponse Response { get; private set; }

    #endregion
  }
}
