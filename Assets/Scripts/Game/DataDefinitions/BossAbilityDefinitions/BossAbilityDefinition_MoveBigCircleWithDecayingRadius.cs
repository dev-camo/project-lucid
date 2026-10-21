using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 02000487; bodies translated from the 1.10.1 x86-64 binary (0x53EB40..0x53EBC0).
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "BossAbilityDefinition_MoveBigCircleWithDecayingRadius", menuName = "HardlightProject/DefinitionData/AbilityDef/MoveBigCircleWithDecayingRadius", order = 1)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class BossAbilityDefinition_MoveBigCircleWithDecayingRadius : AbilityDefinition
	{
		public float MaxMovementSpeed;

		public AnimationCurve MovementSpeedCurve;

		[Tooltip("To line up with art placement of charging ports")]
		public float EndingRadius = 3f;

		public float StartingRadius = 25f;

		public AnimationCurve RadiusDecayCurve;

		public float TimeToDecayFully;

		[Tooltip("How close should we be to consider the destination 'reached'?")]
		public float EndDistanceTolerance = 1.1f;

		public override Type ScriptType => typeof(BossAbility_MoveBigCircleWithDecayingRadius);
	}
}
