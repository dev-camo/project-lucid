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
        // Original imports (PinvokeImpl, PreserveSig; the import module is not recovered, so no DllImport
        // is invented). They are declared like Cloud_MacOS declares its unresolved imports; CS0626 is
        // expected. The offline hooks below never reach them.
#pragma warning disable 0626
        [PreserveSig] private static extern void Unity_CoreInitialise(string gameObjectName);
        [PreserveSig] private static extern string Unity_GetLocaleString();
        [PreserveSig] private static extern string Unity_GetLanguageCode();
        [PreserveSig] private static extern string Unity_GetISO2CountryCode();
        [PreserveSig] private static extern string Unity_GetClientCode();
#pragma warning restore 0626

        // Original 0x06000918; ARM64 0x1af5b1c evaluates owner object/name before
        // crossing the adapted import boundary.
        public void Initialise(HLUnityCore componentOwner)
        {
            // Offline hook H-011: the original crossed the Unity_CoreInitialise import; we call the local forwarder (owner and name are still evaluated first)
            if (ProjectLucid.Offline.OfflineAdapters.Active)
            {
                ProjectLucid.Offline.LocalCoreNativeCalls.Initialise(componentOwner.gameObject.name);
                return;
            }
            Unity_CoreInitialise(componentOwner.gameObject.name);
        }

        // Original 0x06000919..1d, ARM64 0x1af5b58..0x1af5c40.
        public string GetDeviceOS() => SystemInfo.operatingSystem;
        public string GetDeviceID() => string.Empty;
        public string GetDeviceRawLocale()
        {
            if (ProjectLucid.Offline.OfflineAdapters.Active) return ProjectLucid.Offline.LocalCoreNativeCalls.GetLocaleString(); // Offline hook H-058: the original returned the Unity_GetLocaleString import; we return the local forwarder
            return Unity_GetLocaleString();
        }

        public string GetDeviceLanguageCode()
        {
            if (ProjectLucid.Offline.OfflineAdapters.Active) return ProjectLucid.Offline.LocalCoreNativeCalls.GetLanguageCode(); // Offline hook H-059: the original returned the Unity_GetLanguageCode import; we return the local forwarder
            return Unity_GetLanguageCode();
        }

        public string GetDeviceISO2CountryCode()
        {
            if (ProjectLucid.Offline.OfflineAdapters.Active) return ProjectLucid.Offline.LocalCoreNativeCalls.GetISO2CountryCode(); // Offline hook H-060: the original returned the Unity_GetISO2CountryCode import; we return the local forwarder
            return Unity_GetISO2CountryCode();
        }

        // 0x0600091e; ARM64 0x1af5c48. Return status is one regardless of
        // parse success; failure writes zero. Success preserves signed bits.
        public int GetClientCode(out uint clientCode)
        {
            int parsed;
            // Offline hook H-061: the original parsed the Unity_GetClientCode import; we parse the local forwarder's text the same way
            if (ProjectLucid.Offline.OfflineAdapters.Active)
            {
                bool localSuccess = int.TryParse(ProjectLucid.Offline.LocalCoreNativeCalls.GetClientCode(), out parsed);
                clientCode = localSuccess ? unchecked((uint)parsed) : 0u;
                return 1;
            }
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

        // 0x06000923. The universal original differs by slice, so spell out each
        // native result across managed JITs. ARM64 0x1af5cd0 is saturating FCVTZS
        // (NaN gives 0) except positive infinity; x86-64 0x1af16c0 is a bare
        // CVTTSS2SI, whose NaN and out-of-range results are int.MinValue.
        public int GetPixelsFromNativeUnitDistance(float distance)
        {
            if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                if (float.IsPositiveInfinity(distance)) return int.MinValue;
                if (float.IsNaN(distance)) return 0;
                if (distance >= 2147483648f) return int.MaxValue;
                if (distance <= -2147483648f) return int.MinValue;
                return (int)distance;
            }
            if (float.IsNaN(distance) || distance >= 2147483648f || distance < -2147483648f) return int.MinValue;
            return (int)distance;
        }

        // Original 0x06000925; Object base constructor only.
        public HLUnityCoreNativeBridge() { }
    }
}
