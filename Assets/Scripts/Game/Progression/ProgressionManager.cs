using System.Collections.Generic;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class ProgressionManager : ISystem, ISaveGameListener
    {
        private SaveManager m_saveManager;
        private ProgressionCollectableHandler m_collectables;

        // Original 06002b06: base construction precedes both subscriptions.
        public ProgressionManager()
        {
            this.SubscribeToAction(SystemAction.Initialise, Initialise);
            this.SubscribeToAction(SystemAction.Shutdown, Shutdown);
        }

        // Original 06002b07: listener registration happens before construction
        // of the handler. A synchronous save-open callback sees that original
        // partial state; no early handler or extra callback guard is added.
        private void Initialise(object context = null)
        {
            m_saveManager = ProcessManager.GetSystem<SaveManager>();
            m_saveManager.AddListener(this, true);
            m_collectables = new ProgressionCollectableHandler();
        }

        // Original 06002b08 keeps both fields after removing the listener.
        private void Shutdown(object context = null) => m_saveManager.RemoveListener(this);

        // Original 06002b09 uses the manager's current index and passed save.
        public void OnSaveGameOpen(SaveDataGame saveDataGame) =>
            m_collectables.CacheProgress(m_saveManager.CurrentSaveIndex, saveDataGame);

        // Original 06002b0a is an empty shipped return on both architectures.
        public void OnSaveGameClose(SaveDataGame saveDataGame) { }

        // Original 06002b0b: level recaching completes before challenges.
        public void CacheLevelProgress(GameplayLevelDefinition levelDefinition)
        {
            m_collectables.CacheLevelProgress(m_saveManager.CurrentSaveIndex, m_saveManager.CurrentSave, levelDefinition);
            CacheChallengeProgress();
        }

        // Original 06002b0c captures the cached save, recalculates its total,
        // then reads CurrentSave afresh for the challenge update.
        public void CacheChallengeProgress()
        {
            ProgressionCollectableSave saveProgress = GetCurrentSaveProgress();
            saveProgress.RecalculateTotal();
            saveProgress.CacheChallengesProgress(m_saveManager.CurrentSave);
        }

        // Original 06002b0d returns the live readonly dictionary interface.
        public IReadOnlyDictionary<CollectableType, ProgressionCollectable> GetTotalProgress() =>
            GetCurrentSaveProgress().Total;

        // Original 06002b0e enumerates all values, matching each value's Type,
        // rather than using its dictionary key. Both sums wrap as Int32, and
        // enumeration is disposed before the aggregate value is returned.
        public ProgressionCollectable GetTotalProgress(CollectableType type)
        {
            int collected = 0;
            int total = 0;
            foreach (var (_, progress) in GetTotalProgress())
                if (progress.Type == type)
                {
                    collected = unchecked(collected + progress.Collected);
                    total = unchecked(total + progress.Total);
                }
            return new ProgressionCollectable(type, collected, total);
        }

        // Original 06002b0f: the missing current-save case faults before the
        // level GUID is read. Only a missing level key receives the fallback.
        public ProgressionCollectableLevel GetLevelProgress(GameplayLevelDefinition levelDefinition) =>
            GetCurrentSaveProgress().ProgressPerLevel.GetValueOrDefault(levelDefinition.GetGUID());

        // Original 06002b10 lazily fills uncached slots from RawSaves. Empty
        // text uses the real empty-cache path; other text uses Unity's original
        // JSON deserializer. The final required indexer is read afresh after
        // caching, keeping the native fault and mutation order.
        public ProgressionCollectableSave GetSaveProgress(int saveIndex)
        {
            if (m_collectables.ProgressPerSave.TryGetValue(saveIndex, out ProgressionCollectableSave progress))
                return progress;
            string raw = m_saveManager.RawSaves[saveIndex];
            if (string.IsNullOrWhiteSpace(raw))
                m_collectables.CacheEmptyProgress(saveIndex);
            else
            {
                SaveDataGame save = JsonUtility.FromJson<SaveDataGame>(raw);
                m_collectables.CacheProgress(saveIndex, save);
            }
            return m_collectables.ProgressPerSave[saveIndex];
        }

        // Original 06002b11 does not initialize an uncached current slot.
        private ProgressionCollectableSave GetCurrentSaveProgress() =>
            m_collectables.ProgressPerSave.TryGetValue(m_saveManager.CurrentSaveIndex, out ProgressionCollectableSave progress)
                ? progress : null;
    }
}
