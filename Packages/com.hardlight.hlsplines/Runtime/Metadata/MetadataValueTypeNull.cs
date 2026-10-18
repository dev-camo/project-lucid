using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLSplines.Runtime 0200001a; bodies translated from the 1.10.1 x86-64 binary.
	[CreateAssetMenu(fileName = "MetadataValueTypeNull", menuName = "Hardlight/Metadata/MetadataValueTypeNull", order = 0)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class MetadataValueTypeNull : MetadataValueType
	{
		// 06000051 (1.10.1 RVA 0x1A55760)
		protected override void Reset()
		{
			base.Reset();
			m_label = "Null";
		}

		// 06000052 (1.10.1 RVA 0x1A557B0): returns a itself, never a copy.
		public override void Interpolate(Metadata a, Metadata b, float t, MetadataInterpolationType type, out Metadata newValue)
		{
			newValue = a;
		}

		// 06000053 (1.10.1 RVA 0x1A557C0)
		public MetadataValueTypeNull()
		{
		}
	}
}
