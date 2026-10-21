using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 0200048b; bodies translated from the 1.10.1 x86-64 binary (0x53EDC0..0x53EF30).
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	[CreateAssetMenu(fileName = "BossAbilityDefinition_MoveRandom", menuName = "HardlightProject/DefinitionData/AbilityDef/Random", order = 1)]
	public class BossAbilityDefinition_MoveRandom : AbilityDefinition
	{
		public float MinimumSpeed;

		public float MaximumSpeed;

		public float AccelerationPerSecond;

		public float MinDistanceTargetX = 3f;

		[Tooltip("How close should we be to consider the destination 'reached'?")]
		public float EndDistanceTolerance = 1.1f;

		public float FaceAngleTolerance = 10f;

		public float TurnSpeed = 10f;

		public float DistanceAllowedFromOrigin = 25f;

		[Tooltip("Higher numbers means more tilting when moving.")]
		public float MovementAnimationAngleMultiplier = 50f;

		[Tooltip("How much further 'in' should final targets be when compared to radius? Keeps bosses turning inwards towards the center.")]
		[Range(1.1f, 1.9f)]
		public float FinalTargetRadiusMultiplier = 1.5f;

		public override Type ScriptType => typeof(BossAbility_MoveRandom);

		public float DistanceAllowedFromOriginSquared { get; private set; }
		public float DistanceAllowedFromOriginAdjustedForDiameter { get; private set; }
		public float DistanceAllowedFromOriginAdjustedForDiameterSquared { get; private set; }
		public float DistanceAllowedFromOriginAdjustedForRadius { get; private set; }

		public override void CalculateCachedValues()
		{
			base.CalculateCachedValues();
			DistanceAllowedFromOriginSquared = DistanceAllowedFromOrigin * DistanceAllowedFromOrigin;
			// Single precision throughout, as in native (mulss, then subss).
			DistanceAllowedFromOriginAdjustedForDiameter = DistanceAllowedFromOrigin - FinalTargetRadiusMultiplier * MinDistanceTargetX;
			DistanceAllowedFromOriginAdjustedForDiameterSquared = DistanceAllowedFromOriginAdjustedForDiameter * DistanceAllowedFromOriginAdjustedForDiameter;
			DistanceAllowedFromOriginAdjustedForRadius = DistanceAllowedFromOrigin - MinDistanceTargetX;
		}
	}
}
