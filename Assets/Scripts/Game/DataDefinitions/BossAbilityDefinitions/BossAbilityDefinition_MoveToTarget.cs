using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 0200048e; bodies translated from the 1.10.1 x86-64 binary (0x53F050..0x53F120).
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "BossAbilityDefinition_MoveToTarget", menuName = "HardlightProject/DefinitionData/AbilityDef/MoveToTarget", order = 1)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class BossAbilityDefinition_MoveToTarget : BossAbilityDefinition_MoveTowardsTarget
	{
		[Tooltip("Higher numbers means more tilting when moving.")]
		public float MovementAnimationAngleMultiplier = 15f;

		public float FaceAngleTolerance = 1f;

		public float TurnSpeed = 10f;

		public float DistanceAllowedFromOrigin = 30f;

		public bool TargetIsBossOrigin;

		[HideIf("TargetIsBossOrigin", null)]
		public Vector3 Target;

		public override Type ScriptType => typeof(BossAbility_MoveToTargetPoint);

		public float DistanceAllowedFromOriginSquared { get; private set; }

		public override void CalculateCachedValues()
		{
			base.CalculateCachedValues();
			DistanceAllowedFromOriginSquared = DistanceAllowedFromOrigin * DistanceAllowedFromOrigin;
		}
	}
}
