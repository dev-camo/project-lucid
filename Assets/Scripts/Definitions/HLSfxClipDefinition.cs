using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "HLSfxClipDefinition", menuName = "HardlightProject/DefinitionData/Definitions/HLSfxClipDefinition")]
    public class HLSfxClipDefinition : HLAudioClipDefinition
    {
        [SerializeField] private bool m_dampenRepeatedOneShotVolume;
        [Tooltip("If this array contains 1 number it will affect all repeated sounds after the 1st. If multiple numbers they will be applied in order until reaching the final one which will apply to subsequent extra sounds.")]
        [ShowIf("m_dampenRepeatedOneShotVolume", (string)null)]
        [SerializeField]
        [Range(0f, 1f)] private List<float> m_volumeOfRepeats;
        public bool DampenRepeatedOneShotVolume => m_dampenRepeatedOneShotVolume;
        public IReadOnlyList<float> Dampening => m_volumeOfRepeats;
        public HLSfxClipDefinition() { }
    }
}
