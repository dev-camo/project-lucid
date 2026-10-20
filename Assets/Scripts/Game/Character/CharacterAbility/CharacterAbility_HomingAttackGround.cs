using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000303; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class CharacterAbility_HomingAttackGround : CharacterAbility_HomingAttack<CharacterAbilityDefinition_HomingAttackGround>
	{
		// 06001262 (1.10.1 RVA 0x74F8A0); the box only detects while the character is steering, and a triggered
		// attack keeps the boost stamina fed.
		protected override void DoOnUpdate(CharacterBrain brain, float deltaTime)
		{
			base.DoOnUpdate(brain, deltaTime);
			bool hasControllerMovement = m_character.HasControllerMovement;
			EnableHomingTarget(hasControllerMovement);
			if (!hasControllerMovement)
			{
				m_character.HomingPool.RemoveTarget(this);
			}
			if (IsTriggered)
			{
				m_character.BoostStamina.UpdateFromAbility(Definition.AbilityType, Definition.BoostDefinition, true);
			}
		}

		// 06001263 (1.10.1 RVA 0x74FBC0)
		protected override bool ValidateTargetType(HomingTarget target)
		{
			return base.ValidateTargetType(target) && target.TargetableFromGround && m_character.Collider.CanTrackToLocation(target.WorldPosition);
		}

		// 06001264 (1.10.1 RVA 0x74FF30)
		public void UpdateForwardSpeed(ref float forwardSpeed, ref Vector3 forwardDirection, float deltaTime)
		{
			UpdateForwardSpeed(ref forwardSpeed, ref forwardDirection, deltaTime, Definition.ForwardMotion);
		}

		// 06001265 (1.10.1 RVA 0x74FFD0)
		protected CharacterAbility_HomingAttackGround()
		{
		}
	}
}
