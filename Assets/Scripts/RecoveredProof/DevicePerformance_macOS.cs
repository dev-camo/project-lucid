using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "DevicePerformance_macOS", menuName = "HardlightProject/DefinitionData/Definitions/DevicePerformance_macOS")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class DevicePerformance_macOS : DevicePerformance
    {
        [SerializeField] private List<DevicePerformanceMatch_macOS> m_devicePerformanceMatches;
        public override RuntimePlatform Platform { get { return RuntimePlatform.OSXPlayer; } }
        // Original06002da3 queries deviceModel once before acquiring the list
        // enumerator, then each row's real matcher queries it again. Preserve
        // the unused first query, row getter/call ordering, and disposal.
        public override PerformanceProfile GetMatch()
        {
            _ = SystemInfo.deviceModel;
            foreach (DevicePerformanceMatch_macOS match in m_devicePerformanceMatches)
                if (match.MacDevice == match.MatchMacToDeviceGeneration())
                    return match.Profile;
            return m_fallback;
        }
        // Original06002da4 is the natural base-only constructor.
    }
}
