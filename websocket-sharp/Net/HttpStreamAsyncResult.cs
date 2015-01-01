#region License
/*
 * HttpStreamAsyncResult.cs
 *
 * This code is derived from System.Net.HttpStreamAsyncResult.cs of Mono
 * (http://www.mono-project.com).
 *
 * The MIT License
 *
 * Copyright (c) 2005 Novell, Inc. (http://www.novell.com)
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
 * - Gonzalo Paniagua Javier <gonzalo@novell.com>
 */
#endregion

using System;
using System.Threading;

namespace WebSocketSharp.Net
{
  internal class HttpStreamAsyncResult : IAsyncResult
  {
    #region Private Fields

    private AsyncCallback    _callback;
    private bool             _completed;
    private object           _state;
    private object           _sync;
    private ManualResetEvent _waitHandle;

    #endregion

    #region Internal Fields

    internal byte []   Buffer;
    internal int       Count;
    internal Exception Error;
    internal int       Offset;
    internal int       SyncRead;

    #endregion

    #region Public Constructors

    public HttpStreamAsyncResult (AsyncCallback callback, object state)
    {
      _callback = callback;
      _state = state;
      _sync = new object ();
    }

    #endregion

    #region Public Properties

    public object AsyncState {
      get {
        return _state;
      }
    }

    public WaitHandle AsyncWaitHandle {
      get {
        lock (_sync)
          return _waitHandle ?? (_waitHandle = new ManualResetEvent (_completed));
      }
    }

    public bool CompletedSynchronously {
      get {
        return SyncRead == Count;
      }
    }

    public bool IsCompleted {
      get {
        lock (_sync)
          return _completed;
      }
    }

    #endregion

    #region Public Methods

    public void Complete ()
    {
      lock (_sync) {
        if (_completed)
          return;

        _completed = true;
        if (_waitHandle != null)
          _waitHandle.Set ();

        if (_callback != null)
          _callback.BeginInvoke (this, ar => _callback.EndInvoke (ar), null);
      }
    }

    public void Complete (Exception exception)
    {
      Error = exception;
      Complete ();
    }

    #endregion
  }
}
