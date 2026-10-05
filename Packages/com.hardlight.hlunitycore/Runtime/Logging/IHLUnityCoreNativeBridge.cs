using System;
using UnityEngine;

namespace Hardlight
{
    public interface IHLUnityCoreNativeBridge
    {
        void Initialise(HLUnityCore componentOwner);
        string GetDeviceOS();
        string GetDeviceID();
        string GetDeviceRawLocale();
        string GetDeviceLanguageCode();
        string GetDeviceISO2CountryCode();
        int GetClientCode(out uint clientCode);
        int GetScreenUnusableHeaderHeightInPixels();
        int GetScreenUnusableFooterHeightInPixels();
        bool DoesThisDeviceHaveANotch();
        void LogToDeviceConsole(string message);
        int GetPixelsFromNativeUnitDistance(float distance);
        int CalculateApplicationChecksum();

        // Original 0x06000933; ARM64 0x1af5cf4. This original default interface
        // body returns safeArea.yMax directly, without subtracting screen height.
        int GetBottomSafeAreaInsetInPixels()
        {
            float value = Screen.safeArea.yMax;
            if (float.IsPositiveInfinity(value)) return int.MinValue;
            if (float.IsNaN(value)) return 0;
            if (value >= 2147483648f) return int.MaxValue;
            if (value <= -2147483648f) return int.MinValue;
            return (int)value;
        }

        // Original 0x06000934; ARM64 0x1af5d28. Preserve safeArea.y and the
        // original FCVTZS/positive-infinity conversion semantics.
        int GetTopSafeAreaInsetInPixels()
        {
            float value = Screen.safeArea.y;
            if (float.IsPositiveInfinity(value)) return int.MinValue;
            if (float.IsNaN(value)) return 0;
            if (value >= 2147483648f) return int.MaxValue;
            if (value <= -2147483648f) return int.MinValue;
            return (int)value;
        }
    }
}
