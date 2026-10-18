using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020002c6; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterAbility_ClimbingAir : CharacterAbility_EnterClimbing<CharacterAbilityDefinition_ClimbingAir>
	{
		// 060010d6 (1.10.1 RVA 0x744F10); enters at once when the angle of attack reaches the minimum, otherwise when the
		// input heading (forward without controller movement) points against the collision normal.
		protected override bool CanEnterClimb()
		{
			Vector3 collisionNormal = ClimbingCollisionTracker.CollisionNormal;
			float angleOfAttack = Vector3.Angle(collisionNormal, ClimbingCollisionTracker.WorldVelocityAtLastTerrainEnter) - 90f;
			if (angleOfAttack >= Definition.MinAngleOfAttack)
			{
				return true;
			}
			Vector3 direction = m_character.HasControllerMovement
				? new Vector3(m_character.ControllerMovement.x, 0f, m_character.ControllerMovement.y)
				: Vector3.forward;
			Vector3 worldDirection = (m_character.WorldRotation * direction).normalized;
			return Vector3.Dot(collisionNormal, worldDirection) < 0f;
		}

		// 060010d7 (1.10.1 RVA 0x745270)
		public CharacterAbility_ClimbingAir()
		{
		}
	}
}
