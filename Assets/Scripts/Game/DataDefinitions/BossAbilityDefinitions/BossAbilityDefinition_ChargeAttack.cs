using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 02000483; bodies translated from the 1.10.1 x86-64 binary (0x53E870..0x53E960).
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "BossAbilityDefinition_ChargeAttack", menuName = "HardlightProject/DefinitionData/AbilityDef/ChargeAttack", order = 1)]
	public class BossAbilityDefinition_ChargeAttack : AbilityDefinition
	{
		[Tooltip("Time to full speed.")]
		public float AccelerationTime = 0.5f;

		public float TopSpeed;

		[Tooltip("Will be evaluated from starting position to target position")]
		public AnimationCurve AccelerationCurve;

		[Tooltip("Time to decelerate after reaching the player position.")]
		public float DecelerationTime = 0.5f;

		[Tooltip("Will be evaluated by timer once reaching player position.")]
		public AnimationCurve DecelerationCurve;

		[Tooltip("Higher numbers means more tilting when moving.")]
		public float MovementAnimationAngleMultiplier = 15f;

		public float FaceAngleTolerance = 1f;

		public float TurnSpeed = 10f;

		public float DistanceAllowedFromOrigin = 30f;

		public float ConeOfVisionDistance = 50f;

		public float ConeOfVisionAngle = 35f;

		[Tooltip("After this many seconds of trying to find a clear line of sight to the player, the attack will be aborted.")]
		public float LineOfSightTimeout = 6f;

		public override Type ScriptType => typeof(BossAbility_BossFourmenChargeAttack);

		public float DistanceAllowedFromOriginSquared { get; private set; }

		public override void CalculateCachedValues()
		{
			base.CalculateCachedValues();
			DistanceAllowedFromOriginSquared = DistanceAllowedFromOrigin * DistanceAllowedFromOrigin;
		}
	}
}
