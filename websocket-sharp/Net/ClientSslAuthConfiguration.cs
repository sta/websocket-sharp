#region License
/*
 * ClientSslAuthConfiguration.cs
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

namespace WebSocketSharp
{
    public class ClientSslAuthConfiguration
    {
        /// <summary>
        /// Gets or sets the certificate configuration used to authenticate the clients on the secure connection.
        /// </summary>
        /// <value>
        /// A <see cref="X509CertificateCollection"/> that represents the certificate collection used to authenticate
        /// the clients.
        /// </value>
        public X509CertificateCollection clientCertificates { get; set; }

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
        /// Initializes a new instance of the <see cref="ClientSslAuthConfiguration"/> class.
        /// </summary>
        public ClientSslAuthConfiguration(X509CertificateCollection clientCertificates)
            : this(clientCertificates, SslProtocols.Default, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientSslAuthConfiguration"/> class.
        /// </summary>
        public ClientSslAuthConfiguration(X509CertificateCollection clientCertificates,
          SslProtocols enabledSslProtocols)
            : this(clientCertificates, enabledSslProtocols, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ClientSslAuthConfiguration"/> class.
        /// </summary>
        public ClientSslAuthConfiguration(X509CertificateCollection clientCertificates,
            SslProtocols enabledSslProtocols, bool checkCertificateRevocation)
        {
            this.clientCertificates = clientCertificates;
            this.EnabledSslProtocols = enabledSslProtocols;
            this.CheckCertificateRevocation = checkCertificateRevocation;
        }
    }
}