using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SaveDataDreamPowerStoreItem : SaveDataItem
    {
        [SerializeField]
        private string m_guid;

        [SerializeField]
        private bool m_bought;

        // Game.Runtime.dll 0x06002be7.
        public string GUID
        {
            get { return m_guid; }
        }

        // Game.Runtime.dll 0x06002be8.
        public bool Bought
        {
            get { return m_bought; }
            // Game.Runtime.dll 0x06002be9.
            set
            {
                m_bought = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002bea.
        public SaveDataDreamPowerStoreItem(string guid)
        {
            m_guid = guid;
        }

        // Game.Runtime.dll 0x06002beb.
        public void ResolveNewData(SaveDataDreamPowerStoreItem newSaveData)
        {
            m_bought |= newSaveData.m_bought;
        }

        // Game.Runtime.dll 0x06002bec.
        protected override void IterateChildren(Action<SaveDataItem> action)
        {
            // Original ARM64 body is a single RET: this record has no children.
        }

    }
}
