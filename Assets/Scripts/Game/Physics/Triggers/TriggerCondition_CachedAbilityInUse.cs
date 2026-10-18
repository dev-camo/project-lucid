using Hardlight.Utils;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0x0200074a; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(menuName = "HardlightProject/TriggerConditions/TriggerCondition_CachedAbilityInUse", fileName = "TriggerCondition_CachedAbilityInUse")]
	public class TriggerCondition_CachedAbilityInUse : TriggerCondition_CachedBase
	{
		[HashEnum(null)]
		[SerializeField]
		private ActorAbilityType m_abilityType;
		[Tooltip("Will return true if ability is in use or was last used this many seconds ago.")]
		[SerializeField]
		private float m_usedWithinSeconds = 0.2f;
		private bool m_cachedValue;

		// 060028fb (1.10.1 RVA 0x5CCE10)
		protected override bool DoEvaluate(Character character)
		{
			if (m_hasCache)
				return m_cachedValue;
			m_cachedValue = ActorAbilityUtilities.IsAbilityInUse(character, m_abilityType, m_usedWithinSeconds);
			m_hasCache = true;
			return m_cachedValue;
		}
	}
}
