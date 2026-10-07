using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SaveDataCharacterSkin : SaveDataItem, ISerializationCallbackReceiver
    {
        [SerializeField]
        private string m_guid;

        [SerializeField]
        private bool m_unlocked;

        [SerializeField]
        private bool m_unlockSeen;

        // Game.Runtime.dll 0x06002baf.
        public string GUID
        {
            get { return m_guid; }
        }

        // Game.Runtime.dll 0x06002bb0.
        public bool Unlocked
        {
            get { return m_unlocked; }
            // Game.Runtime.dll 0x06002bb1.
            set
            {
                m_unlocked = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002bb2.
        public bool UnlockSeen
        {
            get { return m_unlockSeen; }
            // Game.Runtime.dll 0x06002bb3.
            set
            {
                m_unlockSeen = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002bb4.
        public SaveDataCharacterSkin(string skinGUID)
        {
            m_guid = skinGUID;
        }

        // Game.Runtime.dll 0x06002bb5.
        public void ResolveNewData(SaveDataCharacterSkin newSaveData)
        {
            m_unlocked |= newSaveData.m_unlocked;
            m_unlockSeen |= newSaveData.m_unlockSeen;
        }

        // Game.Runtime.dll 0x06002bb6.
        protected override void IterateChildren(Action<SaveDataItem> action)
        {
            // Original ARM64 body is a single RET: this record has no children.
        }

        // Game.Runtime.dll 0x06002bb7.
        public void OnBeforeSerialize()
        {
            // Original ARM64 body is a single RET.
        }

        // Game.Runtime.dll 0x06002bb8.
        public void OnAfterDeserialize()
        {
            // Original ARM64 body is a single RET.
        }

    }
}
