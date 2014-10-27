using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace WebSocketSharp
{
    public class ServerCertAuthConfiguration
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
        /// Initializes a new instance of the <see cref="ServerCertAuthConfiguration"/> class.
        /// </summary>
        public ServerCertAuthConfiguration(X509Certificate2 serverCertificate, bool clientCertificateRequired = false,
            SslProtocols enabledSslProtocols = SslProtocols.Default, bool checkCertificateRevocation = false)
        {
            this.ServerCertificate = serverCertificate;
            this.ClientCertificateRequired = clientCertificateRequired;
            this.EnabledSslProtocols = enabledSslProtocols;
            this.CheckCertificateRevocation = checkCertificateRevocation;
        }
    }
}