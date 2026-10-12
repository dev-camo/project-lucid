using Unity.IL2CPP.CompilerServices;
using System.Runtime.CompilerServices;
using System;

namespace Hardlight.Utils
{
	// Original HLUnityCore.Runtime 020002cf; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public sealed class IntObject
	{
		public int Value { get; set; }

		// 0600124a (1.10.1 RVA 0x1B36CE0)
		public IntObject()
		{
		}
	}
}
