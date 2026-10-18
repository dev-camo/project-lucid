using Unity.IL2CPP.CompilerServices;
using System.Collections.Generic;

namespace HardlightProject
{
	// Original Game.Runtime 020002ef; bodies translated from the 1.10.1 x86-64 binary (shared generic
	// bodies at 0xE63B50..0xE63D70). A modifier that plays an animation definition while it is active.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class CharacterAbility_ModifierEffect<T> : CharacterAbility_Modifier<T> where T : CharacterAbilityDefinition_Modifier
	{
		private ActorAnimationDefinition m_animationDefinition;

		private List<FullscreenShaderManager.ParametersHandle> m_effectHandleInstance;

		// 060011eb (1.10.1 RVA 0xE63B50)
		protected override void AddModifier(CharacterAbility modifyAbility)
		{
			m_effectHandleInstance = m_character.GetFullscreenEffectHandleInstances(AbilityId);
		}

		// 060011ec (1.10.1 RVA 0xE63B90)
		protected override void RemoveModifier(CharacterAbility modifyAbility)
		{
			TriggerAnimationLeave();
			m_effectHandleInstance = null;
		}

		// 060011ed (1.10.1 RVA 0xE63C30); a running animation is left before the new one starts.
		protected void TriggerAnimationEnter(ActorAnimationDefinition animationDefinition)
		{
			TriggerAnimationLeave();
			m_animationDefinition = animationDefinition;
			m_character.TriggerAnimationEnter(m_animationDefinition, m_effectHandleInstance);
		}

		// 060011ee (1.10.1 RVA 0xE63CE0)
		protected void TriggerAnimationLeave()
		{
			if (m_animationDefinition == null)
			{
				return;
			}
			m_character.TriggerAnimationLeave(m_animationDefinition, m_effectHandleInstance);
			m_animationDefinition = null;
		}

		// 060011ef (1.10.1 RVA 0xE63D70)
		protected CharacterAbility_ModifierEffect()
		{
		}
	}
}
