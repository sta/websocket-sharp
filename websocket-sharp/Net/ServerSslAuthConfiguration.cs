#region License
/*
 * ServerSslAuthConfiguration.cs
 *
 * The MIT License
 *
 * Copyright (c) 2014 liryna
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
 * - Liryna liryna.stark@gmail.com
 */
#endregion

using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace WebSocketSharp.Net
{
    /// <summary>
    /// Stores the parameters used in configuring <see cref="System.Net.Security.SslStream"/>
    /// as a server.
    /// </summary>
    public class ServerSslAuthConfiguration
    {
        /// <summary>
        /// Gets or sets the certificate used to authenticate the server on the secure connection.
        /// </summary>
        /// <value>
        /// A <see cref="X509Certificate2"/> that represents the certificate used to authenticate
        /// the server.
        /// </value>
        public X509Certificate2 ServerCertificate { get; set; }

        /// <summary>
        /// Gets or sets the client certificate request option.
        /// </summary>
        /// <value>
        /// A Boolean value that specifies whether the client must supply a certificate for authentication.
        /// </value>
        public bool ClientCertificateRequired { get; set; }

        /// <summary>
        /// Gets or sets the Ssl protocols type enabled.
        /// </summary>
        /// <value>
        /// The <see cref="SslProtocols"/> value that represents the protocol used for authentication.
        /// </value>
        public SslProtocols EnabledSslProtocols { get; set; }

        /// <summary>
        /// Gets or sets the verification of certificate revocation option.
        /// </summary>
        /// <value>
        /// A Boolean value that specifies whether the certificate revocation list is checked during authentication.
        /// </value>
        public bool CheckCertificateRevocation { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerSslAuthConfiguration"/> class.
        /// </summary>
        public ServerSslAuthConfiguration(X509Certificate2 serverCertificate)
            : this(serverCertificate, false, SslProtocols.Default, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerSslAuthConfiguration"/> class.
        /// </summary>
        public ServerSslAuthConfiguration(X509Certificate2 serverCertificate, bool clientCertificateRequired)
            : this(serverCertificate, clientCertificateRequired, SslProtocols.Default, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerSslAuthConfiguration"/> class.
        /// </summary>
        public ServerSslAuthConfiguration(X509Certificate2 serverCertificate, bool clientCertificateRequired,
            SslProtocols enabledSslProtocols)
            : this(serverCertificate, clientCertificateRequired, enabledSslProtocols, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ServerSslAuthConfiguration"/> class.
        /// </summary>
        public ServerSslAuthConfiguration(X509Certificate2 serverCertificate, bool clientCertificateRequired,
            SslProtocols enabledSslProtocols, bool checkCertificateRevocation)
        {
            this.ServerCertificate = serverCertificate;
            this.ClientCertificateRequired = clientCertificateRequired;
            this.EnabledSslProtocols = enabledSslProtocols;
            this.CheckCertificateRevocation = checkCertificateRevocation;
        }
    }
}