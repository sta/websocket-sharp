namespace WebSocketSharp
{
    using System.Threading.Tasks;

    internal static class AsyncEx
    {
        public static Task Completed()
        {
            return Task.FromResult(0);
        }
    }
}