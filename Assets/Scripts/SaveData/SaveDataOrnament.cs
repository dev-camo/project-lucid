using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SaveDataOrnament : SaveDataItem, ISerializationCallbackReceiver
    {
        [SerializeField]
        private string m_guid;

        [SerializeField]
        private bool m_unlocked;

        [SerializeField]
        private bool m_unlockSeen;

        // Game.Runtime.dll 0x06002ca0.
        public string GUID
        {
            get { return m_guid; }
        }

        // Game.Runtime.dll 0x06002ca1.
        public bool Unlocked
        {
            get { return m_unlocked; }
            // Game.Runtime.dll 0x06002ca2.
            set
            {
                m_unlocked = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002ca3.
        public bool UnlockSeen
        {
            get { return m_unlockSeen; }
            // Game.Runtime.dll 0x06002ca4.
            set
            {
                m_unlockSeen = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002ca5.
        public SaveDataOrnament(string ornamentGUID)
        {
            m_guid = ornamentGUID;
        }

        // Game.Runtime.dll 0x06002ca6.
        public void ResolveNewData(SaveDataOrnament newSaveData)
        {
            m_unlocked |= newSaveData.m_unlocked;
            m_unlockSeen |= newSaveData.m_unlockSeen;
        }

        // Game.Runtime.dll 0x06002ca7.
        protected override void IterateChildren(Action<SaveDataItem> action)
        {
            // Original ARM64 body is a single RET: this record has no children.
        }

        // Game.Runtime.dll 0x06002ca8.
        public void OnBeforeSerialize()
        {
            // Original ARM64 body is a single RET.
        }

        // Game.Runtime.dll 0x06002ca9.
        public void OnAfterDeserialize()
        {
            // Original ARM64 body is a single RET.
        }

    }
}
