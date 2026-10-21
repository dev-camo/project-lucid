using System;
using System.Collections;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020008db: the HUD / results view of an instanced mission's objective count, optionally
    // counting up from the previous progress and lighting its ordered slots one by one.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class UIContainerMissionInstancedProgress : UIContainer
    {
        [SerializeField]
        [Header("Progress Delta Animation")]
        private float m_initialTickDelaySeconds;
        [SerializeField] private float m_animateTickIntervalSeconds;
        [SerializeField] private float m_delayBeforeSetCompleteSeconds;
        [SerializeField] private bool m_useOrderedObjectives;
        [ShowIf("m_useOrderedObjectives", null)]
        [SerializeField]
        private UIWidgetSDTSlot[] m_orderedObjectiveSlots;
        [SerializeField] private GameObject[] m_activateIfCountingUp;
        public readonly Bindable<Sprite> Icon = new Bindable<Sprite>();
        public readonly Bindable<Sprite> RewardIcon = new Bindable<Sprite>();
        public readonly Bindable<int> Progress = new Bindable<int>();
        public readonly Bindable<int> Target = new Bindable<int>();
        public readonly Bindable<string> StringDescription = new Bindable<string>();
        public readonly Bindable<string> StringFormat = new Bindable<string>();
        public readonly Bindable<bool> IsComplete = new Bindable<bool>();
        public readonly Bindable<bool> CountUpComplete = new Bindable<bool>();
        public readonly Bindable<bool> IsAnimatingProgressDelta = new Bindable<bool>();
        private IReadOnlyList<int> m_newlyCompletedObjectives;
        private CollectableDefinition m_collectableDefinition;

        // 0600330f (0x63C220): the description comes from the active mission context when it is for this mission, and
        // the displayed progress counts up only for an animated increase that has not started at the target.
        public override void Setup(IUIContainerParameters parameters)
        {
            base.Setup(parameters);
            UIContainerInstancedMissionProgressParameters progressParameters = parameters.GetAs<UIContainerInstancedMissionProgressParameters>();
            MissionDefinition mission = progressParameters.MissionDefinition;
            Icon.Value = mission.MissionObjectiveIcon;
            Target.Value = mission.TargetObjectivesToComplete;
            StringFormat.Value = StringTable.GetString(Strings.FORMAT_X_OF_Y);

            MissionManager missionManager = ProcessManager.GetSystem<MissionManager>();
            IMissionContext activeContext = missionManager.ActiveMissionContext;
            MissionDefinition activeMission = activeContext?.MissionDefinition;
            StringDescription.Value = activeMission == mission
                ? missionManager.ActiveMissionContext.Description
                : mission.GetFormattedMissionName();

            bool isCountingUp = progressParameters.AnimateProgressDelta
                && progressParameters.PreviousProgress < progressParameters.Progress
                && progressParameters.PreviousProgress != mission.TargetObjectivesToComplete;
            foreach (GameObject countingUpObject in m_activateIfCountingUp)
                countingUpObject.SetActive(isCountingUp);
            IsAnimatingProgressDelta.Value = isCountingUp;
            if (m_useOrderedObjectives)
                SetupOrderedObjectives(progressParameters, isCountingUp);

            if (isCountingUp)
            {
                Progress.Value = progressParameters.PreviousProgress;
                StartCoroutine(AnimateProgressDelta(progressParameters.Progress));
            }
            else
            {
                Progress.Value = progressParameters.Progress;
                IsComplete.Value = progressParameters.Progress == mission.TargetObjectivesToComplete;
            }

            m_collectableDefinition = ProcessManager.GetSystem<DataManager>().CollectableDefinitions[mission.RewardType];
            RewardIcon.Value = m_collectableDefinition.LoadIcon();
        }

        // 06003310 (0x63CEB0)
        private void OnDisable()
        {
            if (m_collectableDefinition == null) return;
            m_collectableDefinition.UnloadIcon();
        }

        // 06003311 (0x63C740): the objectives completed in this run (completed but not previously completed) are kept
        // sorted for the count-up. Counting up shows only the previously completed slots as lit; otherwise the newly
        // completed ones are lit too.
        private void SetupOrderedObjectives(UIContainerInstancedMissionProgressParameters progressParameters, bool isCountingUp)
        {
            IReadOnlyList<int> previouslyCompleted = progressParameters.PreviouslyCompletedObjectiveIndices ?? Array.Empty<int>();
            IReadOnlyList<int> completed = progressParameters.CompletedObjectiveIndices;
            if (completed == null)
            {
                m_newlyCompletedObjectives = Array.Empty<int>();
            }
            else
            {
                List<int> newlyCompleted = new List<int>(completed.Count);
                foreach (int index in completed)
                {
                    if (!previouslyCompleted.Contains(index))
                        newlyCompleted.Add(index);
                }
                newlyCompleted.Sort();
                m_newlyCompletedObjectives = newlyCompleted;
            }

            for (int i = 0; i < m_orderedObjectiveSlots.Length; i++)
            {
                bool active = isCountingUp
                    ? previouslyCompleted.Contains(i)
                    : previouslyCompleted.Contains(i) || m_newlyCompletedObjectives.Contains(i);
                m_orderedObjectiveSlots[i].SetSlotActive(active);
            }
        }

        // 06003312 (0x63CE30) and its state machine (0x63D160): after an initial delay the progress ticks up one step per
        // interval, lighting the next newly completed slot with each step. The first step happens without a prior check
        // against the target.
        private IEnumerator AnimateProgressDelta(int targetCount)
        {
            yield return new WaitForSeconds(m_initialTickDelaySeconds);
            WaitForSeconds animateTickInterval = new WaitForSeconds(m_animateTickIntervalSeconds);
            int newlyCompletedListIndex = 0;
            while (true)
            {
                Progress.Value++;
                if (m_useOrderedObjectives && newlyCompletedListIndex < m_newlyCompletedObjectives.Count)
                {
                    int slotIndex = m_newlyCompletedObjectives[newlyCompletedListIndex];
                    newlyCompletedListIndex++;
                    if (slotIndex < m_orderedObjectiveSlots.Length)
                        m_orderedObjectiveSlots[slotIndex].Activate();
                }
                if (Progress.Value >= targetCount) break;
                yield return animateTickInterval;
            }
            CountUpComplete.Value = true;
            yield return new WaitForSeconds(m_delayBeforeSetCompleteSeconds);
            IsComplete.Value = Progress.Value == Target.Value;
        }

        public UIContainerMissionInstancedProgress() { }
    }
}
