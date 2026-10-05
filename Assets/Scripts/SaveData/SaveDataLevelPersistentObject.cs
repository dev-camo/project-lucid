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

        // Game.Runtime.dll 0x06002c7e; ARM640x5c27d8: no value-type check.
        public bool GetBooleanValue() { return m_boolValue; }

        // Game.Runtime.dll 0x06002c7f; ARM640x5c198c.
        public void Set(bool value)
        {
            if (m_boolValue == value) return;
            m_boolValue = value;
            MarkDirty();
        }

        // Game.Runtime.dll 0x06002c80; ARM640x5c11fc: type of incoming record is
        // not checked; OR changes the field without marking dirty.
        public void ResolveNewData(SaveDataLevelPersistentObject newSaveDataLevelPersistentObject)
        {
            if (m_valueType == ValueType.Boolean) m_boolValue |= newSaveDataLevelPersistentObject.m_boolValue;
        }

        // Game.Runtime.dll 0x06002c81; ARM640x5c27e0: original single RET.
        [Conditional("BUILD_DEVELOPMENT")]
        public void AssertType(ValueType expectedValueType) { }

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

        // Game.Runtime.dll 0x06002c85; Boolean shared ARM640x9a0d38. All other
        // types return literal zero, which is not a declared ValueType member.
        public static ValueType GetValueType<T>()
        {
            return typeof(T) == typeof(bool) ? ValueType.Boolean : (ValueType)0;
        }
    }
}
