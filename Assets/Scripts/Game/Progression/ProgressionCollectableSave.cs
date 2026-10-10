using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class ProgressionCollectableSave
    {
        private readonly Dictionary<CollectableType, ProgressionCollectable> m_total =
            new Dictionary<CollectableType, ProgressionCollectable>(HardlightEnumComparers.CollectableTypeComparer);
        private readonly Dictionary<string, ProgressionCollectableLevel> m_progressPerLevel =
            new Dictionary<string, ProgressionCollectableLevel>();
        private readonly ProgressionCollectableChallenges m_progressChallenges = new ProgressionCollectableChallenges();

        // Game.Runtime 06002aff, 06002b00: these are live views of the original dictionaries.
        public IReadOnlyDictionary<CollectableType, ProgressionCollectable> Total => m_total;
        public IReadOnlyDictionary<string, ProgressionCollectableLevel> ProgressPerLevel => m_progressPerLevel;

        // 06002b01: a level without an authored mission list contributes nothing. The Unity
        // object equality test precedes construction; a null level itself still faults.
        public void CacheProgress(GameplayLevelDefinition levelDefinition, SaveDataGame saveDataGame, bool skipRecalculateTotal = false)
        {
            if (levelDefinition.MissionList == null) return;
            var levelProgress = new ProgressionCollectableLevel(levelDefinition);
            levelProgress.CacheProgress(saveDataGame);
            m_progressPerLevel[levelDefinition.GetGUID()] = levelProgress;
            if (!skipRecalculateTotal) RecalculateTotal();
        }

        // 06002b02: clear aggregate totals first, then sum each level's ordinary progress.
        // Keep the authored inner dictionary key distinct from the value's Type field.
        public void RecalculateTotal()
        {
            m_total.Clear();
            foreach (var (_, levelProgress) in m_progressPerLevel)
            {
                foreach (var (type, value) in levelProgress.Progress)
                {
                    if (m_total.TryGetValue(type, out ProgressionCollectable previous))
                        m_total[type] = new ProgressionCollectable(type,
                            unchecked(previous.Collected + value.Collected), unchecked(previous.Total + value.Total));
                    else m_total[type] = value;
                }
            }
        }

        // 06002b03: ignored groups are a separate view. Do not synthesize an absent cache:
        // the original required indexer throws after the mission-list and empty-save checks.
        public void CacheProgressIgnoreGroups(GameplayLevelDefinition levelDefinition, SaveDataGame saveDataGame)
        {
            if (levelDefinition.MissionList == null || saveDataGame.IsSaveEmpty()) return;
            m_progressPerLevel[levelDefinition.GetGUID()].CacheIgnoredGroupsProgress(saveDataGame);
        }

        // 06002b04: add the newly calculated challenge counts to the existing total. There
        // is deliberately no clear here; the manager recalculates level totals before this call.
        public void CacheChallengesProgress(SaveDataGame saveData)
        {
            m_progressChallenges.CacheChallengesProgress(saveData);
            foreach (var (type, value) in m_progressChallenges.Progress)
            {
                if (m_total.TryGetValue(type, out ProgressionCollectable previous))
                    m_total[type] = new ProgressionCollectable(value.Type,
                        unchecked(previous.Collected + value.Collected), unchecked(previous.Total + value.Total));
                else m_total[type] = value;
            }
        }

        // 06002b05: enum-comparer totals, level dictionary and challenge model are initialized
        // in this order before the original System.Object base constructor.
        public ProgressionCollectableSave() { }
    }
}
