using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLSplines.Runtime 02000016; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "MetadataValueTypeBool", menuName = "Hardlight/Metadata/MetadataValueTypeBool", order = 0)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class MetadataValueTypeBool : MetadataValueType
	{
		// Original 02000017.
		private enum BoolValue
		{
			False = 0,
			True = 1
		}

		[SerializeField]
		private BoolValue m_default;

		// 06000045 (1.10.1 RVA 0x1A55040)
		protected override void Reset()
		{
			base.Reset();
			m_label = "Bool";
		}

		// 06000046 (1.10.1 RVA 0x1A55090): the enum name, "False" or "True".
		public override string GetDefaultAsString() => m_default.ToString();

		// 06000047 (1.10.1 RVA 0x1A550F0): parses a then b before the interpolation type is checked.
		public override void Interpolate(Metadata a, Metadata b, float t, MetadataInterpolationType type, out Metadata newValue)
		{
			bool value = MetadataUtilities.InterpolateValue(a.AsBool(), b.AsBool(), t, type);
			newValue = new Metadata(a.Key, value.ToString());
		}

		// 06000048 (1.10.1 RVA 0x1A552C0)
		public MetadataValueTypeBool()
		{
		}
	}
}
