using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class DevicePerformanceMatch_iOS : DevicePerformanceMatch
    {
        [SerializeField] private DeviceGeneration m_deviceGeneration;
        public DevicePerformanceMatch_iOS(DeviceGeneration device, PerformanceProfile profile) : base(profile)
        {
            m_deviceGeneration = device;
        }
        public DeviceGeneration Device { get { return m_deviceGeneration; } }
        public override void OnBeforeSerialize() { name = m_deviceGeneration.ToString(); }
        public override void OnAfterDeserialize() { name = m_deviceGeneration.ToString(); }

        public enum DeviceGeneration
        {
            Unknown = 0,
            iPhone = 1,
            iPhone3G = 2,
            iPhone3GS = 3,
            iPodTouch1Gen = 4,
            iPodTouch2Gen = 5,
            iPodTouch3Gen = 6,
            iPad1Gen = 7,
            iPhone4 = 8,
            iPodTouch4Gen = 9,
            iPad2Gen = 10,
            iPhone4S = 11,
            iPad3Gen = 12,
            iPhone5 = 13,
            iPodTouch5Gen = 14,
            iPadMini1Gen = 15,
            iPad4Gen = 16,
            iPhone5C = 17,
            iPhone5S = 18,
            iPadAir1 = 19,
            iPadMini2Gen = 20,
            iPhone6 = 21,
            iPhone6Plus = 22,
            iPadMini3Gen = 23,
            iPadAir2 = 24,
            iPhone6S = 25,
            iPhone6SPlus = 26,
            iPadPro1Gen = 27,
            iPadMini4Gen = 28,
            iPhoneSE1Gen = 29,
            iPadPro10Inch1Gen = 30,
            iPhone7 = 31,
            iPhone7Plus = 32,
            iPodTouch6Gen = 33,
            iPad5Gen = 34,
            iPadPro2Gen = 35,
            iPadPro10Inch2Gen = 36,
            iPhone8 = 37,
            iPhone8Plus = 38,
            iPhoneX = 39,
            iPhoneXS = 40,
            iPhoneXSMax = 41,
            iPhoneXR = 42,
            iPadPro11Inch = 43,
            iPadPro3Gen = 44,
            iPad6Gen = 45,
            iPadAir3Gen = 46,
            iPadMini5Gen = 47,
            iPhone11 = 48,
            iPhone11Pro = 49,
            iPhone11ProMax = 50,
            iPodTouch7Gen = 51,
            iPad7Gen = 52,
            iPhoneSE2Gen = 53,
            iPadPro11Inch2Gen = 54,
            iPadPro4Gen = 55,
            iPhone12Mini = 56,
            iPhone12 = 57,
            iPhone12Pro = 58,
            iPhone12ProMax = 59,
            iPad8Gen = 60,
            iPadAir4Gen = 61,
            iPad9Gen = 62,
            iPadMini6Gen = 63,
            iPhone13 = 64,
            iPhone13Mini = 65,
            iPhone13Pro = 66,
            iPhone13ProMax = 67,
            iPadPro5Gen = 68,
            iPadPro11Inch3Gen = 69,
            iPhoneSE3Gen = 70,
            iPadAir5Gen = 71,
            iPhone14 = 72,
            iPhone14Plus = 73,
            iPhone14Pro = 74,
            iPhone14ProMax = 75,
            iPadPro6Gen = 76,
            iPadPro11Inch4Gen = 77,
            iPad10Gen = 78,
            iPhoneUnknown = 10001,
            iPadUnknown = 10002,
            iPodTouchUnknown = 10003
        }
    }
}
