using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SaveDataPlayerStat : SaveDataItem, ISerializationCallbackReceiver
    {
        public enum Type
        {
            TotalPlayTimeMS = 0,
            EnemiesDestroyed = 1,
            BoostTimeMS = 2,
            RingsCollected = 3,
            AirTimeMS = 4
        }

        [SerializeField]
        private Type m_playerStatType;

        [SerializeField]
        private long m_playerStatCounter;

        // Game.Runtime.dll 0x06002caa.
        public long PlayerStatCounter
        {
            get { return m_playerStatCounter; }
        }

        // Game.Runtime.dll 0x06002cab.
        public SaveDataPlayerStat.Type StatType
        {
            get { return m_playerStatType; }
        }

        // Game.Runtime.dll 0x06002cac.
        public SaveDataPlayerStat(Type statType)
        {
            m_playerStatType = statType;
        }

        // Game.Runtime.dll 0x06002caf.
        protected override void IterateChildren(Action<SaveDataItem> action)
        {
            // Original ARM64 body is a single RET: this record has no children.
        }

        // Game.Runtime.dll 0x06002cb0.
        public void OnBeforeSerialize()
        {
            // Original ARM64 body is a single RET.
        }

        // Game.Runtime.dll 0x06002cb1.
        public void OnAfterDeserialize()
        {
            // Original ARM64 body is a single RET.
        }

    }
}
