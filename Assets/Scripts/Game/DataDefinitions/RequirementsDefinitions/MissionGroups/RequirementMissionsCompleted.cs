using Hardlight;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020005bf.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [CreateAssetMenu(fileName = "RequirementMissionsCompleted", menuName = "HardlightProject/DefinitionData/Definitions/RequirementMissionsCompleted")]
    public sealed class RequirementMissionsCompleted : RequirementMissionGroupBase
    {
        [SerializeField] private MissionDefinition[] m_missionDefinitions;
        private readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>(null, true);
        private readonly SystemRef<MissionManager> m_missionManagerRef = ProcessManager.GetSystemRef<MissionManager>(null, true);

        // 0x06001f2a (x86-64 0x559150): the retail body begins with the class
        // initialisation of DebugUnlockLevels (its inlined always-false query; the dead branch's return value is inferred from the sibling requirements).
        // Each listed mission is looked up in the save data of the first level
        // that hosts it; a mission with no save entry does not fail the
        // requirement. Neither argument is read.
        public override bool RequirementsMet(MissionGroup missionGroup, GameplayLevelDefinition _)
        {
            if (DebugUnlockLevels.AreAllLevelsUnlocked()) return true;
            MissionManager missionManager = m_missionManagerRef.Get();
            LevelManager levelManager = m_levelManagerRef.Get();
            foreach (MissionDefinition missionDefinition in m_missionDefinitions)
            {
                GameplayLevelDefinition firstLevel = levelManager.GameLevels.GetFirstLevelForMission(missionDefinition);
                IReadOnlyDictionary<MissionDefinition, SaveDataLevelMission> missionSaveData =
                    missionManager.GetMissionSaveDataForLevel(firstLevel);
                if (missionSaveData.TryGetValue(missionDefinition, out SaveDataLevelMission saveData) && !saveData.Complete)
                    return false;
            }
            return true;
        }

        // 0x06001f2b.
        public RequirementMissionsCompleted() { }
    }
}
