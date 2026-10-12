using Unity.IL2CPP.CompilerServices;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Hardlight.Utils
{
	// Original HLUnityCore.Runtime 020002d1; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public sealed class QuaternionObject
	{
		public Quaternion Value { get; set; }

		// 06001250 (1.10.1 RVA 0x1B36D40)
		public QuaternionObject()
		{
		}
	}
}
