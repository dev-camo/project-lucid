using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020005de; bodies translated from the 1.10.1 x86-64 binary.
	[CreateAssetMenu(fileName = "RailTypeMetadataValue", menuName = "Hardlight/Metadata/RailTypeMetadataValue")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class RailTypeMetadataValue : MetadataValueTypeTEnum<RailType>
	{
		// 06001f98 (1.10.1 RVA 0x55B730)
		public RailTypeMetadataValue()
		{
		}
	}
}
