using System;
using System.Collections.Generic;
using System.Diagnostics;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime 0x02000700: a mission made of several MissionObjective children that completes when enough
    // of them have been completed. The compiler-generated names (the Icon backing field) follow member order.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class MissionInstancedTracker : MissionTracker
    {
        [SerializeField]
        [Tooltip("Actions that occur when any individual objective is completed, not counting prior completions.")]
        protected UnityEvent m_onAnyObjectiveCompleted;
        [Tooltip("Actions that occur when the mission is at its last objective.")]
        [SerializeField]
        private UnityEvent m_onLastObjective;
        [SerializeField] protected List<MissionObjective> m_missionObjectives;
        [SerializeField]
        [Tooltip("If ticked, individual mission objective completion will be saved in save data and restored between sessions.")]
        protected bool m_persistObjectiveCompletion;
        [Tooltip("If assigned, will group all mission types in the HUD UI prefab.")]
        [SerializeField]
        protected UIContainerIdentifier m_uiContainerIdentifierGroup;
        [SerializeField]
        [Tooltip("If assigned, will override the HUD UI prefab that displays progression for the mission.")]
        protected UIContainerIdentifier m_uiContainerIdentifierOverride;
        [Tooltip("Must be assigned to display the instanced tracker in the results screen.")]
        [SerializeField]
        protected UIContainerMissionInstancedProgress m_uiContainerForResultsScreen;
        [SerializeField]
        [Tooltip("Used for editor rename mission objectives button.")]
        private string m_objectiveNamePrefix = "gate";
        // 06002696/06002697: declared here so its backing field keeps the original position between the fields.
        public Sprite Icon { get; private set; }
        public Action<MissionInstancedTracker> OnStatusChanged;
        protected readonly HashSet<MissionObjective> m_completedObjectives = new HashSet<MissionObjective>();
        protected int m_targetObjectivesToComplete;

        // 06002698..060026a0: direct field reads; MissionComplete compares the saved progress with the target.
        public int ObjectiveCount => m_missionObjectives.Count;
        public int ObjectivesComplete => m_completedObjectives.Count;
        public int ObjectivesTarget => m_targetObjectivesToComplete;
        public bool PersistObjectiveCompletion => m_persistObjectiveCompletion;
        public UIContainerIdentifier UIContainerIdentifierGroup => m_uiContainerIdentifierGroup;
        public UIContainerIdentifier UIContainerIdentifierOverride => m_uiContainerIdentifierOverride;
        public UIContainerMissionInstancedProgress UIContainerForResultsScreen => m_uiContainerForResultsScreen;
        public IEnumerable<MissionObjective> MissionObjectives => m_missionObjectives;
        private bool MissionComplete => m_missionState.Progress == m_targetObjectivesToComplete;

        // 060026a1 (0x5B01E0): saved objective completion is restored when the mission is complete or persistent; the
        // target and progress are then set up, the callbacks hooked and a last-objective state announced.
        public override void Initialise(MissionState missionState, IMissionContext missionContext)
        {
            base.Initialise(missionState, missionContext);
            m_completedObjectives.Clear();
            bool allowSavedProgress = (missionContext != null && !missionContext.IgnoreSavedProgress)
                || missionState.Definition.IsPersistentTracker;
            bool isTimeTrial = missionState.Definition.IsTimeTrial();
            if (MissionComplete || m_persistObjectiveCompletion)
                InitialiseObjectivesPreviouslyCompleted(missionState, allowSavedProgress && !isTimeTrial);
            InitialiseObjectivesToComplete(missionState, missionContext);
            missionState.UpdateMissionProgress(missionState.Progress, m_targetObjectivesToComplete, m_persistObjectiveCompletion);
            InitialiseObjectivesCallbacks();
            if (missionState.Progress == m_targetObjectivesToComplete - 1)
                m_onLastObjective?.Invoke();
        }

        // 060026a2 (0x5B0370): indices past the list are skipped; a negative index reaches the list indexer and faults.
        private void InitialiseObjectivesPreviouslyCompleted(MissionState missionState, bool allowSavedProgress)
        {
            if (!allowSavedProgress) return;
            IReadOnlyCollection<int> completedIndices = missionState.CompletedObjectiveIndices;
            if (completedIndices == null) return;
            foreach (int index in completedIndices)
            {
                if (m_missionObjectives.Count <= index) continue;
                MissionObjective objective = m_missionObjectives[index];
                m_completedObjectives.Add(objective);
                objective.OnObjectivePreviouslyCompleted();
            }
        }

        // 060026a3 (0x5B06F0): an overriding context sets the target and, when it lists the active objectives, the others
        // are destroyed back to front and only the active ones count towards the target.
        private void InitialiseObjectivesToComplete(MissionState missionState, IMissionContext missionContext)
        {
            if (missionContext == null)
            {
                m_targetObjectivesToComplete = missionState.Definition.TargetObjectivesToComplete;
                return;
            }
            m_targetObjectivesToComplete = missionContext.ObjectiveTarget;
            IReadOnlyList<int> activeObjectives = missionContext.OverrideActiveObjectives;
            if (activeObjectives == null) return;
            m_targetObjectivesToComplete = 0;
            for (int i = m_missionObjectives.Count - 1; i >= 0; i--)
            {
                if (activeObjectives.Contains(i))
                {
                    m_targetObjectivesToComplete++;
                    continue;
                }
                Destroy(m_missionObjectives[i].gameObject);
                m_missionObjectives.RemoveAt(i);
            }
        }

        // 060026a4 (0x5B0940)
        private void InitialiseObjectivesCallbacks()
        {
            foreach (MissionObjective objective in m_missionObjectives)
            {
                objective.OnStatusChanged += StatusChanged;
                objective.OnTimerStatusChanged += RegisterMissionTimer;
            }
        }

        // 060026a5 (0x5B0C80): an empty method in retail.
        private void UpdateProgressionUI() { }

        // 060026a6 (0x5B0C90): editor reset gathers the objectives below this tracker.
        protected void Reset()
        {
            m_missionObjectives = new List<MissionObjective>(GetComponentsInChildren<MissionObjective>());
        }

        // 060026a7 (0x5B0D30): the first completion of an objective adds its bonus time and score points; the completed
        // count then drives progress, persistence, the last-objective event and completion.
        protected virtual void StatusChanged(MissionObjective missionObjective)
        {
            if (!m_hasStarted && !m_persistObjectiveCompletion) return;
            if (missionObjective.IsComplete)
            {
                if (m_completedObjectives.Add(missionObjective))
                {
                    m_onAnyObjectiveCompleted.Invoke();
                    float timeToAdd = missionObjective.GetTimeToAddSeconds();
                    if (IsTimed() && timeToAdd > 0f)
                        OptionalMissionTimer.AddBonusTime(timeToAdd);
                    if (IsScored() && missionObjective is MissionScorerObjective scorerObjective)
                    {
                        AddMissionScorerPoints(scorerObjective.ScoreToAdd, scorerObjective.UsesComboMultiplier, false);
                        if (OptionalMissionScorer.UpdateStreak(scorerObjective, out float streakScore, out bool streakUsesComboMultiplier))
                            AddMissionScorerPoints(streakScore, streakUsesComboMultiplier, true);
                    }
                }
            }
            else
            {
                m_completedObjectives.Remove(missionObjective);
            }

            // The icon shown is the first incomplete objective's, or the first objective's once all are complete.
            int completeCount = 0;
            bool takeIcon = true;
            foreach (MissionObjective objective in m_missionObjectives)
            {
                if (objective.IsComplete)
                {
                    completeCount++;
                }
                else if (takeIcon)
                {
                    Icon = objective.Icon;
                    takeIcon = false;
                }
            }
            if (takeIcon)
                Icon = m_missionObjectives[0].Icon;

            if (m_persistObjectiveCompletion)
                m_missionState.MarkObjectiveComplete(m_missionObjectives.IndexOf(missionObjective), missionObjective.IsComplete);
            m_missionState.UpdateMissionProgress(completeCount, m_targetObjectivesToComplete, m_persistObjectiveCompletion);
            OnStatusChanged?.Invoke(this);
            if (completeCount == m_targetObjectivesToComplete - 1)
                m_onLastObjective?.Invoke();
            if (completeCount == m_targetObjectivesToComplete)
            {
                UnsubscribeFromObjectives();
                MissionObjectivesCompleted();
            }
        }

        // 060026a8 (0x5B1310)
        private void AddMissionScorerPoints(float scoreToAdd, bool usesComboMultiplier, bool isStreak)
        {
            if (usesComboMultiplier) OptionalMissionScorer.AddPoints(scoreToAdd, isStreak);
            else OptionalMissionScorer.AddPointsFlat(scoreToAdd, isStreak);
        }

        // 060026a9 (0x5B1830)
        protected override void OnDestroy()
        {
            UnsubscribeFromObjectives();
            base.OnDestroy();
        }

        // 060026aa (0x5AFA40)
        protected void UnsubscribeFromObjectives()
        {
            foreach (MissionObjective objective in m_missionObjectives)
            {
                objective.OnStatusChanged -= StatusChanged;
                objective.OnTimerStatusChanged -= RegisterMissionTimer;
            }
        }

        // 060026ab (0x5B1850): only a complete mission replays the previous completion on its objectives.
        public override void OnMissionPreviouslyCompleted()
        {
            if (!MissionComplete) return;
            base.OnMissionPreviouslyCompleted();
            foreach (MissionObjective objective in m_missionObjectives)
            {
                m_completedObjectives.Add(objective);
                objective.OnObjectivePreviouslyCompleted();
            }
        }

        // 060026ac (0x5B1A40): a complete mission keeps its objectives; otherwise each objective resets, which reports its
        // status change before the completed set is cleared.
        public override void ResetMission()
        {
            base.ResetMission();
            if (MissionComplete) return;
            foreach (MissionObjective objective in m_missionObjectives)
                objective.OnObjectiveReset();
            m_completedObjectives.Clear();
        }

        // 060026ad (0x5B1D10): retail keeps the computation of the debug path and button label; the debug-button call that
        // consumed them is compiled out, so nothing is invented for it here.
        [Conditional("BUILD_DEVELOPMENT")]
        private void UpdateObjectiveDebugButton(MissionObjective missionObjective)
        {
            int index = m_missionObjectives.IndexOf(missionObjective);
            if (index < 0) return;
            string path = GetObjectivesDebugPath();
            string label = "Complete #" + (index + 1) + " (" + missionObjective.name + ")";
        }

        // 060026ae (0x5B1E90)
        private string GetObjectivesDebugPath() => "Missions/Objectives " + Definition.name;

        // 060026af/060026b0 (0x5B1F10, 0x5B20A0)
        public void InvokeOnObjectiveCollected(Action onObjectiveCollected)
        {
            foreach (MissionObjective objective in m_missionObjectives)
                objective.OnCollected += onObjectiveCollected;
        }

        public void RemoveOnObjectiveCollected(Action onObjectiveCollected)
        {
            foreach (MissionObjective objective in m_missionObjectives)
                objective.OnCollected -= onObjectiveCollected;
        }

        public MissionInstancedTracker() { }
    }
}
