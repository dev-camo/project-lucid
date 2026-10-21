using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 02000488; bodies translated from the 1.10.1 x86-64 binary (0x53EBC0..0x53EC70).
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "BossAbilityDefinition_MovementRetreatingJump", menuName = "HardlightProject/DefinitionData/AbilityDef/RetreatingJump", order = 1)]
	public class BossAbilityDefinition_MovementRetreatingJump : AbilityDefinition
	{
		public AnimationCurve JumpHeightCurve;

		public AnimationCurve JumpSpeedCurve;

		public float JumpMaxHeight;

		public float MiniumJumpRange = 35f;

		public float FaceAngleTolerance = 10f;

		public float TurnSpeed = 10f;

		[HideInInspector]
		public float MiniumJumpRangeSquared;

		public override Type ScriptType => typeof(BossAbility_RetreatingJump);

		public override void CalculateCachedValues()
		{
			base.CalculateCachedValues();
			MiniumJumpRangeSquared = MiniumJumpRange * MiniumJumpRange;
		}
	}
}
