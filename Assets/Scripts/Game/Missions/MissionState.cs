using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Analytics;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HardlightProject
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class MissionState
    {
        private UIContainerIdentifier m_activeMissionContainer;
        private Action m_introCompletedCallback;
        private SaveDataLevelMission m_saveData;
        private readonly SystemRef<UIManager> m_uiManagerRef = ProcessManager.GetSystemRef<UIManager>();
        private bool m_dataSetUp;
        private int m_deathCount;
        private int m_attempts;
        private bool m_finished;

        private bool TrackerSetUp => Tracker != null;
        public MissionTracker Tracker { get; private set; }
        public bool SetUpComplete => m_dataSetUp && TrackerSetUp;
        public MissionDefinition Definition { get; private set; }
        public bool Complete => m_saveData != null && m_saveData.Complete;
        public bool IsInstanceCompleted { get; private set; }
        public bool HasPendingReward { get; private set; }
        public int Progress { get; private set; }
        public int Target { get; private set; }
        public string LevelGUID { get; private set; }
        public IReadOnlyCollection<int> CompletedObjectiveIndices => m_saveData.CompletedObjectiveIndices;
        public int DeathCount => m_deathCount;
        public int Attempts => m_saveData.Attempts;
        public Action OnUpdated;
        public float BestTimeSeconds { get; private set; }

        public float TrackerTimeElapsed
        {
            get
            {
                if (!SetUpComplete || !Tracker.IsTimed())
                    return 0f;
                return Tracker.OptionalMissionTimer.GetTimeElapsedSeconds();
            }
        }

        private int TrackerScore
        {
            get
            {
                if (!SetUpComplete || !Tracker.IsScored())
                    return 0;
                return Tracker.OptionalMissionScorer.PointsTotal;
            }
        }

        public void SetUpData(MissionDefinition definition, SaveDataLevelMission saveData, string levelGuid)
        {
            Definition = definition;
            // Both original architectures evaluate this Unity object comparison and discard its result.
            _ = TrackerSetUp;
            m_saveData = saveData;
            Progress = saveData.Progress;
            HasPendingReward = false;
            Definition.UpdateOverrides(m_saveData);
            m_dataSetUp = true;
            LevelGUID = levelGuid;
            BestTimeSeconds = saveData.BestTimeSeconds;
        }

        public void SetUpTracker(Transform trackerParent, IMissionContext missionContext)
        {
            Definition.LoadMissionPrefab((tracker, isInstantiated) =>
                OnTrackerSetup(tracker, trackerParent, missionContext, isInstantiated));
        }

        private void OnTrackerSetup(MissionTracker tracker, Transform trackerParent, IMissionContext missionContext, bool isInstantiated)
        {
            if (isInstantiated)
            {
                Tracker = tracker;
                Tracker.transform.SetParent(trackerParent, true);
            }
            else
            {
                Tracker = Object.Instantiate(tracker, trackerParent);
            }

            Tracker.Initialise(this, missionContext);
            if (Complete && !Definition.IsTimeTrial())
                OnMissionPreviouslyCompleted();
        }

        public void UpdateMissionProgress(int progress, int target, bool saveProgress)
        {
            int previousProgress = Progress;
            Progress = progress;
            Target = target;
            if (progress != 0 && previousProgress != progress)
                OnUpdated?.Invoke();

            if (saveProgress)
            {
                m_saveData.Progress = progress;
                m_saveData.RequestSave();
            }
        }

        public void FinishMission(bool isComplete)
        {
            if (m_finished)
                return;
            m_finished = true;
            bool previouslyComplete = isComplete && MarkComplete();
            if (m_activeMissionContainer != null)
                m_uiManagerRef.Get().Close(m_activeMissionContainer);

            MissionManager missionManager = ProcessManager.GetSystem<MissionManager>();
            IMissionContext activeMissionContext = missionManager.ActiveMissionContext;
            if (isComplete)
            {
                // The original first-completion callback is invoked without a null guard.
                if (!previouslyComplete)
                    missionManager.OnRewardCountChanged.Invoke();
                foreach ((string guid, MissionState state) in missionManager.MissionStates)
                {
                    if (guid != Definition.GetGUID() && state.TrackerSetUp)
                        state.Tracker.OnOtherMissionComplete(Definition.GetGUID());
                }
                missionManager.OnMissionCompleted?.Invoke(this);
            }

            if (activeMissionContext != null && activeMissionContext.MissionDefinition == Definition)
            {
                if (isComplete)
                    activeMissionContext.OnActiveMissionComplete(missionManager, TrackerTimeElapsed, TrackerScore);
                else
                    activeMissionContext.SendMissionFailedAnalytics(this);
                OnActiveMissionFinished(isComplete);
            }
        }

        private static void OnActiveMissionFinished(bool isComplete)
        {
            ProcessManager.GetSystem<LevelManager>().ExitLevel(!isComplete, !isComplete);
        }

        private void OnMissionPreviouslyCompleted()
        {
            if (Tracker == null)
                return;
            Tracker.OnMissionPreviouslyCompleted();
        }

        public void MarkObjectiveComplete(int objectiveIndex, bool complete)
        {
            if (complete)
            {
                if (Definition.Type == MissionType.BlueCoins && !m_saveData.IsObjectiveComplete(objectiveIndex))
                    AnalyticsEventCollector.MissionBlueCoinCollectedEvent(Definition);
                m_saveData.MarkObjectiveComplete(objectiveIndex);
            }
            else
            {
                m_saveData.MarkObjectiveIncomplete(objectiveIndex);
            }
        }

        public void DestroyMissionTracker(IReadOnlyDictionary<string, MissionState> missionStates, bool destroySubMissions = true)
        {
            if (!TrackerSetUp)
                return;
            Tracker.Close();
            Object.Destroy(Tracker.gameObject);
            Tracker = null;
            Definition.UnloadMissionPrefab();
            if (destroySubMissions)
            {
                foreach (MissionDefinition subMission in Definition.SubMissions)
                {
                    if (missionStates.TryGetValue(subMission.GetGUID(), out MissionState state))
                        state.DestroyMissionTracker(missionStates);
                }
            }
        }

        public void ReplaceSaveData(SaveDataLevelMission saveData)
        {
            // This comparison is also retained even though the original ignores its result.
            _ = TrackerSetUp;
            m_saveData = saveData;
            Progress = saveData.Progress;
            HasPendingReward = false;
            Definition.UpdateOverrides(m_saveData);
        }

        private bool MarkComplete()
        {
            IsInstanceCompleted = true;
            if (Definition.RewardValue > 0)
                HasPendingReward = true;
            bool previouslyComplete = m_saveData.Complete;
            int savedProgress = !Definition.Replayable || Definition.IsPersistentTracker ? Target : 0;
            m_saveData.MarkComplete(savedProgress);
            m_saveData.RequestSave();
            OnUpdated?.Invoke();
            return previouslyComplete;
        }

        public void ClearPendingReward()
        {
            HasPendingReward = false;
        }

        public void IncrementDeathCount()
        {
            m_deathCount = unchecked(m_deathCount + 1);
        }

        public void MarkNewAttempt()
        {
            m_saveData.Attempts = unchecked(m_saveData.Attempts + 1);
        }

        public void SetAttempts(int attempts)
        {
            m_saveData.Attempts = attempts;
        }

        public bool TimerStarted()
        {
            return SetUpComplete && Tracker.IsTimed() && Tracker.OptionalMissionTimer.IsTimerActive;
        }

        public MissionState()
        {
        }
    }
}
