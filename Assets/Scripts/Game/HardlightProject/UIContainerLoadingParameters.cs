using Hardlight;
using System.Runtime.CompilerServices;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIContainerLoadingParameters : IUIContainerParameters
    {
        // Original 0x060032f9/0x060032fa: these are stored references, not copies.
        public GameplayLevelDefinition LevelDefinition { [CompilerGenerated] get; }
        public MissionDefinition MissionDefinition { [CompilerGenerated] get; }

        // Original 0x060032fb. Only the mission argument has an optional default.
        public UIContainerLoadingParameters(GameplayLevelDefinition levelDefinition, MissionDefinition missionDefinition = null)
        {
            LevelDefinition = levelDefinition;
            MissionDefinition = missionDefinition;
        }
    }
}
