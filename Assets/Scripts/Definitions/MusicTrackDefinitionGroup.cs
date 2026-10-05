using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "MusicTrackDefinitionGroup", menuName = "HardlightProject/DefinitionData/Groups/MusicTrackDefinitionGroup")]
    public class MusicTrackDefinitionGroup : DefinitionDataType<HLAudioClipIdentifier, MusicTrackDefinition>
    {
        // Game.Runtime 0x06001e6c..0x06001e6e: direct key, original maintained comparer, base-only constructor.
        protected override HLAudioClipIdentifier GetElementKey(MusicTrackDefinition data) => data.ClipIdentifier;
        protected override IEqualityComparer<HLAudioClipIdentifier> GetKeyComparer() => HardlightEnumComparers.HLAudioClipIdentifierComparer;
        public MusicTrackDefinitionGroup() { }
    }
}
