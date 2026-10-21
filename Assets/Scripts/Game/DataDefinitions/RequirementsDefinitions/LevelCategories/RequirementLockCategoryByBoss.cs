using Hardlight;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020005b7.
    [CreateAssetMenu(fileName = "RequirementLockCategoryByBoss", menuName = "HardlightProject/Requirements/RequirementLockCategoryByBoss")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class RequirementLockCategoryByBoss : RequirementLevelCategoryBase
    {
        private readonly SystemRef<SaveManager> m_saveManagerRef = ProcessManager.GetSystemRef<SaveManager>(null, true);
        private readonly SystemRef<MissionManager> m_missionManagerRef = ProcessManager.GetSystemRef<MissionManager>(null, true);
        private readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>(null, true);

        // 0x06001f19 (x86-64 0x557e40): the first zone is always open. Any other
        // zone is open once every boss level of the zone before it meets its own
        // requirements and has its first mission group fully complete. A category
        // that is not a zone, or has nothing before it, is locked.
        public override bool RequirementsMet(GameplayLevelCategory category)
        {
            GameLevels gameLevels = m_levelManagerRef.Get().GameLevels;
            IReadOnlyList<GameplayLevelCategory> zones = gameLevels.GetZones();
            if (zones[0] == category) return true;

            GameplayLevelCategory previousZone = null;
            for (int index = 1; index < zones.Count; ++index)
            {
                if (zones[index] == category)
                {
                    previousZone = zones[index - 1];
                    break;
                }
            }
            if (previousZone == null) return false;

            foreach (GameplayLevelDefinition level in gameLevels.GetLevels(previousZone))
            {
                if (!level.IsBossLevel) continue;
                if (!level.MeetsAllRequirements()) return false;

                SaveManager saveManager = m_saveManagerRef.Get();
                MissionManager missionManager = m_missionManagerRef.Get();
                MissionGroup bossGroup = level.MissionList.Groups[0];
                SaveDataLevel levelData = saveManager.CurrentSave.GetOrCreateLevelData(level.GetGUID());
                foreach (MissionDefinition mission in bossGroup.Definitions)
                {
                    if (!missionManager.GetSaveDataForMission(mission.GetGUID(), level.GetGUID(), levelData).Complete)
                        return false;
                }
            }
            return true;
        }

        // 0x06001f1a: the three field initializers run before the ScriptableObject base.
        public RequirementLockCategoryByBoss() { }
    }
}
