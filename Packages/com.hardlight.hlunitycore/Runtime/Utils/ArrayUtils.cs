using System;
using System.Collections.Generic;

namespace Hardlight.Utils
{
    // Original HLUnityCore.Runtime 020002ab (06001154..06001157). The generic
    // bodies shipped only as fully shared instantiations (0x8d8a70, 0x8d8920,
    // 0x8d8d80); their RGCTX tables name Array.Resize<T> and the list accessors.
    public class ArrayUtils
    {
        // 0x06001154: the old length is read before items, and Count is re-read
        // for every copied element.
        public static void AddRange<T>(ref T[] array, IReadOnlyList<T> items)
        {
            int length = array.Length;
            Array.Resize(ref array, length + items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                array[length + i] = items[i];
            }
        }

        // 0x06001155.
        public static void Add<T>(ref T[] array, T item)
        {
            int length = array.Length;
            Array.Resize(ref array, length + 1);
            array[length] = item;
        }

        // 0x06001156: the first element always shifts the rest down, the last
        // element skips the copy, any other index copies the tail.
        public static void RemoveAt<T>(ref T[] array, int index)
        {
            int lastIndex = array.Length - 1;
            if (index == 0)
            {
                Array.Copy(array, 1, array, 0, lastIndex);
            }
            else if (index != lastIndex)
            {
                Array.Copy(array, index + 1, array, index, lastIndex - index);
            }
            Array.Resize(ref array, lastIndex);
        }
    }
}
