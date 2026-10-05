using System;
using System.Collections.Generic;
namespace Hardlight
{
    public static partial class DictionaryExtensions
    {
        // Original HLUnityCore.Runtime 0x06000265: the bool result controls
        // fallback, so an existing null/default value is returned unchanged.
        public static TValue TryGetWithDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary,
            TKey key, TValue defaultValue = default(TValue))
            => dictionary.TryGetValue(key, out TValue value) ? value : defaultValue;

        // HLUnityCore.Runtime.dll:Hardlight.DictionaryExtensions:0x06000266;
        // shared arm64 0x8e4698. Existing null values are returned unchanged;
        // a missing key constructs a value, then calls IDictionary.Add.
        public static TValue TryGetOrNew<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key) where TValue : new()
        {
            if (dictionary.TryGetValue(key, out TValue value)) return value;
            value = new TValue();
            dictionary.Add(key, value);
            return value;
        }

        // 0x06000267: invoke the supplied constructor only after a missing
        // lookup, then Add (not index assignment); callback failures propagate.
        public static TValue TryGetOrNew<TKey, TValue>(this IDictionary<TKey, TValue> dictionary,
            TKey key, Func<TValue> constructor)
        {
            if (dictionary.TryGetValue(key, out TValue value)) return value;
            value = constructor();
            dictionary.Add(key, value);
            return value;
        }

        // 0x06000268: obtain IDictionary.Values before constructing the List.
        public static List<TValue> GetValues<TKey, TValue>(this IDictionary<TKey, TValue> dictionary)
            => new List<TValue>(dictionary.Values);
        // 0x06000269: a direct Dictionary index assignment handles both cases.
        public static void AddOrReplace<TKey, TValue>(this Dictionary<TKey, TValue> dictionary,
            TKey key, TValue value) { dictionary[key] = value; }

        // 0x0600026a: always call Array.Resize with Count before obtaining
        // Values. CopyTo failure leaves any resized array visible to the caller.
        public static void ResizeAndCopyValues<TKey, TValue>(this IDictionary<TKey, TValue> dictionary,
            ref TValue[] array)
        {
            Array.Resize(ref array, dictionary.Count);
            dictionary.Values.CopyTo(array, 0);
        }

        // 0x0600026b, arm64 0x8e2fd8: select keys during the first enumeration,
        // dispose it, then remove the collected keys in a second enumeration.
        // Predicate failure causes no removals; removal failure keeps prior ones.
        public static void RemoveAll<TKey, TValue>(this IDictionary<TKey, TValue> dictionary,
            Predicate<KeyValuePair<TKey, TValue>> predicate)
        {
            var keys = new List<TKey>();
            foreach (KeyValuePair<TKey, TValue> pair in dictionary)
                if (predicate(pair)) keys.Add(pair.Key);
            foreach (TKey key in keys) dictionary.Remove(key);
        }
    }
}
