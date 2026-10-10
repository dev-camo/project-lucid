using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute((Unity.IL2CPP.CompilerServices.Option)1, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute((Unity.IL2CPP.CompilerServices.Option)2, false)]
    [UnityEngine.CreateAssetMenuAttribute(fileName = "ChallengeCycleDefinition", menuName = "HardlightProject/DefinitionData/Definitions/ChallengeCycleDefinition")]
    // Original Game.Runtime 0x0200056e; complete owned fields and methods.
    public sealed class ChallengeCycleDefinition : ScriptableObject
    {
        [UnityEngine.SerializeField]
        [UnityEngine.TooltipAttribute("In order to know what cycle we are in now, we must know what date to start counting from.")]
        [UnityEngine.HeaderAttribute("Challenge system configuration")]
        // Original 0x0400136a, native offset 0x18.
        private System.String m_cycleBeginsFromDateString;

        [UnityEngine.SerializeField]
        [UnityEngine.TooltipAttribute("Prevent leaderboard score submission within this many seconds of the begin or end of a leaderboard cycle.")]
        [UnityEngine.MinAttribute(0.0f)]
        // Original 0x0400136b, native offset 0x20.
        private System.Int32 m_leaderboardSafetyBufferSeconds;

        [UnityEngine.MinAttribute(24.0f)]
        [UnityEngine.SerializeField]
        [UnityEngine.TooltipAttribute("Presumed challenge cycle duration to use when GameCenter leaderboard info is unavailable.")]
        // Original 0x0400136c, native offset 0x24.
        private System.Single m_offlineCycleDurationHours;

        [UnityEngine.TooltipAttribute("In debug builds and in editor, we can't use GameCenter to access leaderboard duration info, so instead use this as the cycle duration.")]
        [UnityEngine.MinAttribute(0.1f)]
        [UnityEngine.SerializeField]
        // Original 0x0400136d, native offset 0x28.
        private System.Single m_debugCycleDurationHours;

        [UnityEngine.TooltipAttribute("Expected number of challenges per cycle period - used for validation only.")]
        [UnityEngine.SerializeField]
        [UnityEngine.MinAttribute(1.0f)]
        // Original 0x0400136e, native offset 0x2c.
        private System.Int32 m_expectedChallengesPerCyclePeriod;

        [UnityEngine.TooltipAttribute("UIWidgetProgression to display when feature first becomes unlocked.")]
        [UnityEngine.SerializeField]
        // Original 0x0400136f, native offset 0x30.
        private HardlightProject.UIWidgetProgression m_progressionWidget;

        [UnityEngine.HeaderAttribute("Challenge cycle")]
        [UnityEngine.SerializeField]
        // Original 0x04001370, native offset 0x38.
        private HardlightProject.ChallengeCyclePeriodDefinition[] m_challengeCyclePeriods;

        [UnityEngine.MinAttribute(1.0f)]
        [UnityEngine.SerializeField]
        [UnityEngine.TooltipAttribute("The bonus XP awarded for completing all missions in any period")]
        [UnityEngine.HeaderAttribute("Rewards")]
        // Original 0x04001371, native offset 0x40.
        private System.Int32 m_periodBonusXP;

        // Original 0x04001372, native offset 0x48.
        private System.DateTime m_cycleBeginsFrom;

        // Original 06001d4d..06001d54 retain live field aliases and seconds construction.
        public DateTime CycleBeginsFrom => m_cycleBeginsFrom;
        public TimeSpan LeaderboardSafetyBuffer => new TimeSpan(0, 0, m_leaderboardSafetyBufferSeconds);
        public TimeSpan OfflineCycleDuration => new TimeSpan(0, 0, unchecked((int)(m_offlineCycleDurationHours * 3600f)));
        public TimeSpan DebugCycleDuration => new TimeSpan(0, 0, unchecked((int)(m_debugCycleDurationHours * 3600f)));
        public IReadOnlyList<ChallengeCyclePeriodDefinition> ChallengeCyclePeriods => m_challengeCyclePeriods;
        public int ExpectedChallengesPerCyclePeriod => m_expectedChallengesPerCyclePeriod;
        public int PeriodBonusXP => m_periodBonusXP;
        public UIWidgetProgression ProgressionWidget => m_progressionWidget;

        // Original 06001d55 retains allocation, Clear and full Current/MoveNext/Dispose traversal.
        // No challenge is added to the HashSet in either shipping body; authored validation
        // calls stripped from this build cannot be recovered by inventing replacement checks.
        private void OnValidate()
        {
            RegenerateCycleBeginsFromDate();
            var challenges = new HashSet<ChallengeDefinition>();
            for (int i = 0; i < m_challengeCyclePeriods.Length; i++)
            {
                ChallengeCyclePeriodDefinition period = m_challengeCyclePeriods[i];
                if (period == null)
                    continue;
                IReadOnlyList<ChallengeDefinition> definitions = period.ChallengeDefinitions;
                challenges.Clear();
                foreach (ChallengeDefinition definition in definitions)
                {
                }
            }
        }

        // Original 06001d56 uses invariant culture with styles80, then explicit zero on failure.
        private void RegenerateCycleBeginsFromDate()
        {
            if (!DateTime.TryParse(m_cycleBeginsFromDateString, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                    out m_cycleBeginsFrom))
                m_cycleBeginsFrom = default(DateTime);
        }

        // Original 06001d57.
        private void Awake()
        {
            RegenerateCycleBeginsFromDate();
        }

        // Original 06001d58 tests negative before any array read; no null/empty guard exists.
        public ChallengeCyclePeriodDefinition GetCyclePeriodDefinition(int cycleIndex)
        {
            if (cycleIndex < 0)
                return null;
            return m_challengeCyclePeriods[cycleIndex % m_challengeCyclePeriods.Length];
        }

        // Original 06001d59 only calls the real engine base; fields retain default values.
        public ChallengeCycleDefinition()
        {
        }
    }
}
