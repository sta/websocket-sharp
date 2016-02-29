using System.Globalization;

namespace WebSocketSharp
{
    public static class CultureInfoHelper
    {
        public static CultureInfo CreateSpecificCulture(string name)
        {
#if (DNXCORE50 || UAP10_0 || DOTNET5_4)
            return new CultureInfo(name);
#else
            return CultureInfo.CreateSpecificCulture(name);
#endif
        }
    }
}