using System.IO;

namespace System.IO
{
#if (DNXCORE50 || UAP10_0 || DOTNET5_4)
    public static class StreamExtensions
    {
        public static void Close(this Stream stream)
        {
            stream.Dispose();
        }
    }
#endif
}