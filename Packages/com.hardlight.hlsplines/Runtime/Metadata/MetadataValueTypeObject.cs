using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLSplines.Runtime 0200001b; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	[CreateAssetMenu(fileName = "MetadataValueTypeObject", menuName = "Hardlight/Metadata/MetadataValueTypeObject", order = 0)]
	public class MetadataValueTypeObject : MetadataValueTypeTObject<Object>
	{
		// 06000054 (1.10.1 RVA 0x1A55830)
		public MetadataValueTypeObject()
		{
		}
	}
}
