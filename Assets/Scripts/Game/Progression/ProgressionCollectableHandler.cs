using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class ProgressionCollectableHandler
    {
        private readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>(null, true);
        private readonly Dictionary<int, ProgressionCollectableSave> m_progressPerSave = new Dictionary<int, ProgressionCollectableSave>();

        // Game.Runtime 06002ae9: return the live dictionary through its original read-only contract.
        public IReadOnlyDictionary<int, ProgressionCollectableSave> ProgressPerSave => m_progressPerSave;

        // 06002aea: publish the fresh slot cache before reading authored zones. A later failure
        // therefore leaves the original partially populated cache observable to callers.
        public void CacheProgress(int saveIndex, SaveDataGame saveData)
        {
            var saveProgress = new ProgressionCollectableSave();
            m_progressPerSave[saveIndex] = saveProgress;
            GameLevels gameLevels = m_levelManagerRef.Get().GameLevels;
            foreach (GameplayLevelCategory zone in gameLevels.GetZones())
            {
                if (zone.HasRequirement<RequirementCategoryAlwaysHidden>()) continue;
                foreach (GameplayLevelDefinition level in gameLevels.GetLevels(zone))
                    saveProgress.CacheProgress(level, saveData, true);
            }
            saveProgress.RecalculateTotal();
            saveProgress.CacheChallengesProgress(saveData);
            CacheProgressIgnoreGroups(saveData, gameLevels, saveProgress);
        }

        // 06002aeb: replace this slot with a fresh empty model, without traversing levels.
        public void CacheEmptyProgress(int saveIndex) => m_progressPerSave[saveIndex] = new ProgressionCollectableSave();

        // 06002aec: a slot absent from the cache is an intentional no-op. Existing slots
        // recalculate ordinary totals, then refresh the ignored-group view of every visible zone.
        public void CacheLevelProgress(int saveIndex, SaveDataGame saveData, GameplayLevelDefinition levelDefinition)
        {
            if (!m_progressPerSave.TryGetValue(saveIndex, out ProgressionCollectableSave saveProgress)) return;
            saveProgress.CacheProgress(levelDefinition, saveData, false);
            CacheProgressIgnoreGroups(saveData, m_levelManagerRef.Get().GameLevels, saveProgress);
        }

        // 06002aed: repeat the same zone traversal after totals and challenges. The actual
        // per-level method retains its required dictionary lookup and empty-save early return.
        private static void CacheProgressIgnoreGroups(SaveDataGame saveData, GameLevels gameLevels, ProgressionCollectableSave saveProgress)
        {
            foreach (GameplayLevelCategory zone in gameLevels.GetZones())
            {
                if (zone.HasRequirement<RequirementCategoryAlwaysHidden>()) continue;
                foreach (GameplayLevelDefinition level in gameLevels.GetLevels(zone))
                    saveProgress.CacheProgressIgnoreGroups(level, saveData);
            }
        }

        // 06002aee: the two field initializers above precede the System.Object constructor.
        public ProgressionCollectableHandler() { }
    }
}
