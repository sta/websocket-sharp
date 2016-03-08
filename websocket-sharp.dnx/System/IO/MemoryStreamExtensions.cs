#if (DNXCORE50 || UAP10_0 || DOTNET5_4)
namespace System.IO
{
    public static class MemoryStreamExtensions
    {
        // https://github.com/dotnet/corefx/issues/1897
        public static byte[] GetBuffer(this MemoryStream stream)
        {
            ArraySegment<byte> buffer;
            if (stream.TryGetBuffer(out buffer))
            {
                return buffer.Array;
            }

            return new byte[0];
        }
    }
}
#endif