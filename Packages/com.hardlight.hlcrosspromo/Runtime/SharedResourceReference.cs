using System;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
	// Original HLCrossPromo.Runtime 02000026; bodies translated from the 1.10.1 x86-64 binary
	// (shared SharedResourceReference<object> instances at 0xBE4130..0xBE41A0).
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class SharedResourceReference<TResource> : IEquatable<TResource>
	{
		private TResource m_resource;

		private int m_refCount;

		// 06000114 (0xBE4130). A new reference starts with one holder.
		public SharedResourceReference(TResource resource)
		{
			m_resource = resource;
			m_refCount = 1;
		}

		// 06000115 (0xBE4170). Every Get counts as another holder.
		public TResource Get()
		{
			m_refCount++;
			return m_resource;
		}

		// 06000116 (0xBE4180). True once no holder is left; the count keeps falling below zero.
		public bool Release()
		{
			m_refCount--;
			return m_refCount <= 0;
		}

		// 06000117 (0xBE41A0)
		public bool Equals(TResource other)
		{
			return m_resource.Equals(other);
		}
	}
}
