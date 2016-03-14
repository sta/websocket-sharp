#if (DNXCORE50 || UAP10_0 || DOTNET5_4)
namespace System.Threading
{
    public static class WaitHandleExtensions
    {
        public static void Close(this WaitHandle handle)
        {
            handle.Dispose();
        }
    }
}
#endif