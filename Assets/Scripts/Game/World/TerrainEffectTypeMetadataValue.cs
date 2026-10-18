using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020005cc; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	[CreateAssetMenu(fileName = "TerrainEffectTypeMetadataValue", menuName = "Hardlight/Metadata/TerrainEffectTypeMetadataValue")]
	public class TerrainEffectTypeMetadataValue : MetadataValueTypeTEnum<TerrainEffectType>
	{
		// 06001f64 (1.10.1 RVA 0x55AD80)
		public TerrainEffectTypeMetadataValue()
		{
		}
	}
}
