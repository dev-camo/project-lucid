using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020004f8; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public abstract class CharacterAbilityDefinition_RailsHoming : CharacterAbilityDefinition_Targeting
	{
		[Tooltip("Rotational velocity when homing to a target. Measured in degrees per second.")]
		[Range(0f, 360f)]
		public float HomingRotationalVelocity = 180f;

		[Min(0f)]
		[Tooltip("The distance to project to activate a homing target.")]
		public float HomingDistance = 1f;

		[Tooltip("Additional speed over time.")]
		public AnimationCurve HomingAdditionalSpeed;

		public float HomingDistanceSqr { get; private set; }

		// 06001b72 (1.10.1 RVA 0x544A90); the native body inlines the targeting cache refresh.
		public override void CalculateCachedValues()
		{
			base.CalculateCachedValues();
			HomingDistanceSqr = HomingDistance * HomingDistance;
		}

		// 06001b73 (1.10.1 RVA 0x544B90)
		protected CharacterAbilityDefinition_RailsHoming()
		{
		}
	}
}
