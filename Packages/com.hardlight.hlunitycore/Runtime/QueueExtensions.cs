using System.Collections.Generic;
using System;

namespace Hardlight
{
	// Original HLUnityCore.Runtime 02000078; bodies translated from the 1.10.1 x86-64 binary.
	public static class QueueExtensions
	{
		// 060002da (1.10.1 RVA 0x997540)
		public static bool TryDequeue<T>(this Queue<T> queue, out T result)
		{
			if (queue.Count > 0)
			{
				result = queue.Dequeue();
				return true;
			}
			result = default(T);
			return false;
		}

		// 060002db (1.10.1 RVA 0x997470)
		public static void Enqueue<T>(this Queue<T> queue, bool condition, T item)
		{
			if (condition)
			{
				queue.Enqueue(item);
			}
		}
	}
}
