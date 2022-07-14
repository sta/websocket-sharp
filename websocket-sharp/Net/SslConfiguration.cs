#region License
/*
 * SslConfiguration.cs
 *
 * This code is derived from ClientSslConfiguration.cs.
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

using System.Net.Security;
using System.Security.Authentication;

namespace WebSocketSharp.Net
{
  /// <summary>
  /// Stores the parameters used to configure a <see cref="SslStream"/> instance.
  /// </summary>
  /// <remarks>
  /// The SslConfiguration class is an abstract class.
  /// </remarks>
  public abstract class SslConfiguration
  {
    #region Private Fields

    private LocalCertificateSelectionCallback   _certSelectionCallback;
    private RemoteCertificateValidationCallback _certValidationCallback;
    private bool                                _checkCertRevocation;
    private SslProtocols                        _enabledProtocols;

    #endregion

    #region Protected Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="SslConfiguration"/> class with
    /// the specified <paramref name="enabledSslProtocols"/> and
    /// <paramref name="checkCertificateRevocation"/>.
    /// </summary>
    /// <param name="enabledSslProtocols">
    /// The <see cref="SslProtocols"/> enum value that represents the protocols used for
    /// authentication.
    /// </param>
    /// <param name="checkCertificateRevocation">
    /// <c>true</c> if the certificate revocation list is checked during authentication;
    /// otherwise, <c>false</c>.
    /// </param>
    protected SslConfiguration (SslProtocols enabledSslProtocols, bool checkCertificateRevocation)
    {
      _enabledProtocols = enabledSslProtocols;
      _checkCertRevocation = checkCertificateRevocation;
    }

    #endregion

    #region Protected Properties

    /// <summary>
    /// Gets or sets the callback used to select a certificate to supply to the remote party.
    /// </summary>
    /// <remarks>
    /// If this callback returns <see langword="null"/>, no certificate will be supplied.
    /// </remarks>
    /// <value>
    /// A <see cref="LocalCertificateSelectionCallback"/> delegate that references the method
    /// used to select a certificate. The default value is a function that only returns
    /// <see langword="null"/>.
    /// </value>
    protected LocalCertificateSelectionCallback CertificateSelectionCallback {
      get {
        return _certSelectionCallback ??
               (_certSelectionCallback =
                 (sender, targetHost, localCertificates, remoteCertificate, acceptableIssuers) =>
                   null);
      }

      set {
        _certSelectionCallback = value;
      }
    }

    /// <summary>
    /// Gets or sets the callback used to validate the certificate supplied by the remote party.
    /// </summary>
    /// <remarks>
    /// If this callback returns <c>true</c>, the certificate will be valid.
    /// </remarks>
    /// <value>
    /// A <see cref="RemoteCertificateValidationCallback"/> delegate that references the method
    /// used to validate the certificate. The default value is a function that only returns
    /// <c>true</c>.
    /// </value>
    protected RemoteCertificateValidationCallback CertificateValidationCallback {
      get {
        return _certValidationCallback ??
               (_certValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true);
      }

      set {
        _certValidationCallback = value;
      }
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets or sets a value indicating whether the certificate revocation list is checked
    /// during authentication.
    /// </summary>
    /// <value>
    /// <c>true</c> if the certificate revocation list is checked; otherwise, <c>false</c>.
    /// </value>
    public bool CheckCertificateRevocation {
      get {
        return _checkCertRevocation;
      }

      set {
        _checkCertRevocation = value;
      }
    }

    /// <summary>
    /// Gets or sets the SSL protocols used for authentication.
    /// </summary>
    /// <value>
    /// The <see cref="SslProtocols"/> enum value that represents the protocols used for
    /// authentication.
    /// </value>
    public SslProtocols EnabledSslProtocols {
      get {
        return _enabledProtocols;
      }

      set {
        _enabledProtocols = value;
      }
    }

    #endregion
  }
}
