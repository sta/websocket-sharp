#region License
/*
 * HttpHeaderInfo.cs
 *
 * The MIT License
 *
 * Copyright (c) 2013-2014 sta.blockhead
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

namespace WebSocketSharp.Net
{
  internal class HttpHeaderInfo
  {
    #region Private Fields

    private string         _name;
    private HttpHeaderType _type;

    #endregion

    #region Internal Constructors

    internal HttpHeaderInfo (string name, HttpHeaderType type)
    {
      _name = name;
      _type = type;
    }

    #endregion

    #region Internal Properties

    internal bool IsMultiValueInRequest {
      get {
        return (_type & HttpHeaderType.MultiValueInRequest) == HttpHeaderType.MultiValueInRequest;
      }
    }

    internal bool IsMultiValueInResponse {
      get {
        return (_type & HttpHeaderType.MultiValueInResponse) == HttpHeaderType.MultiValueInResponse;
      }
    }

    #endregion

    #region Public Properties

    public bool IsRequest {
      get {
        return (_type & HttpHeaderType.Request) == HttpHeaderType.Request;
      }
    }

    public bool IsResponse {
      get {
        return (_type & HttpHeaderType.Response) == HttpHeaderType.Response;
      }
    }

    public string Name {
      get {
        return _name;
      }
    }

    public HttpHeaderType Type {
      get {
        return _type;
      }
    }

    #endregion

    #region Public Methods

    public bool IsMultiValue (bool response)
    {
      return (_type & HttpHeaderType.MultiValue) == HttpHeaderType.MultiValue
             ? (response ? IsResponse : IsRequest)
             : (response ? IsMultiValueInResponse : IsMultiValueInRequest);
    }

    public bool IsRestricted (bool response)
    {
      return (_type & HttpHeaderType.Restricted) == HttpHeaderType.Restricted
             ? (response ? IsResponse : IsRequest)
             : false;
    }

    #endregion
  }
}
