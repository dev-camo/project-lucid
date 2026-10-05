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
        // Explicit local desktop boundary adaptations for the five original
        // studio-plugin imports 0x06000913..17. These managed implementations
        // preserve signatures/identities, but remove the original P/Invoke flag.
        // They are not counted as recovered original native behavior. There is
        // no studio callback target, client identity or remote-service session.
        [PreserveSig]
        private static void Unity_CoreInitialise(string gameObjectName) { }

        [PreserveSig]
        private static string Unity_GetLocaleString() => CultureInfo.CurrentCulture.Name.Replace('-', '_');

        [PreserveSig]
        private static string Unity_GetLanguageCode() => CultureInfo.CurrentCulture.TwoLetterISOLanguageName;

        [PreserveSig]
        private static string Unity_GetISO2CountryCode() => RegionInfo.CurrentRegion.TwoLetterISORegionName;

        [PreserveSig]
        private static string Unity_GetClientCode() => "0";

        // Original 0x06000918; ARM64 0x1af5b1c evaluates owner object/name before
        // crossing the adapted import boundary.
        public void Initialise(HLUnityCore componentOwner) => Unity_CoreInitialise(componentOwner.gameObject.name);

        // Original 0x06000919..1d, ARM64 0x1af5b58..0x1af5c40.
        public string GetDeviceOS() => SystemInfo.operatingSystem;
        public string GetDeviceID() => string.Empty;
        public string GetDeviceRawLocale() => Unity_GetLocaleString();
        public string GetDeviceLanguageCode() => Unity_GetLanguageCode();
        public string GetDeviceISO2CountryCode() => Unity_GetISO2CountryCode();

        // 0x0600091e; ARM64 0x1af5c48. Parse success is ignored; original return
        // is one even when parsing fails and the out value becomes zero.
        public int GetClientCode(out uint clientCode)
        {
            uint.TryParse(Unity_GetClientCode(), out clientCode);
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
