using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x020008e2; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class UIContainerMissionProgressParameters : IUIContainerParameters
    {
        // 0600334d (0x63ECA0)
        public MissionInstancedTracker InstancedTracker { get; }

        // 0600334e (0x6353B0)
        public UIContainerMissionProgressParameters(MissionInstancedTracker instancedTracker)
        {
            InstancedTracker = instancedTracker;
        }
    }
}
