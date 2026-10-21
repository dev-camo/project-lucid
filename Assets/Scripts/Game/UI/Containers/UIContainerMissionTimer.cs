using System;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.Utils;
using TMPro;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020008ea; bodies translated from the 1.10.1 x86-64 binary.
    // The in-game mission timer: shows the time remaining (or elapsed) of the active mission's timer, its pause state and a fading
    // "bonus time" text. Float casts pin the single-precision rounding of the native body.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class UIContainerMissionTimer : UIContainer
    {
        [Tooltip("Formatted string to use for the timer display. {0} = minutes, {1} = seconds, {2} = 1/100th seconds")]
        [SerializeField]
        [HashEnum(null)]
        private Strings m_timeDisplayFormat;
        [Tooltip("Formatted string to use for the bonus time display. {0} = seconds")]
        [SerializeField]
        [HashEnum(null)]
        private Strings m_bonusTimeDisplayFormat;
        [SerializeField]
        [Tooltip("Text used to display bonus time. Is hidden until bonus time is added.")]
        private TextMeshProUGUI m_bonusTimeText;
        [SerializeField]
        [Tooltip("Animation component that is played when bonus time is collected.")]
        private Animation m_bonusTimeAnimation;
        [SerializeField]
        [Min(0.5f)]
        [Tooltip("How long to show the bonus time text for when bonus time is added.")]
        private float m_bonusTimeDisplayDurationSeconds;
        [Tooltip("Opacity over time for the bonus time text, when it is shown. x-axis is scaled to the duration above.")]
        [SerializeField]
        private AnimationCurve m_bonusTimeOpacityCurve;
        [Tooltip("When time remaining goes under this threshold, alternate text colour is used.")]
        [SerializeField]
        [Min(0f)]
        private float m_lowTimeRemainingThresholdSeconds;
        [SerializeField]
        [Tooltip("Typical text colour for the main timer text.")]
        private Color m_textColourNormal;
        [Tooltip("Text colour used on the main timer next when there is not much time remaining, or time has run out.")]
        [SerializeField]
        private Color m_textColourAlternate;
        [SerializeField]
        [Tooltip("Main timer text.")]
        private TextMeshProUGUI m_timerText;
        [SerializeField]
        [Tooltip("Object to be enabled while timer is active.")]
        private GameObject m_enableRoot;
        [SerializeField]
        [Tooltip("Object to be enabled when timer can be paused.")]
        private GameObject m_pauseGroupRoot;
        [SerializeField]
        [Tooltip("Object to be enabled while timer is paused.")]
        private GameObject m_pauseEnabledRoot;
        [Tooltip("Object to be enabled while timer is not paused.")]
        [SerializeField]
        private GameObject m_pauseDisabledRoot;
        private MissionTimer m_missionTimer;
        private readonly SystemRef<MissionManager> m_missionManagerRef = ProcessManager.GetSystemRef<MissionManager>(null, true);
        private bool m_initialised;
        private bool m_countsDown;
        private float m_bonusTimeDisplayTimeRemainingSeconds;
        private string m_timeDisplayFormatString;
        private string m_bonusTimeDisplayFormatString;
        private bool m_hasMissionTimer;
        private bool m_missionTimerCanBePaused;
        private float m_bonusTimeOpacityCurveTotalTime;
        private bool m_isTimeTrial;

        // 0x0600337b (0x668A20)
        private void OnEnable()
        {
            m_initialised = false;
            ClearValues(false);
        }

        // 0x0600337c (0x668AB0): once initialised, mirrors the timer's pause state, then shows the time counting down (or up, for a
        // mission without a fail state) in the "minutes:seconds:hundredths" format and fades out the bonus time text.
        private void Update()
        {
            if (!TryInitialise())
                return;
            if (m_missionTimerCanBePaused)
            {
                bool timerEnabled = m_missionTimer.enabled;
                SetPauseState(m_pauseEnabledRoot, timerEnabled && !m_missionTimer.IsTimerActive);
                SetPauseState(m_pauseDisabledRoot, timerEnabled && m_missionTimer.IsTimerActive);
            }
            if (!m_hasMissionTimer)
            {
                ClearValues(false);
                return;
            }
            if (!m_missionTimer.IsTimerActive)
            {
                ClearValues(m_missionTimer.AlwaysVisible);
                return;
            }
            m_enableRoot.SetActive(!m_missionTimer.IsHidden);
            float timeRemaining = m_missionTimer.GetTimeRemainingSeconds();
            bool lowTimeRemaining;
            float displayedSeconds;
            if (m_countsDown)
            {
                lowTimeRemaining = m_lowTimeRemainingThresholdSeconds > timeRemaining;
                displayedSeconds = timeRemaining;
            }
            else
            {
                lowTimeRemaining = m_isTimeTrial && m_lowTimeRemainingThresholdSeconds > timeRemaining;
                displayedSeconds = (float)(m_missionTimer.GetTimeLimitSeconds() - timeRemaining);
            }
            TimeSpan displayedTime = TimeSpan.FromSeconds(displayedSeconds);
            string minutes = ((int)displayedTime.TotalMinutes).ToString("D2");
            string seconds = displayedTime.Seconds.ToString("D2");
            string hundredths = (displayedTime.Milliseconds / 10).ToString("D2");
            m_timerText.text = string.Format(m_timeDisplayFormatString, minutes, seconds, hundredths);
            m_timerText.color = (lowTimeRemaining | (timeRemaining < 0f)) ? m_textColourAlternate : m_textColourNormal;

            m_bonusTimeDisplayTimeRemainingSeconds = (float)(m_bonusTimeDisplayTimeRemainingSeconds - Time.deltaTime);
            if (m_bonusTimeDisplayTimeRemainingSeconds > 0f)
            {
                float displayProgress = (float)(m_bonusTimeDisplayTimeRemainingSeconds / m_bonusTimeDisplayDurationSeconds);
                float curveTime = (float)((float)(1f - Mathf.Clamp01(displayProgress)) * m_bonusTimeOpacityCurveTotalTime);
                m_bonusTimeText.alpha = Mathf.Clamp01(m_bonusTimeOpacityCurve.Evaluate(curveTime));
            }
            else
            {
                m_bonusTimeText.enabled = false;
            }
        }

        // 0x0600337d (0x669380)
        private void SetPauseState(GameObject pauseRoot, bool state)
        {
            if (pauseRoot != null && pauseRoot.activeSelf != state)
                pauseRoot.SetActive(state);
        }

        // 0x0600337e (0x668A60)
        private void ClearValues(bool timerVisible)
        {
            m_bonusTimeText.enabled = false;
            m_bonusTimeDisplayTimeRemainingSeconds = 0f;
            if (!timerVisible)
                m_enableRoot.SetActive(false);
        }

        // 0x0600337f (0x668F60): waits for the active mission, its tracker and the strings, then binds to the tracker's timer (when it
        // has one) and caches the display formats.
        private bool TryInitialise()
        {
            if (m_initialised)
                return true;
            if (!m_missionManagerRef.IsValid())
                return false;
            if (!m_missionManagerRef.Get().TryGetActiveMissionState(out MissionState missionState))
                return false;
            if (!missionState.SetUpComplete)
                return false;
            if (!StringTable.AreStringsLoaded())
                return false;
            m_bonusTimeOpacityCurveTotalTime = m_bonusTimeOpacityCurve[m_bonusTimeOpacityCurve.length - 1].time;
            m_bonusTimeDisplayTimeRemainingSeconds = 0f;
            m_timeDisplayFormatString = StringTable.GetString(m_timeDisplayFormat);
            m_bonusTimeDisplayFormatString = StringTable.GetString(m_bonusTimeDisplayFormat);
            if (missionState.Tracker.IsTimed())
            {
                if (m_missionTimer != null)
                    m_missionTimer.OnBonusTimeAdded -= OnBonusTimeAdded;
                m_missionTimer = missionState.Tracker.OptionalMissionTimer;
                m_missionTimer.OnBonusTimeAdded += OnBonusTimeAdded;
                m_hasMissionTimer = true;
                m_missionTimerCanBePaused = m_missionTimer.CanBePaused;
                m_isTimeTrial = missionState.Definition.IsTimeTrial();
                m_countsDown = missionState.Tracker.FailStateBehaviour != MissionTracker.FailStateBehaviourType.None;
            }
            SetPauseState(m_pauseGroupRoot, m_missionTimerCanBePaused);
            m_initialised = true;
            return true;
        }

        // 0x06003380 (0x669400)
        private void OnBonusTimeAdded(float timeAddedSeconds)
        {
            m_bonusTimeText.enabled = true;
            m_bonusTimeAnimation.Play();
            m_bonusTimeText.text = string.Format(m_bonusTimeDisplayFormatString, timeAddedSeconds.ToString());
            m_bonusTimeDisplayTimeRemainingSeconds = m_bonusTimeDisplayDurationSeconds;
        }

        // 0x06003381 (0x669480)
        private void OnDestroy()
        {
            if (m_missionTimer != null)
                m_missionTimer.OnBonusTimeAdded -= OnBonusTimeAdded;
        }

        // 0x06003382
        public UIContainerMissionTimer() { }
    }
}
