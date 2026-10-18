using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 020002c8; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterAbility_ClimbingGround : CharacterAbility_EnterClimbing<CharacterAbilityDefinition_ClimbingGround>
	{
		// 060010dc (1.10.1 RVA 0x745580)
		protected override bool CanEnterClimb()
		{
			float threshold = Definition.VelocityThresholdGround;
			return threshold * threshold > m_character.WorldVelocityMagnitudeSqr;
		}

		// 060010dd (1.10.1 RVA 0x7455E0)
		protected override float GetSlopeAngle()
		{
			return ActorMovementUtilities.GetSlopeAngle(m_character);
		}

		// 060010de (1.10.1 RVA 0x7455F0)
		public CharacterAbility_ClimbingGround()
		{
		}
	}
}
