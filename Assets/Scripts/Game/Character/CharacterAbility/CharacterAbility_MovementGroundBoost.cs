using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 020002d9; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterAbility_MovementGroundBoost : CharacterAbility_MovementGroundBoostBase<CharacterAbilityDefinition_MovementGroundBoost>
	{
		// 0600114f (1.10.1 RVA 0x749050)
		public override void Initialise(Actor actor, AbilityDefinition definition)
		{
			base.Initialise(actor, definition);
			m_character.BoostStamina.UpdateFromAbility(Definition.AbilityType, Definition.BoostDefinition, false);
		}

		// 06001150 (1.10.1 RVA 0x749100); true while the one-shot boost flag is absent or false.
		protected override bool CanTrigger()
		{
			return !m_character.TryGetValue(ActorFSMKeys.OneShotCanTriggerBoost);
		}

		// 06001151 (1.10.1 RVA 0x749170)
		public CharacterAbility_MovementGroundBoost()
		{
		}
	}
}
