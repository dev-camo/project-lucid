using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class SerializableDictionary<TKey, TKeySerializable, TValue, TValueSerializable> :
        SerializableDictionaryBase<TKey, TValue, SerializableKeyValuePair<TKeySerializable, TValueSerializable>>
        where TKey : class
        where TKeySerializable : class
        where TValue : class
        where TValueSerializable : class
    {
        public SerializableDictionary() { }
        public SerializableDictionary(IEqualityComparer<TKey> comparer) : base(comparer) { }

        protected override SerializableKeyValuePair<TKeySerializable, TValueSerializable> SerializeKeyValuePair(TKey key, TValue value)
        {
            return new SerializableKeyValuePair<TKeySerializable, TValueSerializable>(key as TKeySerializable, value as TValueSerializable);
        }

        // Original 06000fb0 publishes the key before converting and publishing value.
        protected override void DeserializeKeyValuePair(SerializableKeyValuePair<TKeySerializable, TValueSerializable> pair,
            out TKey key, out TValue value)
        {
            key = pair.KeyAs<TKey>();
            value = pair.ValueAs<TValue>();
        }
    }
}
