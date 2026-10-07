using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SerializableDictionary<TKey, TKeySerializable, TValue> :
        SerializableDictionaryBase<TKey, TValue, SerializableKeyValuePair<TKeySerializable, TValue>>
        where TKey : class
        where TKeySerializable : class
    {
        public SerializableDictionary() { }
        public SerializableDictionary(IEqualityComparer<TKey> comparer) : base(comparer) { }

        // Original 06000fab preserves failed reference conversions as null keys.
        protected override SerializableKeyValuePair<TKeySerializable, TValue> SerializeKeyValuePair(TKey key, TValue value)
        {
            return new SerializableKeyValuePair<TKeySerializable, TValue>(key as TKeySerializable, value);
        }

        protected override void DeserializeKeyValuePair(SerializableKeyValuePair<TKeySerializable, TValue> pair,
            out TKey key, out TValue value)
        {
            key = pair.KeyAs<TKey>();
            value = pair.Value;
        }
    }
}
