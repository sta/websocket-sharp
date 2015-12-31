/*
 * ServerSslConfiguration.cs
 *
 * The MIT License
 *
 * Copyright (c) 2014 liryna
 * Copyright (c) 2014 sta.blockhead
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

/*
 * Authors:
 * - Liryna <liryna.stark@gmail.com>
 */

namespace WebSocketSharp.Net
{
    using System.Net.Security;
    using System.Security.Authentication;
    using System.Security.Cryptography.X509Certificates;

    /// <summary>
    /// Stores the parameters used to configure a <see cref="SslStream"/> instance as a server.
    /// </summary>
    public class ServerSslConfiguration : SslConfiguration
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ServerSslConfiguration"/> class with
        /// the specified <paramref name="serverCertificate"/>.
        /// </summary>
        /// <param name="serverCertificate">
        /// A <see cref="X509Certificate2"/> that represents the certificate used to authenticate
        /// the server.
        /// </param>
        public ServerSslConfiguration(X509Certificate2 serverCertificate)
            : this(serverCertificate, false, SslProtocols.Default, false)
        {
        }
        
        /// <summary>
        /// Initializes a new instance of the <see cref="ServerSslConfiguration"/> class with
        /// the specified <paramref name="serverCertificate"/>,
        /// <paramref name="clientCertificateRequired"/>, <paramref name="enabledSslProtocols"/>,
        /// and <paramref name="checkCertificateRevocation"/>.
        /// </summary>
        /// <param name="serverCertificate">
        /// A <see cref="X509Certificate2"/> that represents the certificate used to authenticate
        /// the server.
        /// </param>
        /// <param name="clientCertificateRequired">
        /// <c>true</c> if the client must supply a certificate for authentication;
        /// otherwise, <c>false</c>.
        /// </param>
        /// <param name="enabledSslProtocols">
        /// The <see cref="SslProtocols"/> enum value that represents the protocols used for
        /// authentication.
        /// </param>
        /// <param name="checkCertificateRevocation">
        /// <c>true</c> if the certificate revocation list is checked during authentication;
        /// otherwise, <c>false</c>.
        /// </param>
        public ServerSslConfiguration(
            X509Certificate2 serverCertificate,
            bool clientCertificateRequired,
            SslProtocols enabledSslProtocols = SslProtocols.Default,
            bool checkCertificateRevocation = false)
      : base(enabledSslProtocols, checkCertificateRevocation)
        {
            ServerCertificate = serverCertificate;
            ClientCertificateRequired = clientCertificateRequired;
            EnabledSslProtocols = enabledSslProtocols;
            CheckCertificateRevocation = checkCertificateRevocation;
        }

        /// <summary>
        /// Gets or sets a value indicating whether the client must supply a certificate for
        /// authentication.
        /// </summary>
        /// <value>
        /// <c>true</c> if the client must supply a certificate; otherwise, <c>false</c>.
        /// </value>
        public bool ClientCertificateRequired { get; private set; }

        /// <summary>
        /// Gets or sets the callback used to validate the certificate supplied by the client.
        /// </summary>
        /// <remarks>
        /// If this callback returns <c>true</c>, the client certificate will be valid.
        /// </remarks>
        /// <value>
        /// A <see cref="RemoteCertificateValidationCallback"/> delegate that references the method
        /// used to validate the client certificate. The default value is a function that only returns
        /// <c>true</c>.
        /// </value>
        public RemoteCertificateValidationCallback ClientCertificateValidationCallback { get; private set; }

        /// <summary>
        /// Gets or sets the certificate used to authenticate the server on the secure connection.
        /// </summary>
        /// <value>
        /// A <see cref="X509Certificate2"/> that represents the certificate used to authenticate
        /// the server.
        /// </value>
        public X509Certificate2 ServerCertificate { get; private set; }
    }
}
