using System;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute((Unity.IL2CPP.CompilerServices.Option)1, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute((Unity.IL2CPP.CompilerServices.Option)2, false)]
    // Original Game.Runtime 020006e7,18 own methods including10 real abstract contracts;
    // natural 020006e8/0600255b..0600255c is retained through the original callback flow.
    public abstract class ChallengeLeaderboardIntegration
    {
        // Original 040018d0,public Int32 constant.
        public const int CycleIndexNotStartedYet = -1;

        // Original 06002549..0600254a and compiler field040018d1 at0x10.
        public bool Ready { get; private set; }

        // Original 040018d2 at0x18,protected authored reference.
        protected ChallengeCycleDefinition m_challengeCycleDefinition;

        // Original 0600254b..06002554,virtual slots4..13 in declaration order.
        protected abstract void DoInitialise(Action callback);
        protected abstract void DoDeinitialise();
        public abstract DateTime GetCurrentPeriodStart();
        public abstract DateTime GetCurrentPeriodEnd();
        public abstract DateTime GetTime();
        protected abstract void DoSubmitScore(int score);
        public abstract void ShowLeaderboard();
        public abstract bool CanShowLeaderboard();
        public abstract void AdvanceCycle();
        public abstract void RegressCycle();

        // Original 06002555: store the authored definition before invoking the virtual
        // initializer; the captured callback sets Ready before optional notification.
        // Original natural closure captures this then readyCallback; emitted ordinal,
        // layout/allocation order remain held until genuine whole-Game emission.
        public void Initialise(ChallengeCycleDefinition challengeCycleDefinition, Action readyCallback = null)
        {
            m_challengeCycleDefinition = challengeCycleDefinition;
            DoInitialise(() =>
            {
                Ready = true;
                readyCallback?.Invoke();
            });
        }

        // Original 06002556: Ready is cleared before virtual shutdown; definition is
        // cleared only after successful return, preserving the original callback fault.
        public void Deinitialise()
        {
            Ready = false;
            DoDeinitialise();
            m_challengeCycleDefinition = null;
        }

        // Original 06002557: cycle checks precede Time/Start/End virtual queries.
        // The nearest absolute boundary distance is compared with the safety buffer;
        // no Ready check or new score/range guard exists in either shipping body.
        public bool SubmitScore(int score, int scoreCycleIndex)
        {
            if (scoreCycleIndex < 0 || GetCycleIndex() != scoreCycleIndex)
                return false;
            DateTime time = GetTime();
            DateTime start = GetCurrentPeriodStart();
            DateTime end = GetCurrentPeriodEnd();
            double closestBoundary = Math.Min(Math.Abs((time - start).TotalSeconds), Math.Abs((time - end).TotalSeconds));
            if (closestBoundary < m_challengeCycleDefinition.LeaderboardSafetyBuffer.TotalSeconds)
                return false;
            DoSubmitScore(score);
            return true;
        }

        // Original 06002558: retain each live definition read and virtual query order.
        // The ratio must compare >=0, including rejecting unordered NaN; conversion of
        // nonnegative out-of-range ratios retains explicit architecture parity holds.
        public int GetCycleIndex()
        {
            DateTime time = GetTime();
            if (time < m_challengeCycleDefinition.CycleBeginsFrom)
                return CycleIndexNotStartedYet;
            DateTime end = GetCurrentPeriodEnd();
            if (end < m_challengeCycleDefinition.CycleBeginsFrom)
                return CycleIndexNotStartedYet;
            DateTime start = GetCurrentPeriodStart();
            TimeSpan duration = end - start;
            TimeSpan elapsed = time - m_challengeCycleDefinition.CycleBeginsFrom;
            double ratio = elapsed / duration;
            if (ratio >= 0d)
                return unchecked((int)ratio);
            return CycleIndexNotStartedYet;
        }

        // Original 06002559,literal4526 and three boxed Int32 format arguments.
        // No guard changes native unguarded modulo with an empty period collection.
        public string GetCycleDebugLabel()
        {
            int cycleIndex = GetCycleIndex();
            int periods = m_challengeCycleDefinition.ChallengeCyclePeriods.Count;
            return string.Format("Cycle index {0} ({1}/{2})", cycleIndex, unchecked(cycleIndex % periods + 1), periods);
        }

        // Original 0600255a invokes only Object's constructor.
        protected ChallengeLeaderboardIntegration()
        {
        }
    }
}
