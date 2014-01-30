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
  /// Provides the basic functions of the WebSocket service provided by the
  /// <see cref="HttpServer"/> or <see cref="WebSocketServer"/>.
  /// </summary>
  /// <remarks>
  /// The WebSocketService class is an abstract class.
  /// </remarks>
  public abstract class WebSocketService : IWebSocketSession
  {
    #region Private Fields

    private WebSocket               _websocket;
    private WebSocketContext        _context;
    private WebSocketSessionManager _sessions;
    private DateTime                _start;

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
    /// Gets or sets the logging functions.
    /// </summary>
    /// <remarks>
    /// If you want to change the current logger to the service own logger, you
    /// set this property to a new <see cref="Logger"/> instance that you created.
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

      set {
        if (_websocket != null)
          _websocket.Log = value;
      }
    }
    
    /// <summary>
    /// Gets the manager of the sessions to the WebSocket service.
    /// </summary>
    /// <value>
    /// A <see cref="WebSocketSessionManager"/> that manages the sessions to the
    /// WebSocket service.
    /// </value>
    protected WebSocketSessionManager Sessions {
      get {
        return _sessions;
      }
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the WebSocket connection request information.
    /// </summary>
    /// <value>
    /// A <see cref="WebSocketContext"/> that represents the WebSocket connection
    /// request.
    /// </value>
    public WebSocketContext Context {
      get {
        return _context;
      }
    }

    /// <summary>
    /// Gets the unique ID of the current <see cref="WebSocketService"/> instance.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that represents the unique ID of the current
    /// <see cref="WebSocketService"/> instance.
    /// </value>
    public string ID {
      get; private set;
    }

    /// <summary>
    /// Gets the time that the current <see cref="WebSocketService"/> instance
    /// has been started.
    /// </summary>
    /// <value>
    /// A <see cref="DateTime"/> that represents the time that the current
    /// <see cref="WebSocketService"/> instance has been started.
    /// </value>
    public DateTime StartTime {
      get {
        return _start;
      }
    }

    /// <summary>
    /// Gets the state of the WebSocket connection.
    /// </summary>
    /// <value>
    /// One of the <see cref="WebSocketState"/> values that indicate the state of
    /// the WebSocket connection.
    /// </value>
    public WebSocketState State {
      get {
        return _websocket != null
               ? _websocket.ReadyState
               : WebSocketState.CONNECTING;
      }
    }

    #endregion

    #region Private Methods

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
        _websocket.Close (CloseStatusCode.AWAY);
        return;
      }

      _start = DateTime.Now;
      OnOpen ();
    }

    #endregion

    #region Internal Methods

    internal void Start (WebSocketContext context, WebSocketSessionManager sessions)
    {
      _context = context;
      _sessions = sessions;

      _websocket = context.WebSocket;
      _websocket.CookiesValidation = ValidateCookies;
      _websocket.OnOpen += onOpen;
      _websocket.OnMessage += onMessage;
      _websocket.OnError += onError;
      _websocket.OnClose += onClose;
      _websocket.ConnectAsServer ();
    }

    #endregion

    #region Protected Methods

    /// <summary>
    /// Calls the <see cref="OnError"/> method with the specified
    /// <paramref name="message"/>.
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
    /// Is called when the WebSocket connection has been closed.
    /// </summary>
    /// <param name="e">
    /// A <see cref="CloseEventArgs"/> that contains the event data associated
    /// with an inner <see cref="WebSocket.OnClose"/> event.
    /// </param>
    protected virtual void OnClose (CloseEventArgs e)
    {
    }

    /// <summary>
    /// Is called when the inner <see cref="WebSocket"/> or current
    /// <see cref="WebSocketService"/> gets an error.
    /// </summary>
    /// <param name="e">
    /// An <see cref="ErrorEventArgs"/> that contains the event data associated
    /// with an inner <see cref="WebSocket.OnError"/> event.
    /// </param>
    protected virtual void OnError (ErrorEventArgs e)
    {
    }

    /// <summary>
    /// Is called when the inner <see cref="WebSocket"/> receives a data frame.
    /// </summary>
    /// <param name="e">
    /// A <see cref="MessageEventArgs"/> that contains the event data associated
    /// with an inner <see cref="WebSocket.OnMessage"/> event.
    /// </param>
    protected virtual void OnMessage (MessageEventArgs e)
    {
    }

    /// <summary>
    /// Is called when the WebSocket connection has been established.
    /// </summary>
    protected virtual void OnOpen ()
    {
    }

    /// <summary>
    /// Sends a binary <paramref name="data"/> to the client of the current
    /// <see cref="WebSocketService"/> instance.
    /// </summary>
    /// <param name="data">
    /// An array of <see cref="byte"/> that contains the binary data to send.
    /// </param>
    protected void Send (byte [] data)
    {
      if (_websocket != null)
        _websocket.Send (data);
    }

    /// <summary>
    /// Sends a binary data from the specified <see cref="FileInfo"/> to
    /// the client of the current <see cref="WebSocketService"/> instance.
    /// </summary>
    /// <param name="file">
    /// A <see cref="FileInfo"/> from which contains the binary data to send.
    /// </param>
    protected void Send (FileInfo file)
    {
      if (_websocket != null)
        _websocket.Send (file);
    }

    /// <summary>
    /// Sends a text <paramref name="data"/> to the client of the current
    /// <see cref="WebSocketService"/> instance.
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
    /// Sends a binary <paramref name="data"/> asynchronously to the client of the
    /// current <see cref="WebSocketService"/> instance.
    /// </summary>
    /// <remarks>
    /// This method doesn't wait for the send to be complete.
    /// </remarks>
    /// <param name="data">
    /// An array of <see cref="byte"/> that contains the binary data to send.
    /// </param>
    /// <param name="completed">
    /// An Action&lt;bool&gt; delegate that references the method(s) called when
    /// the send is complete.
    /// A <see cref="bool"/> passed to this delegate is <c>true</c> if the send is
    /// complete successfully; otherwise, <c>false</c>.
    /// </param>
    protected void SendAsync (byte [] data, Action<bool> completed)
    {
      if (_websocket != null)
        _websocket.SendAsync (data, completed);
    }

    /// <summary>
    /// Sends a binary data from the specified <see cref="FileInfo"/>
    /// asynchronously to the client of the current <see cref="WebSocketService"/>
    /// instance.
    /// </summary>
    /// <remarks>
    /// This method doesn't wait for the send to be complete.
    /// </remarks>
    /// <param name="file">
    /// A <see cref="FileInfo"/> from which contains the binary data to send.
    /// </param>
    /// <param name="completed">
    /// An Action&lt;bool&gt; delegate that references the method(s) called when
    /// the send is complete.
    /// A <see cref="bool"/> passed to this delegate is <c>true</c> if the send is
    /// complete successfully; otherwise, <c>false</c>.
    /// </param>
    protected void SendAsync (FileInfo file, Action<bool> completed)
    {
      if (_websocket != null)
        _websocket.SendAsync (file, completed);
    }

    /// <summary>
    /// Sends a text <paramref name="data"/> asynchronously to the client of the
    /// current <see cref="WebSocketService"/> instance.
    /// </summary>
    /// <remarks>
    /// This method doesn't wait for the send to be complete.
    /// </remarks>
    /// <param name="data">
    /// A <see cref="string"/> that represents the text data to send.
    /// </param>
    /// <param name="completed">
    /// An Action&lt;bool&gt; delegate that references the method(s) called when
    /// the send is complete.
    /// A <see cref="bool"/> passed to this delegate is <c>true</c> if the send is
    /// complete successfully; otherwise, <c>false</c>.
    /// </param>
    protected void SendAsync (string data, Action<bool> completed)
    {
      if (_websocket != null)
        _websocket.SendAsync (data, completed);
    }

    /// <summary>
    /// Sends a binary data from the specified <see cref="Stream"/> asynchronously
    /// to the client of the current <see cref="WebSocketService"/> instance.
    /// </summary>
    /// <remarks>
    /// This method doesn't wait for the send to be complete.
    /// </remarks>
    /// <param name="stream">
    /// A <see cref="Stream"/> object from which contains the binary data to send.
    /// </param>
    /// <param name="length">
    /// An <see cref="int"/> that represents the number of bytes to send.
    /// </param>
    /// <param name="completed">
    /// An Action&lt;bool&gt; delegate that references the method(s) called when
    /// the send is complete.
    /// A <see cref="bool"/> passed to this delegate is <c>true</c> if the send is
    /// complete successfully; otherwise, <c>false</c>.
    /// </param>
    protected void SendAsync (Stream stream, int length, Action<bool> completed)
    {
      if (_websocket != null)
        _websocket.SendAsync (stream, length, completed);
    }

    /// <summary>
    /// Stops the current <see cref="WebSocketService"/> instance.
    /// </summary>
    protected void Stop ()
    {
      if (_websocket != null)
        _websocket.Close ();
    }

    /// <summary>
    /// Stops the current <see cref="WebSocketService"/> instance with the
    /// specified <see cref="ushort"/> and <see cref="string"/>.
    /// </summary>
    /// <param name="code">
    /// A <see cref="ushort"/> that represents the status code for stop.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that represents the reason for stop.
    /// </param>
    protected void Stop (ushort code, string reason)
    {
      if (_websocket != null)
        _websocket.Close (code, reason);
    }

    /// <summary>
    /// Stops the current <see cref="WebSocketService"/> instance with the
    /// specified <see cref="CloseStatusCode"/> and <see cref="string"/>.
    /// </summary>
    /// <param name="code">
    /// One of the <see cref="CloseStatusCode"/> values that indicate the status
    /// codes for stop.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that represents the reason for stop.
    /// </param>
    protected void Stop (CloseStatusCode code, string reason)
    {
      if (_websocket != null)
        _websocket.Close (code, reason);
    }

    /// <summary>
    /// Validates the HTTP Cookies used in the WebSocket connection request.
    /// </summary>
    /// <remarks>
    /// This method is called when the inner <see cref="WebSocket"/> validates
    /// the WebSocket connection request.
    /// </remarks>
    /// <returns>
    /// <c>true</c> if the cookies is valid; otherwise, <c>false</c>. This method
    /// returns <c>true</c> as default.
    /// </returns>
    /// <param name="request">
    /// A <see cref="CookieCollection"/> that contains the collection of the
    /// cookies to validate.
    /// </param>
    /// <param name="response">
    /// A <see cref="CookieCollection"/> that receives the cookies to send to the
    /// client.
    /// </param>
    protected virtual bool ValidateCookies (
      CookieCollection request, CookieCollection response)
    {
      return true;
    }

    #endregion
  }
}
