using System;
using System.Globalization;

namespace ProjectLucid.Tests
{
    // The invariant culture with "~" as its negative sign, as the current culture for one call. Int32.ToString() follows the current
    // culture and Int32.ToString(CultureInfo.InvariantCulture) does not, so a negative number tells the two overloads apart.
    internal static class TildeNegativeSignCulture
    {
        internal static void Run(Action body)
        {
            CultureInfo prior = CultureInfo.CurrentCulture;
            var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            culture.NumberFormat.NegativeSign = "~";
            CultureInfo.CurrentCulture = culture;
            try
            {
                body();
            }
            finally
            {
                CultureInfo.CurrentCulture = prior;
            }
        }
    }
}
