#region License
/*
 * HttpServer.cs
 *
 * A simple HTTP server that allows to accept the WebSocket connection requests.
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

#region Contributors
/*
 * Contributors:
 *   Juan Manuel Lallana <juan.manuel.lallana@gmail.com>
 */
#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp.Net;
using WebSocketSharp.Net.WebSockets;

namespace WebSocketSharp.Server
{
  /// <summary>
  /// Provides a simple HTTP server that allows to accept the WebSocket connection requests.
  /// </summary>
  /// <remarks>
  /// The HttpServer class can provide the multi WebSocket services.
  /// </remarks>
  public class HttpServer
  {
    #region Private Fields

    private HttpListener            _listener;
    private int                     _port;
    private string                  _rootPath;
    private bool                    _secure;
    private volatile ServerState    _state;
    private object                  _sync;
    private bool                    _windows;

    private readonly AutoResetEvent _listenForNextRequest = new AutoResetEvent(false);

    #endregion

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpServer"/> class.
    /// </summary>
    /// <remarks>
    /// An instance initialized by this constructor listens for the incoming requests on port 80.
    /// </remarks>
    public HttpServer ()
      : this (80)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpServer"/> class with the specified
    /// <paramref name="port"/>.
    /// </summary>
    /// <remarks>
    ///   <para>
    ///   An instance initialized by this constructor listens for the incoming requests on
    ///   <paramref name="port"/>.
    ///   </para>
    ///   <para>
    ///   And if <paramref name="port"/> is 443, that instance provides a secure connection.
    ///   </para>
    /// </remarks>
    /// <param name="port">
    /// An <see cref="int"/> that represents the port number on which to listen.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="port"/> isn't between 1 and 65535.
    /// </exception>
    public HttpServer (int port)
      : this (port, port == 443)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpServer"/> class with the specified
    /// <paramref name="port"/> and <paramref name="secure"/>.
    /// </summary>
    /// <remarks>
    /// An instance initialized by this constructor listens for the incoming requests on
    /// <paramref name="port"/>.
    /// </remarks>
    /// <param name="port">
    /// An <see cref="int"/> that represents the port number on which to listen.
    /// </param>
    /// <param name="secure">
    /// A <see cref="bool"/> that indicates providing a secure connection or not. (<c>true</c>
    /// indicates providing a secure connection.)
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="port"/> isn't between 1 and 65535.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Pair of <paramref name="port"/> and <paramref name="secure"/> is invalid.
    /// </exception>
    public HttpServer(int port, bool secure)
        : this(port, secure, null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpServer"/> class with the specified
    /// <paramref name="port"/> and <paramref name="secure"/>.
    /// </summary>
    /// <remarks>
    /// An instance initialized by this constructor listens for the incoming requests on
    /// <paramref name="port"/>.
    /// </remarks>
    /// <param name="port">
    /// An <see cref="int"/> that represents the port number on which to listen.
    /// </param>
    /// <param name="secure">
    /// A <see cref="bool"/> that indicates providing a secure connection or not. (<c>true</c>
    /// indicates providing a secure connection.)
    /// </param>
    /// <param name="prefix">
    /// A <see cref="string"/> that provides a custom url prefix, if desired. (<c>true</c>
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="port"/> isn't between 1 and 65535.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// Pair of <paramref name="port"/> and <paramref name="secure"/> is invalid.
    /// </exception>
    public HttpServer (int port, bool secure, string prefix)
    {
      if (!port.IsPortNumber ())
        throw new ArgumentOutOfRangeException ("port", "Must be between 1 and 65535: " + port);

      if ((port == 80 && secure) || (port == 443 && !secure))
        throw new ArgumentException (
          String.Format ("Invalid pair of 'port' and 'secure': {0}, {1}", port, secure));

      _port = port;
      _secure = secure;
      _listener = new HttpListener ();
      _state = ServerState.Ready;
      _sync = new object ();

      var os = Environment.OSVersion;
      if (os.Platform != PlatformID.Unix && os.Platform != PlatformID.MacOSX)
        _windows = true;

      if (string.IsNullOrEmpty(prefix))
        prefix = String.Format("http{0}://*:{1}/", _secure ? "s" : "", _port);

      _listener.Prefixes.Add(prefix);
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets or sets the certificate used to authenticate the server on the secure connection.
    /// </summary>
    /// <value>
    /// A <see cref="X509Certificate2"/> used to authenticate the server.
    /// </value>
    public X509Certificate2 Certificate {
      get {
        return _listener.DefaultCertificate;
      }

      set {
        if (!canSet ("Certificate"))
          return;

        _listener.DefaultCertificate = value;
      }
    }

    /// <summary>
    /// Gets a value indicating whether the server has started.
    /// </summary>
    /// <value>
    /// <c>true</c> if the server has started; otherwise, <c>false</c>.
    /// </value>
    public bool IsListening {
      get {
        return _state == ServerState.Start;
      }
    }

    /// <summary>
    /// Gets the port on which to listen for incoming requests.
    /// </summary>
    /// <value>
    /// An <see cref="int"/> that represents the port number on which to listen.
    /// </value>
    public int Port {
      get {
        return _port;
      }
    }

    #endregion

    #region Public Events

    /// <summary>
    /// Occurs when the server receives an HTTP request.
    /// </summary>
    public event EventHandler<HttpRequestEventArgs> OnRequest;

    #endregion

    #region Private Methods

    private void acceptRequestAsync (HttpListenerContext context)
    {
        if (OnRequest != null)
        {
            OnRequest(this, new HttpRequestEventArgs(context));
            return;
        }
    }

    private bool canSet (string property)
    {
      if (_state == ServerState.Start || _state == ServerState.ShuttingDown) {

        return false;
      }

      return true;
    }

    private string checkIfCertExists ()
    {
      return _secure &&
             !EndPointListener.CertificateExists (_port, _listener.CertificateFolderPath) &&
             Certificate == null
             ? "The secure connection requires a server certificate."
             : null;
    }

    private void receiveRequest ()
    {
        while (IsListening)
        {
            if (_listener == null) return;

            try
            {
                _listener.BeginGetContext(ListenerCallback, _listener);
                _listenForNextRequest.WaitOne();
            }
            catch (Exception ex)
            {
                return;
            }
            if (_listener == null) return;
        }
    }

    // Handle the processing of a request in here.
    private void ListenerCallback(IAsyncResult asyncResult)
    {
        var listener = asyncResult.AsyncState as HttpListener;
        HttpListenerContext context;

        if (listener == null) return;
        var isListening = listener.IsListening;

        try
        {
            // The EndGetContext() method, as with all Begin/End asynchronous methods in the .NET Framework,
            // blocks until there is a request to be processed or some type of data is available.
            context = listener.EndGetContext(asyncResult);
        }
        catch (Exception ex)
        {
            // You will get an exception when httpListener.Stop() is called
            // because there will be a thread stopped waiting on the .EndGetContext()
            // method, and again, that is just the way most Begin/End asynchronous
            // methods of the .NET Framework work.
            var errMsg = ex + ": " + IsListening;
            return;
        }
        finally
        {
            // Once we know we have a request (or exception), we signal the other thread
            // so that it calls the BeginGetContext() (or possibly exits if we're not
            // listening any more) method to start handling the next incoming request
            // while we continue to process this request on a different thread.
            _listenForNextRequest.Set();
        }

        //Task.Factory.StartNew(() => InitTask(context));
        acceptRequestAsync(context);
    }

    private void startReceiving ()
    {
      Task.Factory.StartNew(receiveRequest, TaskCreationOptions.LongRunning);
    }

    private void stopListener (int millisecondsTimeout)
    {
      _listener.Close ();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Starts receiving the HTTP requests.
    /// </summary>
    public void Start ()
    {
      lock (_sync) {
        var msg = _state.CheckIfStartable () ?? checkIfCertExists ();
        if (msg != null) {
          return;
        }

        _listener.Start ();
        startReceiving ();

        _state = ServerState.Start;
      }
    }

    /// <summary>
    /// Stops receiving the HTTP requests.
    /// </summary>
    public void Stop ()
    {
      lock (_sync) {
        var msg = _state.CheckIfStart ();
        if (msg != null) {
          return;
        }

        _state = ServerState.ShuttingDown;
      }

      stopListener (5000);

      _state = ServerState.Stop;
    }

    /// <summary>
    /// Stops receiving the HTTP requests with the specified <see cref="ushort"/> and
    /// <see cref="string"/> used to stop the WebSocket services.
    /// </summary>
    /// <param name="code">
    /// A <see cref="ushort"/> that represents the status code indicating the reason for stop.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that represents the reason for stop.
    /// </param>
    public void Stop (ushort code, string reason)
    {
      byte [] data = null;
      lock (_sync) {
        var msg = _state.CheckIfStart () ??
                  code.CheckIfValidCloseStatusCode () ??
                  (data = code.Append (reason)).CheckIfValidControlData ("reason");

        if (msg != null) {

          return;
        }

        _state = ServerState.ShuttingDown;
      }

      stopListener (5000);

      _state = ServerState.Stop;
    }

    /// <summary>
    /// Stops receiving the HTTP requests with the specified <see cref="CloseStatusCode"/> and
    /// <see cref="string"/> used to stop the WebSocket services.
    /// </summary>
    /// <param name="code">
    /// One of the <see cref="CloseStatusCode"/> enum values, represents the status code indicating
    /// the reasons for stop.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that represents the reason for stop.
    /// </param>
    public void Stop (CloseStatusCode code, string reason)
    {
      byte [] data = null;
      lock (_sync) {
        var msg = _state.CheckIfStart () ??
                  (data = ((ushort) code).Append (reason)).CheckIfValidControlData ("reason");

        if (msg != null) {
          return;
        }

        _state = ServerState.ShuttingDown;
      }

      stopListener (5000);

      _state = ServerState.Stop;
    }

    #endregion
  }
}
