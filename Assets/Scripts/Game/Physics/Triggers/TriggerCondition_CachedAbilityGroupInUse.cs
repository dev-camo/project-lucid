using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0x02000749; bodies translated from the 1.10.1 x86-64 binary.
	[CreateAssetMenu(menuName = "HardlightProject/TriggerConditions/TriggerCondition_CachedAbilityGroupInUse", fileName = "TriggerCondition_CachedAbilityGroupInUse")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class TriggerCondition_CachedAbilityGroupInUse : TriggerCondition_CachedBase
	{
		[SerializeField]
		private CharacterAbilityTypeGroup m_abilityTypeGroup;
		private bool m_cachedValue;

		// 060028f9 (1.10.1 RVA 0x5CCC60): the answer is computed once per cache lifetime and
		// holds when any ability of the group is in use within its own grace period.
		protected override bool DoEvaluate(Character character)
		{
			if (!m_hasCache)
			{
				m_cachedValue = false;
				foreach (CharacterAbilityTypeGroup.AbilityTypeDefinition ability in m_abilityTypeGroup.Abilities)
				{
					if (ActorAbilityUtilities.IsAbilityInUse(character, ability.Type, ability.GracePeriod))
					{
						m_cachedValue = true;
						break;
					}
				}
				m_hasCache = true;
			}
			return m_cachedValue;
		}
	}
}
