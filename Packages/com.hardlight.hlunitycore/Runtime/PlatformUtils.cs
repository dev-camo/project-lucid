using Unity.IL2CPP.CompilerServices;
using UnityEngine;

// Original HLUnityCore.Runtime 0x02000007, 0x06000026..0x0600002a.
// Recovered shipped macOS variant; complete dependency context, zero save-body credit.
[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
[Il2CppSetOption(Option.NullChecks, false)]
public class PlatformUtils
{
    public static string GetActivePlatformIdentifier() { return "osx"; }
    public static string GetActivePlatformBundleId() { return Application.identifier; }
    public static bool ClearCache() { return Caching.ClearCache(); }
    public static bool ClearCache(int expiration) { return Caching.ClearCache(expiration); }
    public PlatformUtils() { }
}
