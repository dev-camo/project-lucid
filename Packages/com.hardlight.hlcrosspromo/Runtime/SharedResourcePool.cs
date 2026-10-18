using System;
using System.Collections.Generic;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
	// Original HLCrossPromo.Runtime 02000025; bodies translated from the 1.10.1 x86-64 binary
	// (shared SharedResourcePool<object, object> instances at 0xBE3240..0xBE3480).
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class SharedResourcePool<TKey, TResource> : ISharedResourcePool<TKey, TResource>
	{
		private readonly Action<TResource> m_onReleasedCallback;

		private readonly Dictionary<TKey, SharedResourceReference<TResource>> m_pool;

		// 0600010f (0xBE3240). poolSize is only the dictionary's initial capacity.
		public SharedResourcePool(int poolSize = 0, Action<TResource> onReleasedCallback = null)
		{
			m_onReleasedCallback = onReleasedCallback;
			m_pool = new Dictionary<TKey, SharedResourceReference<TResource>>(poolSize);
		}

		// 06000110 (0xBE32D0). Replaces any reference already stored under the key.
		public void Add(TKey key, TResource tex)
		{
			m_pool[key] = new SharedResourceReference<TResource>(tex);
		}

		// 06000111 (0xBE3360). A hit adds a holder to the pooled reference.
		public bool TryGet(TKey key, out TResource tex)
		{
			tex = default;
			bool found = m_pool.TryGetValue(key, out SharedResourceReference<TResource> reference);
			if (found)
			{
				tex = reference.Get();
			}
			return found;
		}

		// 06000112 (0xBE33D0). The callback runs before the entry leaves the pool.
		public void ReleaseResource(TResource texture)
		{
			if (texture != null && TryFindKey(texture, out TKey key) && m_pool[key].Release())
			{
				m_onReleasedCallback?.Invoke(texture);
				m_pool.Remove(key);
			}
		}

		// 06000113 (0xBE3480). Linear search; the first entry in enumeration order wins.
		private bool TryFindKey(TResource texture, out TKey result)
		{
			result = default;
			foreach ((TKey key, SharedResourceReference<TResource> reference) in m_pool)
			{
				if (reference.Equals(texture))
				{
					result = key;
					return true;
				}
			}
			return false;
		}
	}
}
