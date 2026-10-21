using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 02000481; bodies translated from the 1.10.1 x86-64 binary (0x53E640..0x53E7B0).
	[CreateAssetMenu(fileName = "BossAbilityDefinition_BodySlam", menuName = "HardlightProject/DefinitionData/AbilityDef/BodySlam", order = 1)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class BossAbilityDefinition_BodySlam : AbilityDefinition
	{
		[Tooltip("Time to complete attack as defined by the animation and animator.")]
		public float AttackAnimationLength = 1f;

		[Tooltip("Time to spawn the wave (line up with moment the boss hits the ground).")]
		public float TriggerWaveTime = 0.8f;

		[Tooltip("Time the boss should be traversing X&Z across the ground.")]
		public float TraversalTime = 0.7f;

		[Tooltip("Curve affecting X&Z movement amount.")]
		public AnimationCurve TraversalCurve;

		[Tooltip("Further than this means no target.")]
		public float TargetValidRangeFar;

		[Tooltip("Closer than this means no target, unless player is within targeting cone.")]
		public float TargetValidRangeNear;

		[Tooltip("Meters of range growth per second when player is out of range.")]
		public float TargetGrowthPerSecond;

		[Tooltip("Tolerance in degrees by which the boss will stop rotating towards the player.")]
		public float FaceAngleTolerance = 10f;

		[Tooltip("Apply a custom face angle tolerance to the first body slam.")]
		public bool CustomInitialFaceAngleTolerance;

		[ShowIf("CustomInitialFaceAngleTolerance", null)]
		public float InitialFaceAngleTolerance;

		[Tooltip("The speed at which the boss will rotate towards the player until it meets the angle tolerance.")]
		public float TurnSpeed = 10f;

		[Tooltip("Prefab of projectile.")]
		public PrefabPoolType Projectile;

		[Tooltip("Lifetime of projectile, will despawn after this time expires.")]
		public float ProjectileLifetime;

		[Tooltip("Prefab of CameraTarget.")]
		public PrefabPoolType CameraTarget;

		[Tooltip("Lifetime of camera target, will despawn after this time expires.")]
		public float CameraTargetLifetime;

		[Tooltip("Distance from origin of each point.")]
		public float TriangleJumpDistance = 30f;

		[Tooltip("Degree offset of jumps 2 & 3. Will be +- to make a triangle.")]
		public float AngleOfJumps = 36f;

		[Tooltip("Triangle rotation degrees per cycle.")]
		public float MinTriangleRotationDegreesPerCycle = 30f;

		[Tooltip("Triangle rotation degrees per cycle.")]
		public float MaxTriangleRotationDegreesPerCycle = 90f;

		public override Type ScriptType => typeof(BossAbility_BodySlam);

		public float TargetValidRangeNearSquared { get; private set; }
		public float MinTriangleRotationRadiansPerCycle { get; private set; }
		public float MaxTriangleRotationRadiansPerCycle { get; private set; }
		public float AngleOfJumpsAsRadians { get; private set; }

		public override void CalculateCachedValues()
		{
			base.CalculateCachedValues();
			TargetValidRangeNearSquared = TargetValidRangeNear * TargetValidRangeNear;
			MinTriangleRotationRadiansPerCycle = MinTriangleRotationDegreesPerCycle * Mathf.Deg2Rad;
			MaxTriangleRotationRadiansPerCycle = MaxTriangleRotationDegreesPerCycle * Mathf.Deg2Rad;
			AngleOfJumpsAsRadians = AngleOfJumps * Mathf.Deg2Rad;
		}
	}
}
