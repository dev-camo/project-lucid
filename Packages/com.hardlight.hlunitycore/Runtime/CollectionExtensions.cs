using System.Collections.Generic;

namespace Hardlight
{
    // Original dependency subset; other collection operations remain to be recovered.
    public static partial class CollectionExtensions
    {
        // HLUnityCore.Runtime 0x06000255: CLR null and negative index short-circuit before Count.
        public static bool IsIndexValid<T>(this IReadOnlyCollection<T> collection, int index) =>
            collection != null && index >= 0 && index < collection.Count;

        // HLUnityCore.Runtime 0x06000256: only the range has a null diagnostic. A null target is untouched
        // for an empty range, and fails at Add after Current for a nonempty range. Foreach disposal is retained.
        public static void AddRange<T>(this ICollection<T> collection, IReadOnlyCollection<T> range)
        {
            if (range == null)
            {
                HLOutput.LogError("Provided range to add is null.");
                return;
            }
            foreach (var item in range) collection.Add(item);
        }
    }
}
