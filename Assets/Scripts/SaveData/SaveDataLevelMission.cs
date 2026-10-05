using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SaveDataLevelMission : SaveDataItem, ISerializationCallbackReceiver
    {
        [SerializeField]
        private string m_guid;

        [SerializeField]
        private bool m_complete;

        [SerializeField]
        private long m_completedAt;

        [SerializeField]
        private int m_progress;

        [SerializeField]
        private List<int> m_completedObjectiveIndices;

        [SerializeField]
        private bool m_levelSelectUnlockSeen;

        [SerializeField]
        private bool m_timeTrialUnlockSeen;

        [SerializeField]
        private MissionIsNewState m_isNewState;

        [SerializeField]
        private int m_attempts;

        [SerializeField]
        private float m_bestTimeSeconds;

        [SerializeField]
        private int m_xpEarned;

        private DateTime m_completedAtTime;

        // Game.Runtime.dll 0x06002c5d.
        public string GUID
        {
            get { return m_guid; }
        }

        // Game.Runtime.dll 0x06002c5e.
        public IReadOnlyList<int> CompletedObjectiveIndices
        {
            get { return m_completedObjectiveIndices; }
        }

        // Game.Runtime.dll 0x06002c60.
        public bool Complete
        {
            get { return m_complete; }
            // Game.Runtime.dll 0x06002c61.
            set
            {
                m_complete = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002c62.
        public DateTime CompletedAt
        {
            get { return m_completedAtTime; }
            // Game.Runtime.dll 0x06002c63.
            set
            {
                m_completedAtTime = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002c64.
        public int Progress
        {
            get { return m_progress; }
            // Game.Runtime.dll 0x06002c65.
            set
            {
                m_progress = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002c66.
        public bool LevelSelectUnlockSeen
        {
            get { return m_levelSelectUnlockSeen; }
            // Game.Runtime.dll 0x06002c67.
            set
            {
                m_levelSelectUnlockSeen = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002c68.
        public bool TimeTrialUnlockSeen
        {
            get { return m_timeTrialUnlockSeen; }
            // Game.Runtime.dll 0x06002c69.
            set
            {
                m_timeTrialUnlockSeen = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002c6a.
        public MissionIsNewState IsNewState
        {
            get { return m_isNewState; }
            // Game.Runtime.dll 0x06002c6b.
            set
            {
                m_isNewState = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002c6c.
        public int Attempts
        {
            get { return m_attempts; }
            // Game.Runtime.dll 0x06002c6d.
            set
            {
                m_attempts = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002c6e.
        public float BestTimeSeconds
        {
            get { return m_bestTimeSeconds; }
            // Game.Runtime.dll 0x06002c6f.
            set
            {
                m_bestTimeSeconds = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002c70.
        public int XPEarned
        {
            get { return m_xpEarned; }
            // Game.Runtime.dll 0x06002c71.
            set
            {
                m_xpEarned = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002c5f.
        public SaveDataLevelMission(string guid)
        {
            m_guid = guid;
            m_isNewState = MissionIsNewState.None;
        }

        // Game.Runtime.dll 0x06002c77.
        protected override void IterateChildren(Action<SaveDataItem> action)
        {
            // Original ARM64 body is a single RET: this record has no children.
        }

        // Game.Runtime.dll 0x06002c79.
        public void OnBeforeSerialize()
        {
            m_completedAt = Hardlight.TimeUtils.ToUnixTimeMs(m_completedAtTime);
            if (m_completedObjectiveIndices != null && m_completedObjectiveIndices.Count == 0) m_completedObjectiveIndices = null;
        }

        // Game.Runtime.dll 0x06002c7a.
        public void OnAfterDeserialize()
        {
            m_completedAtTime = Hardlight.TimeUtils.FromUnixTime(m_completedAt);
        }

    }
}
