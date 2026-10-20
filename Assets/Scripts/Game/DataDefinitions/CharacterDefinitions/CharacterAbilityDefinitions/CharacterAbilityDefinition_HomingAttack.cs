using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020004d4; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public abstract class CharacterAbilityDefinition_HomingAttack : CharacterAbilityDefinition_HomingAttackBase
	{
		[Tooltip("Target types for this homing attack.")]
		public HomingTargetType[] TargetTypes;

		[Min(0f)]
		[Tooltip("Maximum velocity when homing to a target.")]
		public float HomingVelocityMax = 80f;

		[Tooltip("Additional speed over time.")]
		public AnimationCurve HomingAdditionalSpeed;

		[Tooltip("Velocity is determined by time to target.")]
		public bool HomingVelocityFromTime;

		[Tooltip("Time to reach target.")]
		public float HomingVelocityTime;

		[Tooltip("After a hit, combo can be activated again while having another target for this time.")]
		public float ComboBufferTime = 0.2f;

		[Tooltip("Time setting for each combo level.")]
		public TimeSettings_SDT[] ComboTimeSettings;

		[Tooltip("Distance from target to deactivate homing.")]
		public float DeactivationDistance;

		// 06001b14 (1.10.1 RVA 0x5431E0); an unassigned list throws from the array lookup.
		public bool HasTargetType(HomingTargetType targetType)
		{
			return Array.IndexOf(TargetTypes, targetType) >= 0;
		}

		// 06001b15 (1.10.1 RVA 0x543230)
		protected CharacterAbilityDefinition_HomingAttack()
		{
		}
	}
}
