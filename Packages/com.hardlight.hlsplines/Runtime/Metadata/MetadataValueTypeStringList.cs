using Unity.IL2CPP.CompilerServices;
using System.Collections.Generic;
using UnityEngine;

namespace Hardlight
{
	// Original HLSplines.Runtime 0200001e; bodies translated from the 1.10.1 x86-64 binary.
	[CreateAssetMenu(fileName = "MetadataValueTypeStringList", menuName = "Hardlight/Metadata/MetadataValueTypeStringList", order = 0)]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class MetadataValueTypeStringList : MetadataValueType
	{
		[SerializeField]
		private List<string> m_strings = new List<string>();

		[SerializeField]
		private string m_default;

		// 0600005a (1.10.1 RVA 0x1A55AB0): the label literal is shared with MetadataValueTypeString.
		protected override void Reset()
		{
			base.Reset();
			m_label = "Text";
		}

		// 0600005b (1.10.1 RVA 0x1A55B00)
		public override string GetDefaultAsString() => m_default;

		// 0600005c (1.10.1 RVA 0x1A55B10): the result need not be one of m_strings.
		public override void Interpolate(Metadata a, Metadata b, float t, MetadataInterpolationType type, out Metadata newValue)
		{
			string value = MetadataUtilities.InterpolateValue(a.AsString(), b.AsString(), t, type);
			newValue = new Metadata(a.Key, value);
		}

		// 0600005d (1.10.1 RVA 0x1A55C10)
		public MetadataValueTypeStringList()
		{
		}
	}
}
