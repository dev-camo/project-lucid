using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "DevicePerformance_iOS", menuName = "HardlightProject/DefinitionData/Definitions/DevicePerformance_iOS")]
    public class DevicePerformance_iOS : DevicePerformance
    {
        [SerializeField] private List<DevicePerformanceMatch_iOS> m_devicePerformanceMatches;
        private readonly Dictionary<string, DevicePerformanceMatch_iOS.DeviceGeneration> m_deviceFallbacks =
            new Dictionary<string, DevicePerformanceMatch_iOS.DeviceGeneration>
            {
                { "iPad", DevicePerformanceMatch_iOS.DeviceGeneration.iPadUnknown },
                { "iPod", DevicePerformanceMatch_iOS.DeviceGeneration.iPodTouchUnknown }
            };
        // Original06002d9c returns8. The original type has no GetMatch override.
        public override RuntimePlatform Platform { get { return RuntimePlatform.IPhonePlayer; } }
        // Original06002d9d publishes the two-entry dictionary before its base;
        // the authored matches list stays null. No device matching is invented.
    }
}
