using System.Threading.Tasks;

namespace Example4
{
    public interface IConnect
    {
        /// <summary>
        /// Create connection. This method is used before every message is sent to the server
        /// </summary>
        /// <param name="Port">Port</param>
        /// <returns>IConnect</returns>
        IConnect Connection(string Url, string Port);

        /// <summary>
        /// Sends a message to the server
        /// </summary>
        /// <param name="message">Text message</param>
        /// <returns>Server response</returns>
        Task<string> SendMessage(string message);

        /// <summary>
        /// Sends a message to the server
        /// </summary>
        /// <param name="message">Binnary message</param>
        /// <returns>Server response</returns>
        Task<string> SendMessage(byte[] message);

        /// <summary>
        /// Sends a message to the server and get byte data
        /// </summary>
        /// <param name="message">Text message</param>
        /// <returns>Bytes</returns>
        Task<byte[]> GetBytes(string message);

        /// <summary>
        /// Sends a message to the server and get byte data
        /// </summary>
        /// <param name="message">Binnary message</param>
        /// <returns>Bytes</returns>
        Task<byte[]> GetBytes(byte[] message);

        /// <summary>
        /// Waiting response
        /// </summary>
        /// <returns>Response</returns>
        Task<bool> WaitAsync();
    }
}