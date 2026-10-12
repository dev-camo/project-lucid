using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;

namespace Hardlight.Utils
{
	// Original HLUnityCore.Runtime 020002ac; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class BitMaskAttribute : PropertyAttribute
	{
		public Type PropType;

		// 06001158 (1.10.1 RVA 0x1B30CD0)
		public BitMaskAttribute(Type aType)
		{
			PropType = aType;
		}
	}
}
