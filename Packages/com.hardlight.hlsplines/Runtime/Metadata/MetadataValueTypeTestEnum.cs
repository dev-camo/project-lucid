using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLSplines.Runtime 02000021; bodies translated from the 1.10.1 x86-64 binary.
	[CreateAssetMenu(fileName = "MetadataValueTypeTestEnum", menuName = "Hardlight/Metadata/MetadataValueTypeTestEnum", order = 0)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class MetadataValueTypeTestEnum : MetadataValueTypeTEnum<TestEnum>
	{
		// 06000063 (1.10.1 RVA 0x1A55CE0)
		public MetadataValueTypeTestEnum()
		{
		}
	}
}
