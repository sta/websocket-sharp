using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace WebSocketSharp
{
    public class ClientCertAuthConfiguration
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
        /// Initializes a new instance of the <see cref="ClientCertAuthConfiguration"/> class.
        /// </summary>
        public ClientCertAuthConfiguration(X509CertificateCollection clientCertificates,
            SslProtocols enabledSslProtocols = SslProtocols.Default, bool checkCertificateRevocation = false)
        {
            this.clientCertificates = clientCertificates;
            this.EnabledSslProtocols = enabledSslProtocols;
            this.CheckCertificateRevocation = checkCertificateRevocation;
        }
    }
}