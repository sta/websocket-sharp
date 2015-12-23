#region License
/*
 * MessageEventArgs.cs
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
using System.Text;

namespace WebSocketSharp
{
	using System.IO;

	/// <summary>
	/// Contains the event data associated with a <see cref="WebSocket.OnMessage"/> event.
	/// </summary>
	/// <remarks>
	///   <para>
	///   A <see cref="WebSocket.OnMessage"/> event occurs when the <see cref="WebSocket"/> receives
	///   a text or binary message.
	///   </para>
	///   <para>
	///   If you would like to get the message data, you should access
	///   the <see cref="MessageEventArgs.Data"/> or <see cref="MessageEventArgs.RawData"/> property.
	///   </para>
	/// </remarks>
	public class MessageEventArgs : EventArgs
	{
		private readonly WebSocketMessage _message;

		internal MessageEventArgs(WebSocketMessage message)
		{
			_message = message;
		}

		public Opcode Opcode => _message.Opcode;

	    public StreamReader Text => _message.Text;

	    public Stream Data => _message.RawData;
	}
}
