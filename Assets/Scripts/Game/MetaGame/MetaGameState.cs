using System.Runtime.CompilerServices;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class MetaGameState
    {
        // Original 0x06002500/0x06002501: retain the two readonly backing fields
        // and reference identity, including null definitions.
        public GameplayLevelDefinition LevelDefinition { [CompilerGenerated] get; }
        public MissionDefinition MissionDefinition { [CompilerGenerated] get; }

        // Original 0x06002502. Base construction precedes the ordered stores.
        public MetaGameState(GameplayLevelDefinition levelDefinition, MissionDefinition missionDefinition)
        {
            LevelDefinition = levelDefinition;
            MissionDefinition = missionDefinition;
        }
    }
}
