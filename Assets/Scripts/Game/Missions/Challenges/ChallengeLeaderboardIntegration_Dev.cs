using System;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute((Unity.IL2CPP.CompilerServices.Option)2, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute((Unity.IL2CPP.CompilerServices.Option)1, false)]
    // Original Game.Runtime020006e9: complete sealed development implementation13.
    public sealed class ChallengeLeaderboardIntegration_Dev : ChallengeLeaderboardIntegration
    {
        // Original040018d5 at0x20,explicit TimeSpan.Zero initialization before base.
        private TimeSpan m_debugOffset = TimeSpan.Zero;
        // Original040018d6,private string constant preserved although debug body is RET.
        private const string m_debugMenuPath = "Challenges/Cycle Index";

        // Original0600255d directly invokes the required callback without a null guard.
        protected override void DoInitialise(Action callback)
        {
            callback();
        }

        // Original0600255e is immediate RET in both complete architecture ranges.
        private void SetupDebugButtons()
        {
        }

        // Original0600255f captures offset before querying the live configured duration.
        public override void RegressCycle()
        {
            m_debugOffset = m_debugOffset - m_challengeCycleDefinition.DebugCycleDuration;
        }

        // Original06002560 retains the corresponding addition and no range guard.
        public override void AdvanceCycle()
        {
            m_debugOffset = m_debugOffset + m_challengeCycleDefinition.DebugCycleDuration;
        }

        // Original06002561 is immediate RET.
        protected override void DoDeinitialise()
        {
        }

        // Original06002562 captures the epoch BEFORE querying debug cycle index.
        public override DateTime GetCurrentPeriodStart()
        {
            DateTime beginning = m_challengeCycleDefinition.CycleBeginsFrom;
            int index = GetDebugCycleIndex();
            TimeSpan duration = m_challengeCycleDefinition.DebugCycleDuration;
            return beginning + duration * (double)index;
        }

        // Original06002563 calls virtual start before reading the configured duration.
        public override DateTime GetCurrentPeriodEnd()
        {
            DateTime start = GetCurrentPeriodStart();
            return start + m_challengeCycleDefinition.DebugCycleDuration;
        }

        // Original06002564 casts the signed ratio directly, without floor or a negative
        // guard. Exceptional double→Int32 outcomes differ across CPUs and remain held.
        private int GetDebugCycleIndex()
        {
            DateTime time = GetTime();
            TimeSpan elapsed = time - m_challengeCycleDefinition.CycleBeginsFrom;
            TimeSpan duration = m_challengeCycleDefinition.DebugCycleDuration;
            return unchecked((int)(elapsed / duration));
        }

        // Original06002565 captures UtcNow before reading debug offset.
        public override DateTime GetTime()
        {
            return DateTime.UtcNow + m_debugOffset;
        }

        // Original06002566 and06002567 are genuine shipped RET bodies.
        protected override void DoSubmitScore(int score)
        {
        }

        public override void ShowLeaderboard()
        {
        }

        // Original06002568 returns false in both architectures.
        public override bool CanShowLeaderboard()
        {
            return false;
        }

        // Original06002569 initializes TimeSpan.Zero before the genuine base constructor.
        public ChallengeLeaderboardIntegration_Dev()
        {
        }
    }
}
