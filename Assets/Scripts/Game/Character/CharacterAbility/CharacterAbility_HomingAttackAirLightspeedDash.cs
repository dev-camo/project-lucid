using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 020002fd; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterAbility_HomingAttackAirLightspeedDash : CharacterAbility_HomingAttackAir<CharacterAbilityDefinition_HomingAttackAirLightspeedDash>
	{
		// 0600122e (1.10.1 RVA 0x74E8D0)
		protected override void DoOnEnter()
		{
			base.DoOnEnter();
			m_character.EndLightspeedDashImpulse();
		}

		// 0600122f (1.10.1 RVA 0x74E920)
		protected override bool HomingAttackActivated(CharacterBrain brain)
		{
			return base.HomingAttackActivated(brain) && brain.HomingAttackLightspeedDash;
		}

		// 06001230 (1.10.1 RVA 0x74E980); a successful lock consumes the dash action.
		protected override bool HandleAttack(CharacterBrain brain, float deltaTime)
		{
			bool locked = base.HandleAttack(brain, deltaTime);
			if (locked)
			{
				brain.EndAction(GameAction.CharacterLightspeedDash);
			}
			return locked;
		}

		// 06001231 (1.10.1 RVA 0x74E9F0)
		public CharacterAbility_HomingAttackAirLightspeedDash()
		{
		}
	}
}
