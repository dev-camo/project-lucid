using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 02000484; bodies translated from the 1.10.1 x86-64 binary (0x53E960..0x53EA30).
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "BossAbilityDefinition_DeathFlight", menuName = "HardlightProject/DefinitionData/AbilityDef/Death", order = 1)]
	public class BossAbilityDefinition_DeathFlight : AbilityDefinition
	{
		[Tooltip("Rate of rotation per axis.")]
		public Vector3 SpinRateDegreesPerSecond = Vector3.up;

		[Tooltip("Starting speed for the boss to fly away.")]
		public float MovementRateMetersPerSecond = 5f;

		[Tooltip("Speed the boss will accelerate.")]
		public float AccelerationMetersPerSecond = 2f;

		[Tooltip("Curve affecting movement Y.")]
		public AnimationCurve HeightCurve;

		public override Type ScriptType => typeof(BossAbility_DeathFlight);
	}
}
