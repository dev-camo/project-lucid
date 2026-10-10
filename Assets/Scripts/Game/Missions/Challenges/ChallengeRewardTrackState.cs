using System;
using System.Collections.Generic;

namespace HardlightProject
{
    [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute((Unity.IL2CPP.CompilerServices.Option)1, false)]
    [Unity.IL2CPP.CompilerServices.Il2CppSetOptionAttribute((Unity.IL2CPP.CompilerServices.Option)2, false)]
    // Original Game.Runtime 020006f4: whole sealed owner, six fields and 22 methods.
    public sealed class ChallengeRewardTrackState
    {
        // Original 060025db and readonly compiler field 0400192a at native offset0x10.
        public RewardTrackDefinition Definition { get; }

        // Original 060025dc..060025dd; compiler field 0400192b at0x18.
        public ChallengeReward NextReward { get; private set; }

        // Original 060025de..060025df; compiler field 0400192c at0x20.
        // Initial -1 is written before the Object base constructor in both architectures.
        public int HighestAchievedRewardIndex { get; private set; } = -1;

        // Original 060025e0 reads the live saved track through its genuine getter.
        public int SpentXP => m_saveData.SpentXP;

        // Original 060025e1..060025e2; compiler field 0400192d at0x24.
        public SaveDataChallengeRewardTrack.FeatureUnlockState UnlockState { get; private set; }

        // Original 060025e3 returns the live save reference, without a null fallback.
        public SaveDataChallengeRewardTrack SaveData => m_saveData;

        // Original 0400192e at0x28 and 0400192f at0x30, after the four compiler fields.
        private SaveDataChallengeRewardTrack m_saveData;
        private int m_nextRewardIndex = -1;

        // Original 060025e4: the index initializers precede Object's constructor, then
        // the original definition reference is assigned. No saved track is fabricated.
        public ChallengeRewardTrackState(RewardTrackDefinition definition)
        {
            Definition = definition;
        }

        // Original 060025e5: save store, unlock read/store, then highest-index reset.
        // Neither NextReward nor m_nextRewardIndex is cleared when a save is opened.
        public void OnSaveGameOpen(SaveDataChallengeRewardTrack saveData)
        {
            m_saveData = saveData;
            UnlockState = m_saveData.UnlockState;
            HighestAchievedRewardIndex = -1;
        }

        // Original 060025e6: capture the live list once; Count and saved XP remain live
        // across callback reentry. Highest is not reset first, and is stored before Give.
        // Callbacks are required here; no added null guards alter the original faults.
        public void RefreshRewards(Func<ChallengeReward, int, bool> checkIfRewardOwned, Action<ChallengeReward> giveReward)
        {
            NextReward = null;
            m_nextRewardIndex = -1;
            List<ChallengeReward> rewards = Definition.Rewards;
            for (int i = 0; i < rewards.Count; i++)
            {
                ChallengeReward reward = rewards[i];
                if (checkIfRewardOwned(reward, SpentXP))
                {
                    HighestAchievedRewardIndex = i;
                    continue;
                }
                if (reward.XPThreshold <= SpentXP)
                {
                    HighestAchievedRewardIndex = i;
                    giveReward(reward);
                    continue;
                }
                NextReward = reward;
                m_nextRewardIndex = i;
                break;
            }
            UnlockState = m_saveData.UnlockState;
        }

        // Original 060025e7 continues past unaffordable entries and selects the first
        // affordable saved reward that is missing or uncollected. A successful output
        // store precedes enumerator disposal; a failed output clear follows disposal.
        public bool TryGetNextCollectableReward(out ChallengeReward collectableReward)
        {
            foreach (ChallengeReward reward in Definition.Rewards)
            {
                if (reward.XPThreshold > SpentXP)
                    continue;
                SaveDataChallengeReward savedReward = m_saveData.GetRewardDataByTrackGUIDUnsafe(reward.GetGUID());
                if (savedReward != null && savedReward.Collected)
                    continue;
                collectableReward = reward;
                return true;
            }
            collectableReward = null;
            return false;
        }

        // Original 060025e8 clears the output before IndexOf; the list is read again
        // for a positive index. The real List equality behavior is retained.
        public bool TryGetPreviousReward(ChallengeReward nextReward, out ChallengeReward previousReward)
        {
            previousReward = null;
            int index = Definition.Rewards.IndexOf(nextReward);
            if (index > 0)
                previousReward = Definition.Rewards[index - 1];
            return index > 0;
        }

        // Original 060025e9 uses ScriptableObjectWithGuid's genuine inequality, then
        // the unguarded last-list lookup. No empty/null-list fallback is introduced.
        public ChallengeReward GetNextOrLastReward()
        {
            if (NextReward != null)
                return NextReward;
            return GetLastReward();
        }

        // Original 060025ea avoids reading Definition when the highest index is negative.
        public ChallengeReward GetHighestRewardAchieved()
        {
            if (HighestAchievedRewardIndex < 0)
                return null;
            return Definition.Rewards[HighestAchievedRewardIndex];
        }

        // Original 060025eb retains Count-1/indexer ordering and empty-list fault.
        public ChallengeReward GetLastReward()
        {
            List<ChallengeReward> rewards = Definition.Rewards;
            return rewards[rewards.Count - 1];
        }

        // Original 060025ec requires a strictly positive amount as well as affordability.
        public bool CanSpendXP(int availableXP)
        {
            int xp = XPToNextReward();
            return xp > 0 && xp <= availableXP;
        }

        // Original 060025ed forwards even a zero result; it does not repeat CanSpendXP.
        public void SpendXP()
        {
            SpendXP(XPToNextReward());
        }

        // Original 060025ee initializes the local output, then forwards the genuine query.
        public bool HasUncollectedReward()
        {
            ChallengeReward reward = null;
            return TryGetNextCollectableReward(out reward);
        }

        // Original 060025ef captures saved XP before obtaining the enumerator and returns
        // the first strictly positive unchecked Int32 difference; no sort/minimum pass.
        public int XPToNextReward()
        {
            int spentXP = SpentXP;
            foreach (ChallengeReward reward in Definition.Rewards)
            {
                int difference = unchecked(reward.XPThreshold - spentXP);
                if (difference > 0)
                    return difference;
            }
            return 0;
        }

        // Original 060025f0: capture analytics sink, stringify current cached index,
        // then send telemetry before reading/modifying saved XP. The saved object used
        // for the getter/setter is captured after telemetry; RequestSave reads it again.
        public void SpendXP(int xp)
        {
            ChallengeManager.XPSink sink = Definition.XPSinkTypeForAnalytics;
            string itemId = m_nextRewardIndex.ToString();
            Hardlight.Analytics.AnalyticsEventCollector.XPSpentEvent(xp, sink, itemId);
            SaveDataChallengeRewardTrack saveData = m_saveData;
            saveData.SpentXP = unchecked(saveData.SpentXP + xp);
            m_saveData.RequestSave();
        }
    }
}
