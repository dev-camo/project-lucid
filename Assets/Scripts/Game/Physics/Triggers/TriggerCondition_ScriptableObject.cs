using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0x02000757; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class TriggerCondition_ScriptableObject : TriggerCondition
	{
		[SerializeField]
		private TriggerCondition_CachedBase m_condition;

		// 06002928 (1.10.1 RVA 0x5CDCC0): the asset's Evaluate(Character) applies the asset's own
		// m_invert; this component's inversion is applied by Evaluate.
		protected override bool DoEvaluate(Character character)
		{
			return m_condition.Evaluate(character);
		}
	}
}
