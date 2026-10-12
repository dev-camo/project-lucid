using System;
using System.Collections.Generic;

namespace Hardlight
{
	// Original HLUnityCore.Runtime 02000079; bodies translated from the 1.10.1 x86-64 binary.
	public static class ReadOnlyCollectionExtensions
	{
		// 060002dc (1.10.1 RVA 0x997890)
		public static bool IsNullOrEmpty<T>(this IReadOnlyCollection<T> collection)
		{
			return collection == null || collection.Count == 0;
		}
	}
}
