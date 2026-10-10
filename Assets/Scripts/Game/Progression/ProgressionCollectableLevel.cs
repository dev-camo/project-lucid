using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class ProgressionCollectableLevel
    {
        private readonly GameplayLevelDefinition m_levelDefinition;
        private readonly Dictionary<CollectableType, ProgressionCollectable> m_progress =
            new Dictionary<CollectableType, ProgressionCollectable>(HardlightEnumComparers.CollectableTypeComparer);
        private readonly Dictionary<CollectableType, ProgressionCollectable> m_progressGroupsIgnored =
            new Dictionary<CollectableType, ProgressionCollectable>(HardlightEnumComparers.CollectableTypeComparer);

        public IReadOnlyDictionary<CollectableType, ProgressionCollectable> Progress => m_progress;
        public IReadOnlyDictionary<CollectableType, ProgressionCollectable> ProgressGroupsIgnored => m_progressGroupsIgnored;

        public ProgressionCollectableLevel(GameplayLevelDefinition levelDefinition)
        {
            m_levelDefinition = levelDefinition;
        }

        // Original 06002af2 accumulates on repeated calls; it does not clear this cache.
        public void CacheProgress(SaveDataGame saveDataGame)
        {
            foreach (MissionGroup missionGroup in m_levelDefinition.MissionList.Groups)
            {
                if (ShouldExcludeMissionGroup(missionGroup))
                    continue;
                CacheMissionGroup(saveDataGame, missionGroup, m_progress);
            }
        }

        public void CacheIgnoredGroupsProgress(SaveDataGame saveDataGame)
        {
            m_progressGroupsIgnored.Clear();
            foreach (MissionGroup missionGroup in m_levelDefinition.MissionList.Groups)
            {
                bool locked = missionGroup.HideOrbCountUntilUnlocked && !missionGroup.MeetsAllRequirements(m_levelDefinition);
                // Original 06002af3 evaluates exclusion even when the first flag is true.
                bool excluded = ShouldExcludeMissionGroup(missionGroup);
                if (locked | excluded)
                    continue;
                CacheMissionGroup(saveDataGame, missionGroup, m_progressGroupsIgnored);
            }
        }

        private bool ShouldExcludeMissionGroup(MissionGroup missionGroup)
        {
            return missionGroup.HasAlwaysHiddenRequirement() ||
                missionGroup.HasRequirementOfType<RequirementMissionGroupShadowFTUEChallenge>();
        }

        private void CacheMissionGroup(SaveDataGame saveDataGame, MissionGroup missionGroup,
            Dictionary<CollectableType, ProgressionCollectable> progress)
        {
            var missionGroupProgress = new ProgressionCollectableMissionGroup(missionGroup);
            missionGroupProgress.CacheProgress(m_levelDefinition, saveDataGame);
            foreach (var (type, value) in missionGroupProgress.Progress)
            {
                if (progress.TryGetValue(type, out ProgressionCollectable previous))
                    progress[type] = new ProgressionCollectable(type,
                        unchecked(previous.Collected + value.Collected), unchecked(previous.Total + value.Total));
                else
                    progress[type] = value;
            }
        }
    }
}
