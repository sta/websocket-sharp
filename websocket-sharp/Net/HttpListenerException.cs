//
// HttpListenerException.cs
//	Copied from System.Net.HttpListenerException.cs
//
// Author:
//	Gonzalo Paniagua Javier (gonzalo@novell.com)
//
// Copyright (c) 2005 Novell, Inc. (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.ComponentModel;
using System.Runtime.Serialization;

namespace WebSocketSharp.Net {

	/// <summary>
	/// The exception that is thrown when an error occurs processing an HTTP request.
	/// </summary>
	[Serializable]
	public class HttpListenerException : Win32Exception {

		#region Public Constructors

		/// <summary>
		/// Initializes a new instance of the <see cref="HttpListenerException"/> class.
		/// </summary>
		public HttpListenerException ()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="HttpListenerException"/> class
		/// with the specified <paramref name="errorCode"/>.
		/// </summary>
		/// <param name="errorCode">
		/// An <see cref="int"/> that contains an error code.
		/// </param>
		public HttpListenerException (int errorCode) : base (errorCode)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="HttpListenerException"/> class
		/// with the specified <paramref name="errorCode"/> and <paramref name="message"/>.
		/// </summary>
		/// <param name="errorCode">
		/// An <see cref="int"/> that contains an error code.
		/// </param>
		/// <param name="message">
		/// A <see cref="string"/> that describes the error.
		/// </param>
		public HttpListenerException (int errorCode, string message) : base (errorCode, message)
		{
		}

		#endregion

		#region Protected Constructor

		/// <summary>
		/// Initializes a new instance of the <see cref="HttpListenerException"/> class
		/// from the specified <see cref="SerializationInfo"/> and <see cref="StreamingContext"/> classes.
		/// </summary>
		/// <param name="serializationInfo">
		/// A <see cref="SerializationInfo"/> that contains the information required to deserialize
		/// the new <see cref="HttpListenerException"/> object.
		/// </param>
		/// <param name="streamingContext">
		/// A <see cref="StreamingContext"/>.
		/// </param>
		protected HttpListenerException (SerializationInfo serializationInfo, StreamingContext streamingContext) : base (serializationInfo, streamingContext)
		{
		}

		#endregion

		#region Property

		/// <summary>
		/// Gets a value that represents the error that occurred.
		/// </summary>
		/// <value>
		/// An <see cref="int"/> that contains an error code.
		/// </value>
		public override int ErrorCode {
			get { return base.ErrorCode; }
		}

		#endregion
	}
}
