#region MIT License
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
using System.Threading;
using WebSocketSharp.Net;
using WebSocketSharp.Net.WebSockets;

namespace WebSocketSharp.Server {

  /// <summary>
  /// Provides the basic functions of the WebSocket service.
  /// </summary>
  /// <remarks>
  /// The WebSocketService class is an abstract class.
  /// </remarks>
  public abstract class WebSocketService {

    #region Private Fields

    private WebSocketContext        _context;
    private WebSocketServiceManager _sessions;
    private WebSocket               _websocket;

    #endregion

    #region Public Constructor

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketService"/> class.
    /// </summary>
    public WebSocketService()
    {
      ID      = String.Empty;
      IsBound = false;
    }

    #endregion

    #region Protected Properties

    /// <summary>
    /// Gets the collection of query string variables used in the WebSocket opening handshake.
    /// </summary>
    /// <value>
    /// A <see cref="NameValueCollection"/> that contains the collection of query string variables.
    /// </value>
    protected NameValueCollection QueryString {
      get {
        return IsBound
               ? _context.QueryString
               : null;
      }
    }

    /// <summary>
    /// Gets the sessions to the <see cref="WebSocketService"/>.
    /// </summary>
    /// <value>
    /// A <see cref="WebSocketServiceManager"/> that contains the sessions to the the <see cref="WebSocketService"/>.
    /// </value>
    protected WebSocketServiceManager Sessions {
      get {
        return IsBound
               ? _sessions
               : null;
      }
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the ID of the <see cref="WebSocketService"/> instance.
    /// </summary>
    /// <value>
    /// A <see cref="string"/> that contains an ID.
    /// </value>
    public string ID { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the <see cref="WebSocketService"/> instance is bound to a <see cref="WebSocket"/>.
    /// </summary>
    /// <value>
    /// <c>true</c> if the <see cref="WebSocketService"/> instance is bound to a <see cref="WebSocket"/>; otherwise, <c>false</c>.
    /// </value>
    public bool IsBound { get; private set; }

    #endregion

    #region Private Methods

    private void onClose(object sender, CloseEventArgs e)
    {
      _sessions.Remove(ID);
      OnClose(e);
    }

    private void onError(object sender, ErrorEventArgs e)
    {
      OnError(e);
    }

    private void onMessage(object sender, MessageEventArgs e)
    {
      OnMessage(e);
    }

    private void onOpen(object sender, EventArgs e)
    {
      ID = _sessions.Add(this);
      OnOpen();
    }

    #endregion

    #region Internal Methods

    internal void Bind(WebSocketContext context, WebSocketServiceManager sessions)
    {
      if (IsBound)
        return;

      if (!ProcessCookies(context.CookieCollection, context.WebSocket.CookieCollection))
      {
        context.WebSocket.Close(HttpStatusCode.BadRequest);
        return;
      }

      _context = context;
      _sessions = sessions;
      _websocket = context.WebSocket;

      _websocket.OnOpen    += onOpen;
      _websocket.OnMessage += onMessage;
      _websocket.OnError   += onError;
      _websocket.OnClose   += onClose;

      IsBound = true;
    }

    internal void SendAsync(byte[] data, Action completed)
    {
      _websocket.SendAsync(data, completed);
    }

    internal void SendAsync(string data, Action completed)
    {
      _websocket.SendAsync(data, completed);
    }

    #endregion

    #region Protected Methods

    /// <summary>
    /// Occurs when the inner <see cref="WebSocket"/> receives a Close frame or the Stop method is called.
    /// </summary>
    /// <param name="e">
    /// A <see cref="CloseEventArgs"/> that contains the event data associated with a <see cref="WebSocket.OnClose"/> event.
    /// </param>
    protected virtual void OnClose(CloseEventArgs e)
    {
    }

    /// <summary>
    /// Occurs when the inner <see cref="WebSocket"/> gets an error.
    /// </summary>
    /// <param name="e">
    /// An <see cref="ErrorEventArgs"/> that contains the event data associated with a <see cref="WebSocket.OnError"/> event.
    /// </param>
    protected virtual void OnError(ErrorEventArgs e)
    {
    }

    /// <summary>
    /// Occurs when the inner <see cref="WebSocket"/> receives a data frame.
    /// </summary>
    /// <param name="e">
    /// A <see cref="MessageEventArgs"/> that contains the event data associated with a <see cref="WebSocket.OnMessage"/> event.
    /// </param>
    protected virtual void OnMessage(MessageEventArgs e)
    {
    }

    /// <summary>
    /// Occurs when the WebSocket connection has been established.
    /// </summary>
    protected virtual void OnOpen()
    {
    }

    /// <summary>
    /// Processes the cookies used in the WebSocket opening handshake.
    /// </summary>
    /// <returns>
    /// <c>true</c> if processing the cookies is successfully; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="request">
    /// A <see cref="CookieCollection"/> that contains a collection of the HTTP Cookies received from the client.
    /// </param>
    /// <param name="response">
    /// A <see cref="CookieCollection"/> that contains a collection of the HTTP Cookies to send to the client.
    /// </param>
    protected virtual bool ProcessCookies(CookieCollection request, CookieCollection response)
    {
      return true;
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Broadcasts the specified array of <see cref="byte"/> to the clients of every <see cref="WebSocketService"/> instances
    /// in the <see cref="WebSocketService.Sessions"/>.
    /// </summary>
    /// <param name="data">
    /// An array of <see cref="byte"/> to broadcast.
    /// </param>
    public void Broadcast(byte[] data)
    {
      if (IsBound)
        _sessions.Broadcast(data);
    }

    /// <summary>
    /// Broadcasts the specified <see cref="string"/> to the clients of every <see cref="WebSocketService"/> instances
    /// in the <see cref="WebSocketService.Sessions"/>.
    /// </summary>
    /// <param name="data">
    /// A <see cref="string"/> to broadcast.
    /// </param>
    public void Broadcast(string data)
    {
      if (IsBound)
        _sessions.Broadcast(data);
    }

    /// <summary>
    /// Pings to the clients of every <see cref="WebSocketService"/> instances
    /// in the <see cref="WebSocketService.Sessions"/>.
    /// </summary>
    /// <returns>
    /// A Dictionary&lt;string, bool&gt; that contains the collection of IDs and values
    /// indicating whether each <see cref="WebSocketService"/> instances received a Pong in a time.
    /// </returns>
    public Dictionary<string, bool> Broadping()
    {
      return Broadping(String.Empty);
    }

    /// <summary>
    /// Pings with the specified <see cref="string"/> to the clients of every <see cref="WebSocketService"/> instances
    /// in the <see cref="WebSocketService.Sessions"/>.
    /// </summary>
    /// <returns>
    /// A Dictionary&lt;string, bool&gt; that contains the collection of IDs and values
    /// indicating whether each <see cref="WebSocketService"/> instances received a Pong in a time.
    /// </returns>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message.
    /// </param>
    public Dictionary<string, bool> Broadping(string message)
    {
      return IsBound
             ? _sessions.Broadping(message)
             : null;
    }

    /// <summary>
    /// Pings to the client of the <see cref="WebSocketService"/> instance.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the <see cref="WebSocketService"/> instance receives a Pong in a time; otherwise, <c>false</c>.
    /// </returns>
    public bool Ping()
    {
      return Ping(String.Empty);
    }

    /// <summary>
    /// Pings with the specified <see cref="string"/> to the client of the <see cref="WebSocketService"/> instance.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the <see cref="WebSocketService"/> instance receives a Pong in a time; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message.
    /// </param>
    public bool Ping(string message)
    {
      return IsBound
             ? _websocket.Ping(message)
             : false;
    }

    /// <summary>
    /// Pings to the client of the <see cref="WebSocketService"/> instance
    /// associated with the specified <paramref name="id"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the <see cref="WebSocketService"/> instance receives a Pong in a time; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="id">
    /// A <see cref="string"/> that contains an ID that represents the destination for the Ping.
    /// </param>
    public bool PingTo(string id)
    {
      return PingTo(id, String.Empty);
    }

    /// <summary>
    /// Pings with the specified <see cref="string"/> to the client of the <see cref="WebSocketService"/> instance
    /// associated with the specified <paramref name="id"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the <see cref="WebSocketService"/> instance receives a Pong in a time; otherwise, <c>false</c>.
    /// </returns>
    /// <param name="id">
    /// A <see cref="string"/> that contains an ID that represents the destination for the Ping.
    /// </param>
    /// <param name="message">
    /// A <see cref="string"/> that contains a message.
    /// </param>
    public bool PingTo(string id, string message)
    {
      if (!IsBound)
        return false;

      WebSocketService service;
      return _sessions.TryGetWebSocketService(id, out service)
             ? service.Ping(message)
             : false;
    }

    /// <summary>
    /// Sends a binary data to the client of the <see cref="WebSocketService"/> instance.
    /// </summary>
    /// <param name="data">
    /// An array of <see cref="byte"/> that contains a binary data to send.
    /// </param>
    public void Send(byte[] data)
    {
      if (IsBound)
        _websocket.Send(data);
    }

    /// <summary>
    /// Sends a text data to the client of the <see cref="WebSocketService"/> instance.
    /// </summary>
    /// <param name="data">
    /// A <see cref="string"/> that contains a text data to send.
    /// </param>
    public void Send(string data)
    {
      if (IsBound)
        _websocket.Send(data);
    }

    /// <summary>
    /// Sends a binary data to the client of the <see cref="WebSocketService"/> instance
    /// associated with the specified <paramref name="id"/>.
    /// </summary>
    /// <param name="id">
    /// A <see cref="string"/> that contains an ID that represents the destination for the data.
    /// </param>
    /// <param name="data">
    /// An array of <see cref="byte"/> that contains a binary data to send.
    /// </param>
    public void SendTo(string id, byte[] data)
    {
      if (!IsBound)
        return;

      WebSocketService service;
      if (_sessions.TryGetWebSocketService(id, out service))
        service.Send(data);
    }

    /// <summary>
    /// Sends a text data to the client of the <see cref="WebSocketService"/> instance
    /// associated with the specified <paramref name="id"/>.
    /// </summary>
    /// <param name="id">
    /// A <see cref="string"/> that contains an ID that represents the destination for the data.
    /// </param>
    /// <param name="data">
    /// A <see cref="string"/> that contains a text data to send.
    /// </param>
    public void SendTo(string id, string data)
    {
      if (!IsBound)
        return;

      WebSocketService service;
      if (_sessions.TryGetWebSocketService(id, out service))
        service.Send(data);
    }

    /// <summary>
    /// Starts the <see cref="WebSocketService"/> instance.
    /// </summary>
    public void Start()
    {
      if (IsBound)
        _websocket.Connect();
    }

    /// <summary>
    /// Stops the <see cref="WebSocketService"/> instance.
    /// </summary>
    public void Stop()
    {
      if (!IsBound)
        return;

      _websocket.Close();
    }

    /// <summary>
    /// Stops the <see cref="WebSocketService"/> instance with the specified <see cref="ushort"/> and <see cref="string"/>.
    /// </summary>
    /// <param name="code">
    /// A <see cref="ushort"/> that contains a status code indicating the reason for stop.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that contains a reason for stop.
    /// </param>
    public void Stop(ushort code, string reason)
    {
      if (!IsBound)
        return;

      _websocket.Close(code, reason);
    }

    /// <summary>
    /// Stops the <see cref="WebSocketService"/> instance with the specified <see cref="CloseStatusCode"/> and <see cref="string"/>.
    /// </summary>
    /// <param name="code">
    /// One of the <see cref="CloseStatusCode"/> values that contains a status code indicating the reason for stop.
    /// </param>
    /// <param name="reason">
    /// A <see cref="string"/> that contains a reason for stop.
    /// </param>
    public void Stop(CloseStatusCode code, string reason)
    {
      Stop((ushort)code, reason);
    }

    #endregion
  }
}
