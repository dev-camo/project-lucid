using System.Collections.Generic;
using Hardlight;

namespace HardlightProject
{
    // Original02000095: the whole readonly value owner, three APIs and seven fields.
    public readonly struct AnalyticsChallengePayload
    {
        public readonly MissionContextChallenge ChallengeContext;
        public readonly ChallengeState ChallengeState;
        public readonly int Score;
        public readonly int TotalScore;
        public readonly int TotalXp;
        public readonly float TimeElapsedSeconds;
        private readonly IReadOnlyDictionary<int, DreamPowerDefinition> m_dreamPowerLoadout;

        // Original06000509: the loadout reference precedes the four numeric zero stores.
        public AnalyticsChallengePayload(MissionContextChallenge challengeContext, ChallengeState challengeState,
            IReadOnlyDictionary<int, DreamPowerDefinition> dreamPowerLoadout)
        {
            ChallengeContext = challengeContext;
            ChallengeState = challengeState;
            m_dreamPowerLoadout = dreamPowerLoadout;
            Score = 0;
            TotalScore = 0;
            TotalXp = 0;
            TimeElapsedSeconds = 0f;
        }

        // Original0600050a: all supplied values are retained without validation or normalization.
        public AnalyticsChallengePayload(MissionContextChallenge challengeContext, ChallengeState challengeState,
            int score, int totalScore, int totalXp, float timeElapsedSeconds,
            IReadOnlyDictionary<int, DreamPowerDefinition> dreamPowerLoadout)
        {
            ChallengeContext = challengeContext;
            ChallengeState = challengeState;
            m_dreamPowerLoadout = dreamPowerLoadout;
            Score = score;
            TotalScore = totalScore;
            TotalXp = totalXp;
            TimeElapsedSeconds = timeElapsedSeconds;
        }

        // Original0600050b: dispose enumeration before default string Sort and original Join.
        // Slot indices are deconstructed but are not used to order the strings.
        public string GetDreamPowerSlotsAnalytics()
        {
            List<string> powers = new List<string>();
            foreach (var entry in m_dreamPowerLoadout)
            {
                entry.Deconstruct(out int slot, out DreamPowerDefinition definition);
                powers.Add(definition.GetStrippedDefinitionName());
            }
            powers.Sort();
            return powers.Join();
        }
    }
}
