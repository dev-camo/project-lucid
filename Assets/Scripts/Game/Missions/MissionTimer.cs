using System;
using System.Collections.Generic;
using Hardlight;
using UnityEngine;
using UnityEngine.Events;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class MissionTimer : TimeScaledComponent_SDT, IMissionTimer
    {
        [SerializeField, Tooltip("Whether the timer remains visible when paused.")]
        private bool m_alwaysVisible;
        [SerializeField, Tooltip("Time limit (seconds)"), Min(0f)]
        private float m_timeLimit = 60f;
        [SerializeField, Tooltip("Whether the timer initialises to the previous best time or always uses the time limit.")]
        private bool m_useBestTime = true;
        [SerializeField, Tooltip("Time expired events will be fired if true.")]
        private bool m_failOnTimeLimitReached = true;
        [SerializeField, Tooltip("Whether game logic will pause and unpause timer.")]
        private bool m_canBePaused;
        [SerializeField, Tooltip("Events to fire on time elapsing to the next second.  Each list entry represents the seconds remaining.")]
        private List<UnityEvent> m_onTimeElapseEvents;
        [SerializeField, Tooltip("Events to fire on time expired.")]
        private UnityEvent m_onTimeExpiredEvents;
        [Tooltip("Events to fire on timer being destroyed."), SerializeField]
        private UnityEvent m_onTimerDestroy;
        [Tooltip("Should this timer be hidden in the UI."), SerializeField]
        private bool m_isHidden;

        public Action<IMissionTimer, bool> OnTimerStatusChanged { get; set; }
        public Action OnTimerExpired { get; set; }
        public Action<float> OnBonusTimeAdded { get; set; }
        public bool IsTimerActive { get; private set; }

        private float m_timeRemaining;
        private bool m_hasExpired;
        private float m_overrideTimeLimit;
        private float m_totalBonusTime;
        private bool m_isPaused;
        private readonly SystemRef<MissionManager> m_missionManagerRef = ProcessManager.GetSystemRef<MissionManager>();
        private static bool s_unlimitedTime;

        public bool AlwaysVisible => m_alwaysVisible;
        public bool FailOnTimeLimitReached => m_failOnTimeLimitReached;
        public bool UseBestTime => m_useBestTime;
        public float TimeLimit => m_timeLimit;
        public bool CanBePaused => m_canBePaused;
        public bool IsHidden => m_isHidden;
        public static bool UnlimitedTime => s_unlimitedTime;

        protected override void OnValidate()
        {
            base.OnValidate();
            // The original queries the component and discards both the result and value.
            gameObject.TryGetComponent(out MissionTracker missionTracker);
        }

        protected override void InternalUpdate(float deltaTime)
        {
            if (!IsTimerActive || s_unlimitedTime || m_isPaused) return;
            int previousSeconds = Mathf.FloorToInt(m_timeRemaining);
            m_timeRemaining = Mathf.Max(m_timeRemaining - deltaTime, 0f);
            int secondsRemaining = Mathf.FloorToInt(m_timeRemaining);
            if (secondsRemaining != previousSeconds && secondsRemaining < m_onTimeElapseEvents.Count)
                m_onTimeElapseEvents[secondsRemaining]?.Invoke();

            // A time-elapsed listener may change the remaining time. Re-read it here;
            // the original expires unordered values as well as zero and negative values.
            if (m_timeRemaining > 0f) return;
            if (!m_hasExpired)
            {
                m_hasExpired = true;
                m_onTimeExpiredEvents?.Invoke();
            }
            if (m_failOnTimeLimitReached)
            {
                OnTimerExpired?.Invoke();
                SetTimerActive(false);
            }
        }

        public void Action_StartTimer()
        {
            if (IsTimerActive) return;
            if (m_timeRemaining <= 0f)
            {
                m_totalBonusTime = 0f;
                m_timeRemaining = GetTimeLimitSeconds();
            }
            m_hasExpired = false;
            SetTimerActive(true);
        }

        public void Action_ResumeTimer()
        {
            if (!IsTimerActive) SetTimerActive(true);
        }

        public void Action_PauseTimer() { SetTimerActive(false); }

        public void ResetTimer()
        {
            m_totalBonusTime = 0f;
            m_timeRemaining = 0f;
            SetTimerActive(false);
        }

        private void SetTimerActive(bool active)
        {
            IsTimerActive = active;
            OnTimerStatusChanged?.Invoke(this, active);
        }

        public float GetTimeRemainingSeconds() { return m_timeRemaining; }
        public float GetTimeElapsedSeconds() { return GetTimeLimitSeconds() + m_totalBonusTime - m_timeRemaining; }
        public float GetTimeLimitSeconds() { return m_overrideTimeLimit > 0f ? m_overrideTimeLimit : m_timeLimit; }

        protected override void Awake()
        {
            base.Awake();
            m_missionManagerRef.Get().OnMissionTimersPaused += OnMissionTimersPaused;
        }

        public override void OnDestroy()
        {
            SetTimerActive(false);
            m_onTimerDestroy?.Invoke();
            // Original base destruction is conditional on this reference being valid.
            if (m_missionManagerRef.IsValid())
            {
                m_missionManagerRef.Get().OnMissionTimersPaused -= OnMissionTimersPaused;
                base.OnDestroy();
            }
        }

        private void OnMissionTimersPaused(bool paused) { m_isPaused = paused; }
        public void SetTimeLimitOverride(float overrideSeconds) { m_overrideTimeLimit = overrideSeconds; }
        public void ClearTimeLimitOverride() { m_overrideTimeLimit = 0f; }

        public void AddBonusTime(float timeToAddSeconds)
        {
            m_timeRemaining += timeToAddSeconds;
            m_totalBonusTime += timeToAddSeconds;
            OnBonusTimeAdded?.Invoke(timeToAddSeconds);
        }

        protected override TimeCategory GetDefaultTimeCategoryEnum() { return TimeCategory.PlayerMovement; }
        protected override UpdateOn GetDefaultUpdateOn() { return UpdateOn.FixedUpdate; }
        public static void Debug_SetUnlimitedTime(bool isActive) { s_unlimitedTime = isActive; }
        public MissionTimer() { }
    }
}
