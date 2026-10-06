using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
namespace HardlightProject
{
    [CreateAssetMenu(fileName = "ActorAudioDefinition", menuName = "HardlightProject/DefinitionData/Definitions/ActorAudioDefinition")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ActorAudioDefinition : ScriptableObject
    {
        [SerializeField] private ActorAudioReference[] m_audioReferences = Array.Empty<ActorAudioReference>();
        // Game.Runtime 0x06001980 getter; constructor 0x06001981 publishes the
        // original shared empty reference array before ScriptableObject construction.
        public ActorAudioReference[] ActorAudioReferences => m_audioReferences;
    }
}
