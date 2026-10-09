using System;
using System.Collections.Generic;

namespace Hardlight
{
    // HLUnityCore02000072: complete five-method original owner. AddUnique and
    //AddUniqueFromRange reuse the maintained bodies with zero new-method credit.
    public static class ListExtensions
    {
        //060002c2, reference shared ARM9551ac: keep the authentic IReadOnlyList
        //search and the second index read after callbacks; missing writes default.
        public static bool TryFind<T>(this List<T> list, Func<T, bool> match, out T entry)
        {
            int index = ReadOnlyListExtensions.FindIndex(list, match);
            entry = index != -1 ? list[index] : default;
            return index != -1;
        }

        //Maintained060002c3 body, zero new credit. Null list and collection
        //callback errors escape exactly as in the original provider.
        public static bool AddUnique<T>(this IList<T> list, T entry)
        {
            bool contains = list.Contains(entry);
            if (!contains) list.Add(entry);
            return !contains;
        }

        //Maintained060002c4 body, zero new credit. Every AddUnique executes after
        //the first success; exceptions retain earlier additions and disposal.
        public static bool AddUniqueFromRange<T>(this IList<T> list, IReadOnlyCollection<T> entries)
        {
            bool changed = false;
            foreach (T entry in entries) changed |= AddUnique(list, entry);
            return changed;
        }

        //060002c5, Int32 ARM953b50/x86970a00: descending Unity-random variant.
        //Random selection precedes the decrement/read; both reads precede writes,
        //then the selected index is written before the final index.
        public static void Shuffle<T>(this IList<T> list)
        {
            int remaining = list.Count;
            while (remaining > 1)
            {
                int selected = UnityEngine.Random.Range(0, remaining);
                T last = list[--remaining];
                T replacement = list[selected];
                list[selected] = last;
                list[remaining] = replacement;
            }
        }

        //060002c6, Int32 ARM954630/x869714f0: ascending seeded variant. Original
        //Random.Next uses both lower and upper bounds. Snapshot Count once;
        //selected read, current read, current write, selected write stay ordered.
        //A null random is not accessed for an empty or singleton list.
        public static void Shuffle<T>(this IList<T> list, System.Random random)
        {
            int count = list.Count;
            for (int index = 0; index < unchecked(count - 1); ++index)
            {
                int selected = random.Next(index, count);
                T replacement = list[selected];
                T current = list[index];
                list[index] = replacement;
                list[selected] = current;
            }
        }
    }
}
