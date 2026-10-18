using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLSplines.Runtime 0200001d; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "MetadataValueTypeString", menuName = "Hardlight/Metadata/MetadataValueTypeString", order = 0)]
	public class MetadataValueTypeString : MetadataValueType
	{
		[SerializeField]
		private string m_regEx = "";

		[SerializeField]
		private string m_default;

		// 06000056 (1.10.1 RVA 0x1A558B0)
		protected override void Reset()
		{
			base.Reset();
			m_label = "Text";
		}

		// 06000057 (1.10.1 RVA 0x1A55900)
		public override string GetDefaultAsString() => m_default;

		// 06000058 (1.10.1 RVA 0x1A55910): m_regEx is not applied to the result.
		public override void Interpolate(Metadata a, Metadata b, float t, MetadataInterpolationType type, out Metadata newValue)
		{
			string value = MetadataUtilities.InterpolateValue(a.AsString(), b.AsString(), t, type);
			newValue = new Metadata(a.Key, value);
		}

		// 06000059 (1.10.1 RVA 0x1A55A10)
		public MetadataValueTypeString()
		{
		}
	}
}
