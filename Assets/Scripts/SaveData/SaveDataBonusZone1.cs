using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SaveDataBonusZone1 : SaveDataItem
    {
        [SerializeField]
        private bool m_unlockSeen;

        [SerializeField]
        private BonusZoneIsNewState m_isNewState;

        // Game.Runtime.dll 0x06002b46.
        public bool UnlockSeen
        {
            get { return m_unlockSeen; }
            // Game.Runtime.dll 0x06002b47.
            set
            {
                m_unlockSeen = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002b48.
        public BonusZoneIsNewState IsNewState
        {
            get { return m_isNewState; }
            // Game.Runtime.dll 0x06002b49.
            set
            {
                m_isNewState = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002b4a.
        public void ResolveNewData(SaveDataBonusZone1 newSaveData)
        {
            m_unlockSeen |= newSaveData.m_unlockSeen;
            if (newSaveData.m_isNewState == BonusZoneIsNewState.Seen)
                m_isNewState = BonusZoneIsNewState.Seen;
            else if (newSaveData.m_isNewState == BonusZoneIsNewState.IsNew && m_isNewState == BonusZoneIsNewState.None)
                m_isNewState = BonusZoneIsNewState.IsNew;
        }

        // Game.Runtime.dll 0x06002b4b.
        protected override void IterateChildren(Action<SaveDataItem> action)
        {
            // Original ARM64 body is a single RET: this record has no children.
        }

        // Game.Runtime.dll 0x06002b4c.
        public SaveDataBonusZone1()
        {
        }

    }
}
