using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000463; complete 3 own fields/4 own methods.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "CameraAbilityDefinition", menuName = "HardlightProject/DefinitionData/Definitions/CameraAbilityDefinition")]
    public class CameraAbilityDefinition : ScriptableObject
    {
        // 0x04000f12; original instance offset 0x18.
        [SerializeField]
        private CameraType m_type;
        // 0x04000f13; original instance offset 0x20.
        [SerializeField]
        private CameraProxyTargetSettings m_proxyTargetSettings;
        // 0x04000f14; original instance offset 0x60.
        [SerializeField]
        private CameraHeadingOverride.CameraHeadingOverrideSettings m_headingOverrideSettings;

        // 0x060019b0..0x060019b2: original direct field getters; settings copied by value.
        public CameraType Type => m_type;
        public CameraProxyTargetSettings ProxyTargetSettings => m_proxyTargetSettings;
        public CameraHeadingOverride.CameraHeadingOverrideSettings HeadingOverrideSettings => m_headingOverrideSettings;

        // 0x060019b3: engine base only; value settings stay zero, heading settings stay null.
        public CameraAbilityDefinition() { }
    }
}
