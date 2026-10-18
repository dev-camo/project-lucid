using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 020002eb; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterAbility_ModifierCollectableBoost : CharacterAbility_ModifierEffect<CharacterAbilityDefinition_ModifierCollectableBoost>
	{
		// 060011db (1.10.1 RVA 0x74B7D0)
		protected override void AddModifier(CharacterAbility modifyAbility)
		{
			base.AddModifier(modifyAbility);
			m_character.BoostStamina.OnCollectableOverride += ApplyCollectableOverride;
		}

		// 060011dc (1.10.1 RVA 0x74B8E0); the boost stamina is overridden for the definition's type
		// only, and any other type contributes nothing here.
		private float ApplyCollectableOverride(CollectableType collectableType)
		{
			if (Definition.CollectableType != collectableType)
			{
				return 0f;
			}
			if (Definition.CollectAnimationDefinition != null)
			{
				TriggerAnimationEnter(Definition.CollectAnimationDefinition);
			}
			return Definition.StaminaMultiplier;
		}

		// 060011dd (1.10.1 RVA 0x74B9A0)
		protected override void RemoveModifier(CharacterAbility modifyAbility)
		{
			base.RemoveModifier(modifyAbility);
			m_character.BoostStamina.OnCollectableOverride -= ApplyCollectableOverride;
		}

		// 060011de (1.10.1 RVA 0x74BAB0)
		public CharacterAbility_ModifierCollectableBoost()
		{
		}
	}
}
