using System.Globalization;

namespace ProjectLucid.Offline
{
    // Managed offline policies; no original native import or studio session runs.
    internal static class LocalCoreNativeCalls
    {
        internal static void Initialise(string gameObjectName) { }
        internal static string GetLocaleString() => CultureInfo.CurrentCulture.Name.Replace('-', '_');
        internal static string GetLanguageCode() => CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
        // Resolve the current culture on every call. Mono caches RegionInfo.CurrentRegion
        // from its first culture and rejects neutral cultures, so a neutral culture uses
        // its default specific culture; only the invariant culture keeps the runtime result.
        internal static string GetISO2CountryCode()
        {
            CultureInfo culture = CultureInfo.CurrentCulture;
            if (culture.Equals(CultureInfo.InvariantCulture))
                return RegionInfo.CurrentRegion.TwoLetterISORegionName;
            if (culture.IsNeutralCulture)
                culture = CultureInfo.CreateSpecificCulture(culture.Name);
            return new RegionInfo(culture.LCID).TwoLetterISORegionName;
        }
        internal static string GetClientCode() => "0";
    }
}
