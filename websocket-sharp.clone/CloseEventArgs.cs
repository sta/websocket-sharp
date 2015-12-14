#region License
/*
 * CloseEventArgs.cs
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

namespace WebSocketSharp
{
	/// <summary>
	/// Contains the event data associated with a <see cref="WebSocket.OnClose"/> event.
	/// </summary>
	/// <remarks>
	///   <para>
	///   A <see cref="WebSocket.OnClose"/> event occurs when the WebSocket connection has been
	///   closed.
	///   </para>
	///   <para>
	///   If you would like to get the reason for the close, you should access
	///   the <see cref="CloseEventArgs.Code"/> or <see cref="CloseEventArgs.Reason"/> property.
	///   </para>
	/// </remarks>
	public class CloseEventArgs : EventArgs
	{
		private readonly byte[] _rawData;

	    private PayloadData _payloadData;

		internal CloseEventArgs()
		{
			_payloadData = new PayloadData();
			_rawData = _payloadData.ApplicationData;
		}

		internal CloseEventArgs(ushort code)
		{
			_rawData = code.InternalToByteArray(ByteOrder.Big);
		}

		internal CloseEventArgs(CloseStatusCode code)
			: this((ushort)code)
		{
		}

		internal CloseEventArgs(PayloadData payloadData)
		{
			_payloadData = payloadData;
			_rawData = payloadData.ApplicationData;
		}

		internal CloseEventArgs(ushort code, string reason)
		{
			_rawData = code.Append(reason);
		}

		internal CloseEventArgs(CloseStatusCode code, string reason)
			: this((ushort)code, reason)
		{
		}

		internal PayloadData PayloadData => _payloadData ?? (_payloadData = new PayloadData(_rawData));

	    internal byte[] RawData => _rawData;

	    /// <summary>
		/// Gets a value indicating whether the WebSocket connection has been closed cleanly.
		/// </summary>
		/// <value>
		/// <c>true</c> if the WebSocket connection has been closed cleanly; otherwise, <c>false</c>.
		/// </value>
		public bool WasClean { get; internal set; }
	}
}
