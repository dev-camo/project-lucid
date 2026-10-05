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

        // Game.Runtime.dll 0x06002b5f.
        protected override void IterateChildren(Action<SaveDataItem> action)
        {
            foreach (var child in m_rewards) action(child);
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
            ListToDictionary(m_rewards, m_rewardsByTrackGUID, entry => entry.TrackGUID);
            ListToDictionary(m_rewards, m_rewardsByRewardGUID, entry => entry.RewardGUID);
        }

    }
}
