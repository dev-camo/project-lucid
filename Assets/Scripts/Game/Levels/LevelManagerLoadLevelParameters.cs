using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020006bf. The shipped name includes LoadLevelParameters.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class LevelManagerLoadLevelParameters
    {
        // Original 0x06002497; ARM 0x56f68c. Natural readonly backing field at +0x10.
        public GameplayLevelDefinition LevelDefinition { get; }

        // Original 0x06002498; ARM 0x56be5c. Object construction precedes assignment.
        public LevelManagerLoadLevelParameters(GameplayLevelDefinition levelDefinition)
        {
            LevelDefinition = levelDefinition;
        }
    }
}
