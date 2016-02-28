#if DNXCORE50 || UAP10_0 || DOTNET5_4
using System;
using System.Globalization;

namespace System
{
    public static class StringExtensions
    {
#if (DNXCORE50 || DOTNET5_4 || UAP10_0)
        public static string ToLower(this string input, CultureInfo culture)
        {
            if (culture == null)
                throw new ArgumentNullException("culture");

            return input.ToLower();
        }

        public static string ToUpper(this string input, CultureInfo culture)
        {
            if (culture == null)
                throw new ArgumentNullException("culture");

            return input.ToUpper();
        }
#endif
    }
}
#endif