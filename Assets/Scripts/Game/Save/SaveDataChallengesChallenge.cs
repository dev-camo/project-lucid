using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SaveDataChallengesChallenge : SaveDataItem
    {
        [SerializeField]
        private int m_index;

        [SerializeField]
        private string m_challengeGuid;

        [SerializeField]
        private int m_challengeHash;

        [SerializeField]
        private int m_seed;

        [SerializeField]
        private int m_score;

        [SerializeField]
        private float m_timeSeconds;

        [SerializeField]
        private int m_attempts;

        // Game.Runtime.dll 0x06002b94.
        public int Index
        {
            get { return m_index; }
        }

        // Game.Runtime.dll 0x06002b95.
        public string GUID
        {
            get { return m_challengeGuid; }
        }

        // Game.Runtime.dll 0x06002b96.
        public int Score
        {
            get { return m_score; }
        }

        // Game.Runtime.dll 0x06002b97.
        public float TimeSeconds
        {
            get { return m_timeSeconds; }
        }

        // Game.Runtime.dll 0x06002b98.
        public int Attempts
        {
            get { return m_attempts; }
        }

        // Game.Runtime.dll 0x06002b99.
        public SaveDataChallengesChallenge(int index, string challengeGuid, ChallengeDefinition.ChallengeSeededData challengeSeededData, int attempts = 0)
        {
            m_index = index;
            m_challengeGuid = challengeGuid;
            m_challengeHash = challengeSeededData.DefinitionHash;
            m_seed = challengeSeededData.SeedUsed;
            m_attempts = attempts;
        }

        // 0x06002b9a: direct replacement, not best-score or best-time selection.
        public void UpdateScoreAndTime(int score, float timeSeconds)
        {
            m_score = score;
            m_timeSeconds = timeSeconds;
            MarkDirty();
        }

        // 0x06002b9b: string comparison precedes the integer comparisons.
        public bool Validate(int index, string challengeGuid, int hash, int seed)
        {
            bool sameGuid = m_challengeGuid == challengeGuid;
            return m_index == index && sameGuid && m_challengeHash == hash && m_seed == seed;
        }

        // Game.Runtime.dll 0x06002b9c.
        protected override void IterateChildren(Action<SaveDataItem> action)
        {
        }

        // 0x06002b9d: mismatched identities do nothing; attempts are not merged.
        // The incoming-first time comparison retains local on equality or NaN,
        // matching ARM FCSEL MI and x86 MINSS with local as its second operand.
        public void ResolveNewData(SaveDataChallengesChallenge newSaveDataChallenge)
        {
            if (!Validate(newSaveDataChallenge.m_index, newSaveDataChallenge.m_challengeGuid,
                newSaveDataChallenge.m_challengeHash, newSaveDataChallenge.m_seed)) return;
            m_score = Math.Max(m_score, newSaveDataChallenge.m_score);
            m_timeSeconds = newSaveDataChallenge.m_timeSeconds < m_timeSeconds
                ? newSaveDataChallenge.m_timeSeconds : m_timeSeconds;
        }

        // 0x06002b9e.
        public void MarkNewAttempt()
        {
            m_attempts++;
            MarkDirty();
        }
    }
}
