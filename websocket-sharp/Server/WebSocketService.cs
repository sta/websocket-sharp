#region License
/*
 * WebSocketService.cs
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
using System.IO;
using WebSocketSharp.Net;
using WebSocketSharp.Net.WebSockets;

namespace WebSocketSharp.Server
{
  /// <summary>
  /// Exposes a set of the methods and properties for a WebSocket service provided by the
  /// <see cref="HttpServer"/> or <see cref="WebSocketServer"/>.
  /// </summary>
  /// <remarks>
  /// The WebSocketService class is an abstract class.
  /// </remarks>
  public abstract class WebSocketService : IWebSocketSession
  {
    #region Private Fields

    private WebSocketContext                               _context;
    private Func<CookieCollection, CookieCollection, bool> _cookiesValidator;
    private Func<string, bool>                             _originValidator;
    private string                                         _protocol;
    private WebSocketSessionManager                        _sessions;
    private DateTime                                       _start;
    private WebSocket                                      _websocket;

    #endregion

    #region Protected Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketService"/> class.
    /// </summary>
    protected WebSocketService ()
    {
      _start = DateTime.MaxValue;
    }

    #endregion

    #region Protected Properties

    /// <summary>
    /// Gets the logging functions.
    /// </summary>
    /// <remarks>
    /// This property is available after the WebSocket connection has been established.
    /// </remarks>
    /// <value>
    /// A <see cref="Logger"/> that provides the logging functions.
    /// </value>
    protected Logger Log {
      get {
        return _websocket != null
               ? _websocket.Log
               : null;
      }
    }
    
    /// <summary>
    /// Gets the access to the sessions in the WebSocket service.
    /// </summary>
    /// <remarks>
    /// This property is available after the WebSocket connection has been established.
    /// </remarks>
    /// <value>
    /// A <see cref="WebSocketSessionManager"/> that provides the access to the sessions.
    /// </value>
    protected WebSocketSessionManager Sessions {
      get {
        return _sessions;
      }
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the information in the current connection request to the WebSocket service.
    /// </summary>
    /// <value>
    /// A <see cref="WebSocketContext"/> that provides the access to the current connection request.
    /// </value>
    public WebSocketContext Context {
      get {
        return _context;
      }
    }

    /// <summary>
    /// Gets or sets the delegate called to validate the HTTP cookies included in a connection
    /// request to the WebSocket service.
    /// </summary>
    /// <remarks>
    /// The delegate is called when the <see cref="WebSocket"/> used in the current session
    /// validates the connection request.
    /// </remarks>
    /// <value>
    ///   <para>
    ///   A <c>Func&lt;CookieCollection, CookieCollection, bool&gt;</c> delegate that references
    ///   the method(s) used to validate the cookies. 1st <see cref="CookieCollection"/> passed to
    ///   this delegate contains the cookies to validate, if any. 2nd <see cref="CookieCollection"/>
    ///   passed to this delegate receives the cookies to send to the client.
    ///   </para>
    ///   <para>
    ///   This delegate should return <c>true</c> if the cookies are valid; otherwise, <c>false</c>.
    ///   </para>
    ///   <para>
    ///   The default value is <see langword="null"/>, and it does nothing to validate.
    ///   </para>
    /// </value>
    public Func<CookieCollection, CookieCollection, bool> CookiesValidator {
      get {
        return _cookiesValidator;
      }

      set {
        _cookiesValidator = value;
      }
    }

    /// <summary>
    /// Gets the unique ID of the current session.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the unique ID of the current session.
    /// </value>
    public string ID {
      get; private set;
    }

    /// <summary>
    /// Gets or sets the delegate called to validate the Origin header included in a connection
    /// request to the WebSocket service.
    /// </summary>
    /// <remarks>
    /// The delegate is called when the <see cref="WebSocket"/> used in the current session
    /// validates the connection request.
    /// </remarks>
    /// <value>
    ///   <para>
    ///   A <c>Func&lt;string, bool&gt;</c> delegate that references the method(s) used to validate
    ///   the origin header. A <see cref="string"/> passed to this delegate represents the value of
    ///   the origin header to validate, if any.
    ///   </para>
    ///   <para>
    ///   This delegate should return <c>true</c> if the origin header is valid; otherwise,
    ///   <c>false</c>.
    ///   </para>
    ///   <para>
    ///   The default value is <see langword="null"/>, and it does nothing to validate.
    ///   </para>
    /// </value>
    public Func<string, bool> OriginValidator {
      get {
        return _originValidator;
      }

      set {
        _originValidator = value;
      }
    }

    /// <summary>
    /// Gets or sets the WebSocket subprotocol used in the current session.
    /// </summary>
    /// <remarks>
    /// Set operation of this property is available before the WebSocket connection has been
    /// established.
    /// </remarks>
    /// <value>
    ///   <para>
    ///   A <see cref="string"/> that represents the subprotocol if any. The default value is
    ///   <see cref="String.Empty"/>.
    ///   </para>
    ///   <para>
    ///   The value to set must be a token defined in
    ///   <see href="http://tools.ietf.org/html/rfc2616#section-2.2">RFC 2616</see>.
    ///   </para>
    /// </value>
    public string Protocol {
      get {
        return _websocket != null
               ? _websocket.Protocol
               : _protocol ?? String.Empty;
      }

      set {
        if (State != WebSocketState.Connecting)
          return;

        if (value != null && (value.Length == 0 || !value.IsToken ()))
          return;

        _protocol = value;
      }
    }

    /// <summary>
    /// Gets the time that the current session has started.
    /// </summary>
    /// <value>
    /// A <see cref="DateTime"/> that represents the time that the current session has started.
    /// </value>
    public DateTime StartTime {
      get {
        return _start;
      }
    }

    /// <summary>
    /// Gets the state of the <see cref="WebSocket"/> used in the current session.
    /// </summary>
    /// <value>
    /// One of the <see cref="WebSocketState"/> enum values, indicates the state of the
    /// <see cref="WebSocket"/> used in the current session.
    /// </value>
    public WebSocketState State {
      get {
        return _websocket != null
               ? _websocket.ReadyState
               : WebSocketState.Connecting;
      }
    }

    #endregion

    #region Private Methods

    private string checkIfValidConnectionRequest (WebSocketContext context)
    {
      return _originValidator != null && !_originValidator (context.Origin)
             ? "Invalid Origin header."
             : _cookiesValidator != null &&
               !_cookiesValidator (context.CookieCollection, context.WebSocket.CookieCollection)
               ? "Invalid Cookies."
               : null;
    }

    private void onClose (object sender, CloseEventArgs e)
    {
      if (ID == null)
        return;

      _sessions.Remove (ID);
      OnClose (e);
    }

    private void onError (object sender, ErrorEventArgs e)
    {
      OnError (e);
    }

    private void onMessage (object sender, MessageEventArgs e)
    {
      OnMessage (e);
    }

    private void onOpen (object sender, EventArgs e)
    {
      ID = _sessions.Add (this);
      if (ID == null) {
        _websocket.Close (CloseStatusCode.Away);
        return;
      }

      _start = DateTime.Now;
      OnOpen ();
    }

    #endregion

    #region Internal Methods

    internal void Start (WebSocketContext context, WebSocketSessionManager sessions)
    {
      if (_websocket != null) {
        _websocket.Log.Error ("This session has already been started.");
        context.WebSocket.Close (HttpStatusCode.ServiceUnavailable);

        return;
      }

      _context = context;
      _sessions = sessions;

      _websocket = context.WebSocket;
      _websocket.Protocol = _protocol;
      _websocket.CustomHandshakeRequestChecker = checkIfValidConnectionRequest;

      _websocket.OnOpen += onOpen;
      _websocket.OnMessage += onMessage;
      _websocket.OnError += onError;
      _websocket.OnClose += onClose;

      _websocket.ConnectAsServer ();
    }

    #endregion

    #region Protected Methods

    /// <summary>
    /// Calls the <see cref="OnError"/> method with the specified <paramref name="message"/>.
    /// </summary>
    /// <param name="message">
    /// A <see cref="string"/> that represents the error message.
    /// </param>
    protected void Error (string message)
    {
      if (message != null && message.Length > 0)
        OnError (new ErrorEventArgs (message));
    }

    /// <summary>
    /// Called when the WebSocket connection used in the current session has been closed.
    /// </summary>
    /// <param name="e">
    /// A <see cref="CloseEventArgs"/> that represents the event data received by
    /// a <see cref="WebSocket.OnClose"/> event.
    /// </param>
    protected virtual void OnClose (CloseEventArgs e)
    {
    }

    /// <summary>
    /// Called when the current session gets an error.
    /// </summary>
    /// <param name="e">
    /// A <see cref="ErrorEventArgs"/> that represents the event data received by
    /// a <see cref="WebSocket.OnError"/> event.
    /// </param>
    protected virtual void OnError (ErrorEventArgs e)
    {
    }

    /// <summary>
    /// Called when the current session receives a message.
    /// </summary>
    /// <param name="e">
    /// A <see cref="MessageEventArgs"/> that represents the event data received by
    /// a <see cref="WebSocket.OnMessage"/> event.
    /// </param>
    protected virtual void OnMessage (MessageEventArgs e)
    {
    }

    /// <summary>
    /// Called when the WebSocket connection used in the current session has been established.
    /// </summary>
    protected virtual void OnOpen ()
    {
    }

    /// <summary>
    /// Sends a binary <paramref name="data"/> to the client on the current session.
    /// </summary>
    /// <param name="data">
    /// An array of <see cref="byte"/> that represents the binary data to send.
    /// </param>
    protected void Send (byte [] data)
    {
      if (_websocket != null)
        _websocket.Send (data);
    }

    /// <summary>
    /// Sends the specified <paramref name="file"/> as a binary data to the client on the current
    /// session.
    /// </summary>
    /// <param name="file">
    /// A <see cref="FileInfo"/> that represents the file to send.
    /// </param>
    protected void Send (FileInfo file)
    {
      if (_websocket != null)
        _websocket.Send (file);
    }

    /// <summary>
    /// Sends a text <paramref name="data"/> to the client on the current session.
    /// </summary>
    /// <param name="data">
    /// A <see cref="string"/> that represents the text data to send.
    /// </param>
    protected void Send (string data)
    {
      if (_websocket != null)
        _websocket.Send (data);
    }

    /// <summary>
    /// Sends a binary <paramref name="data"/> asynchronously to the client on the current session.
    /// </summary>
    /// <remarks>
    /// This method doesn't wait for the send to be complete.
    /// </remarks>
    /// <param name="data">
    /// An array of <see cref="byte"/> that represents the binary data to send.
    /// </param>
    /// <param name="completed">
    /// An Action&lt;bool&gt; delegate that references the method(s) called when the send is
    /// complete. A <see cref="bool"/> passed to this delegate is <c>true</c> if the send is
    /// complete successfully; otherwise, <c>false</c>.
    /// </param>
    protected void SendAsync (byte [] data, Action<bool> completed)
    {
      if (_websocket != null)
        _websocket.SendAsync (data, completed);
    }

    /// <summary>
    /// Sends the specified <paramref name="file"/> as a binary data asynchronously to the client
    /// on the current session.
    /// </summary>
    /// <remarks>
    /// This method doesn't wait for the send to be complete.
    /// </remarks>
    /// <param name="file">
    /// A <see cref="FileInfo"/> that represents the file to send.
    /// </param>
    /// <param name="completed">
    /// An Action&lt;bool&gt; delegate that references the method(s) called when the send is
    /// complete. A <see cref="bool"/> passed to this delegate is <c>true</c> if the send is
    /// complete successfully; otherwise, <c>false</c>.
    /// </param>
    protected void SendAsync (FileInfo file, Action<bool> completed)
    {
      if (_websocket != null)
        _websocket.SendAsync (file, completed);
    }

    /// <summary>
    /// Sends a text <paramref name="data"/> asynchronously to the client on the current session.
    /// </summary>
    /// <remarks>
    /// This method doesn't wait for the send to be complete.
    /// </remarks>
    /// <param name="data">
    /// A <see cref="string"/> that represents the text data to send.
    /// </param>
    /// <param name="completed">
    /// An Action&lt;bool&gt; delegate that references the method(s) called when the send is
    /// complete. A <see cref="bool"/> passed to this delegate is <c>true</c> if the send is
    /// complete successfully; otherwise, <c>false</c>.
    /// </param>
    protected void SendAsync (string data, Action<bool> completed)
    {
      if (_websocket != null)
        _websocket.SendAsync (data, completed);
    }

    /// <summary>
    /// Sends a binary data from the specified <see cref="Stream"/> asynchronously to the client on
    /// the current session.
    /// </summary>
    /// <remarks>
    /// This method doesn't wait for the send to be complete.
    /// </remarks>
    /// <param name="stream">
    /// A <see cref="Stream"/> from which contains the binary data to send.
    /// </param>
    /// <param name="length">
    /// An <see cref="int"/> that represents the number of bytes to send.
    /// </param>
    /// <param name="completed">
    /// An Action&lt;bool&gt; delegate that references the method(s) called when the send is
    /// complete. A <see cref="bool"/> passed to this delegate is <c>true</c> if the send is
    /// complete successfully; otherwise, <c>false</c>.
    /// </param>
    protected void SendAsync (Stream stream, int length, Action<bool> completed)
    {
      if (_websocket != null)
        _websocket.SendAsync (stream, length, completed);
    }

    #endregion
  }
}
