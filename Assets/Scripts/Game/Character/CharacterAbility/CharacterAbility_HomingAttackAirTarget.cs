using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 020002ff; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterAbility_HomingAttackAirTarget : CharacterAbility_HomingAttackAir<CharacterAbilityDefinition_HomingAttackAirTarget>
	{
		// 0600123d (1.10.1 RVA 0x74F590)
		protected override bool HomingAttackActivated(CharacterBrain brain)
		{
			return base.HomingAttackActivated(brain) && brain.ActionStateQueued.GetValue(GameAction.CharacterHomingAttack);
		}

		// 0600123e (1.10.1 RVA 0x74F690); a successful lock records when the attack was pressed and consumes the press.
		protected override bool HandleAttack(CharacterBrain brain, float deltaTime)
		{
			bool locked = base.HandleAttack(brain, deltaTime);
			if (locked)
			{
				m_character.Storage.SetValue(ActorFSMKeys.HomingAttackActionTimestamp, m_character.GetBrainActionTimestamp(GameAction.CharacterHomingAttack));
				brain.SetState(GameAction.CharacterHomingAttack, false);
			}
			return locked;
		}

		// 0600123f (1.10.1 RVA 0x74F810)
		public CharacterAbility_HomingAttackAirTarget()
		{
		}
	}
}
