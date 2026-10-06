using System.Collections.Generic;
using UnityEngine;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.NullChecks, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOption(Unity.IL2CPP.CompilerServices.Option.ArrayBoundsChecks, false)]
    [UnityEngine.CreateAssetMenu(fileName = "FadeTransitionDefinition", menuName = "HardlightProject/DefinitionData/Definitions/FadeTransitionDefinition")]
    public class FadeTransitionDefinition : UnityEngine.ScriptableObject
    {
        [UnityEngine.SerializeField]
        private HardlightProject.FadeTransitionType m_type;

        [UnityEngine.SerializeField]
        private Hardlight.UIContainerIdentifier m_containerIdentifier;

        // Original Game.Runtime 0x06001cca, ARM 0x523ad4.
        public HardlightProject.FadeTransitionType Type => m_type;

        // Original Game.Runtime 0x06001ccb, ARM 0x523adc.
        public Hardlight.UIContainerIdentifier ContainerIdentifier => m_containerIdentifier;

        // Original Game.Runtime 0x06001ccc, ARM 0x523ae4.
        // The compiler emits the original public, parameterless base-only constructor.
    }
}
