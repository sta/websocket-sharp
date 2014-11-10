#region License
/*
 * ClientSslConfiguration.cs
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
	/// Stores the parameters used to configure a <see cref="SslStream"/> instance as a client.
	/// </summary>
	public class ClientSslConfiguration : SslConfiguration
	{
		#region Private Fields

		private X509CertificateCollection _certs;
		private string _host;

		#endregion

		/// <summary>
		/// Initializes a new instance of the <see cref="ClientSslConfiguration"/> class with
		/// the specified <paramref name="targetHost"/>, <paramref name="clientCertificates"/>,
		/// <paramref name="enabledSslProtocols"/>, and <paramref name="checkCertificateRevocation"/>.
		/// </summary>
		/// <param name="targetHost">
		/// A <see cref="string"/> that represents the name of the server that shares
		/// a secure connection.
		/// </param>
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
		public ClientSslConfiguration(
				string targetHost,
				X509CertificateCollection clientCertificates = null,
				SslProtocols enabledSslProtocols = SslProtocols.Default,
				LocalCertificateSelectionCallback certificateSelection = null,
				bool checkCertificateRevocation = false,
				RemoteCertificateValidationCallback certificateValidationCallback = null)
			: base(enabledSslProtocols, checkCertificateRevocation)
		{
			_host = targetHost;
			_certs = clientCertificates;
			ClientCertificates = clientCertificates;
			EnabledSslProtocols = enabledSslProtocols;
			CertificateSelection = certificateSelection;
			CheckCertificateRevocation = checkCertificateRevocation;
			CertificateValidationCallback = certificateValidationCallback;
		}

		public RemoteCertificateValidationCallback CertificateValidationCallback { get; private set; }

		/// <summary>
		/// Gets or sets a value indicating whether the certificate revocation list is checked
		/// during authentication.
		/// </summary>
		/// <value>
		/// <c>true</c> if the certificate revocation list is checked; otherwise, <c>false</c>.
		/// </value>
		public bool CheckCertificateRevocation { get; private set; }

		/// <summary>
		/// Gets or sets the collection that contains client certificates.
		/// </summary>
		/// <value>
		/// A <see cref="X509CertificateCollection"/> that contains client certificates.
		/// </value>
		public X509CertificateCollection ClientCertificates { get; private set; }

		/// <summary>
		/// Gets or sets the callback used to select a client certificate to supply to the server.
		/// </summary>
		/// <remarks>
		/// If this callback returns <see langword="null"/>, no client certificate will be supplied.
		/// </remarks>
		/// <value>
		/// A <see cref="LocalCertificateSelectionCallback"/> delegate that references the method
		/// used to select the client certificate. The default value is a function that only returns
		/// <see langword="null"/>.
		/// </value>
		public LocalCertificateSelectionCallback ClientCertificateSelectionCallback { get; private set; }

		/// <summary>
		/// Gets or sets the SSL protocols used for authentication.
		/// </summary>
		/// <value>
		/// The <see cref="SslProtocols"/> enum value that represents the protocols used for
		/// authentication.
		/// </value>
		public SslProtocols EnabledSslProtocols { get; private set; }

		public LocalCertificateSelectionCallback CertificateSelection { get; private set; }
	}
}
