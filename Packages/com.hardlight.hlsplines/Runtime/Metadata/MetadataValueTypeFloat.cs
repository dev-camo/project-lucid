using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLSplines.Runtime 02000018; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "MetadataValueTypeFloat", menuName = "Hardlight/Metadata/MetadataValueTypeFloat", order = 0)]
	public class MetadataValueTypeFloat : MetadataValueType
	{
		[SerializeField]
		private bool m_hasMinimum;

		[SerializeField]
		private float m_minimum;

		[SerializeField]
		private bool m_hasMaximum;

		[SerializeField]
		private float m_maximum;

		[SerializeField]
		private bool m_noDefaultHelpText;

		[SerializeField]
		private float m_default;

		// 06000049 (1.10.1 RVA 0x1A55330)
		protected override void Reset()
		{
			base.Reset();
			m_label = "Float";
		}

		// 0600004a (1.10.1 RVA 0x1A55380)
		public override string GetDefaultAsString() => m_default.ToString();

		// 0600004b (1.10.1 RVA 0x1A55390): the inspector bounds do not constrain the result.
		public override void Interpolate(Metadata a, Metadata b, float t, MetadataInterpolationType type, out Metadata newValue)
		{
			float value = MetadataUtilities.InterpolateValue(a.AsFloat(), b.AsFloat(), t, type);
			newValue = new Metadata(a.Key, value.ToString());
		}

		// 0600004c (1.10.1 RVA 0x1A55520)
		public MetadataValueTypeFloat()
		{
		}
	}
}
