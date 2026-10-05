using System.Collections.Generic;
namespace Hardlight
{
    public static partial class DictionaryExtensions
    {
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
    }
}
