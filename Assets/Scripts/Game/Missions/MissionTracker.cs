using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime 0200071d. Whole owner:32 ordinary methods,
    // two naturally generated Initialise callbacks, the real nested event
    // constructor and the two natural OnOtherMissionComplete closure methods.
    // Generated closure layout remains unverified until the full graph emits.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [RequireComponent(typeof(HashedTrackGroup))]
    [Il2CppSetOption(Option.NullChecks, false)]
    public abstract class MissionTracker : MonoBehaviour
    {
        public enum FailStateBehaviourType { None = 0, ShowResultsScreen = 2 }
        public enum IntroSequenceBehaviour { None = 0, UseDefault = 1, CustomSequence = 2 }

        //02000720/0600281d: serializable two-field class, no UnityEvent
        //allocation in its original constructor; Unity authoring supplies it.
        [Serializable]
        public class MissionCompleteEvent
        {
            public MissionDefinition Mission;
            public UnityEvent Event;
        }

        protected MissionState m_missionState;
        private IMissionContext m_missionContext;
        [Tooltip("Track data is registered to track manager on mission instantiation at runtime.")]
        [SerializeField] private HashedTrackGroup m_trackData;
        [SerializeField]
        [Tooltip("Whether the mission can complete. If false, UI will not instantiate any objective progress visuals.")]
        private bool m_canComplete = true;
        [Tooltip("Actions that occur when the mission is completed. UI changes handled by the mission manager will happen automatically.")]
        [SerializeField] private UnityEvent m_onCompleted;
        [Tooltip("Actions that occur on loading the level, when the associated mission has already been completed.")]
        [SerializeField] private UnityEvent m_onPreviouslyCompleted;
        [Tooltip("Actions that occur when Action_OnRestart is called.")]
        [SerializeField] private UnityEvent m_onRestart;
        [Tooltip("Actions that occur when an other mission is completed.")]
        [SerializeField] private List<MissionCompleteEvent> m_onOtherMissionCompleteEvents = new List<MissionCompleteEvent>();
        [SerializeField]
        [Tooltip("Use this to display different UI when the level ends and the results shows up.")]
        private UIContainerIdentifier m_resultsActiveMissionOverrideIdentifier;
        [SerializeField] private IntroSequenceBehaviour m_introBehaviour = IntroSequenceBehaviour.UseDefault;
        [ShowIf("m_introBehaviour", IntroSequenceBehaviour.CustomSequence)]
        [Tooltip("Use this to display different UI when the level ends and the results shows up.")]
        [SerializeField] private IntroSequence m_customIntroSequence;
        [Tooltip("Assign timer to make mission into a speed run.")]
        [SerializeField] private MissionTimer m_optionalMissionTimer;
        [Tooltip("Assign scorer to make mission into a score attack.")]
        [SerializeField] private MissionScorer m_optionalMissionScorer;
        [SerializeField]
        [Header("Mission failure behaviour")]
        private FailStateBehaviourType m_failStateBehaviour;
        [Header("String to show on mission failure screen if failure is due to timer expiry")]
        [SerializeField] private Strings m_failTimeExpiredMessage = Strings.MISSION_TIME_UP_TITLE;
        [InspectorReadOnly]
        [SerializeField] private List<WaypointTarget> m_waypointTargets = new List<WaypointTarget>();
        private readonly SystemRef<LevelManager> m_levelManagerRef = ProcessManager.GetSystemRef<LevelManager>();
        private readonly SystemRef<TrackManager> m_trackManagerRef = ProcessManager.GetSystemRef<TrackManager>();
        private readonly SystemRef<CollectableManager> m_collectableManagerRef = ProcessManager.GetSystemRef<CollectableManager>();
        private LevelManager m_levelManager;
        private CollectableManager m_collectableManager;
        private CollectableChangeMetadata m_collectableMetadata;
        protected bool m_hasStarted;
        private readonly HashSet<IMissionTimer> m_activeMissionTimers = new HashSet<IMissionTimer>();
        private float m_lowestTimeRemaining;

        //060027fb..06002804: direct authored fields and original managed-null
        //state tests; Unity's destroyed-object comparison is not used for state.
        public MissionTimer OptionalMissionTimer => m_optionalMissionTimer;
        public MissionScorer OptionalMissionScorer => m_optionalMissionScorer;
        public FailStateBehaviourType FailStateBehaviour => m_failStateBehaviour;
        public bool CanComplete => m_canComplete;
        public bool IsComplete => m_missionState != null && m_missionState.Complete;
        public MissionDefinition Definition => m_missionState != null ? m_missionState.Definition : null;
        public UIContainerIdentifier ResultsActiveMissionOverrideIdentifier => m_resultsActiveMissionOverrideIdentifier;
        public IntroSequenceBehaviour IntroBehaviour => m_introBehaviour;
        public IntroSequence CustomIntroSequence => m_customIntroSequence;
        public IReadOnlyList<WaypointTarget> WaypointTargets => m_waypointTargets;

        //06002805/ARM59ba80/x86 corresponding full range: actual empty hook.
        protected virtual void OnValidate() { }

        //06002806 and natural0600281b/c. Assign state/context first; retain
        //otherwise unused original validation reads for their fault/callback
        //order. Registration and timer subscriptions are not made idempotent.
        public virtual void Initialise(MissionState missionState, IMissionContext missionContext)
        {
            m_missionState = missionState;
            m_missionContext = missionContext;
            if (m_missionContext != null && m_missionContext.OverrideTimeLimitSeconds > 0f)
                _ = m_optionalMissionTimer == null;
            m_hasStarted = false;
            m_levelManager = m_levelManagerRef.Get();
            if (m_missionState.Complete) _ = m_missionState.Definition.IsTimeTrial();
            if (m_trackManagerRef.IsNull() && m_levelManager.TryGetCurrentLevel(out LevelManagerLevel level))
                _ = level.SceneDefinition.SceneName;
            m_trackManagerRef.InvokeOnValid(trackManager => trackManager.Register(m_trackData));
            m_collectableManagerRef.InvokeOnValid(manager =>
            {
                m_collectableManager = manager;
                m_collectableMetadata.Source = CollectableSource.MissionStart;
            });
            m_levelManager.OnIntroSequenceComplete += OnIntroSequenceComplete;
            if ((!m_missionState.Complete || m_missionState.Definition.IsTimeTrial()) && IsTimed())
            {
                m_optionalMissionTimer.OnTimerStatusChanged += RegisterMissionTimer;
                m_optionalMissionTimer.OnTimerExpired += OnTimerExpired;
            }
        }

        //06002807: original failure-message storage precedes re-reading the
        //failure mode and finishing. The real App/graph interfaces are retained.
        private void OnTimerExpired()
        {
            if (m_failStateBehaviour == FailStateBehaviourType.ShowResultsScreen)
            {
                ProcessManager.GetSystem<App>().Storage.SetValue(AppFSMKeys.FailureScreenStringOverride, m_failTimeExpiredMessage);
                Action_FailMission();
            }
        }

        //06002808: the context override is read twice on its positive branch.
        //ResetTimer's status callback can change the timer before Action_StartTimer.
        private void OnIntroSequenceComplete()
        {
            if (!IsTimed()) return;
            if (m_missionContext != null && m_missionContext.OverrideTimeLimitSeconds > 0f)
                m_optionalMissionTimer.SetTimeLimitOverride(m_missionContext.OverrideTimeLimitSeconds);
            else m_optionalMissionTimer.ClearTimeLimitOverride();
            m_optionalMissionTimer.ResetTimer();
            m_optionalMissionTimer.Action_StartTimer();
        }

        //06002809: unregister before testing optional timer. This does not
        //unsubscribe the level intro callback or clear the timer set/fields.
        public virtual void Close()
        {
            m_trackManagerRef.Get().Unregister(m_trackData);
            if (IsTimed())
            {
                m_optionalMissionTimer.OnTimerStatusChanged -= RegisterMissionTimer;
                m_optionalMissionTimer.OnTimerExpired -= OnTimerExpired;
            }
        }

        //0600280a: DidCompleteOnTime owns failure completion as a side effect.
        //Positive cooldown is fetched again after delegate allocation; delayed
        //completion is not canceled or captured to an earlier state instance.
        public virtual void MissionObjectivesCompleted()
        {
            if (IsTimed() && !DidCompleteOnTime()) return;
            if (m_missionState.Definition.CompletionCooldownTime > 0f)
                CoroutineUtils.Delay(ReportMissionComplete, m_missionState.Definition.CompletionCooldownTime);
            else ReportMissionComplete();
        }

        //0600280b: state completion precedes the unguarded authored event.
        public void ReportMissionComplete()
        {
            m_missionState.FinishMission(true);
            m_onCompleted.Invoke();
        }

        //0600280c/closure0600281e/f: only the first matching event executes.
        //The predicate deliberately dereferences its entry and Mission rather
        //than sanitizing malformed definitions or checking Unity lifetime.
        public void OnOtherMissionComplete(string otherGUID)
        {
            MissionCompleteEvent matchingEvent = m_onOtherMissionCompleteEvents.Find(completeEvent => completeEvent.Mission.GetGUID() == otherGUID);
            if (matchingEvent != null) matchingEvent.Event.Invoke();
        }

        //0600280d: original RET, not an unresolved placeholder.
        private void RemoveDebug() { }
        //0600280e/f: optional Unity component lifetime, not managed-null only.
        public bool IsTimed() => m_optionalMissionTimer != null;
        public bool IsScored() => m_optionalMissionScorer != null;

        //06002810: sub-missions skip the tier count; a zero tier count falls
        //back to the mission definition. Only strictly positive grants occur.
        public virtual void StartMission(bool isSubMission)
        {
            int count = isSubMission ? 0 : m_collectableManager.GetCollectableTierStartingCount(CollectableType.Ring);
            if (count == 0) count = Definition.StartingRingCount;
            if (count > 0) m_collectableManager.ChangeCollectableAmount(CollectableType.Ring, count, m_collectableMetadata);
            m_hasStarted = true;
        }

        //06002811/12: no timer reset, progress reset, or synthesized event.
        public virtual void OnMissionPreviouslyCompleted() => m_onPreviouslyCompleted.Invoke();
        public virtual void ResetMission() => m_hasStarted = false;

        //06002813: nullable restart event, real level restart, then the current
        //context's failed-attempt analytics, even if Restart changes other state.
        public void Action_OnRestart()
        {
            m_onRestart?.Invoke();
            m_levelManager.Restart();
            m_missionContext.SendMissionFailedAnalytics(m_missionState);
        }

        //06002814/15: only intro completion is removed; Close owns timer hooks.
        protected virtual void OnDestroy() => UnsubscribeEvents();
        private void UnsubscribeEvents()
        {
            if (m_levelManager == null) return;
            m_levelManager.OnIntroSequenceComplete -= OnIntroSequenceComplete;
        }

        //06002816: stop before reading flags/time, allowing the original status
        //callback to mutate the field. Negative time can fail ordinary missions;
        //time trials may complete after their timer limit. No null timer guard.
        private bool DidCompleteOnTime()
        {
            m_optionalMissionTimer.Action_PauseTimer();
            if (m_optionalMissionTimer.FailOnTimeLimitReached && m_optionalMissionTimer.TimeLimit > 0f &&
                m_optionalMissionTimer.GetTimeRemainingSeconds() < 0f && !Definition.IsTimeTrial())
            {
                m_missionState.FinishMission(false);
                return false;
            }
            return true;
        }

        //06002817/18: null timer entries are allowed into the original set;
        //invalid authored data then faults during Update as it originally did.
        public void RegisterMissionTimer(IMissionTimer missionTimer, bool timerActive)
        {
            if (timerActive) m_activeMissionTimers.Add(missionTimer);
            else m_activeMissionTimers.Remove(missionTimer);
        }
        public void Action_FailMission()
        {
            if (m_failStateBehaviour == FailStateBehaviourType.ShowResultsScreen)
                m_missionState.FinishMission(false);
        }

        //06002819: context update precedes resetting the timer minimum. Capture
        //the prior minimum before invoking each interface getter; enumeration
        //disposes on failure and skips the final clamp when an exception escapes.
        private void Update()
        {
            if (m_missionState == null) return;
            m_missionContext?.UpdateRealTimeTaken(Time.unscaledDeltaTime);
            m_lowestTimeRemaining = float.MaxValue;
            foreach (IMissionTimer timer in m_activeMissionTimers)
                m_lowestTimeRemaining = Mathf.Min(m_lowestTimeRemaining, timer.GetTimeRemainingSeconds());
            m_lowestTimeRemaining = Mathf.Max(m_lowestTimeRemaining, 0f);
        }

        //0600281a: all declaration initializers above execute in original field
        //order before MonoBehaviour construction. Authored UnityEvents stay null.
        protected MissionTracker() { }
    }
}
