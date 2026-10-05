using System;
using System.Collections.Generic;
using Hardlight.Utils;

namespace Hardlight
{
    public static class ReadOnlyListExtensions
    {
        // HLUnityCore.Runtime 0x060002dd: index and Count remain live across callbacks.
        public static int FindIndex<T>(this IReadOnlyList<T> list, Func<T, bool> func)
        {
            for (int i = 0; i < list.Count; i++)
                if (func(list[i])) return i;
            return -1;
        }

        // HLUnityCore.Runtime 0x060002de: return the value read before the callback.
        public static T Find<T>(this IReadOnlyList<T> list, Func<T, bool> func)
        {
            for (int i = 0; i < list.Count; i++)
            {
                T item = list[i];
                if (func(item)) return item;
            }
            return default;
        }

        // HLUnityCore.Runtime 0x060002df.
        public static bool Exists<T>(this IReadOnlyList<T> list, Func<T, bool> func)
        {
            for (int i = 0; i < list.Count; i++)
                if (func(list[i])) return true;
            return false;
        }

        // HLUnityCore.Runtime 0x060002e0: initial Count is capacity, not a loop bound.
        public static IReadOnlyList<TOutput> ConvertAll<TInput, TOutput>(this IReadOnlyList<TInput> list, Converter<TInput, TOutput> converter)
        {
            var result = new List<TOutput>(list.Count);
            for (int i = 0; i < list.Count; i++) result.Add(converter(list[i]));
            return result;
        }

        // HLUnityCore.Runtime 0x060002e1: null queries use boxed/reference null,
        // bypassing user equality and overloaded null operators.
        public static bool Contains<T>(this IReadOnlyList<T> list, T element)
        {
            if ((object)element == null)
            {
                for (int i = 0; i < list.Count; i++)
                    if ((object)list[i] == null) return true;
            }
            else
            {
                EqualityComparer<T> comparer = EqualityComparer<T>.Default;
                for (int i = 0; i < list.Count; i++)
                    if (comparer.Equals(list[i], element)) return true;
            }
            return false;
        }

        // HLUnityCore.Runtime 0x060002e2: snapshot Count once; compare value to item.
        public static int BinarySearch<T>(this IReadOnlyList<T> list, T value) where T : IComparable<T>
        {
            int lower = 0;
            int upper = unchecked(list.Count - 1);
            if (upper < 0) return -1;
            while (true)
            {
                int middle = unchecked(lower + upper) / 2;
                int comparison = value.CompareTo(list[middle]);
                if (comparison == 0) return middle;
                if (comparison > 0)
                {
                    lower = unchecked(middle + 1);
                    if (middle < upper) continue;
                    return ~lower;
                }
                upper = unchecked(middle - 1);
                if (lower < middle) continue;
                return ~middle;
            }
        }

        // HLUnityCore.Runtime 0x060002e3 and compiler cache/callback 0x060002ef..2f1.
        public static T BinarySearchClosest<T>(this IReadOnlyList<T> sortedList, T value, bool midpointRoundUp = true) where T : IComparable<T>
        {
            return BinarySearchClosest(sortedList, value, (item1, item2) => item1.CompareTo(item2), midpointRoundUp);
        }

        // HLUnityCore.Runtime 0x060002e4: compare the two signed results directly.
        // Search and endpoint reads precede delegate access; both neighbors are read
        // before either callback. Equal results use midpointRoundUp.
        public static T BinarySearchClosest<T>(this IReadOnlyList<T> sortedList, T value, Comparison<T> closestComparison, bool midpointRoundUp = true) where T : IComparable<T>
        {
            int result = BinarySearch(sortedList, value);
            if (result >= 0) return sortedList[result];
            int insertion = ~result;
            if (insertion == 0) return sortedList[0];
            if (insertion == sortedList.Count) return sortedList[insertion - 1];
            T lower = sortedList[insertion - 1];
            T upper = sortedList[insertion];
            int lowerDistance = closestComparison(value, lower);
            int upperDistance = closestComparison(upper, value);
            if (lowerDistance.CompareTo(upperDistance) < 0) return lower;
            if (lowerDistance.CompareTo(upperDistance) > 0) return upper;
            return midpointRoundUp ? upper : lower;
        }

        // HLUnityCore.Runtime 0x060002e5: direct index, no Count read.
        public static T First<T>(this IReadOnlyList<T> list) => list[0];

        // HLUnityCore.Runtime 0x060002e6.
        public static T Last<T>(this IReadOnlyList<T> list) => list[unchecked(list.Count - 1)];

        // HLUnityCore.Runtime 0x060002e7.
        public static T FirstOrDefault<T>(this IReadOnlyList<T> list)
        {
            TryGetFirst(list, out T result);
            return result;
        }

        // HLUnityCore.Runtime 0x060002e8.
        public static T ElementAtOrDefault<T>(this IReadOnlyList<T> list, int index)
        {
            TryGetIndex(list, index, out T result);
            return result;
        }

        // HLUnityCore.Runtime 0x060002e9.
        public static T LastOrDefault<T>(this IReadOnlyList<T> list)
        {
            TryGetLast(list, out T result);
            return result;
        }

        // HLUnityCore.Runtime 0x060002ea.
        public static bool TryGetFirst<T>(this IReadOnlyList<T> list, out T result) => TryGetIndex(list, 0, out result);

        // HLUnityCore.Runtime 0x060002eb: Count is read here, and again by TryGetIndex
        // when Count - 1 is nonnegative.
        public static bool TryGetLast<T>(this IReadOnlyList<T> list, out T result) => TryGetIndex(list, unchecked(list.Count - 1), out result);

        // HLUnityCore.Runtime 0x060002ec: negative indices short-circuit even null
        // lists. Exceptions from Count/index leave the caller's out storage intact.
        public static bool TryGetIndex<T>(this IReadOnlyList<T> list, int index, out T result)
        {
            if (index >= 0 && index < list.Count)
            {
                result = list[index];
                return true;
            }
            result = default;
            return false;
        }

        // HLUnityCore.Runtime 0x060002ed: original enumeration and finally disposal.
        public static void Validate<TValidatable>(this IReadOnlyList<TValidatable> validatables) where TValidatable : IValidatable
        {
            foreach (TValidatable validatable in validatables) validatable.Validate();
        }

        // HLUnityCore.Runtime 0x060002ee: no preflight; live Count, then source read,
        // then destination assignment. Earlier assignments survive later errors.
        public static void CopyTo<T>(this IReadOnlyList<T> list, T[] destination, int startIndex = 0)
        {
            for (int i = 0; i < list.Count; i++) destination[unchecked(i + startIndex)] = list[i];
        }
    }
}
