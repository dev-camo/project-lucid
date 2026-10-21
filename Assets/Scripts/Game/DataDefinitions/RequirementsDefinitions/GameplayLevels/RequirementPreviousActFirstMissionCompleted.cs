using Hardlight;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020005aa.
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "RequirementPreviousActFirstMissionCompleted", menuName = "HardlightProject/Requirements/RequirementPreviousActFirstMissionCompleted")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class RequirementPreviousActFirstMissionCompleted : RequirementGameplayLevelBase
    {
        private readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>(null, true);
        private readonly SystemRef<MissionManager> m_missionManagerRef = ProcessManager.GetSystemRef<MissionManager>(null, true);

        // 0x06001f00 (x86-64 0x556b90): after the DebugUnlockLevels class
        // initialisation (its inlined always-false query; the dead branch's return value is inferred from the sibling requirements) the level before this
        // one must exist, have mission groups, and have its first mission
        // recorded as complete.
        public override bool RequirementsMet(GameplayLevelDefinition levelDefinition)
        {
            if (DebugUnlockLevels.AreAllLevelsUnlocked()) return true;
            if (!m_levelManagerRef.Get().GameLevels.TryGetPreviousLevel(levelDefinition, out GameplayLevelDefinition previousLevel))
                return false;
            if (!previousLevel.TryGetMissionGroups(out IReadOnlyList<MissionGroup> missionGroups)) return false;

            MissionGroup firstGroup = missionGroups[0];
            if (firstGroup.Definitions.Count == 0) return false;
            MissionDefinition firstMission = firstGroup.Definitions[0];
            return m_missionManagerRef.Get().GetSaveDataForMission(firstMission.GetGUID(), previousLevel.GetGUID()).Complete;
        }

        // 0x06001f01.
        public RequirementPreviousActFirstMissionCompleted() { }
    }
}
