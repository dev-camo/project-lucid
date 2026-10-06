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
    public class DevicePerformanceMatch_tvOS : DevicePerformanceMatch
    {
        [SerializeField] private DeviceGeneration m_deviceGeneration;
        public DeviceGeneration Device { get { return m_deviceGeneration; } }
        public DevicePerformanceMatch_tvOS(DeviceGeneration device, PerformanceProfile profile) : base(profile)
        {
            m_deviceGeneration = device;
        }
        public override void OnBeforeSerialize() { name = m_deviceGeneration.ToString(); }
        public override void OnAfterDeserialize() { name = m_deviceGeneration.ToString(); }

        public enum DeviceGeneration
        {
            Unknown = 0,
            [Obsolete("AppleTV1Gen has been renamed. Use AppleTVHD instead (UnityUpgradable) -> AppleTVHD", false)]
            AppleTV1Gen = 1001,
            AppleTVHD = 1001,
            [Obsolete("AppleTV2Gen has been renamed. Use AppleTV4K instead (UnityUpgradable) -> AppleTV4K", false)]
            AppleTV2Gen = 1002,
            AppleTV4K = 1002,
            AppleTV4K2Gen = 1003,
            AppleTV4K3Gen = 1004
        }
    }
}
