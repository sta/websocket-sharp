#region License
/*
 * HttpRequestEventArgs.cs
 *
 * The MIT License
 *
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

using System;
using WebSocketSharp.Net;

namespace WebSocketSharp.Server
{
  /// <summary>
  /// Contains the event data associated with an HTTP request event that
  /// the <see cref="HttpServer"/> emits.
  /// </summary>
  /// <remarks>
  ///   <para>
  ///   An HTTP request event occurs when the <see cref="HttpServer"/> receives an HTTP request.
  ///   </para>
  ///   <para>
  ///   If you would like to get the request data, you should access
  ///   the <see cref="HttpRequestEventArgs.Request"/> property.
  ///   </para>
  ///   <para>
  ///   And if you would like to get the data used to return a response, you should access
  ///   the <see cref="HttpRequestEventArgs.Response"/> property.
  ///   </para>
  /// </remarks>
  public class HttpRequestEventArgs : EventArgs
  {
    #region Private Fields

    private HttpListenerRequest  _request;
    private HttpListenerResponse _response;

    #endregion

    #region Internal Constructors

    internal HttpRequestEventArgs (HttpListenerContext context)
    {
      _request = context.Request;
      _response = context.Response;
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the <see cref="HttpListenerRequest"/> that represents the HTTP request sent from
    /// a client.
    /// </summary>
    /// <value>
    /// A <see cref="HttpListenerRequest"/> that represents the request.
    /// </value>
    public HttpListenerRequest Request {
      get {
        return _request;
      }
    }

    /// <summary>
    /// Gets the <see cref="HttpListenerResponse"/> used to return an HTTP response to the client.
    /// </summary>
    /// <value>
    /// A <see cref="HttpListenerResponse"/> used to return a response.
    /// </value>
    public HttpListenerResponse Response {
      get {
        return _response;
      }
    }

    #endregion
  }
}
