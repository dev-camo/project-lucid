using System;
using Hardlight;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;
using UnityEngine.Events;

namespace HardlightProject
{
    // Original Game.Runtime 0x0200070c: 27 own methods and four natural
    // compiler-generated methods. The callbacks and timer ordering are retained.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class MissionObjective : TimeScaledComponent_SDT, IMissionTimer
    {
        [SerializeField] private Sprite m_icon;

        [Tooltip("Actions that occur when the objective is activated.")]
        [SerializeField] private UnityEvent m_onActivated;

        [SerializeField]
        [Tooltip("Actions that occur on loading the level, when the associated objective has already been completed.")]
        private UnityEvent m_onPreviouslyCompleted;

        [Tooltip("Actions that occur when the objective is reset due to failing or restarting the mission.")]
        [SerializeField] private UnityEvent m_onReset;

        [SerializeField]
        [Tooltip("Actions that occur when the objective is completed.")]
        private UnityEvent m_onComplete;

        [Tooltip("Time limit from when this objective becomes active. Objective resets if time runs out without completion. 0 = no time limit.")]
        [Min(0f)]
        [SerializeField] private float m_timeLimitSeconds;

        [Tooltip("Seconds to add to overall mission timer (if it has one) on completing this objective.")]
        [SerializeField]
        [Min(0f)] private float m_timeToAddSeconds;

        public readonly Bindable<bool> TimerActive = new Bindable<bool>();
        public readonly Bindable<float> TimeRemaining = new Bindable<float>();
        public readonly Bindable<float> TimeToAddSeconds = new Bindable<float>();

        public bool IsTimerActive { get; private set; }
        public Action<MissionObjective> OnStatusChanged;
        public Action<MissionObjective, bool> OnTimerStatusChanged;
        public Action OnCollected = () => { };
        public bool IsComplete { get; private set; }
        public bool IsActive { get; private set; }

        private float m_timeRemaining;
        private bool m_isPaused;
        private readonly SystemRef<MissionManager> m_missionManagerRef = ProcessManager.GetSystemRef<MissionManager>(null, true);
        private readonly SystemRef<CharacterManager> m_characterManagerRef = ProcessManager.GetSystemRef<CharacterManager>(null, true);

        public Sprite Icon => m_icon;

        protected override void OnValidate()
        {
            base.OnValidate();
        }

        protected override void Awake()
        {
            base.Awake();
            m_missionManagerRef.Get().OnMissionTimersPaused += OnMissionTimersPaused;
            InternalReset();
        }

        public override void OnDestroy()
        {
            // Both native architectures put base cleanup inside the valid branch.
            if (m_missionManagerRef.IsValid())
            {
                m_missionManagerRef.Get().OnMissionTimersPaused -= OnMissionTimersPaused;
                base.OnDestroy();
            }
        }

        public void OnMissionTimersPaused(bool paused)
        {
            m_isPaused = paused;
        }

        private void TryStartTimer()
        {
            // The ordered <= comparison sends NaN through the start branch.
            if (m_timeLimitSeconds <= 0f)
            {
                SetTimerActive(false);
                return;
            }
            SetTimerActive(true);
            SetTimeRemaining(m_timeLimitSeconds);
        }

        private void TryStopTimer()
        {
            SetTimerActive(false);
        }

        public void Action_Collect()
        {
            OnCollected();
        }

        public void Action_ObjectiveComplete()
        {
            IsComplete = true;
            TryStopTimer();
            OnCollected = () => { };
            m_onComplete.Invoke();
            OnStatusChanged?.Invoke(this);
        }

        private void InternalReset(bool triggerStatusChangedEvent = true)
        {
            TimeToAddSeconds.Value = m_timeToAddSeconds;
            IsComplete = false;
            IsActive = false;
            TryStopTimer();
            m_onReset.Invoke();
            if (triggerStatusChangedEvent)
                OnStatusChanged?.Invoke(this);
        }

        public void OnObjectivePreviouslyCompleted()
        {
            IsComplete = true;
            TryStopTimer();
            m_onPreviouslyCompleted.Invoke();
        }

        public void OnObjectiveActivated()
        {
            if (IsActive)
                return;
            IsActive = true;
            TryStartTimer();
            m_onActivated.Invoke();
        }

        public void OnObjectiveReset(bool triggerStatusChangedEvent = true)
        {
            InternalReset(triggerStatusChangedEvent);
        }

        protected override void InternalUpdate(float deltaTime)
        {
            if (!IsActive || !IsTimerActive || IsComplete || m_isPaused)
                return;
            SetTimeRemaining(m_timeRemaining - deltaTime);
            // Re-read after binding notification; unordered remaining time stops.
            if (m_timeRemaining > 0f)
                return;
            TryStopTimer();
        }

        private void SetTimerActive(bool active)
        {
            if (IsTimerActive == active)
                return;
            IsTimerActive = active;
            TimerActive.Value = active;
            OnTimerStatusChanged?.Invoke(this, active);
        }

        private void SetTimeRemaining(float timeRemaining)
        {
            m_timeRemaining = timeRemaining;
            TimeRemaining.Value = timeRemaining;
        }

        public float GetTimeRemainingSeconds() => m_timeRemaining;
        public float GetTimeLimitSeconds() => m_timeLimitSeconds;
        public float GetTimeToAddSeconds() => m_timeToAddSeconds;

        protected bool TryGetCharacter(out Character character)
        {
            return m_characterManagerRef.Get().TryGetCurrentCharacter(out character) && character.IsActive();
        }

        public MissionObjective()
        {
        }
    }
}
