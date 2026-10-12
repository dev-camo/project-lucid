using Unity.IL2CPP.CompilerServices;
using System.Runtime.CompilerServices;
using System;

namespace Hardlight.Utils
{
	// Original HLUnityCore.Runtime 020002ce; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public sealed class FloatObject
	{
		public float Value { get; set; }

		// 06001247 (1.10.1 RVA 0x1B36CB0)
		public FloatObject()
		{
		}
	}
}
