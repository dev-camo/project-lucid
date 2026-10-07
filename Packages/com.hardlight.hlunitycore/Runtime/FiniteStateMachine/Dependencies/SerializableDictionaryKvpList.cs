using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SerializableDictionaryKvpList<TKey, TKeySerializable, TValue, TValueSerializable> :
        SerializableDictionaryBase<TKey, List<TValue>, SerializableKeyValueListPair<TKeySerializable, TValueSerializable>>
        where TKey : class
        where TKeySerializable : class
        where TValue : class
        where TValueSerializable : class
    {
        protected override SerializableKeyValueListPair<TKeySerializable, TValueSerializable> SerializeKeyValuePair(TKey key, List<TValue> values)
        {
            var pair = new SerializableKeyValueListPair<TKeySerializable, TValueSerializable>();
            pair.Set(key, values);
            return pair;
        }

        protected override void DeserializeKeyValuePair(SerializableKeyValueListPair<TKeySerializable, TValueSerializable> pair,
            out TKey key, out List<TValue> value)
        {
            key = pair.KeyAs<TKey>();
            value = pair.ValuesAs<TValue>();
        }

        // Original 06000fe8 clears before reading the argument, copies each array into
        // a new List, and keeps earlier additions if a later row fails.
        public void Set(Dictionary<TKey, TValue[]> dictionary)
        {
            Clear();
            foreach (var (key, values) in dictionary)
                Add(key, new List<TValue>(values));
        }

        public SerializableDictionaryKvpList() { }
    }
}
