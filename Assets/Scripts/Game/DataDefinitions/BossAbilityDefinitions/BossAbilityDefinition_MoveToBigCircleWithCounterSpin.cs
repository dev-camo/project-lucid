using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 0200048d; bodies translated from the 1.10.1 x86-64 binary (0x53EFD0..0x53F050).
	[Il2CppSetOption(Option.NullChecks, false)]
	[CreateAssetMenu(fileName = "BossAbilityDefinition_MoveBigCircleWithCounterSpin", menuName = "HardlightProject/DefinitionData/AbilityDef/MoveToBigCircleWithCounterSpin", order = 1)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class BossAbilityDefinition_MoveToBigCircleWithCounterSpin : BossAbilityDefinition_MoveTowardsTarget
	{
		public float BigCircleRadius = 20f;

		public float CounterSpinRadius = 7f;

		public override Type ScriptType => typeof(BossAbility_MoveToBigCircleWithCounterSpin);
	}
}
