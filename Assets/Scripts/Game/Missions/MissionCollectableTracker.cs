using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020006fb: counts one collectable type towards a target and completes when it is reached.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class MissionCollectableTracker : MissionTracker
    {
        [SerializeField] private CollectableType m_collectableType;
        [SerializeField]
        [Min(1)]
        private int m_targetAmount;
        [SerializeField]
        [Tooltip("If true, negative changes are ignored - the amount collected only increases")]
        private bool m_ignoreLosses = true;
        [Tooltip("Only count collectables from these sources. If empty, all sources will be allowed.")]
        [SerializeField]
        private CollectableSource[] m_collectableSources;
        private int m_lastKnownAmount;
        private int m_collectedTotal;

        // 06002644 (0x5AC110): a previously completed mission keeps its saved progress and never hooks the manager.
        public override void Initialise(MissionState missionState, IMissionContext missionContext)
        {
            base.Initialise(missionState, missionContext);
            if (m_missionState.Complete) return;
            m_missionState.UpdateMissionProgress(0, m_targetAmount, false);
            ProcessManager.GetSystemRef<CollectableManager>(null, true).InvokeOnValid(InitialiseCollectableManager);
        }

        // 06002645 (0x5AC780): both counters restart, then the collected amount of the type is adopted as the baseline.
        private void InitialiseCollectableManager(CollectableManager collectableManager)
        {
            collectableManager.OnCollectableStateUpdate += OnCollectableStateUpdate;
            m_lastKnownAmount = 0;
            m_collectedTotal = 0;
            if (collectableManager.TryGetCollectableState(m_collectableType, out CollectableState collectableState))
                m_lastKnownAmount = collectableState.Collected;
        }

        // 06002646 (0x5AC8A0)
        public override void MissionObjectivesCompleted()
        {
            DeinitialiseCollectableManager();
            base.MissionObjectivesCompleted();
        }

        // 06002647 (0x5AC8C0): the manager may already be gone at teardown, so the safe lookup is null-checked.
        private void DeinitialiseCollectableManager()
        {
            CollectableManager collectableManager = ProcessManager.GetSystemSafe<CollectableManager>(null, true);
            if (collectableManager != null)
                collectableManager.OnCollectableStateUpdate -= OnCollectableStateUpdate;
        }

        // 06002648 (0x5ACB80): zero changes, ignored losses and sources outside the authored list are skipped; the total
        // is clamped to 0..target (Math.Clamp throws for a negative target) and reaching the target completes the mission.
        private void OnCollectableStateUpdate(CollectableType collectableType, CollectableState collectableState, CollectableChangeMetadata metadata)
        {
            if (m_collectableType != collectableType) return;
            int amount = collectableState.Collected;
            int change = amount - m_lastKnownAmount;
            if (change == 0) return;
            m_lastKnownAmount = amount;
            if (change < 0 && m_ignoreLosses) return;
            if (m_collectableSources.Length != 0 && !m_collectableSources.Contains(metadata.Source)) return;
            m_collectedTotal = Math.Clamp(m_collectedTotal + change, 0, m_targetAmount);
            m_missionState.UpdateMissionProgress(m_collectedTotal, m_targetAmount, false);
            if (m_collectedTotal == m_targetAmount) MissionObjectivesCompleted();
        }

        // 06002649 (0x5ACD50)
        protected override void OnDestroy()
        {
            DeinitialiseCollectableManager();
            base.OnDestroy();
        }

        public MissionCollectableTracker() { }
    }
}
