#region License
/*
 * ListenerAsyncResult.cs
 *
 * This code is derived from System.Net.ListenerAsyncResult.cs of Mono
 * (http://www.mono-project.com).
 *
 * The MIT License
 *
 * Copyright (c) 2005 Ximian, Inc. (http://www.ximian.com)
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
 * - Gonzalo Paniagua Javier <gonzalo@ximian.com>
 */
#endregion

using System;
using System.Threading;

namespace WebSocketSharp.Net
{
	internal class ListenerAsyncResult : IAsyncResult
	{
	    private readonly AsyncCallback _callback;
		private bool _completed;
		private HttpListenerContext _context;
		private Exception _exception;

	    private readonly object _sync;

	    private ManualResetEvent _waitHandle;

	    internal bool EndCalled;
		internal bool InGet;

	    public ListenerAsyncResult(AsyncCallback callback, object state)
		{
			_callback = callback;
			AsyncState = state;
			_sync = new object();
		}

	    public object AsyncState { get; }

	    public WaitHandle AsyncWaitHandle
		{
			get
			{
				lock (_sync)
					return _waitHandle ?? (_waitHandle = new ManualResetEvent(_completed));
			}
		}

		public bool CompletedSynchronously { get; private set; }

	    public bool IsCompleted
		{
			get
			{
				lock (_sync)
					return _completed;
			}
		}

	    private static void complete(ListenerAsyncResult asyncResult)
		{
			asyncResult._completed = true;

			var waitHandle = asyncResult._waitHandle;
	        waitHandle?.Set();

	        var callback = asyncResult._callback;
			if (callback != null)
				ThreadPool.UnsafeQueueUserWorkItem(
				  state =>
				  {
					  try
					  {
						  callback(asyncResult);
					  }
					  catch
					  {
					  }
				  },
				  null);
		}

	    internal void Complete(Exception exception)
		{
			_exception = InGet && (exception is ObjectDisposedException)
						 ? new HttpListenerException(500, "Listener closed.")
						 : exception;

			lock (_sync)
				complete(this);
		}

		internal void Complete(HttpListenerContext context)
		{
			Complete(context, false);
		}

		internal void Complete(HttpListenerContext context, bool syncCompleted)
		{
			var listener = context.Listener;
			var schm = listener.SelectAuthenticationScheme(context);
			if (schm == AuthenticationSchemes.None)
			{
				context.Response.Close(HttpStatusCode.Forbidden);
				listener.BeginGetContext(this);

				return;
			}

			var res = context.Request.Headers["Authorization"];
			if (schm == AuthenticationSchemes.Basic &&
				(res == null || !res.StartsWith("basic", StringComparison.OrdinalIgnoreCase)))
			{
				context.Response.CloseWithAuthChallenge(
				  AuthenticationChallenge.CreateBasicChallenge(listener.Realm).ToBasicString());

				listener.BeginGetContext(this);
				return;
			}

			if (schm == AuthenticationSchemes.Digest &&
				(res == null || !res.StartsWith("digest", StringComparison.OrdinalIgnoreCase)))
			{
				context.Response.CloseWithAuthChallenge(
				  AuthenticationChallenge.CreateDigestChallenge(listener.Realm).ToDigestString());

				listener.BeginGetContext(this);
				return;
			}

			_context = context;
			CompletedSynchronously = syncCompleted;

			lock (_sync)
				complete(this);
		}

		internal HttpListenerContext GetContext()
		{
			if (_exception != null)
				throw _exception;

			return _context;
		}
	}
}
