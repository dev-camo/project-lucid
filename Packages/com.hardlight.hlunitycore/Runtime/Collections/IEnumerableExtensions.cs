using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

namespace Hardlight
{
    // Reconstructed original HLUnityCore.Runtime collection helpers (TypeDef 0x02000065).
    // Fault and enumerator lifetime behavior is part of the shipped implementation.
    public static class IEnumerableExtensions
    {
        // Original 0x06000280: evaluate both MoveNext calls, including unequal-length tails.
        // Acquiring the right enumerator precedes the disposal scopes, so its failure
        // leaves an already acquired left enumerator undisposed, as in the original.
        public static bool EqualsDeep(this IEnumerable collectionLhs, IEnumerable collectionRhs)
        {
            if (ReferenceEquals(collectionLhs, collectionRhs))
                return true;
            if (collectionRhs == null)
                return false;

            IEnumerator enumeratorLhs = collectionLhs.GetEnumerator();
            IEnumerator enumeratorRhs = collectionRhs.GetEnumerator();
            using (enumeratorLhs as IDisposable)
            using (enumeratorRhs as IDisposable)
            {
                bool moveLhs;
                bool moveRhs;
                while ((moveLhs = enumeratorLhs.MoveNext()) & (moveRhs = enumeratorRhs.MoveNext()))
                {
                    object currentLhs = enumeratorLhs.Current;
                    object currentRhs = enumeratorRhs.Current;
                    if (ReferenceEquals(currentLhs, currentRhs))
                        continue;
                    if (currentLhs == null || currentRhs == null)
                        return false;

                    IEnumerable enumerableLhs = currentLhs as IEnumerable;
                    IEnumerable enumerableRhs = currentRhs as IEnumerable;
                    if (enumerableLhs != null || enumerableRhs != null)
                    {
                        if (enumerableLhs == null || enumerableRhs == null || !enumerableLhs.EqualsDeep(enumerableRhs))
                            return false;
                    }
                    else if (!currentLhs.Equals(currentRhs))
                        return false;
                }
                return moveLhs == moveRhs;
            }
        }

        // Original 0x06000281: nested collections recurse; positions are one-based,
        // null contributes -1, and integer overflow wraps in the hash accumulation.
        public static int GetHashCodeDeep(this IEnumerable collection)
        {
            int hash = 541;
            int index = 0;
            foreach (object item in collection)
            {
                int elementHash;
                if (item == null)
                    elementHash = -1;
                else if (item is IEnumerable enumerable)
                    elementHash = enumerable.GetHashCodeDeep();
                else
                    elementHash = item.GetHashCode();
                ++index;
                hash = unchecked(hash * 397 ^ index * (elementHash + 1));
            }
            return hash;
        }

        // Original 0x06000282: an empty sequence returns before clearing a supplied
        // builder. Nonempty input clears it only after the first successful MoveNext.
        public static string Join(this IEnumerable collection, string separator = ",", string nullSubstitute = "", StringBuilder stringBuilder = null)
        {
            IEnumerator enumerator = collection.GetEnumerator();
            using (enumerator as IDisposable)
            {
                if (!enumerator.MoveNext())
                    return string.Empty;
                if (stringBuilder == null)
                    stringBuilder = new StringBuilder();
                else
                    stringBuilder.Clear();
                do
                {
                    object item = enumerator.Current;
                    stringBuilder.Append(item == null ? nullSubstitute : item.ToString());
                    if (!enumerator.MoveNext())
                        break;
                    stringBuilder.Append(separator);
                }
                while (true);
                return stringBuilder.ToString();
            }
        }

        // Original 0x06000283: defer enumeration to the original pair iterator.
        public static EnumerableZip<T1, T2> Zip<T1, T2>(this IEnumerable<T1> collection1, IEnumerable<T2> collection2)
        {
            return new EnumerableZip<T1, T2>(collection1, collection2);
        }

        // Original 0x06000284 and natural 0x06000286/87: call the captured item's
        // Equals method; a null reference item faults when an element is tested.
        public static int CountOccurrences<T>(this IEnumerable<T> collection, T item)
        {
            bool Predicate(T element) => item.Equals(element);
            return collection.CountOccurrences(Predicate);
        }

        // Original 0x06000285: visit each element once and preserve predicate faults.
        public static int CountOccurrences<T>(this IEnumerable<T> collection, Func<T, bool> predicate)
        {
            int count = 0;
            foreach (T element in collection)
                if (predicate(element))
                    ++count;
            return count;
        }
    }
}
