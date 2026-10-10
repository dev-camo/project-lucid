using System;
using System.Globalization;
using System.Runtime.InteropServices;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class HLUnityCoreNativeBridge : IHLUnityCoreNativeBridge
    {
        // Preserve original import signatures for research. Exact shipping
        // import descriptors are unresolved; the portable build keeps their
        // managed forwarding signatures and selects separate offline policies.
#if PROJECT_LUCID_ORIGINAL_UNITY_CORE_NATIVE_BRIDGE
#error Exact original native import descriptors are required before enabling this branch.
        [PreserveSig] private static extern void Unity_CoreInitialise(string gameObjectName);
        [PreserveSig] private static extern string Unity_GetLocaleString();
        [PreserveSig] private static extern string Unity_GetLanguageCode();
        [PreserveSig] private static extern string Unity_GetISO2CountryCode();
        [PreserveSig] private static extern string Unity_GetClientCode();
#else
        [PreserveSig]
        private static void Unity_CoreInitialise(string gameObjectName) => ProjectLucid.Offline.LocalCoreNativeCalls.Initialise(gameObjectName);

        [PreserveSig]
        private static string Unity_GetLocaleString() => ProjectLucid.Offline.LocalCoreNativeCalls.GetLocaleString();

        [PreserveSig]
        private static string Unity_GetLanguageCode() => ProjectLucid.Offline.LocalCoreNativeCalls.GetLanguageCode();

        [PreserveSig]
        private static string Unity_GetISO2CountryCode() => ProjectLucid.Offline.LocalCoreNativeCalls.GetISO2CountryCode();

        [PreserveSig]
        private static string Unity_GetClientCode() => ProjectLucid.Offline.LocalCoreNativeCalls.GetClientCode();
#endif

        // Original 0x06000918; ARM64 0x1af5b1c evaluates owner object/name before
        // crossing the adapted import boundary.
        public void Initialise(HLUnityCore componentOwner) => Unity_CoreInitialise(componentOwner.gameObject.name);

        // Original 0x06000919..1d, ARM64 0x1af5b58..0x1af5c40.
        public string GetDeviceOS() => SystemInfo.operatingSystem;
        public string GetDeviceID() => string.Empty;
        public string GetDeviceRawLocale() => Unity_GetLocaleString();
        public string GetDeviceLanguageCode() => Unity_GetLanguageCode();
        public string GetDeviceISO2CountryCode() => Unity_GetISO2CountryCode();

        // 0x0600091e; ARM64 0x1af5c48. Return status is one regardless of
        // parse success; failure writes zero. Success preserves signed bits.
        public int GetClientCode(out uint clientCode)
        {
            int parsed;
            bool success = int.TryParse(Unity_GetClientCode(), out parsed);
            clientCode = success ? unchecked((uint)parsed) : 0u;
            return 1;
        }

        // Original retail Mac bodies 0x0600091f..22 and 0x06000924.
        // These constants/RET come from native evidence, not boundary stubs.
        public int GetScreenUnusableHeaderHeightInPixels() => 0;
        public int GetScreenUnusableFooterHeightInPixels() => 0;
        public bool DoesThisDeviceHaveANotch() => false;
        public void LogToDeviceConsole(string message) { }
        public int CalculateApplicationChecksum() => 0;

        // 0x06000923; ARM64 0x1af5cd0, FCVTZS plus the positive-infinity
        // adjustment. Spell out native conversion results across managed JITs.
        public int GetPixelsFromNativeUnitDistance(float distance)
        {
            if (float.IsPositiveInfinity(distance)) return int.MinValue;
            if (float.IsNaN(distance)) return 0;
            if (distance >= 2147483648f) return int.MaxValue;
            if (distance <= -2147483648f) return int.MinValue;
            return (int)distance;
        }

        // Original 0x06000925; Object base constructor only.
        public HLUnityCoreNativeBridge() { }
    }
}
