using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SaveDataChallenges : SaveDataItem, ISerializationCallbackReceiver
    {
        [SerializeField]
        private int m_lastSeenPeriodIndex = -1;

        [SerializeField]
        private List<SaveDataChallengesChallenge> m_challenges;

        [SerializeField]
        private int m_totalXpEarned;

        [SerializeField]
        private int m_totalXpEarnedMinor;

        [SerializeField]
        private int m_bonusXpUnacknowledged;

        [SerializeField]
        private int m_lastSubmittedScore;

        [SerializeField]
        private ChallengeManager.ChallengeFeatureState m_featureState;

        [SerializeField]
        private bool m_unlockSeen;

        [SerializeField]
        private ChallengesIsNewState m_isNewState;

        [SerializeField]
        private SaveDataChallengeRewardTrack.FeatureUnlockState m_rewardTracksFeatureUnlocked;

        [SerializeField]
        private int m_totalStoryMissionXpEarned;

        [SerializeField]
        private int m_challengesCompleted;

        [SerializeField]
        private bool m_hasPlayedChallenge;

        [SerializeField]
        private List<SaveDataChallengeRewardTrack> m_rewardTracks;

        private Dictionary<RewardTrackType, SaveDataChallengeRewardTrack> m_rewardTracksByType = new Dictionary<RewardTrackType, SaveDataChallengeRewardTrack>();

        private Dictionary<int, SaveDataChallengesChallenge> m_challengesIndexed = new Dictionary<int, SaveDataChallengesChallenge>();

        private Dictionary<string, SaveDataChallengesChallenge> m_challengesByGuid = new Dictionary<string, SaveDataChallengesChallenge>();

        // Game.Runtime.dll 0x06002b6d.
        public int LastSeenPeriodIndex
        {
            get { return m_lastSeenPeriodIndex; }
            // Game.Runtime.dll 0x06002b6e.
            set
            {
                if (m_lastSeenPeriodIndex == value) return;
                m_lastSeenPeriodIndex = value;
                MarkDirty();
            }
        }

        // 0x06002b6f/2b70: the split challenge-XP component excludes story XP.
        public int TotalXPEarned
        {
            get { return ChallengeManager.GetXPFromSplitValues(m_totalXpEarned, m_totalXpEarnedMinor) + m_totalStoryMissionXpEarned; }
            set
            {
                ChallengeManager.SetSplitXPValues(value - m_totalStoryMissionXpEarned, out m_totalXpEarned, out m_totalXpEarnedMinor);
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002b71.
        public int TotalStoryMissionXPEarned
        {
            get { return m_totalStoryMissionXpEarned; }
            // Game.Runtime.dll 0x06002b72.
            set
            {
                m_totalStoryMissionXpEarned = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002b73.
        public int BonusXPUnacknowledged
        {
            get { return m_bonusXpUnacknowledged; }
            // Game.Runtime.dll 0x06002b74.
            set
            {
                if (m_bonusXpUnacknowledged == value) return;
                m_bonusXpUnacknowledged = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002b75.
        public int LastSubmittedScore
        {
            get { return m_lastSubmittedScore; }
            // Game.Runtime.dll 0x06002b76.
            set
            {
                if (m_lastSubmittedScore == value) return;
                m_lastSubmittedScore = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002b77.
        public ChallengeManager.ChallengeFeatureState FeatureState
        {
            get { return m_featureState; }
            // Game.Runtime.dll 0x06002b78.
            set
            {
                if (m_featureState == value) return;
                m_featureState = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002b79.
        public bool UnlockSeen
        {
            get { return m_unlockSeen; }
            // Game.Runtime.dll 0x06002b7a.
            set
            {
                m_unlockSeen = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002b7b.
        public ChallengesIsNewState IsNewState
        {
            get { return m_isNewState; }
            // Game.Runtime.dll 0x06002b7c.
            set
            {
                m_isNewState = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002b7d.
        public SaveDataChallengeRewardTrack.FeatureUnlockState RewardTracksFeatureUnlocked
        {
            get { return m_rewardTracksFeatureUnlocked; }
            // Game.Runtime.dll 0x06002b7e.
            set
            {
                m_rewardTracksFeatureUnlocked = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002b7f.
        public int ChallengesCompleted
        {
            get { return m_challengesCompleted; }
            // Game.Runtime.dll 0x06002b80.
            set
            {
                m_challengesCompleted = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002b81.
        public bool HasPlayedChallenge
        {
            get { return m_hasPlayedChallenge; }
            // Game.Runtime.dll 0x06002b82.
            set
            {
                m_hasPlayedChallenge = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002b8e.
        public SaveDataChallenges()
        {
        }

        // Game.Runtime.dll 0x06002b83.
        public override void Initialise()
        {
            if (m_challenges == null) m_challenges = new List<SaveDataChallengesChallenge>();
            if (m_challengesIndexed == null) m_challengesIndexed = new Dictionary<int,SaveDataChallengesChallenge>();
            if (m_rewardTracks == null) m_rewardTracks = new List<SaveDataChallengeRewardTrack>();
            if (m_rewardTracksByType == null) m_rewardTracksByType = new Dictionary<RewardTrackType,SaveDataChallengeRewardTrack>();
            base.Initialise();
        }

        // Game.Runtime.dll 0x06002b84.
        protected override void IterateChildren(Action<SaveDataItem> action)
        {
            foreach (var child in m_challenges) action(child);
            foreach (var pair in m_rewardTracksByType) action(pair.Value);
        }

        // 0x06002b85: invalid indexed records are removed and replaced.
        // The original does not add the record to the GUID dictionary or list here.
        public SaveDataChallengesChallenge GetOrCreateChallengeData(int index, string challengeGuid, ChallengeDefinition.ChallengeSeededData challengeSeededData)
        {
            if (m_challengesIndexed.TryGetValue(index, out SaveDataChallengesChallenge challenge))
            {
                if (challenge.Validate(index, challengeGuid, challengeSeededData.DefinitionHash, challengeSeededData.SeedUsed)) return challenge;
                m_challengesIndexed.Remove(index);
            }
            challenge = new SaveDataChallengesChallenge(index, challengeGuid, challengeSeededData);
            m_challengesIndexed[index] = challenge;
            MarkDirty();
            return challenge;
        }

        // Game.Runtime.dll 0x06002b86.
        public void OnBeforeSerialize()
        {
            DictionaryToList(m_challengesIndexed, ref m_challenges);
            DictionaryToList(m_rewardTracksByType, ref m_rewardTracks);
        }

        // Game.Runtime.dll 0x06002b87.
        public void OnAfterDeserialize()
        {
            Initialise();
            ListToDictionary(m_challenges, m_challengesIndexed, challenge => challenge.Index);
            ListToDictionary(m_challenges, m_challengesByGuid, challenge => challenge.GUID);
            ListToDictionary(m_rewardTracks, m_rewardTracksByType, rewardTrack => rewardTrack.Type);
        }

        // 0x06002b88: the GUID dictionary is intentionally left untouched.
        public void ClearChallenges()
        {
            m_challenges.Clear();
            m_challengesIndexed.Clear();
            MarkDirty();
        }

        // 0x06002b89: period changes replace only the GUID dictionary. The
        // period index itself, indexed cache and serialized lists remain unchanged.
        public void ResolveNewData(SaveDataChallenges newSaveData)
        {
            if (m_lastSeenPeriodIndex == newSaveData.m_lastSeenPeriodIndex)
            {
                foreach (var pair in newSaveData.m_challengesByGuid)
                    if (m_challengesByGuid.TryGetValue(pair.Key, out SaveDataChallengesChallenge oldChallenge)) oldChallenge.ResolveNewData(pair.Value);
                    else m_challengesByGuid[pair.Key] = pair.Value;
            }
            else
                m_challengesByGuid = m_lastSeenPeriodIndex < newSaveData.m_lastSeenPeriodIndex
                    ? newSaveData.m_challengesByGuid : m_challengesByGuid;
            foreach (var pair in newSaveData.m_rewardTracksByType)
                if (m_rewardTracksByType.TryGetValue(pair.Key, out SaveDataChallengeRewardTrack oldTrack)) oldTrack.ResolveNewData(pair.Value);
                else m_rewardTracksByType[pair.Key] = pair.Value;

            // This setter runs before TotalXPEarned is read for the next merge.
            TotalStoryMissionXPEarned = Math.Max(TotalStoryMissionXPEarned, newSaveData.TotalStoryMissionXPEarned);
            TotalXPEarned = Math.Max(TotalXPEarned, newSaveData.TotalXPEarned);
            m_bonusXpUnacknowledged = Math.Max(m_bonusXpUnacknowledged, newSaveData.m_bonusXpUnacknowledged);
            m_lastSubmittedScore = m_lastSeenPeriodIndex < newSaveData.m_lastSeenPeriodIndex
                ? newSaveData.m_lastSubmittedScore : m_lastSubmittedScore;
            m_featureState = (ChallengeManager.ChallengeFeatureState)Math.Max((int)m_featureState, (int)newSaveData.m_featureState);
            m_unlockSeen = m_unlockSeen | newSaveData.m_unlockSeen;
            m_rewardTracksFeatureUnlocked = (SaveDataChallengeRewardTrack.FeatureUnlockState)Math.Max((int)m_rewardTracksFeatureUnlocked, (int)newSaveData.m_rewardTracksFeatureUnlocked);
            m_challengesCompleted = Math.Max(m_challengesCompleted, newSaveData.m_challengesCompleted);
            m_hasPlayedChallenge = m_hasPlayedChallenge | newSaveData.m_hasPlayedChallenge;
            if (newSaveData.m_isNewState == ChallengesIsNewState.IsNew)
            {
                if (m_isNewState == ChallengesIsNewState.None) m_isNewState = ChallengesIsNewState.IsNew;
            }
            else if (newSaveData.m_isNewState == ChallengesIsNewState.Seen)
                m_isNewState = ChallengesIsNewState.Seen;
        }

        // 0x06002b8a: an existing null entry is replaced and marks parent dirty.
        public SaveDataChallengeRewardTrack GetOrCreateRewardTrackData(RewardTrackType type)
        {
            if (!m_rewardTracksByType.TryGetValue(type, out SaveDataChallengeRewardTrack track) || track == null)
            {
                track = new SaveDataChallengeRewardTrack(type);
                m_rewardTracksByType[type] = track;
                MarkDirty();
            }
            return track;
        }

        // 0x06002b8b: queries iterate the serialized list, not the type cache.
        public SaveDataChallengeReward GetRewardDataByTrackGUIDUnsafe(string trackGUID)
        {
            foreach (SaveDataChallengeRewardTrack track in m_rewardTracks)
                if (track.TryGetRewardByTrackGUID(trackGUID, out SaveDataChallengeReward reward)) return reward;
            return null;
        }

        // 0x06002b8c.
        public SaveDataChallengeReward GetRewardDataByRewardGUIDUnsafe(string rewardGUID)
        {
            foreach (SaveDataChallengeRewardTrack track in m_rewardTracks)
                if (track.TryGetRewardByRewardGUID(rewardGUID, out SaveDataChallengeReward reward)) return reward;
            return null;
        }

        // 0x06002b8d: returns true even when an indexed reward value is null.
        public bool TryGetRewardDataByRewardGUID(string rewardGUID, out SaveDataChallengeReward reward)
        {
            foreach (SaveDataChallengeRewardTrack track in m_rewardTracks)
                if (track.TryGetRewardByRewardGUID(rewardGUID, out reward)) return true;
            reward = null;
            return false;
        }
    }
}
