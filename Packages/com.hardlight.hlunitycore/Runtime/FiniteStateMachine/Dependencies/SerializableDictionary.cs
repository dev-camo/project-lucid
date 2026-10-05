using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [Serializable]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class SerializableDictionary<TKey, TValue> :
        SerializableDictionaryBase<TKey, TValue, SerializableKeyValuePair<TKey, TValue>>
    {
        // Original HLUnityCore.Runtime 0x06000fa5/fa6: matching base overloads.
        public SerializableDictionary() { }
        public SerializableDictionary(IEqualityComparer<TKey> comparer) : base(comparer) { }

        // 0x06000fa7/fa8, arm64 0xbc8764/0xbc87dc: real pair construction
        // followed by ordered Key then Value reads; null pairs are not skipped.
        protected override SerializableKeyValuePair<TKey, TValue> SerializeKeyValuePair(TKey key, TValue value)
            => new SerializableKeyValuePair<TKey, TValue>(key, value);
        protected override void DeserializeKeyValuePair(SerializableKeyValuePair<TKey, TValue> pair,
            out TKey key, out TValue value)
        { key = pair.Key; value = pair.Value; }
    }
}
