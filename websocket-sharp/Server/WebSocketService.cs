#region License
/*
 * WebSocketService.cs
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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using System.Threading;
using WebSocketSharp.Net;
using WebSocketSharp.Net.WebSockets;

namespace WebSocketSharp.Server
{
  /// <summary>
  /// Provides the basic functions of the WebSocket service used by the WebSocket service host.
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

    #region Public Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketService"/> class.
    /// </summary>
    public WebSocketService ()
    {
      IsBound = false;
      _start = DateTime.MaxValue;
    }

    #endregion

    #region Protected Properties

    /// <summary>
    /// Gets or sets the logging functions.
    /// </summary>
    /// <remarks>
    /// If you want to change the current logger to the service own logger, you set this property
    /// to a new <see cref="Logger"/> instance that you created.
    /// </remarks>
    /// <value>
    /// A <see cref="Logger"/> that provides the logging functions.
    /// </value>
    protected Logger Log {
      get {
        return IsBound
               ? _websocket.Log
               : null;
      }

      set {
        if (IsBound)
          _websocket.Log = value;
      }
    }
    
    /// <summary>
    /// Gets the manager of the sessions to the WebSocket service.
    /// </summary>
    /// <value>
    /// A <see cref="WebSocketSessionManager"/> that manages the sessions
    /// to the WebSocket service.
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
    /// A <see cref="WebSocketContext"/> that contains the WebSocket connection request information.
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
    /// A <see cref="string"/> that contains the unique ID.
    /// </value>
    public string ID {
      get; private set;
    }

    /// <summary>
    /// Gets a value indicating whether the current <see cref="WebSocketService"/> instance
    /// has been bound to a <see cref="WebSocket"/>.
    /// </summary>
    /// <value>
    /// <c>true</c> if the current <see cref="WebSocketService"/> instance has been bound to
    /// a <see cref="WebSocket"/>; otherwise, <c>false</c>.
    /// </value>
    public bool IsBound {
      get; private set;
    }

    /// <summary>
    /// Gets the time that the current <see cref="WebSocketService"/> instance has been started.
    /// </summary>
    /// <value>
    /// A <see cref="DateTime"/> that represents the time that the current <see cref="WebSocketService"/>
    /// instance has been started.
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
    /// One of the <see cref="WebSocketState"/> values.
    /// </value>
    public WebSocketState State {
      get {
        return IsBound
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
      if (ID == null)
      {
        _websocket.Close (CloseStatusCode.AWAY);
        return;
      }

      _start = DateTime.Now;
      OnOpen ();
    }

    #endregion

    #region Internal Methods

    internal void Bind (WebSocketContext context, WebSocketSessionManager sessions)
    {
      if (IsBound)
        return;

      _context = context;
      _sessions = sessions;
      _websocket = context.WebSocket;
      _websocket.CookiesValidation = ValidateCookies;
      _websocket.OnOpen += onOpen;
      _websocket.OnMessage += onMessage;
      _websocket.OnError += onError;
      _websocket.OnClose += onClose;

      IsBound = true;
    }

    internal bool Ping (byte [] data)
    {
      return _websocket.Ping (data);
    }

    internal void Stop (byte [] data)
    {
      _websocket.Close (data);
    }

    #endregion

    #region Protected Methods

    /// <summary>
    /// Broadcasts the specified array of <see cref="byte"/> to all clients of the WebSocket service.
    /// </summary>
    /// <param name="data">
    /// An array of <see cref="byte"/> to broadcast.
    /// </param>
    protected virtual void Broadcast (byte [] data)
    {
      if (!IsBound)
        return;

      var msg = data.CheckIfValidSendData ();
      if (msg != null)
      {
        Log.Error (msg);
        Error (msg);

        return;
      }

      _sessions.Broadcast (data);
    }

    /// <summary>
    /// Broadcasts the specified <see cref="string"/> to all clients of the WebSocket service.
    /// </summary>
    /// <param name="data">
    /// A <see cref="string"/> to broadcast.
    /// </param>
    protected virtual void Broadcast (string data)
    {
      if (!IsBound)
        return;

      var msg = data.CheckIfValidSendData ();
      if (msg != null)
      {
        Log.Error (msg);
        Error (msg);

        return;
      }

      _sessions.Broadcast (data);
    }

    /// <summary>
    /// Sends Pings to all clients of the WebSocket service.
    /// </summary>
    /// <returns>
    /// A Dictionary&lt;string, bool&gt; that contains the collection of pairs of session ID and value
    /// indicating whether the WebSocket service received a Pong from each client in a time.
    /// </returns>
    protected virtual Dictionary<string, bool> Broadping ()
    {
      return IsBound
             ? _sessions.Broadping (new byte [] {})
             : null;
    }

    /// <summary>
    /// Sends Pings with the specified <paramref name="message"/> to all clients of the WebSocket service.
    /// </summary>
    /// <returns>
    /// A Dictionary&lt;string, bool&gt; that contains the collection of pairs of session ID and value
    /// indicating whether the WebSocket service received a Pong from each client in a time.
    /// </returns>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to send.
    /// </param>
    protected virtual Dictionary<string, bool> Broadping (string message)
    {
      if (!IsBound)
        return null;

      if (message == null || message.Length == 0)
        return _sessions.Broadping (new byte [] {});

      var data = Encoding.UTF8.GetBytes (message);
      var msg = data.CheckIfValidPingData ();
      if (msg != null)
      {
        Log.Error (msg);
        Error (msg);

        return null;
      }

      return _sessions.Broadping (data);
    }

    /// <summary>
    /// Calls the <see cref="OnError"/> method with the specified <paramref name="message"/>.
    /// </summary>
    /// <param name="message">
    /// A <see cref="string"/> that contains an error message.
    /// </param>
    protected virtual void Error (string message)
    {
      if (!message.IsNullOrEmpty ())
        OnError (new ErrorEventArgs (message));
    }

    /// <summary>
    /// Is called when the WebSocket connection has been closed.
    /// </summary>
    /// <param name="e">
    /// A <see cref="CloseEventArgs"/> that contains an event data associated with
    /// an inner <see cref="WebSocket.OnClose"/> event.
    /// </param>
    protected virtual void OnClose (CloseEventArgs e)
    {
    }

    /// <summary>
    /// Is called when the inner <see cref="WebSocket"/> or current <see cref="WebSocketService"/>
    /// gets an error.
    /// </summary>
    /// <param name="e">
    /// An <see cref="ErrorEventArgs"/> that contains an event data associated with
    /// an inner <see cref="WebSocket.OnError"/> event.
    /// </param>
    protected virtual void OnError (ErrorEventArgs e)
    {
    }

    /// <summary>
    /// Is called when the inner <see cref="WebSocket"/> receives a data frame.
    /// </summary>
    /// <param name="e">
    /// A <see cref="MessageEventArgs"/> that contains an event data associated with
    /// an inner <see cref="WebSocket.OnMessage"/> event.
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
    /// Sends a Ping to the client associated with the specified <paramref name="id"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the WebSocket service receives a Pong from the client in a time;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID that represents the destination for the Ping.
    /// </param>
    protected virtual bool PingTo (string id)
    {
      if (!IsBound)
        return false;

      var msg = id.CheckIfValidSessionID ();
      if (msg != null)
      {
        Log.Error (msg);
        Error (msg);

        return false;
      }

      return _sessions.PingTo (id);
    }

    /// <summary>
    /// Sends a Ping with the specified <paramref name="message"/> to the client associated with
    /// the specified <paramref name="id"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the WebSocket service receives a Pong from the client in a time;
    /// otherwise, <c>false</c>.
    /// </returns>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to send.
    /// </param>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID that represents the destination for the Ping.
    /// </param>
    protected virtual bool PingTo (string message, string id)
    {
      if (!IsBound)
        return false;

      var msg = id.CheckIfValidSessionID ();
      if (msg != null)
      {
        Log.Error (msg);
        Error (msg);

        return false;
      }

      return _sessions.PingTo (message, id);
    }

    /// <summary>
    /// Sends a binary <paramref name="data"/> to the client associated with the specified
    /// <paramref name="id"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="data"/> is successfully sent; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="data">
    /// An array of <see cref="byte"/> that contains a binary data to send.
    /// </param>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID that represents the destination for the data.
    /// </param>
    protected virtual bool SendTo (byte [] data, string id)
    {
      if (!IsBound)
        return false;

      var msg = id.CheckIfValidSessionID ();
      if (msg != null)
      {
        Log.Error (msg);
        Error (msg);

        return false;
      }

      return _sessions.SendTo (data, id);
    }

    /// <summary>
    /// Sends a text <paramref name="data"/> to the client associated with the specified
    /// <paramref name="id"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if <paramref name="data"/> is successfully sent; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="data">
    /// A <see cref="string"/> that contains a text data to send.
    /// </param>
    /// <param name="id">
    /// A <see cref="string"/> that contains a session ID that represents the destination for the data.
    /// </param>
    protected virtual bool SendTo (string data, string id)
    {
      if (!IsBound)
        return false;

      var msg = id.CheckIfValidSessionID ();
      if (msg != null)
      {
        Log.Error (msg);
        Error (msg);

        return false;
      }

      return _sessions.SendTo (data, id);
    }

    /// <summary>
    /// Validates the cookies used in the WebSocket connection request.
    /// </summary>
    /// <remarks>
    /// This method is called when the inner <see cref="WebSocket"/> validates
    /// the WebSocket connection request.
    /// </remarks>
    /// <returns>
    /// <c>true</c> if the cookies is valid; otherwise, <c>false</c>.
    /// The default returns <c>true</c>.
    /// </returns>
    /// <param name="request">
    /// A <see cref="CookieCollection"/> that contains a collection of the HTTP Cookies
    /// to validate.
    /// </param>
    /// <param name="response">
    /// A <see cref="CookieCollection"/> that receives the HTTP Cookies to send to the client.
    /// </param>
    protected virtual bool ValidateCookies (CookieCollection request, CookieCollection response)
    {
      return true;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sends a Ping to the client of the current <see cref="WebSocketService"/> instance.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the current <see cref="WebSocketService"/> instance receives a Pong
    /// from the client in a time; otherwise, <c>false</c>.
    /// </returns>
    public virtual bool Ping ()
    {
      return IsBound
             ? _websocket.Ping ()
             : false;
    }

    /// <summary>
    /// Sends a Ping with the specified <paramref name="message"/> to the client of
    /// the current <see cref="WebSocketService"/> instance.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the current <see cref="WebSocketService"/> instance receives a Pong
    /// from the client in a time; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message to send.
    /// </param>
    public virtual bool Ping (string message)
    {
      return IsBound
             ? _websocket.Ping (message)
             : false;
    }

    /// <summary>
    /// Sends a binary <paramref name="data"/> to the client of the current
    /// <see cref="WebSocketService"/> instance.
    /// </summary>
    /// <param name="data">
    /// An array of <see cref="byte"/> that contains a binary data to send.
    /// </param>
    public virtual void Send (byte [] data)
    {
      if (IsBound)
        _websocket.Send (data);
    }

    /// <summary>
    /// Sends a text <paramref name="data"/> to the client of the current
    /// <see cref="WebSocketService"/> instance.
    /// </summary>
    /// <param name="data">
    /// A <see cref="string"/> that contains a text data to send.
    /// </param>
    public virtual void Send (string data)
    {
      if (IsBound)
        _websocket.Send (data);
    }

    /// <summary>
    /// Sends a binary <paramref name="data"/> to the client of the current
    /// <see cref="WebSocketService"/> instance asynchronously.
    /// </summary>
    /// <param name="data">
    /// An array of <see cref="byte"/> that contains a binary data to send.
    /// </param>
    /// <param name="completed">
    /// An <see cref="Action"/> delegate that references the method(s) called when
    /// the asynchronous operation completes.
    /// </param>
    public virtual void SendAsync (byte [] data, Action completed)
    {
      if (IsBound)
        _websocket.SendAsync (data, completed);
    }

    /// <summary>
    /// Sends a text <paramref name="data"/> to the client of the current
    /// <see cref="WebSocketService"/> instance asynchronously.
    /// </summary>
    /// <param name="data">
    /// A <see cref="string"/> that contains a text data to send.
    /// </param>
    /// <param name="completed">
    /// An <see cref="Action"/> delegate that references the method(s) called when
    /// the asynchronous operation completes.
    /// </param>
    public virtual void SendAsync (string data, Action completed)
    {
      if (IsBound)
        _websocket.SendAsync (data, completed);
    }

    /// <summary>
    /// Starts the current <see cref="WebSocketService"/> instance.
    /// </summary>
    public void Start ()
    {
      if (IsBound)
        _websocket.Connect ();
    }

    /// <summary>
    /// Stops the current <see cref="WebSocketService"/> instance.
    /// </summary>
    public void Stop ()
    {
      if (IsBound)
        _websocket.Close ();
    }

    /// <summary>
    /// Stops the current <see cref="WebSocketService"/> instance with the specified
    /// <see cref="ushort"/> and <see cref="string"/>.
    /// </summary>
    /// <param name="code">
    /// A <see cref="ushort"/> that contains a status code indicating the reason for stop.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that contains the reason for stop.
    /// </param>
    public void Stop (ushort code, string reason)
    {
      if (IsBound)
        _websocket.Close (code, reason);
    }

    /// <summary>
    /// Stops the current <see cref="WebSocketService"/> instance with the specified
    /// <see cref="CloseStatusCode"/> and <see cref="string"/>.
    /// </summary>
    /// <param name="code">
    /// One of the <see cref="CloseStatusCode"/> values that indicates a status code
    /// indicating the reason for stop.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that contains the reason for stop.
    /// </param>
    public void Stop (CloseStatusCode code, string reason)
    {
      if (IsBound)
        _websocket.Close (code, reason);
    }

    #endregion
  }
}
