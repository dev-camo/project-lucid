using Unity.IL2CPP.CompilerServices;
using System.Collections.Generic;
using System;

namespace Hardlight
{
	// Original HLUnityCore.Runtime 02000042; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class HLProfilerCategoryEqualityComparer : IEqualityComparer<HLProfilerCategory>
	{
		// 06000196 (1.10.1 RVA 0x1AA2A60)
		public bool Equals(HLProfilerCategory a, HLProfilerCategory b)
		{
			return a == b;
		}

		// 06000197 (1.10.1 RVA 0x1AA2A70)
		public int GetHashCode(HLProfilerCategory a)
		{
			return (int)a;
		}

		// 06000198 (1.10.1 RVA 0x1AA2A80)
		public HLProfilerCategoryEqualityComparer()
		{
		}
	}
}
