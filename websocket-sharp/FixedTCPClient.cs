//------------------------------------------------------------------------------ 
// <copyright file="TCPClient.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//----------------------------------------------------------------------------- 

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Security.Permissions;

/// <devdoc>
/// <para>The <see cref='System.Net.Sockets.TcpClient'/> class provide TCP services at a higher level 
///    of abstraction than the <see cref='System.Net.Sockets.Socket'/> class. <see cref='System.Net.Sockets.TcpClient'/>
///    is used to create a Client connection to a remote host.</para> 
/// </devdoc> 
public class FixedTcpClient : IDisposable
{

    Socket m_ClientSocket;
    bool m_Active;
    NetworkStream m_DataStream;

    //
    // IPv6: Maintain address family for the client 
    // 
    AddressFamily m_Family = AddressFamily.InterNetwork;

    // specify local IP and port
    /// <devdoc>
    ///    <para>
    ///       Initializes a new instance of the <see cref='System.Net.Sockets.TcpClient'/> 
    ///       class with the specified end point.
    ///    </para> 
    /// </devdoc> 
    public FixedTcpClient(IPEndPoint localEP)
    {
        if (localEP == null)
        {
            throw new ArgumentNullException("localEP");
        }
        // 
        // IPv6: Establish address family before creating a socket
        // 
        m_Family = localEP.AddressFamily;

        initialize();
        Client.Bind(localEP);
    }

    // TcpClient(IPaddress localaddr); // port is arbitrary
    // TcpClient(int outgoingPort); // local IP is arbitrary 

    // address+port is arbitrary
    /// <devdoc> 
    ///    <para>
    ///       Initializes a new instance of the <see cref='System.Net.Sockets.TcpClient'/> class.
    ///    </para>
    /// </devdoc> 
    public FixedTcpClient() : this(AddressFamily.InterNetwork)
    {
    }

    /// <devdoc>
    ///    <para>
    ///       Initializes a new instance of the <see cref='System.Net.Sockets.TcpClient'/> class.
    ///    </para> 
    /// </devdoc>
#if COMNET_DISABLEIPV6
    private TcpClient(AddressFamily family) { 
#else
    public FixedTcpClient(AddressFamily family)
    {
#endif
        //
        // Validate parameter 
        //
        if (family != AddressFamily.InterNetwork && family != AddressFamily.InterNetworkV6)
        {
            throw new ArgumentException("some error with ipv6");
        }

        m_Family = family;

        initialize();
    }

    // bind and connect 
    /// <devdoc>
    /// <para>Initializes a new instance of the <see cref='System.Net.Sockets.TcpClient'/> class and connects to the 
    ///    specified port on the specified host.</para>
    /// </devdoc>
    public FixedTcpClient(string hostname, int port)
    {
        if (hostname == null)
        {
            throw new ArgumentNullException("hostname");
        }
        /*
        if (!ValidationHelper.ValidateTcpPort(port))
        {
            throw new ArgumentOutOfRangeException("port");
        }
        */
        //
        // IPv6: Delay creating the client socket until we have
        //       performed DNS resolution and know which address 
        //       families we can use.
        // 
        //initialize(); 

        try
        {
            Connect(hostname, port);
        }

        catch (Exception e)
        {
            if (e is ThreadAbortException || e is StackOverflowException || e is OutOfMemoryException)
            {
                throw;
            }

            if (m_ClientSocket != null)
            {
                m_ClientSocket.Close();
            }
            throw e;
        }
    }

    // 
    // used by TcpListener.Accept()
    //
    internal FixedTcpClient(Socket acceptedSocket)
    {
        Client = acceptedSocket;
        m_Active = true;
    }

    /// <devdoc>
    ///    <para>
    ///       Used by the class to provide
    ///       the underlying network socket. 
    ///    </para>
    /// </devdoc> 
    public Socket Client
    {
        get
        {
            return m_ClientSocket;
        }
        set
        {
            m_ClientSocket = value;
        }
    }

    /// <devdoc> 
    ///    <para>
    ///       Used by the class to indicate that a connection has been made. 
    ///    </para>
    /// </devdoc>
    protected bool Active
    {
        get
        {
            return m_Active;
        }
        set
        {
            m_Active = value;
        }
    }

    public int Available { get { return m_ClientSocket.Available; } }
    public bool Connected { get { return m_ClientSocket.Connected; } }
    public bool ExclusiveAddressUse
    {
        get
        {
            return m_ClientSocket.ExclusiveAddressUse;
        }
        set
        {
            m_ClientSocket.ExclusiveAddressUse = value;
        }
    }    //new



    /// <devdoc> 
    ///    <para>
    ///       Connects the Client to the specified port on the specified host. 
    ///    </para>
    /// </devdoc>
    public void Connect(string hostname, int port)
    {
        if (m_CleanedUp)
        {
            throw new ObjectDisposedException(this.GetType().FullName);
        }
        if (hostname == null)
        {
            throw new ArgumentNullException("hostname");
        }
        /*
        if (!ValidationHelper.ValidateTcpPort(port))
        {
            throw new ArgumentOutOfRangeException("port");
        }
        */
        //
        // Check for already connected and throw here. This check 
        // is not required in the other connect methods as they 
        // will throw from WinSock. Here, the situation is more
        // complex since we have to resolve a hostname so it's 
        // easier to simply block the request up front.
        //
        if (m_Active)
        {
            throw new SocketException((int) SocketError.IsConnected);
        }

        // 
        // IPv6: We need to process each of the addresses return from
        //       DNS when trying to connect. Use of AddressList[0] is 
        //       bad form.
        //


        IPAddress[] addresses = Dns.GetHostAddresses(hostname);
        Exception lastex = null;
        Socket ipv4Socket = null;

        try
        {
            if (m_ClientSocket == null)
            {
                ipv4Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            }

            foreach (IPAddress address in addresses)
            {
                try
                {
                    if (m_ClientSocket == null)
                    {
                        // 
                        // We came via the <hostname,port> constructor. Set the 
                        // address family appropriately, create the socket and
                        // try to connect. 
                        //
                        if (address.AddressFamily == AddressFamily.InterNetwork && ipv4Socket != null)
                        {
                            ipv4Socket.Connect(address, port);
                            m_ClientSocket = ipv4Socket;
                        }

                        m_Family = address.AddressFamily;
                        m_Active = true;
                        break;
                    }
                    else if (address.AddressFamily == m_Family)
                    {
                        //
                        // Only use addresses with a matching family 
                        //
                        Connect(new IPEndPoint(address, port));
                        m_Active = true;
                        break;
                    }
                }

                catch (Exception ex)
                {
                    if (ex is ThreadAbortException || ex is StackOverflowException || ex is OutOfMemoryException)
                    {
                        throw;
                    }
                    lastex = ex;
                }
            }
        }

        catch (Exception ex)
        {
            if (ex is ThreadAbortException || ex is StackOverflowException || ex is OutOfMemoryException)
            {
                throw;
            }
            lastex = ex;
        }

        finally
        {

            //cleanup temp sockets if failed 
            //main socket gets closed when tcpclient gets closed

            //did we connect? 
            if (!m_Active)
            {
                if (ipv4Socket != null)
                {
                    ipv4Socket.Close();
                }

                // 
                // The connect failed - rethrow the last error we had
                //
                if (lastex != null)
                    throw lastex;
                else
                    throw new SocketException((int) SocketError.NotConnected);
            }
        }
    }

    /// <devdoc> 
    ///    <para>
    ///       Connects the Client to the specified port on the specified host. 
    ///    </para> 
    /// </devdoc>
    public void Connect(IPAddress address, int port)
    {
        if (m_CleanedUp)
        {
            throw new ObjectDisposedException(this.GetType().FullName);
        }
        if (address == null)
        {
            throw new ArgumentNullException("address");
        }
        /*
        if (!ValidationHelper.ValidateTcpPort(port))
        {
            throw new ArgumentOutOfRangeException("port");
        }
        */
        IPEndPoint remoteEP = new IPEndPoint(address, port);
        Connect(remoteEP);
    }

    /// <devdoc> 
    ///    <para>
    ///       Connect the Client to the specified end point. 
    ///    </para>
    /// </devdoc>
    public void Connect(IPEndPoint remoteEP)
    {
        if (m_CleanedUp)
        {
            throw new ObjectDisposedException(this.GetType().FullName);
        }
        if (remoteEP == null)
        {
            throw new ArgumentNullException("remoteEP");
        }
        Client.Connect(remoteEP);
        m_Active = true;
    }



    //methods 
    public void Connect(IPAddress[] ipAddresses, int port)
    {
        Client.Connect(ipAddresses, port);
        m_Active = true;
    }


    [HostProtection(ExternalThreading = true)]
    public IAsyncResult BeginConnect(string host, int port, AsyncCallback requestCallback, object state)
    {
        IAsyncResult result = Client.BeginConnect(host, port, requestCallback, state);
        return result;
    }

    [HostProtection(ExternalThreading = true)]
    public IAsyncResult BeginConnect(IPAddress address, int port, AsyncCallback requestCallback, object state)
    {
        IAsyncResult result = Client.BeginConnect(address, port, requestCallback, state);
        return result;
    }

    [HostProtection(ExternalThreading = true)]
    public IAsyncResult BeginConnect(IPAddress[] addresses, int port, AsyncCallback requestCallback, object state)
    {
        IAsyncResult result = Client.BeginConnect(addresses, port, requestCallback, state);
        return result;
    }

    public void EndConnect(IAsyncResult asyncResult)
    {
        Client.EndConnect(asyncResult);
        m_Active = true;
    }





    /// <devdoc> 
    ///    <para>
    ///       Returns the stream used to read and write data to the 
    ///       remote host. 
    ///    </para>
    /// </devdoc> 
    public NetworkStream GetStream()
    {
        if (m_CleanedUp)
        {
            throw new ObjectDisposedException(this.GetType().FullName);
        }
        if (!Client.Connected)
        {
            throw new InvalidOperationException("not connected");
        }
        if (m_DataStream == null)
        {
            m_DataStream = new NetworkStream(Client, true);
        }
        return m_DataStream;
    }

    /// <devdoc> 
    ///    <para>
    ///       Disposes the Tcp connection. 
    ///    </para>
    /// </devdoc>
    //UEUE
    public void Close()
    {
        //GlobalLog.Print("TcpClient::Close()");
        ((IDisposable)this).Dispose();
    }

    private bool m_CleanedUp = false;

    protected virtual void Dispose(bool disposing)
    {
        if (m_CleanedUp)
        {
            return;
        }

        if (disposing)
        {
            IDisposable dataStream = m_DataStream;
            if (dataStream != null)
            {
                dataStream.Dispose();
            }
            else
            {
                //
                // if the NetworkStream wasn't created, the Socket might
                // still be there and needs to be closed. In the case in which
                // we are bound to a local IPEndPoint this will remove the 
                // binding and free up the IPEndPoint for later uses.
                // 
                Socket chkClientSocket = Client;
                if (chkClientSocket != null)
                {
                    try
                    {
                        //chkClientSocket.InternalShutdown(SocketShutdown.Both);
                    }
                    finally
                    {
                        chkClientSocket.Close();
                        Client = null;
                    }
                }
            }

            GC.SuppressFinalize(this);
        }

        m_CleanedUp = true;
    }

    /// <internalonly/>
    void IDisposable.Dispose()
    {
        Dispose(true);
    }

    /// <devdoc> 
    ///    <para> 
    ///       Gets or sets the size of the receive buffer in bytes.
    ///    </para> 
    /// </devdoc>
    public int ReceiveBufferSize
    {
        get
        {
            return numericOption(SocketOptionLevel.Socket,
                                    SocketOptionName.ReceiveBuffer);
        }
        set
        {
            Client.SetSocketOption(SocketOptionLevel.Socket,
                                SocketOptionName.ReceiveBuffer, value);
        }
    }


    /// <devdoc>
    ///    <para> 
    ///       Gets or 
    ///       sets the size of the send buffer in bytes.
    ///    </para> 
    /// </devdoc>
    public int SendBufferSize
    {
        get
        {
            return numericOption(SocketOptionLevel.Socket,
                                    SocketOptionName.SendBuffer);
        }

        set
        {
            Client.SetSocketOption(SocketOptionLevel.Socket,
                                SocketOptionName.SendBuffer, value);
        }
    }

    /// <devdoc>
    ///    <para> 
    ///       Gets or sets the receive time out value of the connection in seconds. 
    ///    </para>
    /// </devdoc> 
    public int ReceiveTimeout
    {
        get
        {
            return numericOption(SocketOptionLevel.Socket,
                                    SocketOptionName.ReceiveTimeout);
        }
        set
        {
            Client.SetSocketOption(SocketOptionLevel.Socket,
                                SocketOptionName.ReceiveTimeout, value);
        }
    }

    /// <devdoc>
    ///    <para> 
    ///       Gets or sets the send time out value of the connection in seconds.
    ///    </para> 
    /// </devdoc> 
    public int SendTimeout
    {
        get
        {
            return numericOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout);
        }

        set
        {
            Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendTimeout, value);
        }
    }

    /// <devdoc> 
    ///    <para>
    ///       Gets or sets the value of the connection's linger option.
    ///    </para>
    /// </devdoc> 
    public LingerOption LingerState
    {
        get
        {
            return (LingerOption)Client.GetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger);
        }
        set
        {
            Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger, value);
        }
    }

    /// <devdoc>
    ///    <para> 
    ///       Enables or disables delay when send or receive buffers are full. 
    ///    </para>
    /// </devdoc> 
    public bool NoDelay
    {
        get
        {
            return numericOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay) != 0 ? true : false;
        }
        set
        {
            Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, value ? 1 : 0);
        }
    }

    private void initialize()
    {
        //
        // IPv6: Use the address family from the constructor (or Connect method)
        // 
        Client = new Socket(m_Family, SocketType.Stream, ProtocolType.Tcp);
        m_Active = false;
    }

    private int numericOption(SocketOptionLevel optionLevel, SocketOptionName optionName)
    {
        return (int)Client.GetSocketOption(optionLevel, optionName);
    }

};