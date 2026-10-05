using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SaveDataCollectable : SaveDataItem
    {
        [SerializeField]
        private string m_guid;

        [SerializeField]
        private int m_spent;

        // Game.Runtime.dll 0x06002bb9.
        public string GUID
        {
            get { return m_guid; }
        }

        // Game.Runtime.dll 0x06002bba.
        public int Spent
        {
            get { return m_spent; }
            // Game.Runtime.dll 0x06002bbb.
            set
            {
                m_spent = value;
                MarkDirty();
            }
        }

        // Game.Runtime.dll 0x06002bbe.
        public SaveDataCollectable()
        {
        }

        // Game.Runtime.dll 0x06002bbd.
        protected override void IterateChildren(Action<SaveDataItem> action)
        {
            // Original ARM64 body is a single RET: this record has no children.
        }

    }
}
