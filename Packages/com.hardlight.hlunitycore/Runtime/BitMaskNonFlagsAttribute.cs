using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;

namespace Hardlight.Utils
{
	// Original HLUnityCore.Runtime 020002ad; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class BitMaskNonFlagsAttribute : PropertyAttribute
	{
		public Type PropType;

		// 06001159 (1.10.1 RVA 0x1B30D00)
		public BitMaskNonFlagsAttribute(Type aType)
		{
			PropType = aType;
		}
	}
}
