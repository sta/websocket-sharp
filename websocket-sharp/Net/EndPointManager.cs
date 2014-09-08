#region License
/*
 * EndPointManager.cs
 *
 * This code is derived from System.Net.EndPointManager.cs of Mono
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
 * - Gonzalo Paniagua Javier <gonzalo@ximian.com>
 */
#endregion

using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using WebSocketSharp.Logging;

namespace WebSocketSharp.Net
{
    sealed class EndPointManager
    {
        // Dictionary<IPAddress, Dictionary<int, EndPointListener>>
        static Hashtable ip_to_endpoints = new Hashtable();

        private EndPointManager()
        {
        }

        public static void AddListener(ILogger logger, HttpListener listener)
        {
            ArrayList added = new ArrayList();
            try
            {
                lock (ip_to_endpoints)
                {
                    foreach (string prefix in listener.Prefixes)
                    {
                        AddPrefixInternal(logger, prefix, listener);
                        added.Add(prefix);
                    }
                }
            }
            catch
            {
                foreach (string prefix in added)
                {
                    RemovePrefix(logger, prefix, listener);
                }
                throw;
            }
        }

        public static void AddPrefix(ILogger logger, string prefix, HttpListener listener)
        {
            lock (ip_to_endpoints)
            {
                AddPrefixInternal(logger, prefix, listener);
            }
        }

        static void AddPrefixInternal(ILogger logger, string p, HttpListener listener)
        {
            ListenerPrefix lp = new ListenerPrefix(p);
            if (lp.Path.IndexOf('%') != -1)
                throw new System.Net.HttpListenerException(400, "Invalid path.");

            if (lp.Path.IndexOf("//", StringComparison.Ordinal) != -1) // TODO: Code?
                throw new System.Net.HttpListenerException(400, "Invalid path.");

            // listens on all the interfaces if host name cannot be parsed by IPAddress.
            EndPointListener epl = GetEPListener(logger, lp.Host, lp.Port, listener, lp.Secure);
            epl.AddPrefix(lp, listener);
        }

        static EndPointListener GetEPListener(ILogger logger, string host, int port, HttpListener listener, bool secure)
        {
            IPAddress addr;
            if (host == "*" || host == "+")
                addr = IPAddress.Any;
            else if (IPAddress.TryParse(host, out addr) == false)
            {
                try
                {
                    IPHostEntry iphost = Dns.GetHostByName(host);
                    if (iphost != null)
                        addr = iphost.AddressList[0];
                    else
                        addr = IPAddress.Any;
                }
                catch
                {
                    addr = IPAddress.Any;
                }
            }
            Hashtable p = null;  // Dictionary<int, EndPointListener>
            if (ip_to_endpoints.ContainsKey(addr))
            {
                p = (Hashtable)ip_to_endpoints[addr];
            }
            else
            {
                p = new Hashtable();
                ip_to_endpoints[addr] = p;
            }

            EndPointListener epl = null;
            if (p.ContainsKey(port))
            {
                epl = (EndPointListener)p[port];
            }
            else
            {
                epl = new EndPointListener(logger, addr, port, secure);
                p[port] = epl;
            }

            return epl;
        }

        public static void RemoveEndPoint(EndPointListener epl, IPEndPoint ep)
        {
            lock (ip_to_endpoints)
            {
                // Dictionary<int, EndPointListener> p
                Hashtable p = null;
                p = (Hashtable)ip_to_endpoints[ep.Address];
                p.Remove(ep.Port);
                if (p.Count == 0)
                {
                    ip_to_endpoints.Remove(ep.Address);
                }
                epl.Close();
            }
        }

        public static void RemoveListener(ILogger logger, HttpListener listener)
        {
            lock (ip_to_endpoints)
            {
                foreach (string prefix in listener.Prefixes)
                {
                    RemovePrefixInternal(logger, prefix, listener);
                }
            }
        }

        public static void RemovePrefix(ILogger logger, string prefix, HttpListener listener)
        {
            lock (ip_to_endpoints)
            {
                RemovePrefixInternal(logger, prefix, listener);
            }
        }

        static void RemovePrefixInternal(ILogger logger, string prefix, HttpListener listener)
        {
            ListenerPrefix lp = new ListenerPrefix(prefix);
            if (lp.Path.IndexOf('%') != -1)
                return;

            if (lp.Path.IndexOf("//", StringComparison.Ordinal) != -1)
                return;

            EndPointListener epl = GetEPListener(logger, lp.Host, lp.Port, listener, lp.Secure);
            epl.RemovePrefix(lp, listener);
        }
    }
}
