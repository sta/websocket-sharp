#if (DNXCORE50 || UAP10_0 || DOTNET5_4)
namespace System.Net
{
    public class WebException : InvalidOperationException
    {
        public WebExceptionStatus Status { get; } = WebExceptionStatus.UnknownError;

        public WebException()
        {
        }

        public WebException(string message) : this(message, null)
        {
        }

        public WebException(string message, Exception innerException) :
            base(message, innerException)
        {
        }

        public WebException(string message, WebExceptionStatus status) :
           base(message, null)
        {
            Status = status;
        }

        public WebException(string message, Exception innerException, WebExceptionStatus status, object response) :
            base(message, innerException)
        {
            Status = status;
        }
    }
}
#endif