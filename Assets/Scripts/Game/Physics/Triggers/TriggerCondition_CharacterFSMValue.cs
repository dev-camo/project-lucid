using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0x02000750; bodies translated from the x86-64 binary (the <bool>,
	// <int> and <float> instantiations). The generic parameter carries no constraint.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public abstract class TriggerCondition_CharacterFSMValue<T> : TriggerCondition
	{
		[SerializeField]
		private string m_valueName;
		[SerializeField]
		protected T m_valueThreshold;
		[SerializeField]
		protected ComparisonType m_comparisonType = ComparisonType.Equal;
		private GraphStorageKey m_fsmVarName;

		// 06002913 (<bool> 0xC81D40)
		private void Awake()
		{
			m_fsmVarName = new GraphStorageKey(m_valueName);
		}

		// 06002914 (<bool> 0xC81D80): the character's stored value, or the default when it has none.
		protected T GetValue(Character character)
		{
			T value;
			return character.Storage.TryGetValue(m_fsmVarName, out value) ? value : GetValueDefault();
		}

		// 06002915
		protected abstract T GetValueDefault();
	}
}
