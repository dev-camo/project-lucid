using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game020006f5: all eighteen owner methods and eleven fields.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class ChallengeState
    {
        //060025f1: both original branches test BestScore for a NaN time.
        //The strict > comparison preserves that observed predicate; whole-class
        //source/native equivalence remains unproven.
        public bool Played => BestTimeSeconds > 0f || BestScore > 0;
        public float BestTimeSeconds { get; private set; }
        public int BestScore { get; private set; }
        public int ChallengeIndex { get; private set; }
        public int ChallengeSetIndex { get; private set; }
        public int BonusXPAwarded { get; set; }
        public int SRankXPAwarded { get; set; }
        public readonly ChallengeDefinition ChallengeDefinition;
        public readonly ChallengeDefinition.ChallengeSeededData ChallengeSeededData;
        public Action<ChallengeState> OnUpdateScore;
        public Action<ChallengeState, bool, int> OnAwardXP;
        private readonly SaveDataChallengesChallenge m_challengeSaveData;

        public int Attempts => m_challengeSaveData != null ? m_challengeSaveData.Attempts : 0;

        //060025ff: base construction precedes all assignments. Seeded-data
        //generation precedes indices/save lookup; the displayed index is one-based,
        //while the genuine save lookup receives the original zero-based index.
        public ChallengeState(int indexWithinCycle, ChallengeDefinition challengeDefinition,
            int seed, SaveDataChallenges saveDataChallenges, int challengeCycleIndex)
        {
            ChallengeDefinition = challengeDefinition;
            ChallengeSeededData = challengeDefinition.GetSeededData(seed);
            ChallengeIndex = unchecked(indexWithinCycle + 1);
            ChallengeSetIndex = challengeCycleIndex;
            m_challengeSaveData = saveDataChallenges != null
                ? saveDataChallenges.GetOrCreateChallengeData(indexWithinCycle,
                    challengeDefinition.GetGUID(), ChallengeSeededData)
                : null;
            BestTimeSeconds = m_challengeSaveData != null ? m_challengeSaveData.TimeSeconds : 0f;
            BestScore = m_challengeSaveData != null ? m_challengeSaveData.Score : 0;
        }

        //06002600: rank crossing is tested against the previous score even when
        //forceSet is requested. Save replacement and RequestSave precede both
        //required delegates; XP callback runs before score callback, without null
        //guards. The first-play bool is computed after the save request returns.
        public void UpdateScoreInfo(int score, float timeSeconds, int ringXpEarned, bool forceSet = false)
        {
            bool wasPlayed = Played;
            ApplySRankBonusIfValid(score);
            if (forceSet)
            {
                BestScore = score;
                BestTimeSeconds = timeSeconds;
            }
            else
            {
                BestScore = Mathf.Max(BestScore, score);
                BestTimeSeconds = BestTimeSeconds <= 0f
                    ? timeSeconds : Mathf.Min(BestTimeSeconds, timeSeconds);
            }
            m_challengeSaveData?.UpdateScoreAndTime(BestScore, BestTimeSeconds);
            m_challengeSaveData?.RequestSave();
            OnAwardXP(this, !wasPlayed && Played, ringXpEarned);
            OnUpdateScore(this);
        }

        //06002601: original Unity null comparison on the real rank definition;
        //SRank bonus is assigned only when this score first crosses its threshold.
        //It is not cleared by a later lower forced score or by an absent definition.
        private void ApplySRankBonusIfValid(int score)
        {
            ChallengeRankDefinition rankDefinition = ChallengeDefinition.RankDefinition;
            if (rankDefinition == null) return;
            ChallengeRank bestRank = rankDefinition.GetBestRank();
            if (BestScore < bestRank.Score && score >= bestRank.Score)
                SRankXPAwarded = rankDefinition.SRankBonus;
        }

        //06002602: both original calls retain independently guarded field reads.
        public void MarkNewAttempt()
        {
            m_challengeSaveData?.MarkNewAttempt();
            m_challengeSaveData?.RequestSave();
        }
    }
}
