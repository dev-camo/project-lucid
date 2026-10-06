using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [CreateAssetMenu(fileName = "TransporterDefinition", menuName = "HardlightProject/DefinitionData/Definitions/TransporterDefinition")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class TransporterDefinition : HardlightProject.TrackerMetadataDefinition
    {
        [Tooltip("Defines the transporter type.")]
        [SerializeField]
        private HardlightProject.TransporterType m_type;

        // Original Game.Runtime 0x06001faf.
        // Direct native field load at 80; no validation or substitution.
        public HardlightProject.TransporterType Type => m_type;

        // Original Game.Runtime 0x06001fb0.
        // Native performs only the empty original base-constructor chain.
        public TransporterDefinition() { }

    }
}
