#region License
/*
 * Ext.cs
 *
 * Some parts of this code are derived from Mono (http://www.mono-project.com):
 * - GetStatusDescription is derived from HttpListenerResponse.cs (System.Net)
 * - IsPredefinedScheme is derived from Uri.cs (System)
 * - MaybeUri is derived from Uri.cs (System)
 *
 * The MIT License
 *
 * Copyright (c) 2001 Garrett Rooney
 * Copyright (c) 2003 Ian MacLean
 * Copyright (c) 2003 Ben Maurer
 * Copyright (c) 2003, 2005, 2009 Novell, Inc. (http://www.novell.com)
 * Copyright (c) 2009 Stephane Delcroix
 * Copyright (c) 2010-2016 sta.blockhead
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
 * - Liryna <liryna.stark@gmail.com>
 * - Nikola Kovacevic <nikolak@outlook.com>
 * - Chris Swiedler
 */
#endregion

using System;
using WebSocketSharp.Net;

namespace WebSocketSharp
{
	/// <summary>
	/// Provides a set of static methods for websocket-sharp.
	/// </summary>
	public static class ServerExt
	{

		internal static void Close(this HttpListenerResponse response, HttpStatusCode code)
		{
			response.StatusCode = (int)code;
			response.OutputStream.Close();
		}

		internal static void CloseWithAuthChallenge(
		  this HttpListenerResponse response, string challenge)
		{
			response.Headers.InternalSet("WWW-Authenticate", challenge, true);
			response.Close(HttpStatusCode.Unauthorized);
		}



		/// <summary>
		/// Writes and sends the specified <paramref name="content"/> data with the specified
		/// <see cref="HttpListenerResponse"/>.
		/// </summary>
		/// <param name="response">
		/// A <see cref="HttpListenerResponse"/> that represents the HTTP response used to
		/// send the content data.
		/// </param>
		/// <param name="content">
		/// An array of <see cref="byte"/> that represents the content data to send.
		/// </param>
		/// <exception cref="ArgumentNullException">
		///   <para>
		///   <paramref name="response"/> is <see langword="null"/>.
		///   </para>
		///   <para>
		///   -or-
		///   </para>
		///   <para>
		///   <paramref name="content"/> is <see langword="null"/>.
		///   </para>
		/// </exception>
		public static void WriteContent(this HttpListenerResponse response, byte[] content)
		{
			if (response == null)
				throw new ArgumentNullException("response");

			if (content == null)
				throw new ArgumentNullException("content");

			var len = content.LongLength;
			if (len == 0)
			{
				response.Close();
				return;
			}

			response.ContentLength64 = len;
			var output = response.OutputStream;
			if (len <= Int32.MaxValue)
				output.Write(content, 0, (int)len);
			else
				output.WriteBytes(content, 1024);

			output.Close();
		}

	}
}
