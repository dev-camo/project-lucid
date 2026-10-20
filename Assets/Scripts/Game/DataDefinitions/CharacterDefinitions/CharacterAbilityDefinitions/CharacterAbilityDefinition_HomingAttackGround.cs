using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020004da; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public abstract class CharacterAbilityDefinition_HomingAttackGround : CharacterAbilityDefinition_HomingAttack
	{
		[Tooltip("Time since boost activation that homing can still be valid.")]
		public float ActivationTime;

		[Tooltip("Movement in the forward axis.")]
		public Motion ForwardMotion;

		[Tooltip("Boost parameters while homing attack on the ground.")]
		public CharacterStamina.Definition BoostDefinition;

		// 06001b1f (1.10.1 RVA 0x543620); always reports the authored boost definition, even when it is unassigned.
		public override bool TryGetBoostDefinition(out CharacterStamina.Definition boostDefinition)
		{
			boostDefinition = BoostDefinition;
			return true;
		}

		// 06001b20 (1.10.1 RVA 0x543640)
		protected CharacterAbilityDefinition_HomingAttackGround()
		{
		}
	}
}
