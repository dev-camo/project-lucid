using Unity.IL2CPP.CompilerServices;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020004fc; bodies translated from the 1.10.1 x86-64 binary.
	[CreateAssetMenu(fileName = "CharacterAbilityDefinition_RailSwitch", menuName = "HardlightProject/DefinitionData/AbilityDef/RailSwitch", order = 1)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterAbilityDefinition_RailSwitch : CharacterAbilityDefinition_Rails
	{
		public override Type ScriptType
		{
			get
			{
				return typeof(CharacterAbility_RailSwitch);
			}
		}

		[Tooltip("The angle to project to look for a target rail.")]
		[Min(0f)]
		public float RailProjectAngle = 45f;

		[Min(0f)]
		[Tooltip("The distance to project to look for a target rail.")]
		public float RailProjectDistance = 10f;

		[Min(0f)]
		[Tooltip("Input threshold to activate switch.")]
		public float ActivationInputThreshold = 0.5f;

		[Min(0f)]
		[Tooltip("Input timer to activate switch.")]
		public float ActivationInputTime = 1f;

		[Tooltip("Time vs height for the rail jump.")]
		public AnimationCurve JumpCurve;

		[Min(0f)]
		[Tooltip("Cooldown timer before allowing a subsequent turn around.")]
		public float TurnAroundCooldown = 0.1f;

		// 06001b81 (1.10.1 RVA 0x544FF0); the private setter is 06001b82 (RVA 0x545000). Declared here because the
		// backing field sits between TurnAroundCooldown and TriggerAbilities in the original field order.
		public float RailProjectDistanceSqr { get; private set; }

		[Tooltip("Abilities that are eligible to trigger a rail switch if activated in the grace period.")]
		public List<ActorAbilityType> TriggerAbilities = new List<ActorAbilityType>();

		[Tooltip("Period of time to allow a rail switch after leaving any of the above abilities.")]
		public float TriggerGracePeriod = 0.5f;

		[Tooltip("Whether to use game time scaling.")]
		public bool UseTimeScaling;

		// 06001b83 (1.10.1 RVA 0x545010)
		public override void CalculateCachedValues()
		{
			base.CalculateCachedValues();
			RailProjectDistanceSqr = RailProjectDistance * RailProjectDistance;
		}

		// 06001b84 (1.10.1 RVA 0x545040)
		public CharacterAbilityDefinition_RailSwitch()
		{
		}
	}
}
