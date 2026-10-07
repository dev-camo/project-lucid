using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SerializableKeyValueListPair<TKey, TValue>
        where TKey : class
        where TValue : class
    {
        [SerializeField] private TKey m_key;
        [SerializeField] private List<TValue> m_values = new List<TValue>();

        public TKey Key => m_key;
        public List<TValue> Values => m_values;

        // Original 06000fec stores the cast key and clears the destination list
        // before obtaining the argument's enumerator. Cast mismatches remain null.
        public void Set<TKeyCast, TValueCast>(TKeyCast key, List<TValueCast> values)
            where TKeyCast : class
            where TValueCast : class
        {
            m_key = key as TKey;
            m_values.Clear();
            foreach (TValueCast value in values)
                m_values.Add(value as TValue);
        }

        public TKeyCast KeyAs<TKeyCast>() where TKeyCast : class => m_key as TKeyCast;

        // Original 06000fee always allocates a new list, including for zero items.
        public List<TValueCast> ValuesAs<TValueCast>() where TValueCast : class
        {
            var values = new List<TValueCast>();
            foreach (TValue value in m_values)
                values.Add(value as TValueCast);
            return values;
        }

        public SerializableKeyValueListPair() { }
    }
}
