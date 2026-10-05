using System;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SerializableKeyValuePair<TKey, TValue>
    {
        [SerializeField] private TKey m_key;
        [SerializeField] private TValue m_value;

        // Original HLUnityCore.Runtime 0x06000ff0/ff1.
        public TKey Key => m_key;
        public TValue Value => m_value;
        // 0x06000ff2, arm64 0xbc9478: Object..ctor then ordered field stores.
        public SerializableKeyValuePair(TKey key, TValue value)
        { m_key = key; m_value = value; }
        // 0x06000ff3/ff4: ReferenceTypeConstraint only; isinst returns null
        // for mismatches, with no conversion, exception or value-type constraint.
        public TKeyCast KeyAs<TKeyCast>() where TKeyCast : class => m_key as TKeyCast;
        public TValueCast ValueAs<TValueCast>() where TValueCast : class => m_value as TValueCast;
    }
}
