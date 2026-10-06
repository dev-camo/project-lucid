using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [CreateAssetMenu(fileName = "AnalyticsConfigurationGroup", menuName = "HardlightProject/DefinitionData/Groups/AnalyticsConfigurationGroup")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class AnalyticsConfigurationGroup : DefinitionDataType<string, AnalyticsConfiguration>
    {
        [SerializeField] private AnalyticsConfiguration m_default;
        // Original060019c0..c3, ARM0x513224..0x513294.
        public AnalyticsConfiguration Default => m_default;
        protected override string GetElementKey(AnalyticsConfiguration data) => data.name;
        protected override IEqualityComparer<string> GetKeyComparer() => null;
    }
}
