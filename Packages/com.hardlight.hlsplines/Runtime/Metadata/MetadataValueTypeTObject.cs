using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
	// Original HLSplines.Runtime 02000022; bodies translated from the 1.10.1 x86-64 shared
	// MetadataValueTypeTObject<object> instantiation.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public abstract class MetadataValueTypeTObject<TObject> : MetadataValueType where TObject : Object
	{
		// 06000064 (1.10.1 RVA 0x17CA7A0)
		protected override void Reset()
		{
			base.Reset();
			m_label = typeof(TObject).Name;
		}

		// 06000065 (1.10.1 RVA 0x17CA830)
		public override string GetDefaultAsString() => string.Empty;

		// 06000066 (1.10.1 RVA 0x17CA870): copies only the string value; a's object reference is dropped.
		public override void Interpolate(Metadata a, Metadata b, float t, MetadataInterpolationType type, out Metadata newValue)
		{
			newValue = new Metadata(a.Key, a.Value);
		}

		// 06000067 (1.10.1 RVA 0x17CA8E0)
		protected MetadataValueTypeTObject()
		{
		}
	}
}
