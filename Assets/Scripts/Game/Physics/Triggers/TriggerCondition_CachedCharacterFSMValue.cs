using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0x0200074c; bodies translated from the x86-64 binary. Only the
	// <bool> instantiation is used, but the generic parameter carries no constraint.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public abstract class TriggerCondition_CachedCharacterFSMValue<T> : TriggerCondition_CachedBase
	{
		[SerializeField]
		private string m_valueName;
		[SerializeField]
		protected T m_valueThreshold;
		[SerializeField]
		protected ComparisonType m_comparisonType = ComparisonType.Equal;
		private GraphStorageKey m_fsmVarName;
		protected T m_cachedValue;

		// 06002908 (<bool> 0xC81920)
		protected override void OnEnable()
		{
			base.OnEnable();
			m_fsmVarName = new GraphStorageKey(m_valueName);
		}

		// 06002909 (<bool> 0xC81960): the stored value, or the default when the character's
		// storage has none, is cached until the cache is reset.
		protected T GetValue(Character character)
		{
			if (!m_hasCache)
			{
				T value;
				m_cachedValue = character.Storage.TryGetValue(m_fsmVarName, out value) ? value : GetValueDefault();
				m_hasCache = true;
			}
			return m_cachedValue;
		}

		// 0600290a
		protected abstract T GetValueDefault();
	}
}
