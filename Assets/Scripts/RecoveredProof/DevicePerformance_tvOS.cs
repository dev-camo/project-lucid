using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "DevicePerformance_tvOS", menuName = "HardlightProject/DefinitionData/Definitions/DevicePerformance_tvOS")]
    public class DevicePerformance_tvOS : DevicePerformance
    {
        [SerializeField] private List<DevicePerformanceMatch_tvOS> m_devicePerformanceMatches;
        public override RuntimePlatform Platform { get { return RuntimePlatform.tvOS; } }
        // Original06002dab is base-only. The original has no GetMatch override.
    }
}
