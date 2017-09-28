using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WebSocketSharp.Net;

namespace WebSocketSharp
{
    public class ProxyHTTP : IProxy
    {
        private Logger _logger;
        private Uri _proxyUri;
        private NetworkCredential _proxyCredentials;
        private System.IO.Stream _stream;
        System.Net.Sockets.TcpClient _tcpClient;

        /// <summary>
        /// Occurs when the <see cref="WebSocket"/> gets an error.
        /// </summary>
        public event EventHandler<ErrorEventArgs> OnError;

        public ProxyHTTP(string url, string username, string password)
        {
            _logger = new Logger();

            if (url.IsNullOrEmpty()) {
                _logger.Warn("The url and credentials for the proxy are initialized.");
                _proxyUri = null;
                _proxyCredentials = null;

                return;
            }

            _proxyUri = new Uri(url);

            if (username.IsNullOrEmpty()) {
                _logger.Warn("The credentials for the proxy are initialized.");
                _proxyCredentials = null;

                return;
            }

            _proxyCredentials =
              new NetworkCredential(
                username, password, String.Format("{0}:{1}", _proxyUri.DnsSafeHost, _proxyUri.Port)
              );
        }

        public System.Net.Sockets.TcpClient ConnectThroughProxy(Uri uri)
        {
            _tcpClient = new System.Net.Sockets.TcpClient(_proxyUri.DnsSafeHost, _proxyUri.Port);
            _tcpClient.NoDelay = true;
            
            _stream = _tcpClient.GetStream();

            var req = HttpRequest.CreateConnectRequest(uri);
            var res = sendHttpRequest(req, 90000);
            if (res.IsProxyAuthenticationRequired) {
                var chal = res.Headers["Proxy-Authenticate"];
                _logger.Warn(
                  String.Format("Received a proxy authentication requirement for '{0}'.", chal));

                if (chal.IsNullOrEmpty())
                    throw new WebSocketException("No proxy authentication challenge is specified.");

                var authChal = AuthenticationChallenge.Parse(chal);
                if (authChal == null)
                    throw new WebSocketException("An invalid proxy authentication challenge is specified.");

                if (_proxyCredentials != null) {
                    if (res.HasConnectionClose) {
                        releaseClientResources();
                        _tcpClient = new System.Net.Sockets.TcpClient(_proxyUri.DnsSafeHost, _proxyUri.Port);
                        _tcpClient.NoDelay = true;
                        _stream = _tcpClient.GetStream();
                    }

                    var authRes = new AuthenticationResponse(authChal, _proxyCredentials, 0);
                    req.Headers["Proxy-Authorization"] = authRes.ToString();
                    res = sendHttpRequest(req, 15000);
                }

                if (res.IsProxyAuthenticationRequired)
                    throw new WebSocketException("A proxy authentication is required.");
            }

            if (res.StatusCode[0] != '2')
                throw new WebSocketException(
                  "The proxy has failed a connection to the requested host and port.");
            return _tcpClient;
        }
     
        private void releaseClientResources()
        {
            if (_stream != null) {
                _stream.Dispose();
                _stream = null;
            }

            if (_tcpClient != null) {
                _tcpClient.Close();
                _tcpClient = null;
            }
        }

        private HttpResponse sendHttpRequest(HttpRequest request, int millisecondsTimeout)
        {
            _logger.Debug("A request to the server:\n" + request.ToString());
            var res = request.GetResponse(_stream, millisecondsTimeout);
            _logger.Debug("A response to this request:\n" + res.ToString());

            return res;
        }

        private static bool checkParametersForSetProxy(string url, string username, string password, out string message)
        {
            message = null;

            if (url.IsNullOrEmpty())
                return true;

            Uri uri;
            if (!Uri.TryCreate(url, UriKind.Absolute, out uri)
                || uri.Scheme != "http"
                || uri.Segments.Length > 1
            ) {
                message = "'url' is an invalid URL.";
                return false;
            }

            if (username.IsNullOrEmpty())
                return true;

            if (username.Contains(':') || !username.IsText()) {
                message = "'username' contains an invalid character.";
                return false;
            }

            if (password.IsNullOrEmpty())
                return true;

            if (!password.IsText()) {
                message = "'password' contains an invalid character.";
                return false;
            }

            return true;
        }

        private void error(string message, Exception exception)
        {
            try
            {
                OnError.Emit(this, new ErrorEventArgs(message, exception));
            }
            catch (Exception ex)
            {
                _logger.Error(ex.ToString());
            }
        }
    }
}
