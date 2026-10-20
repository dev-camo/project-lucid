using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 02000304; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterAbility_HomingAttackGroundRolling : CharacterAbility_HomingAttackGround
	{
		// 06001266 (1.10.1 RVA 0x750010); a queued spin dash charge blocks the attack.
		protected override bool HomingAttackActivated(CharacterBrain brain)
		{
			return base.HomingAttackActivated(brain) && !brain.ActionStateQueued.GetValue(GameAction.CharacterSpinDashCharge);
		}

		// 06001267 (1.10.1 RVA 0x750110)
		protected override bool HandleAttack(CharacterBrain brain, float deltaTime)
		{
			bool locked = base.HandleAttack(brain, deltaTime);
			IsTriggered = locked && !brain.ActionStateQueued.GetValue(GameAction.CharacterSpinDashCharge);
			return locked;
		}

		// 06001268 (1.10.1 RVA 0x7501D0)
		public CharacterAbility_HomingAttackGroundRolling()
		{
		}
	}
}
