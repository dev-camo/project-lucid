using Hardlight;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020005ab.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [CreateAssetMenu(fileName = "RequirementPreviousZoneBossDefeated", menuName = "HardlightProject/Requirements/RequirementPreviousZoneBossDefeated")]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class RequirementPreviousZoneBossDefeated : RequirementGameplayLevelBase
    {
        private readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>(null, true);
        private readonly SystemRef<MissionManager> m_missionManagerRef = ProcessManager.GetSystemRef<MissionManager>(null, true);

        // 0x06001f02 (x86-64 0x556ed0): after the DebugUnlockLevels class
        // initialisation (its inlined always-false query; the dead branch's return value is inferred from the sibling requirements) the boss level of the
        // zone before this level's zone must have its first mission complete.
        // The scan keeps the last zone it passed; for the first zone nothing
        // precedes it, so the zone itself is used.
        public override bool RequirementsMet(GameplayLevelDefinition levelDefinition)
        {
            if (DebugUnlockLevels.AreAllLevelsUnlocked()) return true;
            GameLevels gameLevels = m_levelManagerRef.Get().GameLevels;
            if (!gameLevels.TryGetZoneForLevel(levelDefinition, out GameplayLevelCategory zone)) return false;

            GameplayLevelCategory previousZone = null;
            foreach (GameplayLevelCategory candidate in gameLevels.GetZones())
            {
                // The null test runs before the zone comparison (0x557165, then 0x557175).
                bool hadNoPreviousZone = previousZone == null;
                if (candidate == zone)
                {
                    if (hadNoPreviousZone) previousZone = candidate;
                    break;
                }
                previousZone = candidate;
            }

            if (!gameLevels.TryGetBossLevelForZone(previousZone, out GameplayLevelDefinition bossLevel)) return false;
            if (!bossLevel.TryGetMissionGroups(out IReadOnlyList<MissionGroup> missionGroups)) return false;

            MissionGroup firstGroup = missionGroups[0];
            if (firstGroup.Definitions.Count == 0) return false;
            MissionDefinition firstMission = firstGroup.Definitions[0];
            return m_missionManagerRef.Get().GetSaveDataForMission(firstMission.GetGUID(), bossLevel.GetGUID()).Complete;
        }

        // 0x06001f03.
        public RequirementPreviousZoneBossDefeated() { }
    }
}
