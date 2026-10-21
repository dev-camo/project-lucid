using System;
using System.Collections.Generic;
using Hardlight;
using Hardlight.Enums;
using Hardlight.Localisation;
using Hardlight.UI.Binding;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0x020006f3, all six methods and fourteen fields. Mirrors the
    // challenge manager's feature state, the unplayed-challenge count and the time left in
    // the current challenge period into UI bindables.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class ChallengeManagerBinders : MonoBehaviour
    {
        public readonly Bindable<bool> ChallengeFeatureUnlocked = new Bindable<bool>();
        public readonly Bindable<bool> ChallengeFeatureIsNew = new Bindable<bool>();
        public readonly Bindable<int> NewChallengeCount = new Bindable<int>();
        public readonly Bindable<string> TimeRemainingText = new Bindable<string>();

        [Tooltip("Un-tick this if the time remaining string binder won't be used, so we don't waste CPU creating the formatted string.")]
        [SerializeField] private bool m_updateTimeRemaining = true;

        [SerializeField] [ShowIf("m_updateTimeRemaining", null)] private bool m_useShortFormats;

        [Tooltip("String format for the time remaining with days, hours, minutes, and seconds. Parameters: {0} days, {1} hours, {2} minutes, {3} seconds.")]
        [SerializeField] [ShowIf("m_updateTimeRemaining", null)]
        private Strings m_timeRemainingFormat = Strings.CHALLENGES_CYCLE_DAYS_HOURS_MINUTES_SECONDS_SHORT;

        [Tooltip("String format for the time remaining with only days and hours. Parameters: {0} days, {1} hours.")]
        [SerializeField] [ShowIf("m_useShortFormats", null)]
        private Strings m_timeRemainingFormatDaysHours = Strings.CHALLENGES_CYCLE_DAYS_HOURS_SHORT;

        [ShowIf("m_useShortFormats", null)] [SerializeField]
        [Tooltip("String format for the time remaining with only hours and minutes. Parameters: {0} hours, {1} minutes.")]
        private Strings m_timeRemainingFormatHoursMinutes = Strings.CHALLENGES_CYCLE_HOURS_MINUTES_SHORT;

        [ShowIf("m_useShortFormats", null)]
        [Tooltip("String format for the time remaining with only minutes and seconds. Parameters: {0} minutes, {1} seconds.")]
        [SerializeField] private Strings m_timeRemainingFormatMinutesSeconds = Strings.CHALLENGES_CYCLE_MINUTES_SECONDS_SHORT;

        [SerializeField]
        [Tooltip("Optional. Alert badge to display count of new challenges. Displays alert icon if feature is newly unlocked or there is unacknowledged bonus XP.")]
        private UIWidgetSDTAlertBadge m_alertBadge;

        private SystemRef<ChallengeManager> m_challengeManagerRef;
        private TimeSpan m_lastTimeRemaining;
        private readonly object[] m_stringParams = new object[4];

        // 0x060025d5: the reference is created once; the callback runs now if the
        // manager is already registered, otherwise when it starts up.
        private void OnEnable()
        {
            m_challengeManagerRef ??= ProcessManager.GetSystemRef<ChallengeManager>(null, true);
            m_challengeManagerRef.InvokeOnValid(OnChallengeManagerValid);
        }

        // 0x060025d6: subscribe, then publish the current challenge list at once.
        private void OnChallengeManagerValid(ChallengeManager challengeManager)
        {
            challengeManager.OnChallengesUpdated += OnChallengesUpdated;
            OnChallengesUpdated(challengeManager.GetChallengeStates());
        }

        // 0x060025d7: unsubscribe only while the manager still exists.
        private void OnDisable()
        {
            if (m_challengeManagerRef.IsNull())
                return;
            m_challengeManagerRef.Get().OnChallengesUpdated -= OnChallengesUpdated;
        }

        // 0x060025d8: the feature is unlocked once it leaves Locked, and new while the
        // interstitial is pending or just seen. A challenge is new while it is unplayed.
        private void OnChallengesUpdated(IReadOnlyList<ChallengeState> challengeStates)
        {
            ChallengeManager challengeManager = m_challengeManagerRef.Get();
            ChallengeFeatureUnlocked.Value = challengeManager.FeatureState != ChallengeManager.ChallengeFeatureState.Locked;
            ChallengeFeatureIsNew.Value = challengeManager.FeatureState == ChallengeManager.ChallengeFeatureState.UnlockedShowInterstitial
                || challengeManager.FeatureState == ChallengeManager.ChallengeFeatureState.UnlockedSeenInterstitial;
            int newChallengeCount = 0;
            foreach (ChallengeState challengeState in challengeStates)
            {
                if (!challengeState.Played)
                    newChallengeCount++;
            }
            NewChallengeCount.Value = newChallengeCount;
            if (m_alertBadge == null)
                return;
            m_alertBadge.SetAlert(ChallengeFeatureIsNew.Value || challengeManager.BonusXPUnacknowledged > 0, NewChallengeCount.Value);
        }

        // 0x060025d9: refresh the formatted time left whenever its whole-second value changes.
        private void Update()
        {
            int first = 0;
            int second = 0;
            if (!m_updateTimeRemaining || m_challengeManagerRef.IsNull())
                return;
            ChallengeManager challengeManager = m_challengeManagerRef.Get();
            if (!challengeManager.Ready)
                return;
            TimeSpan timeRemaining = challengeManager.GetChallengePeriodTimeRemaining();
            if (!string.IsNullOrWhiteSpace(TimeRemainingText.Value)
                && (int)timeRemaining.TotalSeconds == (int)m_lastTimeRemaining.TotalSeconds)
                return;
            m_lastTimeRemaining = timeRemaining;
            if (m_useShortFormats)
            {
                Strings format;
                if (timeRemaining.Days > 0)
                {
                    format = m_timeRemainingFormatDaysHours;
                    first = timeRemaining.Days;
                    second = timeRemaining.Hours;
                }
                else if (timeRemaining.Hours > 0)
                {
                    format = m_timeRemainingFormatHoursMinutes;
                    first = timeRemaining.Hours;
                    second = timeRemaining.Minutes;
                }
                else
                {
                    format = m_timeRemainingFormatMinutesSeconds;
                    first = timeRemaining.Minutes;
                    second = timeRemaining.Seconds;
                }
                TimeRemainingText.Value = string.Format(StringTable.GetString(format), first.ToString(), second.ToString());
            }
            else
            {
                string format = StringTable.GetString(m_timeRemainingFormat);
                m_stringParams[0] = timeRemaining.Days.ToString();
                m_stringParams[1] = timeRemaining.Hours.ToString();
                m_stringParams[2] = timeRemaining.Minutes.ToString();
                m_stringParams[3] = timeRemaining.Seconds.ToString();
                TimeRemainingText.Value = string.Format(format, m_stringParams);
            }
        }

        // 0x060025da: field initialisers above, then the MonoBehaviour constructor.
        public ChallengeManagerBinders() { }
    }
}
