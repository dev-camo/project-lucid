using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;

namespace Hardlight
{
	// Original HLSplines.Runtime 0200001f; bodies translated from the 1.10.1 x86-64 shared
	// instantiations (Int32Enum and __Il2CppFullySharedGenericStructType).
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public abstract class MetadataValueTypeTEnum<TEnum> : MetadataValueType where TEnum : struct, Enum
	{
		[SerializeField]
		private TEnum m_default;

		// 0600005e (1.10.1 RVA 0x17CA400)
		protected override void Reset()
		{
			base.Reset();
			m_label = "Enum";
		}

		// 0600005f (1.10.1 RVA 0x17CA450)
		private TEnum GetDefaultValue() => m_default;

		// 06000060 (1.10.1 RVA 0x17CA460)
		public override string GetDefaultAsString() => m_default.ToString();

		// 06000061 (1.10.1 RVA 0x17CA4B0): the stored enum text is selected as a string, never parsed.
		public override void Interpolate(Metadata a, Metadata b, float t, MetadataInterpolationType type, out Metadata newValue)
		{
			string value = MetadataUtilities.InterpolateValue(a.AsString(), b.AsString(), t, type);
			newValue = new Metadata(a.Key, value);
		}

		// 06000062 (1.10.1 RVA 0x17CA550)
		protected MetadataValueTypeTEnum()
		{
		}
	}
}
