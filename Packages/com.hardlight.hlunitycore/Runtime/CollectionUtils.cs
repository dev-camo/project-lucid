using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight
{
	// Original HLUnityCore.Runtime 02000022; bodies translated from the 1.10.1 x86-64 binary.
	public static class CollectionUtils
	{
		// 060000d6 (1.10.1 RVA 0x8EE330)
		public static void Shuffle<T>(List<T> list)
		{
			for (int count = list.Count; count > 1; count--)
			{
				int index = UnityEngine.Random.Range(0, count);
				T value = list[index];
				list[index] = list[count - 1];
				list[count - 1] = value;
			}
		}

		// 060000d7 (1.10.1 RVA 0x8EDA70)
		public static T BestMatch<T>(IEnumerable<T> list, Func<T, int> comparison)
		{
			T result = default(T);
			uint best = int.MaxValue;
			foreach (T item in list)
			{
				// Native magnitude: unchecked negate-if-negative compared unsigned, so int.MinValue (0x80000000)
				// never beats the int.MaxValue start and no OverflowException is raised.
				int value = comparison(item);
				uint score = (uint)(value < 0 ? unchecked(-value) : value);
				if (score < best)
				{
					result = item;
					best = score;
				}
			}
			return result;
		}

		// 060000d8 (1.10.1 RVA 0x8EDF50)
		public static bool ContainsDuplicates<T>(IEnumerable<T> collection, HashSet<T> hashSet)
		{
			hashSet.Clear();
			foreach (T item in collection)
			{
				if (!hashSet.Add(item))
				{
					return true;
				}
			}
			return false;
		}
	}
}
