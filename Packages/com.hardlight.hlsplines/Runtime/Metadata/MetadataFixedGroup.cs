using Unity.IL2CPP.CompilerServices;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight
{
	// Original HLSplines.Runtime 02000007; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "MetadataFixedGroup", menuName = "Hardlight/Metadata/MetadataFixedGroup", order = 0)]
	public class MetadataFixedGroup : MetadataGroup
	{
		[SerializeField]
		private MetadataFixedGroupData m_data = new MetadataFixedGroupData();

		// 06000011 (1.10.1 RVA 0x1A53950)
		public override IReadOnlyList<Metadata> Metadata => m_data.Metadata;

		// 06000012 (1.10.1 RVA 0x1A53960)
		public MetadataFixedGroup()
		{
		}
	}
}
