using System;
using System.Collections;
using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020008f5..020008f7; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class UIContainerOrbCollectableCounter : UIContainer
    {
        [SerializeField]
        private CollectableType m_rewardType;
        [SerializeField]
        private bool m_animatePendingCount;
        [SerializeField]
        [ShowIf("m_animatePendingCount", null)]
        private float m_initialDelaySeconds;
        [ShowIf("m_animatePendingCount", null)]
        [SerializeField]
        private float m_animateTickIntervalSeconds;
        private ProgressionManager m_progressionManager;
        private MissionManager m_missionManager;
        private Coroutine m_animateCountRoutine;
        private readonly SystemRef<DataManager> m_dataManagerRef = ProcessManager.GetSystemRef<DataManager>(null, true);
        public readonly Bindable<int> CollectedAmount = new Bindable<int>();
        public readonly Bindable<bool> HasValues = new Bindable<bool>();
        public readonly Bindable<Sprite> CollectableIcon = new Bindable<Sprite>();

        // 060033c0 (1.10.1 RVA 0x66C870): a counter without a reward type switches itself off.
        private void Awake()
        {
            if (m_rewardType != CollectableType.None)
                ProcessManager.GetSystemRef<ProgressionManager>(null, true).InvokeOnValid(ProgressionManagerValid);
            else
                enabled = false;
        }

        // 060033c1 (1.10.1 RVA 0x66C960)
        private void ProgressionManagerValid(ProgressionManager progressionManager)
        {
            m_progressionManager = progressionManager;
            ProcessManager.GetSystemRef<MissionManager>(null, true).InvokeOnValid(MissionManagerValid);
        }

        // 060033c2 (1.10.1 RVA 0x66CA50)
        private void MissionManagerValid(MissionManager manager)
        {
            m_missionManager = manager;
            m_missionManager.OnRewardCountChanged += UpdateBindings;
            UpdateBindings();
        }

        // 060033c3 (1.10.1 RVA 0x66CB30): while the pending reward is still being earned in a
        // non-replayed mission, the shown count starts below the saved total and ticks up to it.
        private void UpdateBindings()
        {
            int collected = m_progressionManager.GetTotalProgress(m_rewardType).Collected;
            IMissionContext missionContext = m_missionManager.ActiveMissionContext;
            int pendingRewards = m_missionManager.GetPendingRewards(m_rewardType);
            HasValues.Value = collected > 0;
            if (m_animatePendingCount && !missionContext.IsReplay && pendingRewards > 0)
            {
                CollectedAmount.Value = collected - pendingRewards;
                this.SafeStopCoroutine(ref m_animateCountRoutine);
                m_animateCountRoutine = StartCoroutine(AnimateCount(collected));
            }
            else
            {
                CollectedAmount.Value = collected;
            }
            CollectableIcon.Value = m_dataManagerRef.Get().CollectableDefinitions[m_rewardType].LoadIcon();
        }

        // 060033c4 (1.10.1 RVA 0x66CD50)
        private IEnumerator AnimateCount(int targetCount)
        {
            yield return new WaitForSeconds(m_initialDelaySeconds);
            while (CollectedAmount.Value < targetCount)
            {
                yield return new WaitForSeconds(m_animateTickIntervalSeconds);
                CollectedAmount.Value++;
            }
        }

        // 060033c5 (1.10.1 RVA 0x66CDF0)
        private void OnDestroy()
        {
            if (m_missionManager != null)
                m_missionManager.OnRewardCountChanged -= UpdateBindings;
            if (!m_dataManagerRef.IsNull() && m_rewardType != CollectableType.None)
                m_dataManagerRef.Get().CollectableDefinitions[m_rewardType].UnloadIcon();
        }

        // 060033c6 (1.10.1 RVA 0x66CF40)
        public void UpdateValues(GameplayLevelCategory zone)
        {
            UpdateValues(zone.GetRewardType);
        }

        // 060033c7 (1.10.1 RVA 0x66D080)
        public void UpdateValues(GameplayLevelDefinition levelDefinition)
        {
            UpdateValues(levelDefinition.GetRewardType);
        }

        // 060033c8 (1.10.1 RVA 0x66D100): the closure is <>c__DisplayClass19_0.
        public void UpdateValues(MissionDefinition missionDefinition)
        {
            UpdateValues(() => missionDefinition.RewardType);
        }

        // 060033c9 (1.10.1 RVA 0x66CFC0): a changed reward type releases the previous type's icon.
        private void UpdateValues(Func<CollectableType> rewardFunction)
        {
            CollectableType previousRewardType = m_rewardType;
            m_rewardType = rewardFunction();
            if (previousRewardType != CollectableType.None && previousRewardType != m_rewardType)
                m_dataManagerRef.Get().CollectableDefinitions[previousRewardType].UnloadIcon();
            if (m_missionManager != null && m_rewardType != CollectableType.None)
                UpdateBindings();
        }

        // 060033ca (1.10.1 RVA 0x66D1C0)
        public UIContainerOrbCollectableCounter()
        {
        }
    }
}
