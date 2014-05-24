#region License
/*
 * HttpListenerResponse.cs
 *
 * This code is derived from System.Net.HttpListenerResponse.cs of Mono
 * (http://www.mono-project.com).
 *
 * The MIT License
 *
 * Copyright (c) 2005 Novell, Inc. (http://www.novell.com)
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

#region Authors
/*
 * Authors:
 * - Gonzalo Paniagua Javier <gonzalo@novell.com>
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

    private bool                _chunked;
    private Encoding            _contentEncoding;
    private long                _contentLength;
    private bool                _contentLengthSet;
    private string              _contentType;
    private HttpListenerContext _context;
    private CookieCollection    _cookies;
    private bool                _disposed;
    private bool                _forceCloseChunked;
    private WebHeaderCollection _headers;
    private bool                _headersSent;
    private bool                _keepAlive;
    private string              _location;
    private ResponseStream      _outputStream;
    private int                 _statusCode;
    private string              _statusDescription;
    private Version             _version;

    #endregion

    #region Internal Constructors

    internal HttpListenerResponse (HttpListenerContext context)
    {
      _context = context;
      _headers = new WebHeaderCollection ();
      _keepAlive = true;
      _statusCode = 200;
      _statusDescription = "OK";
      _version = HttpVersion.Version11;
    }

    #endregion

    #region Internal Properties

    internal bool CloseConnection {
      get {
        return _headers ["Connection"] == "close";
      }
    }

    internal bool ForceCloseChunked {
      get {
        return _forceCloseChunked;
      }
    }

    internal bool HeadersSent {
      get {
        return _headersSent;
      }
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets or sets the encoding for the entity body data included in the response.
    /// </summary>
    /// <value>
    /// A <see cref="Encoding"/> that represents the encoding for the entity body data, or
    /// <see langword="null"/> if no encoding is specified.
    /// </value>
    /// <exception cref="InvalidOperationException">
    /// The response has already been sent.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This object is closed.
    /// </exception>
    public Encoding ContentEncoding {
      get {
        checkDisposed ();
        return _contentEncoding;
      }

      set {
        checkDisposedOrHeadersSent ();
        _contentEncoding = value;
      }
    }

    /// <summary>
    /// Gets or sets the size of the entity body data included in the response.
    /// </summary>
    /// <value>
    /// A <see cref="long"/> that represents the value of the Content-Length entity-header field.
    /// The value is a number of bytes in the entity body data.
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
        checkDisposed ();
        return _contentLength;
      }

      set {
        checkDisposedOrHeadersSent ();
        if (value < 0)
          throw new ArgumentOutOfRangeException ("Less than zero.", "value");

        _contentLengthSet = true;
        _contentLength = value;
      }
    }

    /// <summary>
    /// Gets or sets the media type of the entity body included in the response.
    /// </summary>
    /// <value>
    /// The type of the content. A <see cref="string"/> that represents the value of
    /// the Content-Type entity-header field.
    /// </value>
    /// <exception cref="ArgumentException">
    /// The value specified for a set operation is empty.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// The value specified for a set operation is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The response has already been sent.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This object is closed.
    /// </exception>
    public string ContentType {
      get {
        checkDisposed ();
        return _contentType;
      }

      set {
        checkDisposedOrHeadersSent ();
        if (value == null)
          throw new ArgumentNullException ("value");

        if (value.Length == 0)
          throw new ArgumentException ("An empty string.", "value");

        _contentType = value;
      }
    }

    /// <summary>
    /// Gets or sets the cookies returned with the response.
    /// </summary>
    /// <value>
    /// A <see cref="CookieCollection"/> that contains the cookies returned with the response.
    /// </value>
    /// <exception cref="InvalidOperationException">
    /// The response has already been sent.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This object is closed.
    /// </exception>
    public CookieCollection Cookies {
      get {
        checkDisposed ();
        return _cookies ?? (_cookies = new CookieCollection ());
      }

      set {
        checkDisposedOrHeadersSent ();
        _cookies = value;
      }
    }

    /// <summary>
    /// Gets or sets the HTTP headers returned to the client.
    /// </summary>
    /// <value>
    /// A <see cref="WebHeaderCollection"/> that contains the headers returned to the client.
    /// </value>
    /// <exception cref="ArgumentNullException">
    /// The value specified for a set operation is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The response has already been sent.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This object is closed.
    /// </exception>
    public WebHeaderCollection Headers {
      get {
        checkDisposed ();
        return _headers;
      }

      set {
        /*
         * "If you attempt to set a Content-Length, Keep-Alive, Transfer-Encoding,
         * or WWW-Authenticate header using the Headers property, an exception
         * will be thrown. Use the KeepAlive or ContentLength64 properties to set
         * these headers. You cannot set the Transfer-Encoding or WWW-Authenticate
         * headers manually."
         */

        // TODO: Check if this is marked readonly after headers are sent.

        checkDisposedOrHeadersSent ();
        if (value == null)
          throw new ArgumentNullException ("value");

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
        checkDisposed ();
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
    /// A <see cref="Version"/> that represents the HTTP version used in the response.
    /// </value>
    /// <exception cref="ArgumentException">
    /// The value specified for a set operation doesn't have its <c>Major</c> property set to 1 or
    /// doesn't have its <c>Minor</c> property set to either 0 or 1.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// The value specified for a set operation is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The response has already been sent.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This object is closed.
    /// </exception>
    public Version ProtocolVersion {
      get {
        checkDisposed ();
        return _version;
      }

      set {
        checkDisposedOrHeadersSent ();
        if (value == null)
          throw new ArgumentNullException ("value");

        if (value.Major != 1 || (value.Minor != 0 && value.Minor != 1))
          throw new ArgumentException ("Neither 1.0 nor 1.1.", "value");

        _version = value;
      }
    }

    /// <summary>
    /// Gets or sets the URL to which the client is redirected to locate a requested resource.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the value of the Location response-header field.
    /// </value>
    /// <exception cref="ArgumentException">
    /// The value specified for a set operation is empty.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The response has already been sent.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This object is closed.
    /// </exception>
    public string RedirectLocation {
      get {
        checkDisposed ();
        return _location;
      }

      set {
        checkDisposedOrHeadersSent ();
        if (value.Length == 0)
          throw new ArgumentException ("An empty string.", "value");

        _location = value;
      }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the response uses the chunked transfer encoding.
    /// </summary>
    /// <value>
    /// <c>true</c> if the response uses the chunked transfer encoding; otherwise, <c>false</c>.
    /// </value>
    /// <exception cref="InvalidOperationException">
    /// The response has already been sent.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This object is closed.
    /// </exception>
    public bool SendChunked {
      get {
        return _chunked;
      }

      set {
        checkDisposedOrHeadersSent ();
        _chunked = value;
      }
    }

    /// <summary>
    /// Gets or sets the HTTP status code returned to the client.
    /// </summary>
    /// <value>
    /// An <see cref="int"/> that represents the HTTP status code for the response to the request.
    /// The default value is <see cref="HttpStatusCode.OK"/>.
    /// </value>
    /// <exception cref="InvalidOperationException">
    /// The response has already been sent.
    /// </exception>
    /// <exception cref="System.Net.ProtocolViolationException">
    /// The value specified for a set operation is invalid. Valid values are between 100 and 999.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This object is closed.
    /// </exception>
    public int StatusCode {
      get {
        checkDisposed ();
        return _statusCode;
      }

      set {
        checkDisposedOrHeadersSent ();
        if (value < 100 || value > 999)
          throw new System.Net.ProtocolViolationException (
            "StatusCode must be between 100 and 999.");

        _statusCode = value;
        _statusDescription = value.GetStatusDescription ();
      }
    }

    /// <summary>
    /// Gets or sets the description of the HTTP status code returned to the client.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the description of the HTTP status code.
    /// </value>
    /// <exception cref="InvalidOperationException">
    /// The response has already been sent.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This object is closed.
    /// </exception>
    public string StatusDescription {
      get {
        checkDisposed ();
        return _statusDescription;
      }

      set {
        checkDisposedOrHeadersSent ();
        _statusDescription = value == null || value.Length == 0
                             ? _statusCode.GetStatusDescription ()
                             : value;
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

      var version = cookie.Version;
      foreach (var c in found)
        if (c.Version == version)
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
        throw new InvalidOperationException ("Cannot be changed after headers are sent.");
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

    internal void SendHeaders (bool closing, MemoryStream stream)
    {
      if (_contentType != null) {
        var contentType = _contentEncoding != null &&
                          _contentType.IndexOf ("charset=", StringComparison.Ordinal) == -1
                          ? _contentType + "; charset=" + _contentEncoding.WebName
                          : _contentType;

        _headers.SetInternally ("Content-Type", contentType, true);
      }

      if (_headers ["Server"] == null)
        _headers.SetInternally ("Server", "websocket-sharp/1.0", true);

      var provider = CultureInfo.InvariantCulture;
      if (_headers ["Date"] == null)
        _headers.SetInternally ("Date", DateTime.UtcNow.ToString ("r", provider), true);

      if (!_chunked) {
        if (!_contentLengthSet && closing) {
          _contentLengthSet = true;
          _contentLength = 0;
        }

        if (_contentLengthSet)
          _headers.SetInternally ("Content-Length", _contentLength.ToString (provider), true);
      }

      var version = _context.Request.ProtocolVersion;
      if (!_contentLengthSet && !_chunked && version >= HttpVersion.Version11)
        _chunked = true;
        
      /* Apache forces closing the connection for these status codes:
       * - HttpStatusCode.BadRequest            400
       * - HttpStatusCode.RequestTimeout        408
       * - HttpStatusCode.LengthRequired        411
       * - HttpStatusCode.RequestEntityTooLarge 413
       * - HttpStatusCode.RequestUriTooLong     414
       * - HttpStatusCode.InternalServerError   500
       * - HttpStatusCode.ServiceUnavailable    503
       */
      var connClose = _statusCode == 400 ||
                      _statusCode == 408 ||
                      _statusCode == 411 ||
                      _statusCode == 413 ||
                      _statusCode == 414 ||
                      _statusCode == 500 ||
                      _statusCode == 503;

      if (!connClose)
        connClose = !_context.Request.KeepAlive;

      // They sent both KeepAlive: true and Connection: close!?
      if (!_keepAlive || connClose) {
        _headers.SetInternally ("Connection", "close", true);
        connClose = true;
      }

      if (_chunked)
        _headers.SetInternally ("Transfer-Encoding", "chunked", true);

      int reuses = _context.Connection.Reuses;
      if (reuses >= 100) {
        _forceCloseChunked = true;
        if (!connClose) {
          _headers.SetInternally ("Connection", "close", true);
          connClose = true;
        }
      }

      if (!connClose) {
        _headers.SetInternally (
          "Keep-Alive", String.Format ("timeout=15,max={0}", 100 - reuses), true);

        if (_context.Request.ProtocolVersion <= HttpVersion.Version10)
          _headers.SetInternally ("Connection", "keep-alive", true);
      }

      if (_location != null)
        _headers.SetInternally ("Location", _location, true);

      if (_cookies != null)
        foreach (Cookie cookie in _cookies)
          _headers.SetInternally ("Set-Cookie", cookie.ToResponseString (), true);

      var encoding = _contentEncoding ?? Encoding.Default;
      var writer = new StreamWriter (stream, encoding, 256);
      writer.Write ("HTTP/{0} {1} {2}\r\n", _version, _statusCode, _statusDescription);

      var headers = _headers.ToStringMultiValue (true);
      writer.Write (headers);
      writer.Flush ();

      var preamble = encoding.CodePage == 65001 ? 3 : encoding.GetPreamble ().Length;
      if (_outputStream == null)
        _outputStream = _context.Connection.GetResponseStream ();

      // Assumes that the stream was at position 0.
      stream.Position = preamble;
      _headersSent = true;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Closes the connection to the client without sending a response.
    /// </summary>
    public void Abort ()
    {
      if (_disposed)
        return;

      close (true);
    }

    /// <summary>
    /// Adds an HTTP header with the specified <paramref name="name"/> and <paramref name="value"/>
    /// to the headers for the response.
    /// </summary>
    /// <param name="name">
    /// A <see cref="string"/> that represents the name of the header to add.
    /// </param>
    /// <param name="value">
    /// A <see cref="string"/> that represents the value of the header to add.
    /// </param>
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
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> is <see langword="null"/> or empty.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The length of <paramref name="value"/> is greater than 65,535 characters.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   <para>
    ///   The response has already been sent.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   The header cannot be allowed to add to the current headers.
    ///   </para>
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This object is closed.
    /// </exception>
    public void AddHeader (string name, string value)
    {
      checkDisposedOrHeadersSent ();
      _headers.Set (name, value);
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
    /// <exception cref="InvalidOperationException">
    /// The response has already been sent.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This object is closed.
    /// </exception>
    public void AppendCookie (Cookie cookie)
    {
      checkDisposedOrHeadersSent ();
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
    /// <exception cref="ArgumentNullException">
    /// <paramref name="name"/> is <see langword="null"/> or empty.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The length of <paramref name="value"/> is greater than 65,535 characters.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   <para>
    ///   The response has already been sent.
    ///   </para>
    ///   <para>
    ///   -or-
    ///   </para>
    ///   <para>
    ///   The current headers cannot allow the header to append a value.
    ///   </para>
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This object is closed.
    /// </exception>
    public void AppendHeader (string name, string value)
    {
      checkDisposedOrHeadersSent ();
      _headers.Add (name, value);
    }

    /// <summary>
    /// Sends the response to the client, and releases the resources used by
    /// this <see cref="HttpListenerResponse"/> instance.
    /// </summary>
    public void Close ()
    {
      if (_disposed)
        return;

      close (false);
    }

    /// <summary>
    /// Sends the response with the specified array of <see cref="byte"/> to the client, and
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
    /// <exception cref="InvalidOperationException">
    /// The response has already been sent.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This object is closed.
    /// </exception>
    public void Close (byte [] responseEntity, bool willBlock)
    {
      if (responseEntity == null)
        throw new ArgumentNullException ("responseEntity");

      var len = responseEntity.Length;
      ContentLength64 = len;

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
    /// Copies properties from the specified <see cref="HttpListenerResponse"/> to this response.
    /// </summary>
    /// <param name="templateResponse">
    /// A <see cref="HttpListenerResponse"/> to copy.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The response has already been sent.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This object is closed.
    /// </exception>
    public void CopyFrom (HttpListenerResponse templateResponse)
    {
      checkDisposedOrHeadersSent ();

      _headers.Clear ();
      _headers.Add (templateResponse._headers);
      _contentLength = templateResponse._contentLength;
      _statusCode = templateResponse._statusCode;
      _statusDescription = templateResponse._statusDescription;
      _keepAlive = templateResponse._keepAlive;
      _version = templateResponse._version;
    }

    /// <summary>
    /// Configures the response to redirect the client's request to the specified
    /// <paramref name="url"/>.
    /// </summary>
    /// <param name="url">
    /// A <see cref="string"/> that represents the URL to redirect the client's request to.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The response has already been sent.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This object is closed.
    /// </exception>
    public void Redirect (string url)
    {
      StatusCode = (int) HttpStatusCode.Redirect;
      _location = url;
    }

    /// <summary>
    /// Adds or updates a <paramref name="cookie"/> in the cookies sent with the response.
    /// </summary>
    /// <param name="cookie">
    /// A <see cref="Cookie"/> to set.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="cookie"/> already exists in the cookies, and couldn't be replaced.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="cookie"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The response has already been sent.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// This object is closed.
    /// </exception>
    public void SetCookie (Cookie cookie)
    {
      checkDisposedOrHeadersSent ();
      if (cookie == null)
        throw new ArgumentNullException ("cookie");

      if (!canAddOrUpdate (cookie))
        throw new ArgumentException ("Cannot be replaced.", "cookie");

      Cookies.Add (cookie);
    }

    #endregion

    #region Explicit Interface Implementation

    /// <summary>
    /// Releases all resource used by the <see cref="HttpListenerResponse"/>.
    /// </summary>
    void IDisposable.Dispose ()
    {
      if (_disposed)
        return;

      // TODO: Abort or Close?
      close (true); // Same as Abort.
    }

    #endregion
  }
}
