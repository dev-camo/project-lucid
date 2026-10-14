using System;
using System.Globalization;

namespace ProjectLucid.Offline
{
    // Managed offline policies; no original native import or studio session runs.
    internal static class LocalCoreNativeCalls
    {
        internal static void Initialise(string gameObjectName) { }
        internal static string GetLocaleString() => CultureInfo.CurrentCulture.Name.Replace('-', '_');
        internal static string GetLanguageCode() => CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
        // The runtime's region code for the invariant culture; Mono cannot construct it
        // directly and only reports it through the process-wide RegionInfo.CurrentRegion cache.
        private const string InvariantRegionCode = "IV";
        // Offline policy for a neutral culture Mono cannot map to a region.
        private const string UnknownRegionCode = "";

        // Resolve the current culture on every call without RegionInfo.CurrentRegion, which
        // Mono caches from its first culture and rejects for neutral cultures. A neutral
        // culture uses its default specific culture when Mono has one.
        internal static string GetISO2CountryCode()
        {
            CultureInfo culture = CultureInfo.CurrentCulture;
            if (culture.Equals(CultureInfo.InvariantCulture))
                return InvariantRegionCode;
            if (culture.IsNeutralCulture)
            {
                try
                {
                    culture = CultureInfo.CreateSpecificCulture(culture.Name);
                }
                // Mono throws NotImplementedException for neutral cultures without a mapping.
                catch (Exception exception) when (exception is CultureNotFoundException || exception is NotImplementedException)
                {
                    return UnknownRegionCode;
                }
                if (culture.IsNeutralCulture)
                    return UnknownRegionCode;
            }
            return new RegionInfo(culture.Name).TwoLetterISORegionName;
        }

        internal static string GetClientCode() => "0";
    }
}
