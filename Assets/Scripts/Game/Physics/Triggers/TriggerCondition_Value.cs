using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0x02000758; bodies translated from the x86-64 binary (the <bool>,
	// <int> and <float> instantiations). The generic parameter carries no constraint.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class TriggerCondition_Value<T> : TriggerCondition
	{
		[SerializeField]
		private T m_valueStart;
		[SerializeField]
		protected T m_valueThreshold;
		[SerializeField]
		protected ComparisonType m_comparisonType = ComparisonType.Equal;
		protected T m_value;

		// 0600292a (<bool> 0xC82290)
		private void Awake()
		{
			Reset();
		}

		// 0600292b (<bool> 0xC822A0)
		public void Reset()
		{
			m_value = m_valueStart;
		}

		// 0600292c
		public abstract void IncrementValue(T delta);
	}
}
