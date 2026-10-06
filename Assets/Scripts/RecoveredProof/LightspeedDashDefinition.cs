using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "LightspeedDashDefinition", menuName = "HardlightProject/DefinitionData/Definitions/LightspeedDashDefinition")]
    public class LightspeedDashDefinition : HardlightProject.TrackerMetadataDefinition
    {
        [Tooltip("Defines the lightspeed dash type.")]
        [SerializeField]
        private HardlightProject.LightspeedDashType m_type;

        // Original Game.Runtime 0x06001f8a.
        // Direct native field load at 80; no validation or substitution.
        public HardlightProject.LightspeedDashType Type => m_type;

        // Original Game.Runtime 0x06001f8b.
        // Native performs only the empty original base-constructor chain.
        public LightspeedDashDefinition() { }

    }
}
