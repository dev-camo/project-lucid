using System.Globalization;

namespace ProjectLucid.Offline
{
    // Managed offline policies; no original native import or studio session runs.
    internal static class LocalCoreNativeCalls
    {
        internal static void Initialise(string gameObjectName) { }
        internal static string GetLocaleString() => CultureInfo.CurrentCulture.Name.Replace('-', '_');
        internal static string GetLanguageCode() => CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
        internal static string GetISO2CountryCode() => RegionInfo.CurrentRegion.TwoLetterISORegionName;
        internal static string GetClientCode() => "0";
    }
}
