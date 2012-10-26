#region MIT License
/**
 * ServiceManager.cs
 *
 * The MIT License
 *
 * Copyright (c) 2012 sta.blockhead
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

namespace WebSocketSharp.Server {

  public class ServiceManager {
  
    #region Field

    private Dictionary<string, IServiceHost> _services;
    private bool                             _sweeped;

    #endregion

    #region Constructor

    public ServiceManager()
    {
      _services = new Dictionary<string, IServiceHost>();
      _sweeped  = true;
    }

    #endregion

    #region Properties

    public int Count {
      get {
        return _services.Count;
      }
    } 

    public bool Sweeped {
      get {
        return _sweeped;
      }

      set {
        if (value ^ _sweeped)
        {
          _sweeped = value;
          foreach (var svcHost in _services.Values)
            svcHost.Sweeped = value;
        }
      }
    }

    #endregion

    #region Public Methods

    public void Add(string absPath, IServiceHost svcHost)
    {
      _services.Add(absPath.UrlDecode(), svcHost);
    }

    public void Broadcast(string data)
    {
      foreach (var svcHost in _services.Values)
        svcHost.Broadcast(data);
    }

    public void Stop()
    {
      foreach (var svcHost in _services.Values)
        svcHost.Stop();
      _services.Clear();
    }

    public bool TryGetServiceHost(string absPath, out IServiceHost svcHost)
    {
      return _services.TryGetValue(absPath, out svcHost);
    }

    #endregion
  }
}
