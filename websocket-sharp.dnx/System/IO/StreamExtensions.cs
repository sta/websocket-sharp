#if (DNXCORE50 || UAP10_0 || DOTNET5_4)
namespace System.IO
{
    public static class StreamExtensions
    {
        public static void Close(this Stream stream)
        {
            stream.Dispose();
        }
    }
}
#endif