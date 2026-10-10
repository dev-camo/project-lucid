using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public sealed class SaveDataChallengeRewardTrack : SaveDataItem, ISerializationCallbackReceiver
    {
        public enum FeatureUnlockState
        {
            Locked = 0,
            Unlocked = 10,
            Seen = 20
        }

        [SerializeField]
        private RewardTrackType m_type;

        [SerializeField]
        private List<SaveDataChallengeReward> m_rewards = new List<SaveDataChallengeReward>();

        [SerializeField]
        private int m_spentXP;

        [SerializeField]
        private int m_spentXPMinor;

        [SerializeField]
        private FeatureUnlockState m_unlockState;

        private Dictionary<string, SaveDataChallengeReward> m_rewardsByTrackGUID = new Dictionary<string, SaveDataChallengeReward>();

        private Dictionary<string, SaveDataChallengeReward> m_rewardsByRewardGUID = new Dictionary<string, SaveDataChallengeReward>();

        // Game.Runtime.dll 0x06002b56.
        public RewardTrackType Type
        {
            get { return m_type; }
        }

        // Game.Runtime.dll 0x06002b57.
        public IReadOnlyList<SaveDataChallengeReward> Rewards
        {
            get { return m_rewards; }
        }

        // Game.Runtime.dll 0x06002b58.
        public SaveDataChallengeRewardTrack.FeatureUnlockState UnlockState
        {
            get { return m_unlockState; }
            // Game.Runtime.dll 0x06002b59.
            set
            {
                if ((int)m_unlockState >= (int)value) return;
                m_unlockState = value;
                MarkDirty();
            }
        }

        // Original 0x06002b5a/2b5b use the real ChallengeManager split-XP provider.
        public int SpentXP
        {
            get { return ChallengeManager.GetXPFromSplitValues(m_spentXP, m_spentXPMinor); }
            set
            {
                ChallengeManager.SetSplitXPValues(value, out m_spentXP, out m_spentXPMinor);
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002b5c.
        public SaveDataChallengeRewardTrack(RewardTrackType type)
        {
            m_type = type;
        }

        // Game.Runtime.dll 0x06002b5d.
        public override void Initialise()
        {
            if (m_rewards == null) m_rewards = new List<SaveDataChallengeReward>();
            if (m_rewardsByTrackGUID == null) m_rewardsByTrackGUID = new Dictionary<string,SaveDataChallengeReward>();
            if (m_rewardsByRewardGUID == null) m_rewardsByRewardGUID = new Dictionary<string,SaveDataChallengeReward>();
            base.Initialise();
        }

        // 0x06002b5e: each index merges separately. Missing rewards retain the
        // incoming object reference; the list is rebuilt only during serialization.
        public void ResolveNewData(SaveDataChallengeRewardTrack newSaveData)
        {
            foreach (var pair in newSaveData.m_rewardsByTrackGUID)
            {
                pair.Deconstruct(out string key, out SaveDataChallengeReward reward);
                if (m_rewardsByTrackGUID.TryGetValue(key, out SaveDataChallengeReward oldReward)) oldReward.ResolveNewData(reward);
                else m_rewardsByTrackGUID[key] = reward;
            }
            foreach (var pair in newSaveData.m_rewardsByRewardGUID)
            {
                pair.Deconstruct(out string key, out SaveDataChallengeReward reward);
                if (m_rewardsByRewardGUID.TryGetValue(key, out SaveDataChallengeReward oldReward)) oldReward.ResolveNewData(reward);
                else m_rewardsByRewardGUID[key] = reward;
            }
            SpentXP = Math.Max(SpentXP, newSaveData.SpentXP);
            m_unlockState = (FeatureUnlockState)Math.Max((int)m_unlockState, (int)newSaveData.m_unlockState);
        }

        // Game.Runtime.dll 0x06002b5f.
        protected override void IterateChildren(Action<SaveDataItem> action)
        {
            foreach (var child in m_rewards) action(child);
        }

        // 0x06002b60: an existing track record retains its original RewardGUID,
        // while the supplied reward GUID receives an additional index entry.
        public void CreateRewardData(string trackGUID, string rewardGUID)
        {
            SaveDataChallengeReward rewardData = m_rewards.Find(reward => reward.TrackGUID == trackGUID);
            if (rewardData == null)
            {
                rewardData = new SaveDataChallengeReward(trackGUID, rewardGUID);
                m_rewards.Add(rewardData);
            }
            m_rewardsByTrackGUID[trackGUID] = rewardData;
            m_rewardsByRewardGUID[rewardGUID] = rewardData;
            MarkDirty();
        }

        // 0x06002b61.
        public bool TryGetRewardByTrackGUID(string trackGUID, out SaveDataChallengeReward reward)
        {
            return m_rewardsByTrackGUID.TryGetValue(trackGUID, out reward);
        }

        // 0x06002b62.
        public bool TryGetRewardByRewardGUID(string rewardGUID, out SaveDataChallengeReward reward)
        {
            return m_rewardsByRewardGUID.TryGetValue(rewardGUID, out reward);
        }

        // 0x06002b63: the original dictionary indexer throws for absent keys.
        public SaveDataChallengeReward GetRewardDataByTrackGUIDUnsafe(string trackGUID)
        {
            return m_rewardsByTrackGUID[trackGUID];
        }

        // Game.Runtime.dll 0x06002b64.
        public void OnBeforeSerialize()
        {
            DictionaryToList(m_rewardsByTrackGUID, ref m_rewards);
        }

        // Game.Runtime.dll 0x06002b65.
        public void OnAfterDeserialize()
        {
            Initialise();
            ListToDictionary(m_rewards, m_rewardsByTrackGUID, reward => reward.TrackGUID);
            ListToDictionary(m_rewards, m_rewardsByRewardGUID, reward => reward.RewardGUID);
        }

        // 0x06002b66: unlike UnlockState, this can regress and always marks dirty.
        [Conditional("BUILD_DEVELOPMENT")]
        [Conditional("DEBUG_MENU")]
        public void DebugForceSetUnlockState(FeatureUnlockState unlockState)
        {
            m_unlockState = unlockState;
            MarkDirty();
        }
    }
}
