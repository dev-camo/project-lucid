using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ActorAudioLookup
    {
        public readonly Dictionary<ActorAudioTypes, ActorAudioReference> AudioDictionary =
            new Dictionary<ActorAudioTypes, ActorAudioReference>(HardlightEnumComparers.ActorAudioTypesComparer);
        // 0x06001982: later entries replace earlier entries without clearing.
        public void CompileLookup(ActorAudioReference[] audioReferences)
        {
            foreach (ActorAudioReference reference in audioReferences)
            {
                ActorAudioTypes type = reference.AudioType;
                AudioDictionary[type] = reference;
            }
        }
        // 0x06001983: lookup/selection complete before assigning the out value.
        public bool TryGet(ActorAudioTypes actorAudioType, out HLAudioClipIdentifier? audioClip, LevelSetupTypes currentLevelType)
        {
            if (AudioDictionary.TryGetValue(actorAudioType, out ActorAudioReference reference))
            {
                audioClip = reference?.Get(currentLevelType);
                return audioClip.HasValue;
            }
            audioClip = null;
            return false;
        }
        // 0x06001984: comparer-backed dictionary initializer before base ctor.
    }
}
