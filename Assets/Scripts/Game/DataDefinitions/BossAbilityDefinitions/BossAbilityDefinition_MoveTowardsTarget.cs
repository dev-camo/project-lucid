using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 02000490; bodies translated from the 1.10.1 x86-64 binary (.ctor 0x53EFB0, get_ScriptType 0x53F1A0).
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "BossAbilityDefinition_MoveTowardsTarget", menuName = "HardlightProject/DefinitionData/AbilityDef/MoveTowardsTarget", order = 1)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class BossAbilityDefinition_MoveTowardsTarget : AbilityDefinition
	{
		[Tooltip("Time to full speed.")]
		public float AccelerationTime = 0.5f;

		public float TopSpeed;

		[Tooltip("Will be evaluated from starting position to target position")]
		public AnimationCurve AccelerationCurve;

		public override Type ScriptType => typeof(BossAbility_MoveTowardsTarget);
	}
}
