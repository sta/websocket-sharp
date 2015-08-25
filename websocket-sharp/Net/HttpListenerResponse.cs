#region License
/*
 * HttpListenerResponse.cs
 *
 * This code is derived from HttpListenerResponse.cs (System.Net) of Mono
 * (http://www.mono-project.com).
 *
 * The MIT License
 *
 * Copyright (c) 2005 Novell, Inc. (http://www.novell.com)
 * Copyright (c) 2012-2015 sta.blockhead
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
 * - Gonzalo Paniagua Javier <gonzalo@novell.com>
 */
#endregion

#region Contributors
/*
 * Contributors:
 * - Nicholas Devenish
 */
#endregion

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace WebSocketSharp.Net
{
  /// <summary>
  /// Provides the access to a response to a request received by the <see cref="HttpListener"/>.
  /// </summary>
  /// <remarks>
  /// The HttpListenerResponse class cannot be inherited.
  /// </remarks>
  public sealed class HttpListenerResponse : IDisposable
  {
    #region Private Fields

    private bool                _closeConnection;
    private Encoding            _contentEncoding;
    private long                _contentLength;
    private string              _contentType;
    private HttpListenerContext _context;
    private CookieCollection    _cookies;
    private bool                _disposed;
    private WebHeaderCollection _headers;
    private bool                _headersSent;
    private bool                _keepAlive;
    private string              _location;
    private ResponseStream      _outputStream;
    private bool                _sendChunked;
    private int                 _statusCode;
    private string              _statusDescription;
    private Version             _version;

    #endregion

    #region Internal Constructors

    internal HttpListenerResponse (HttpListenerContext context)
    {
      _context = context;
      _keepAlive = true;
      _statusCode = 200;
      _statusDescription = "OK";
      _version = HttpVersion.Version11;
    }

    #endregion

    #region Internal Properties

    internal bool CloseConnection {
      get {
        return _closeConnection;
      }

      set {
        _closeConnection = value;
      }
    }

    internal bool HeadersSent {
      get {
        return _headersSent;
      }

      set {
        _headersSent = value;
      }
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets or sets the encoding for the entity body data included in the response.
    /// </summary>
    /// <value>
    /// A <see cref="Encoding"/> that represents the encoding for the entity body data,
    /// or <see langword="null"/> if no encoding is specified.
    /// </value>
    /// <exception cref="ObjectDisposedException">
    /// This object is closed.
    /// </exception>
    public Encoding ContentEncoding {
      get {
        return _contentEncoding;
      }

      set {
        checkDisposed ();
        _contentEncoding = value;
      }
    }

    /// <summary>
    /// Gets or sets the number of bytes in the entity body data included in the response.
    /// </summary>
    /// <value>
    /// A <see cref="long"/> that represents the value of the Content-Length entity-header.
    /// </value>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value specified for a set operation is less than zero.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The response has already been sent.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This object is closed.
    /// </exception>
    public long ContentLength64 {
      get {
        return _contentLength;
      }

      set {
        checkDisposedOrHeadersSent ();
        if (value < 0)
          throw new ArgumentOutOfRangeException ("Less than zero.", "value");

        _contentLength = value;
      }
    }

    /// <summary>
    /// Gets or sets the media type of the entity body included in the response.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the media type of the entity body,
    /// or <see langword="null"/> if no media type is specified. This value is
    /// used for the value of the Content-Type entity-header.
    /// </value>
    /// <exception cref="ArgumentException">
    /// The value specified for a set operation is empty.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This object is closed.
    /// </exception>
    public string ContentType {
      get {
        return _contentType;
      }

      set {
        checkDisposed ();
        if (value != null && value.Length == 0)
          throw new ArgumentException ("An empty string.", "value");

        _contentType = value;
      }
    }

    /// <summary>
    /// Gets or sets the cookies sent with the response.
    /// </summary>
    /// <value>
    /// A <see cref="CookieCollection"/> that contains the cookies sent with the response.
    /// </value>
    public CookieCollection Cookies {
      get {
        return _cookies ?? (_cookies = new CookieCollection ());
      }

      set {
        _cookies = value;
      }
    }

    /// <summary>
    /// Gets or sets the HTTP headers sent to the client.
    /// </summary>
    /// <value>
    /// A <see cref="WebHeaderCollection"/> that contains the headers sent to the client.
    /// </value>
    /// <exception cref="InvalidOperationException">
    /// The value specified for a set operation isn't valid for a response.
    /// </exception>
    public WebHeaderCollection Headers {
      get {
        return _headers ?? (_headers = new WebHeaderCollection (HttpHeaderType.Response, false));
      }

      set {
        if (value != null && value.State != HttpHeaderType.Response)
          throw new InvalidOperationException (
            "The specified headers aren't valid for a response.");

        _headers = value;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the server requests a persistent connection.
    /// </summary>
    /// <value>
    /// <c>true</c> if the server requests a persistent connection; otherwise, <c>false</c>.
    /// The default value is <c>true</c>.
    /// </value>
    /// <exception cref="InvalidOperationException">
    /// The response has already been sent.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This object is closed.
    /// </exception>
    public bool KeepAlive {
      get {
        return _keepAlive;
      }

      set {
        checkDisposedOrHeadersSent ();
        _keepAlive = value;
      }
    }

    /// <summary>
    /// Gets a <see cref="Stream"/> to use to write the entity body data.
    /// </summary>
    /// <value>
    /// A <see cref="Stream"/> to use to write the entity body data.
    /// </value>
    /// <exception cref="ObjectDisposedException">
    /// This object is closed.
    /// </exception>
    public Stream OutputStream {
      get {
        checkDisposed ();
        return _outputStream ?? (_outputStream = _context.Connection.GetResponseStream ());
      }
    }

    /// <summary>
    /// Gets or sets the HTTP version used in the response.
    /// </summary>
    /// <value>
    /// A <see cref="Version"/> that represents the version used in the response.
    /// </value>
    /// <exception cref="ArgumentNullException">
    /// The value specified for a set operation is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// The value specified for a set operation doesn't have its <c>Major</c> property set to 1 or
    /// doesn't have its <c>Minor</c> property set to either 0 or 1.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The response has already been sent.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This object is closed.
    /// </exception>
    public Version ProtocolVersion {
      get {
        return _version;
      }

      set {
        checkDisposedOrHeadersSent ();
        if (value == null)
          throw new ArgumentNullException ("value");

        if (value.Major != 1 || (value.Minor != 0 && value.Minor != 1))
          throw new ArgumentException ("Not 1.0 or 1.1.", "value");

        _version = value;
      }
    }

    /// <summary>
    /// Gets or sets the URL to which the client is redirected to locate a requested resource.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the value of the Location response-header,
    /// or <see langword="null"/> if no redirect location is specified.
    /// </value>
    /// <exception cref="ArgumentException">
    /// The value specified for a set operation isn't an absolute URL.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This object is closed.
    /// </exception>
    public string RedirectLocation {
      get {
        return _location;
      }

      set {
        checkDisposed ();
        if (value == null) {
          _location = null;
          return;
        }

        Uri uri = null;
        if (!value.MaybeUri () || !Uri.TryCreate (value, UriKind.Absolute, out uri))
          throw new ArgumentException ("Not an absolute URL.", "value");

        _location = value;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the response uses the chunked transfer encoding.
    /// </summary>
    /// <value>
    /// <c>true</c> if the response uses the chunked transfer encoding;
    /// otherwise, <c>false</c>. The default value is <c>false</c>.
    /// </value>
    /// <exception cref="InvalidOperationException">
    /// The response has already been sent.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This object is closed.
    /// </exception>
    public bool SendChunked {
      get {
        return _sendChunked;
      }

      set {
        checkDisposedOrHeadersSent ();
        _sendChunked = value;
      }
    }

    /// <summary>
    /// Gets or sets the HTTP status code returned to the client.
    /// </summary>
    /// <value>
    /// An <see cref="int"/> that represents the status code for the response to
    /// the request. The default value is same as <see cref="HttpStatusCode.OK"/>.
    /// </value>
    /// <exception cref="InvalidOperationException">
    /// The response has already been sent.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This object is closed.
    /// </exception>
    /// <exception cref="System.Net.ProtocolViolationException">
    /// The value specified for a set operation is invalid. Valid values are
    /// between 100 and 999 inclusive.
    /// </exception>
    public int StatusCode {
      get {
        return _statusCode;
      }

      set {
        checkDisposedOrHeadersSent ();
        if (value < 100 || value > 999)
          throw new System.Net.ProtocolViolationException (
            "A value isn't between 100 and 999 inclusive.");

        _statusCode = value;
        _statusDescription = value.GetStatusDescription ();
      }
    }

    /// <summary>
    /// Gets or sets the description of the HTTP status code returned to the client.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the description of the status code. The default
    /// value is the <see href="http://tools.ietf.org/html/rfc2616#section-10">RFC 2616</see>
    /// description for the <see cref="HttpListenerResponse.StatusCode"/> property value,
    /// or <see cref="String.Empty"/> if an RFC 2616 description doesn't exist.
    /// </value>
    /// <exception cref="ArgumentException">
    /// The value specified for a set operation contains invalid characters.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The response has already been sent.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This object is closed.
    /// </exception>
    public string StatusDescription {
      get {
        return _statusDescription;
      }

      set {
        checkDisposedOrHeadersSent ();
        if (value == null || value.Length == 0) {
          _statusDescription = _statusCode.GetStatusDescription ();
          return;
        }

        if (!value.IsText () || value.IndexOfAny (new[] { '\r', '\n' }) > -1)
          throw new ArgumentException ("Contains invalid characters.", "value");

        _statusDescription = value;
      }
    }

    #endregion

    #region Private Methods

    private bool canAddOrUpdate (Cookie cookie)
    {
      if (_cookies == null || _cookies.Count == 0)
        return true;

      var found = findCookie (cookie).ToList ();
      if (found.Count == 0)
        return true;

      var ver = cookie.Version;
      foreach (var c in found)
        if (c.Version == ver)
          return true;

      return false;
    }

    private void checkDisposed ()
    {
      if (_disposed)
        throw new ObjectDisposedException (GetType ().ToString ());
    }

    private void checkDisposedOrHeadersSent ()
    {
      if (_disposed)
        throw new ObjectDisposedException (GetType ().ToString ());

      if (_headersSent)
        throw new InvalidOperationException ("Cannot be changed after the headers are sent.");
    }

    private void close (bool force)
    {
      _disposed = true;
      _context.Connection.Close (force);
    }

    private IEnumerable<Cookie> findCookie (Cookie cookie)
    {
      var name = cookie.Name;
      var domain = cookie.Domain;
      var path = cookie.Path;
      if (_cookies != null)
        foreach (Cookie c in _cookies)
          if (c.Name.Equals (name, StringComparison.OrdinalIgnoreCase) &&
              c.Domain.Equals (domain, StringComparison.OrdinalIgnoreCase) &&
              c.Path.Equals (path, StringComparison.Ordinal))
            yield return c;
    }

    #endregion

    #region Internal Methods

    internal WebHeaderCollection WriteHeadersTo (MemoryStream destination)
    {
      var headers = new WebHeaderCollection (HttpHeaderType.Response, true);
      if (_headers != null)
        headers.Add (_headers);

      if (_contentType != null) {
        var type = _contentType.IndexOf ("charset=", StringComparison.Ordinal) == -1 &&
                   _contentEncoding != null
                   ? String.Format ("{0}; charset={1}", _contentType, _contentEncoding.WebName)
                   : _contentType;

        headers.InternalSet ("Content-Type", type, true);
      }

      if (headers["Server"] == null)
        headers.InternalSet ("Server", "websocket-sharp/1.0", true);

      var prov = CultureInfo.InvariantCulture;
      if (headers["Date"] == null)
        headers.InternalSet ("Date", DateTime.UtcNow.ToString ("r", prov), true);

      if (!_sendChunked)
        headers.InternalSet ("Content-Length", _contentLength.ToString (prov), true);
      else
        headers.InternalSet ("Transfer-Encoding", "chunked", true);

      /*
       * Apache forces closing the connection for these status codes:
       * - 400 Bad Request
       * - 408 Request Timeout
       * - 411 Length Required
       * - 413 Request Entity Too Large
       * - 414 Request-Uri Too Long
       * - 500 Internal Server Error
       * - 503 Service Unavailable
       */
      var closeConn = !_context.Request.KeepAlive ||
                      !_keepAlive ||
                      _statusCode == 400 ||
                      _statusCode == 408 ||
                      _statusCode == 411 ||
                      _statusCode == 413 ||
                      _statusCode == 414 ||
                      _statusCode == 500 ||
                      _statusCode == 503;

      var reuses = _context.Connection.Reuses;
      if (closeConn || reuses >= 100) {
        headers.InternalSet ("Connection", "close", true);
      }
      else {
        headers.InternalSet (
          "Keep-Alive", String.Format ("timeout=15,max={0}", 100 - reuses), true);

        if (_context.Request.ProtocolVersion < HttpVersion.Version11)
          headers.InternalSet ("Connection", "keep-alive", true);
      }

      if (_location != null)
        headers.InternalSet ("Location", _location, true);

      if (_cookies != null)
        foreach (Cookie cookie in _cookies)
          headers.InternalSet ("Set-Cookie", cookie.ToResponseString (), true);

      var enc = _contentEncoding ?? Encoding.Default;
      var writer = new StreamWriter (destination, enc, 256);
      writer.Write ("HTTP/{0} {1} {2}\r\n", _version, _statusCode, _statusDescription);
      writer.Write (headers.ToStringMultiValue (true));
      writer.Flush ();

      // Assumes that the destination was at position 0.
      destination.Position = enc.GetPreamble ().Length;

      return headers;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Closes the connection to the client without returning a response.
    /// </summary>
    public void Abort ()
    {
      if (_disposed)
        return;

      close (true);
    }

    /// <summary>
    /// Adds an HTTP header with the specified <paramref name="name"/> and
    /// <paramref name="value"/> to the headers for the response.
    /// </summary>
    /// <param name="name">
    /// A <see cref="string"/> that represents the name of the header to add.
    /// </param>
    /// <param name="value">
    /// A <see cref="string"/> that represents the value of the header to add.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> is <see langword="null"/> or empty.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="name"/> or <paramref name="value"/> contains invalid characters.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="name"/> is a restricted header name.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The length of <paramref name="value"/> is greater than 65,535 characters.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The header cannot be allowed to add to the current headers.
    /// </exception>
    public void AddHeader (string name, string value)
    {
      Headers.Set (name, value);
    }

    /// <summary>
    /// Appends the specified <paramref name="cookie"/> to the cookies sent with the response.
    /// </summary>
    /// <param name="cookie">
    /// A <see cref="Cookie"/> to append.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="cookie"/> is <see langword="null"/>.
    /// </exception>
    public void AppendCookie (Cookie cookie)
    {
      Cookies.Add (cookie);
    }

    /// <summary>
    /// Appends a <paramref name="value"/> to the specified HTTP header sent with the response.
    /// </summary>
    /// <param name="name">
    /// A <see cref="string"/> that represents the name of the header to append
    /// <paramref name="value"/> to.
    /// </param>
    /// <param name="value">
    /// A <see cref="string"/> that represents the value to append to the header.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> is <see langword="null"/> or empty.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///   <para>
    ///   <paramref name="name"/> or <paramref name="value"/> contains invalid characters.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   <paramref name="name"/> is a restricted header name.
    ///   </para>
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The length of <paramref name="value"/> is greater than 65,535 characters.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The current headers cannot allow the header to append a value.
    /// </exception>
    public void AppendHeader (string name, string value)
    {
      Headers.Add (name, value);
    }

    /// <summary>
    /// Returns the response to the client and releases the resources used by
    /// this <see cref="HttpListenerResponse"/> instance.
    /// </summary>
    public void Close ()
    {
      if (_disposed)
        return;

      close (false);
    }

    /// <summary>
    /// Returns the response with the specified array of <see cref="byte"/> to the client and
    /// releases the resources used by this <see cref="HttpListenerResponse"/> instance.
    /// </summary>
    /// <param name="responseEntity">
    /// An array of <see cref="byte"/> that contains the response entity body data.
    /// </param>
    /// <param name="willBlock">
    /// <c>true</c> if this method blocks execution while flushing the stream to the client;
    /// otherwise, <c>false</c>.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="responseEntity"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This object is closed.
    /// </exception>
    public void Close (byte[] responseEntity, bool willBlock)
    {
      checkDisposed ();
      if (responseEntity == null)
        throw new ArgumentNullException ("responseEntity");

      var len = responseEntity.Length;
      var output = OutputStream;
      if (willBlock) {
        output.Write (responseEntity, 0, len);
        close (false);

        return;
      }

      output.BeginWrite (
        responseEntity,
        0,
        len,
        ar => {
          output.EndWrite (ar);
          close (false);
        },
        null);
    }

    /// <summary>
    /// Copies some properties from the specified <see cref="HttpListenerResponse"/> to
    /// this response.
    /// </summary>
    /// <param name="templateResponse">
    /// A <see cref="HttpListenerResponse"/> to copy.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="templateResponse"/> is <see langword="null"/>.
    /// </exception>
    public void CopyFrom (HttpListenerResponse templateResponse)
    {
      if (templateResponse == null)
        throw new ArgumentNullException ("templateResponse");

      if (templateResponse._headers != null) {
        if (_headers != null)
          _headers.Clear ();

        Headers.Add (templateResponse._headers);
      }
      else if (_headers != null) {
        _headers = null;
      }

      _contentLength = templateResponse._contentLength;
      _statusCode = templateResponse._statusCode;
      _statusDescription = templateResponse._statusDescription;
      _keepAlive = templateResponse._keepAlive;
      _version = templateResponse._version;
    }

    /// <summary>
    /// Configures the response to redirect the client's request to
    /// the specified <paramref name="url"/>.
    /// </summary>
    /// <remarks>
    /// This method sets the <see cref="HttpListenerResponse.RedirectLocation"/> property to
    /// <paramref name="url"/>, the <see cref="HttpListenerResponse.StatusCode"/> property to
    /// <c>302</c>, and the <see cref="HttpListenerResponse.StatusDescription"/> property to
    /// <c>"Found"</c>.
    /// </remarks>
    /// <param name="url">
    /// A <see cref="string"/> that represents the URL to redirect the client's request to.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="url"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="url"/> isn't an absolute URL.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The response has already been sent.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This object is closed.
    /// </exception>
    public void Redirect (string url)
    {
      checkDisposedOrHeadersSent ();
      if (url == null)
        throw new ArgumentNullException ("url");

      Uri uri = null;
      if (!url.MaybeUri () || !Uri.TryCreate (url, UriKind.Absolute, out uri))
        throw new ArgumentException ("Not an absolute URL.", "url");

      _location = url;
      _statusCode = 302;
      _statusDescription = "Found";
    }

    /// <summary>
    /// Adds or updates a <paramref name="cookie"/> in the cookies sent with the response.
    /// </summary>
    /// <param name="cookie">
    /// A <see cref="Cookie"/> to set.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="cookie"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="cookie"/> already exists in the cookies and couldn't be replaced.
    /// </exception>
    public void SetCookie (Cookie cookie)
    {
      if (cookie == null)
        throw new ArgumentNullException ("cookie");

      if (!canAddOrUpdate (cookie))
        throw new ArgumentException ("Cannot be replaced.", "cookie");

      Cookies.Add (cookie);
    }

    #endregion

    #region Explicit Interface Implementations

    /// <summary>
    /// Releases all resources used by the <see cref="HttpListenerResponse"/>.
    /// </summary>
    void IDisposable.Dispose ()
    {
      if (_disposed)
        return;

      close (true); // Same as the Abort method.
    }

    #endregion
  }
}
