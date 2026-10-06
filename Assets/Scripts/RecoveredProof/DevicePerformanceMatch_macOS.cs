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
    public class DevicePerformanceMatch_macOS : DevicePerformanceMatch
    {
        [SerializeField] private MacDeviceGeneration m_deviceGeneration;
        public MacDeviceGeneration MacDevice { get { return m_deviceGeneration; } }
        public DevicePerformanceMatch_macOS(MacDeviceGeneration macDevice, PerformanceProfile profile) : base(profile)
        {
            m_deviceGeneration = macDevice;
        }
        // Original06002da7 uses the shared engine model query once, then
        // exact case-sensitive original model strings. Unknown models return
        // High; no current-hardware inference or portable tier is substituted.
        public MacDeviceGeneration MatchMacToDeviceGeneration()
        {
            switch (SystemInfo.deviceModel)
            {
                case "iMac13,2": return MacDeviceGeneration.VeryLow;
                case "MacBookPro13,3": return MacDeviceGeneration.VeryLow;
                case "iMac14,3": return MacDeviceGeneration.VeryLow;
                case "MacBookAir7,2": return MacDeviceGeneration.VeryLow;
                case "Macmini9,1": return MacDeviceGeneration.Medium;
                case "iMac17,1": return MacDeviceGeneration.Low;
                case "MacBookPro14,3": return MacDeviceGeneration.Low;
                case "MacBookPro11,5": return MacDeviceGeneration.VeryLow;
                case "iMac18,2": return MacDeviceGeneration.Medium;
                case "MacBookAir9,1": return MacDeviceGeneration.VeryLow;
                case "MacBookPro16,3": return MacDeviceGeneration.Medium;
                case "Macmini7,1": return MacDeviceGeneration.VeryLow;
                case "iMac15,1": return MacDeviceGeneration.VeryLow;
                case "MacBookPro11,4": return MacDeviceGeneration.VeryLow;
                case "MacBook8,1": return MacDeviceGeneration.VeryLow;
                case "iMac14,4": return MacDeviceGeneration.VeryLow;
                case "MacBookAir6,1": return MacDeviceGeneration.VeryLow;
                case "iMac19,2": return MacDeviceGeneration.Medium;
                case "MacBookPro14,1": return MacDeviceGeneration.Low;
                case "iMac18,1": return MacDeviceGeneration.Medium;
                case "MacBookPro15,1": return MacDeviceGeneration.Medium;
                case "MacBookPro13,2": return MacDeviceGeneration.VeryLow;
                case "MacBookPro10,2": return MacDeviceGeneration.VeryLow;
                case "iMac20,1": return MacDeviceGeneration.Medium;
                case "iMac13,1": return MacDeviceGeneration.VeryLow;
                case "MacBookPro10,1": return MacDeviceGeneration.VeryLow;
                case "Macmini8,1": return MacDeviceGeneration.Low;
                case "MacBookAir8,2": return MacDeviceGeneration.VeryLow;
                case "iMac14,2": return MacDeviceGeneration.VeryLow;
                case "MacBookAir7,1": return MacDeviceGeneration.VeryLow;
                case "IMac16,2": return MacDeviceGeneration.Low;
                case "MacBookPro15,4": return MacDeviceGeneration.Medium;
                case "MacBookPro11,2": return MacDeviceGeneration.VeryLow;
                case "MacBook10,1": return MacDeviceGeneration.Low;
                case "MacBookPro16,1": return MacDeviceGeneration.Medium;
                case "Macmini6,2": return MacDeviceGeneration.VeryLow;
                case "iMac19,1": return MacDeviceGeneration.Medium;
                case "MacBookPro12,1": return MacDeviceGeneration.VeryLow;
                case "MacBookAir8,1": return MacDeviceGeneration.VeryLow;
                case "iMac14,1": return MacDeviceGeneration.VeryLow;
                case "Mac14,7": return MacDeviceGeneration.Ultra;
                case "Macmini6,1": return MacDeviceGeneration.VeryLow;
                case "iMac16,1": return MacDeviceGeneration.Low;
                case "MacBookPro14,2": return MacDeviceGeneration.Low;
                case "MacBook9,1": return MacDeviceGeneration.Low;
                case "MacBookPro11,3": return MacDeviceGeneration.VeryLow;
                case "MacBookPro11,1": return MacDeviceGeneration.VeryLow;
                case "MacBookAir5,2": return MacDeviceGeneration.VeryLow;
                case "iMac18,3": return MacDeviceGeneration.Medium;
                case "MacBookPro15,2": return MacDeviceGeneration.Medium;
                case "MacBookPro15,3": return MacDeviceGeneration.Medium;
                case "MacPro6,1": return MacDeviceGeneration.Low;
                case "MacBookPro13,1": return MacDeviceGeneration.VeryLow;
                case "MacBookAir6,2": return MacDeviceGeneration.VeryLow;
                case "iMacPro1,1": return MacDeviceGeneration.Medium;
                case "Mac14,10": return MacDeviceGeneration.High;
                case "iMac20,2": return MacDeviceGeneration.Medium;
                default: return MacDeviceGeneration.High;
            }
        }
        public override void OnBeforeSerialize() { name = m_deviceGeneration.ToString(); }
        public override void OnAfterDeserialize() { name = m_deviceGeneration.ToString(); }
    }
}
