using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020005b9.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "RequirementAllPersistentMissionsAreComplete", menuName = "HardlightProject/DefinitionData/Definitions/RequirementAllPersistentMissionsAreComplete")]
    public sealed class RequirementAllPersistentMissionsAreComplete : RequirementMissionGroupBase
    {
        // 0x06001f1d (x86-64 0x558900): the retail body begins with the class
        // initialisation of DebugUnlockLevels, i.e. its always-false query
        // inlined away; the dead branch result is not recoverable and follows
        // the sibling requirements. Only persistent trackers that already have
        // a mission state can fail the requirement, so a tracker with no state
        // counts as complete. The levelDefinition argument is not read.
        public override bool RequirementsMet(MissionGroup missionGroup, GameplayLevelDefinition levelDefinition)
        {
            if (DebugUnlockLevels.AreAllLevelsUnlocked()) return true;
            MissionManager missionManager = ProcessManager.GetSystem<MissionManager>(null, true);
            foreach (MissionDefinition missionDefinition in missionGroup.Definitions)
            {
                if (!missionDefinition.IsPersistentTracker) continue;
                if (!missionManager.MissionStates.TryGetValue(missionDefinition.GetGUID(), out MissionState missionState)) continue;
                if (!missionState.Complete) return false;
            }
            return true;
        }

        // 0x06001f1e.
        public RequirementAllPersistentMissionsAreComplete() { }
    }
}
