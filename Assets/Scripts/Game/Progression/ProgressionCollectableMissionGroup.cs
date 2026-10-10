using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class ProgressionCollectableMissionGroup
    {
        public MissionGroup Group { get; }
        private readonly MissionManager m_missionManager;
        private readonly Dictionary<CollectableType, ProgressionCollectable> m_progress =
            new Dictionary<CollectableType, ProgressionCollectable>(HardlightEnumComparers.CollectableTypeComparer);

        public IReadOnlyDictionary<CollectableType, ProgressionCollectable> Progress => m_progress;

        public ProgressionCollectableMissionGroup(MissionGroup missionGroup)
        {
            Group = missionGroup;
            m_missionManager = ProcessManager.GetSystem<MissionManager>(null, true);
        }

        // Original 06002af9 keeps prior counts and visits each direct sub-mission once.
        public void CacheProgress(GameplayLevelDefinition levelDefinition, SaveDataGame saveDataGame)
        {
            var levelSaveData = m_missionManager.GetMissionSaveDataForLevel(levelDefinition, saveDataGame);
            foreach (MissionDefinition missionDefinition in Group.Definitions)
            {
                UpdateProgress(GetProgressFromMission(missionDefinition, levelSaveData));
                foreach (MissionDefinition subMission in missionDefinition.SubMissions)
                    UpdateProgress(GetProgressFromMission(subMission, levelSaveData));
            }
        }

        private void UpdateProgress(ProgressionCollectable progress)
        {
            if (m_progress.TryGetValue(progress.Type, out ProgressionCollectable previous))
                m_progress[progress.Type] = new ProgressionCollectable(progress.Type,
                    unchecked(previous.Collected + progress.Collected), unchecked(previous.Total + progress.Total));
            else
                m_progress[progress.Type] = progress;
        }

        private static ProgressionCollectable GetProgressFromMission(MissionDefinition missionDefinition,
            IReadOnlyDictionary<MissionDefinition, SaveDataLevelMission> levelSaveData)
        {
            CollectableType type = missionDefinition.RewardType;
            if (type == CollectableType.BlueCoin)
                return GetBlueCoinsProgressFromMission(levelSaveData, missionDefinition);
            int total = missionDefinition.RewardValue;
            int collected = GetCollected(levelSaveData, missionDefinition);
            return new ProgressionCollectable(type, collected, total);
        }

        private static ProgressionCollectable GetBlueCoinsProgressFromMission(
            IReadOnlyDictionary<MissionDefinition, SaveDataLevelMission> levelSaveData, MissionDefinition missionDefinition)
        {
            int total = missionDefinition.TotalObjectiveCount;
            int collected = CalculateCollectedBlueCoinsRewards(levelSaveData, missionDefinition);
            return new ProgressionCollectable(CollectableType.BlueCoin, collected, total);
        }

        private static int CalculateCollectedBlueCoinsRewards(
            IReadOnlyDictionary<MissionDefinition, SaveDataLevelMission> levelSaveData, MissionDefinition missionDefinition)
        {
            if (!levelSaveData.TryGetValue(missionDefinition, out SaveDataLevelMission saveData))
                return 0;
            if (saveData.Complete)
                return missionDefinition.TotalObjectiveCount;
            var completedObjectiveIndices = saveData.CompletedObjectiveIndices;
            return completedObjectiveIndices != null ? completedObjectiveIndices.Count : 0;
        }

        private static int GetCollected(IReadOnlyDictionary<MissionDefinition, SaveDataLevelMission> levelSaveData,
            MissionDefinition missionDefinition)
        {
            if (!levelSaveData.TryGetValue(missionDefinition, out SaveDataLevelMission saveData))
                return 0;
            return saveData.Complete ? missionDefinition.RewardValue : 0;
        }
    }
}
