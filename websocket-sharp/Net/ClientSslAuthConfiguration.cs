#region License
/*
 * ClientSslAuthConfiguration.cs
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
#endregion

#region Authors
/*
 * Authors:
 * - Liryna <liryna.stark@gmail.com>
 */
#endregion

namespace WebSocketSharp.Net
{
	using System.Net.Security;
	using System.Security.Authentication;
	using System.Security.Cryptography.X509Certificates;

	/// <summary>
	/// Stores the parameters used in configuring <see cref="System.Net.Security.SslStream"/>
	/// as a client.
	/// </summary>
	public class ClientSslAuthConfiguration
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ClientSslAuthConfiguration"/> class with
		/// the specified <paramref name="clientCertificates"/>.
		/// </summary>
		/// <param name="clientCertificates">
		/// A <see cref="X509CertificateCollection"/> that contains client certificates.
		/// </param>
		public ClientSslAuthConfiguration(X509CertificateCollection clientCertificates)
			: this(clientCertificates, SslProtocols.Default, null, false)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ClientSslAuthConfiguration"/> class with
		/// the specified <paramref name="clientCertificates"/> and
		/// <paramref name="enabledSslProtocols"/>.
		/// </summary>
		/// <param name="clientCertificates">
		/// A <see cref="X509CertificateCollection"/> that contains client certificates.
		/// </param>
		/// <param name="enabledSslProtocols">
		/// The <see cref="SslProtocols"/> enum value that represents the protocols used for
		/// authentication.
		/// </param>
		public ClientSslAuthConfiguration(
		  X509CertificateCollection clientCertificates, SslProtocols enabledSslProtocols)
			: this(clientCertificates, enabledSslProtocols, null, false)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ClientSslAuthConfiguration"/> class with
		/// the specified <paramref name="clientCertificates"/>, <paramref name="enabledSslProtocols"/>,
		/// and <paramref name="checkCertificateRevocation"/>.
		/// </summary>
		/// <param name="clientCertificates">
		/// A <see cref="X509CertificateCollection"/> that contains client certificates.
		/// </param>
		/// <param name="enabledSslProtocols">
		/// The <see cref="SslProtocols"/> enum value that represents the protocols used for
		/// authentication.
		/// </param>
		/// <param name="checkCertificateRevocation">
		/// <c>true</c> if the certificate revocation list is checked during authentication;
		/// otherwise, <c>false</c>.
		/// </param>
		public ClientSslAuthConfiguration(
		  X509CertificateCollection clientCertificates,
		  SslProtocols enabledSslProtocols,
		LocalCertificateSelectionCallback certificateSelection,
		  bool checkCertificateRevocation)
		{
			ClientCertificates = clientCertificates;
			EnabledSslProtocols = enabledSslProtocols;
			CertificateSelection = certificateSelection;
			CheckCertificateRevocation = checkCertificateRevocation;
		}

		/// <summary>
		/// Gets or sets a value indicating whether the certificate revocation list is checked
		/// during authentication.
		/// </summary>
		/// <value>
		/// <c>true</c> if the certificate revocation list is checked; otherwise, <c>false</c>.
		/// </value>
		public bool CheckCertificateRevocation { get; set; }

		/// <summary>
		/// Gets or sets the collection that contains client certificates.
		/// </summary>
		/// <value>
		/// A <see cref="X509CertificateCollection"/> that contains client certificates.
		/// </value>
		public X509CertificateCollection ClientCertificates { get; set; }

		/// <summary>
		/// Gets or sets the SSL protocols used for authentication.
		/// </summary>
		/// <value>
		/// The <see cref="SslProtocols"/> enum value that represents the protocols used for
		/// authentication.
		/// </value>
		public SslProtocols EnabledSslProtocols { get; set; }

		public LocalCertificateSelectionCallback CertificateSelection { get; set; }
	}
}