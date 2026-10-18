using Unity.IL2CPP.CompilerServices;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight
{
	// Original HLSplines.Runtime 02000013; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class MetadataTestMonoBehaviour : MonoBehaviour
	{
		[SerializeField]
		private MetadataGroup m_metadataGroup;

		[SerializeField]
		private List<MetadataGroup> m_metadataGroupArray = new List<MetadataGroup>();

		[SerializeField]
		private MetadataFlexiGroupData m_metadataFlexi = new MetadataFlexiGroupData();

		[SerializeField]
		private List<MetadataFlexiGroupData> m_metadataFlexiArray = new List<MetadataFlexiGroupData>();

		[SerializeField]
		private MetadataFixedGroupData m_metadataFixed = new MetadataFixedGroupData();

		[SerializeField]
		private List<MetadataFixedGroupData> m_metadataFixedArray = new List<MetadataFixedGroupData>();

		[SerializeField]
		private MetadataFlexi m_metadataIndividual = new MetadataFlexi(null, "");

		// 0600002e (1.10.1 RVA 0x1A54880)
		public MetadataTestMonoBehaviour()
		{
		}
	}
}
