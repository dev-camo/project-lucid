using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 02000497; bodies translated from the 1.10.1 x86-64 binary (0x53F7D0..0x53F860).
	[CreateAssetMenu(fileName = "BossAbilityDefinition_SpinOnSpot", menuName = "HardlightProject/DefinitionData/AbilityDef/SpinOnSpot", order = 1)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class BossAbilityDefinition_SpinOnSpot : AbilityDefinition
	{
		public AnimationCurve SpinRotationCurve;

		[Tooltip("Evaluated against curve.")]
		public float TimeToFullyRotateInSeconds = 1f;

		public int RotationsUntilComplete = 3;

		[Tooltip("What direction should we be facing when complete?")]
		public int DesiredEndRotation;

		[Tooltip("End rotation calculation will be satisfied within +- of this value")]
		public int DesiredEndRotationToleranceInDegrees = 1;

		public bool Clockwise;

		public override Type ScriptType => typeof(BossAbility_SpinOnSpot);
	}
}
