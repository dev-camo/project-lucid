using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public sealed class SaveDataChallengeReward : SaveDataItem, ISerializationCallbackReceiver
    {
        [SerializeField]
        private string m_trackGUID;

        [SerializeField]
        private string m_rewardGUID;

        [SerializeField]
        private bool m_collected;

        // Game.Runtime.dll 0x06002b4d.
        public string TrackGUID
        {
            get { return m_trackGUID; }
        }

        // Game.Runtime.dll 0x06002b4e.
        public string RewardGUID
        {
            get { return m_rewardGUID; }
        }

        // Game.Runtime.dll 0x06002b4f.
        public bool Collected
        {
            get { return m_collected; }
            // Game.Runtime.dll 0x06002b50.
            set
            {
                m_collected = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002b51.
        public SaveDataChallengeReward(string trackGUID, string rewardGUID)
        {
            m_trackGUID = trackGUID;
            m_rewardGUID = rewardGUID;
        }

        // Game.Runtime.dll 0x06002b53.
        protected override void IterateChildren(Action<SaveDataItem> action)
        {
            // Original ARM64 body is a single RET: this record has no children.
        }

        // Game.Runtime.dll 0x06002b54.
        public void OnBeforeSerialize()
        {
            // Original ARM64 body is a single RET.
        }

        // Game.Runtime.dll 0x06002b55.
        public void OnAfterDeserialize()
        {
            // Original ARM64 body is a single RET.
        }

    }
}
