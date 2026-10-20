using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 02000305; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterAbility_HomingAttackGroundRunning : CharacterAbility_HomingAttackGround
	{
		// 06001269 (1.10.1 RVA 0x750210)
		protected override bool HomingAttackActivated(CharacterBrain brain)
		{
			return base.HomingAttackActivated(brain) && brain.ActionStateQueued.GetValue(GameAction.CharacterHomingAttack);
		}

		// 0600126a (1.10.1 RVA 0x7502B0); the attack follows the boost stamina's trigger, and the press is consumed.
		protected override bool HandleAttack(CharacterBrain brain, float deltaTime)
		{
			bool triggered = false;
			if (base.HandleAttack(brain, deltaTime))
			{
				IsTriggered = m_character.BoostStamina.IsTriggered;
				triggered = IsTriggered;
			}
			if (brain.ActionStateQueued.GetValue(GameAction.CharacterHomingAttack))
			{
				brain.SetState(GameAction.CharacterHomingAttack, false);
			}
			return triggered;
		}

		// 0600126b (1.10.1 RVA 0x7503B0)
		protected override bool ValidateTargetType(HomingTarget target)
		{
			return base.ValidateTargetType(target) && !m_character.BoostStamina.IsTriggered && m_character.BoostStamina.CanTrigger();
		}

		// 0600126c (1.10.1 RVA 0x750450)
		public CharacterAbility_HomingAttackGroundRunning()
		{
		}
	}
}
