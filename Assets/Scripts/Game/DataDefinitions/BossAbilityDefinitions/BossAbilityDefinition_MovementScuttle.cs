using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 02000489; bodies translated from the 1.10.1 x86-64 binary (0x53EC70..0x53ED40).
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "BossAbilityDefinition_MovementScuttle", menuName = "HardlightProject/DefinitionData/AbilityDef/Scuttle", order = 1)]
	public class BossAbilityDefinition_MovementScuttle : AbilityDefinition
	{
		public float ScuttleSpeed;

		public float ScuttleRange = 10f;

		public float ScuttleStartMinimumMovementDistance = 0.5f;

		[Tooltip("How close should we be to consider the destination 'reached'?")]
		public float ScuttleEndDistanceTolerance = 1.1f;

		[Tooltip("How long should we be at the destination before it is 'reached'?")]
		public float ScuttleDestinationReachedSeconds = 0.2f;

		public float FaceAngleTolerance = 10f;

		public float TurnSpeed = 10f;

		public AnimationCurve FullSpeedCurve;

		public float TargetDistanceForFullSpeed = 30f;

		[HideInInspector]
		public float ScuttleRangeSquared;

		[HideInInspector]
		public float ScuttleMinimumMovementDistanceSquared;

		[HideInInspector]
		public float ScuttleEndDistanceToleranceSquared;

		[HideInInspector]
		public float TargetDistanceForFullSpeedSquared;

		public override Type ScriptType => typeof(BossAbility_Scuttle);

		public override void CalculateCachedValues()
		{
			base.CalculateCachedValues();
			ScuttleRangeSquared = ScuttleRange * ScuttleRange;
			ScuttleMinimumMovementDistanceSquared = ScuttleStartMinimumMovementDistance * ScuttleStartMinimumMovementDistance;
			ScuttleEndDistanceToleranceSquared = ScuttleEndDistanceTolerance * ScuttleEndDistanceTolerance;
			TargetDistanceForFullSpeedSquared = TargetDistanceForFullSpeed * TargetDistanceForFullSpeed;
		}
	}
}
