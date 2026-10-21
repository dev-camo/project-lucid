using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020005c0.
    [CreateAssetMenu(fileName = "RequirementPreviousMissionGroupsAreComplete", menuName = "HardlightProject/DefinitionData/Definitions/RequirementPreviousMissionGroupsAreComplete")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class RequirementPreviousMissionGroupsAreComplete : RequirementMissionGroupBase
    {
        // 0x06001f2c (x86-64 0x5593b0): after the DebugUnlockLevels class
        // initialisation (its inlined always-false query) every mission of each
        // group listed before this one in the level must be recorded complete.
        // A group that is first, or absent from the level, has nothing before it.
        public override bool RequirementsMet(MissionGroup missionGroup, GameplayLevelDefinition levelDefinition)
        {
            if (DebugUnlockLevels.AreAllLevelsUnlocked()) return true;
            MissionManager missionManager = ProcessManager.GetSystem<MissionManager>(null, true);
            var missionGroups = levelDefinition.MissionList.Groups;
            int groupIndex = missionGroups.FindIndex(group => group == missionGroup);
            for (int index = 0; index < groupIndex; ++index)
            {
                foreach (MissionDefinition missionDefinition in missionGroups[index].Definitions)
                {
                    if (!missionManager.GetSaveDataForMission(missionDefinition.GetGUID(), levelDefinition.GetGUID()).Complete)
                        return false;
                }
            }
            return true;
        }

        // 0x06001f2d.
        public RequirementPreviousMissionGroupsAreComplete() { }
    }
}
