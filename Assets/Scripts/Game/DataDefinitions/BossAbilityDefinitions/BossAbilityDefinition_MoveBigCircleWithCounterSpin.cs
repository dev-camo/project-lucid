using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 02000486; bodies translated from the 1.10.1 x86-64 binary (0x53EAC0..0x53EB40).
	[CreateAssetMenu(fileName = "BossAbilityDefinition_MoveBigCircleWithCounterSpin", menuName = "HardlightProject/DefinitionData/AbilityDef/MoveBigCircleWithCounterSpin", order = 1)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class BossAbilityDefinition_MoveBigCircleWithCounterSpin : AbilityDefinition
	{
		public float BigCircleSpeed;

		public float CounterRotateSpeed;

		public float FaceAngleTolerance = 10f;

		public float TurnSpeed = 10f;

		public float MovementAnimationAngleMultiplier = 50f;

		public float BigCircleRadius = 20f;

		public float CounterSpinRadius = 7f;

		public override Type ScriptType => typeof(BossAbility_MoveBigCircleWithCounterSpin);
	}
}
