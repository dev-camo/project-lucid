using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "HLMusicClipDefinition", menuName = "HardlightProject/DefinitionData/Definitions/HLMusicClipDefinition")]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class HLMusicClipDefinition : HLAudioClipDefinition
    {
        [SerializeField] private List<float> m_alternativeStartTimesInSeconds;
        public IReadOnlyList<float> StartTimes => m_alternativeStartTimesInSeconds;
        // The shipped constructor leaves the serialized list null.
        public HLMusicClipDefinition() { }
    }
}
