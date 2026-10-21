using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200070e: completes through the tracker's virtual MissionObjectivesCompleted (slot 7).
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class MissionReachGoalTracker : MissionTracker
    {
        // 06002751 (0x5BF960)
        public void Action_ReachedGoal() => MissionObjectivesCompleted();

        public MissionReachGoalTracker() { }
    }
}
