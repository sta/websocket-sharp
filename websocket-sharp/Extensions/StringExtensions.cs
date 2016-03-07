using System;
using System.Globalization;

namespace WebSocketSharp
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

        public static bool EqualsInvariantCultureIgnoreCase(this string instance, string value)
        {
#if (DNXCORE50 || DOTNET5_4 || UAP10_0)
            return instance.Equals(value, StringComparison.OrdinalIgnoreCase);
#else
            return instance.Equals(value, StringComparison.InvariantCultureIgnoreCase);
#endif
        }

        public static bool EqualsInvariantCulture(this string instance, string value)
        {
#if (DNXCORE50 || DOTNET5_4 || UAP10_0)
            return instance.Equals(value, StringComparison.Ordinal);
#else
            return instance.Equals(value, StringComparison.InvariantCulture);
#endif
        }

        public static bool StartsWithInvariantCultureIgnoreCase(this string instance, string value)
        {
#if (DNXCORE50 || DOTNET5_4 || UAP10_0)
            return instance.StartsWith(value, StringComparison.OrdinalIgnoreCase);
#else
            return instance.StartsWith(value, StringComparison.InvariantCultureIgnoreCase);
#endif
        }

        public static bool StartsWithInvariantCulture(this string instance, string value)
        {
#if (DNXCORE50 || DOTNET5_4 || UAP10_0)
            return instance.StartsWith(value, StringComparison.Ordinal);
#else
            return instance.StartsWith(value, StringComparison.InvariantCulture);
#endif
        }
    }
}