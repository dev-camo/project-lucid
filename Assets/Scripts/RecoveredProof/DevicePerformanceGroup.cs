using System;
using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "DevicePerformanceGroup", menuName = "HardlightProject/DefinitionData/Groups/DevicePerformanceGroup")]
    public class DevicePerformanceGroup : DefinitionDataType<RuntimePlatform, DevicePerformance>
    {
        // Original06002d99 calls the real virtual Platform getter.
        protected override RuntimePlatform GetElementKey(DevicePerformance data) { return data.Platform; }
        // Original06002d9a returns null; the actual definition base handles it.
        protected override IEqualityComparer<RuntimePlatform> GetKeyComparer() { return null; }
        // Original06002d9b is the natural genuine definition-base constructor.
    }
}
