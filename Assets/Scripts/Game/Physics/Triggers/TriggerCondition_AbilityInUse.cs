using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0x02000747; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class TriggerCondition_AbilityInUse : TriggerCondition
	{
		[SerializeField]
		private bool m_useAbilityGroup;
		[ShowIf("m_useAbilityGroup", (string)null)]
		[SerializeField]
		private CharacterAbilityTypeGroup m_abilityTypeGroup;
		[SerializeField]
		[HideIf("m_useAbilityGroup", (string)null)]
		private ActorAbilityType m_abilityType;
		[HideIf("m_useAbilityGroup", (string)null)]
		[SerializeField]
		[Tooltip("Will return true if ability is in use or was last used this many seconds ago.")]
		private float m_usedWithinSeconds = 0.2f;

		// 060028f5 (1.10.1 RVA 0x5CCA00): a group holds when any of its abilities is in use
		// within that ability's own grace period.
		protected override bool DoEvaluate(Character character)
		{
			if (m_useAbilityGroup)
			{
				foreach (CharacterAbilityTypeGroup.AbilityTypeDefinition ability in m_abilityTypeGroup.Abilities)
				{
					if (ActorAbilityUtilities.IsAbilityInUse(character, ability.Type, ability.GracePeriod))
						return true;
				}
				return false;
			}
			return ActorAbilityUtilities.IsAbilityInUse(character, m_abilityType, m_usedWithinSeconds);
		}
	}
}
