using System;
using System.Diagnostics;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SaveDataLevelPersistentObject : SaveDataItem, ISerializationCallbackReceiver
    {
        [SerializeField]
        private PersistentObjectIdentifierType m_id;

        [SerializeField]
        private ValueType m_valueType;

        [SerializeField]
        private bool m_boolValue;

        // Game.Runtime.dll 0x06002c7c.
        public PersistentObjectIdentifierType ID
        {
            get { return m_id; }
        }

        // Game.Runtime.dll 0x06002c7d.
        public SaveDataLevelPersistentObject(PersistentObjectIdentifierType id, ValueType valueType)
        {
            m_id = id;
            m_valueType = valueType;
        }

        // Game.Runtime.dll 0x06002c82.
        protected override void IterateChildren(Action<SaveDataItem> action)
        {
            // Original ARM64 body is a single RET: this record has no children.
        }

        // Game.Runtime.dll 0x06002c83.
        public void OnBeforeSerialize()
        {
            // Original ARM64 body is a single RET.
        }

        // Game.Runtime.dll 0x06002c84.
        public void OnAfterDeserialize()
        {
            // Original ARM64 body is a single RET.
        }

    }
}
