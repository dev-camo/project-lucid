using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 0200048c; bodies translated from the 1.10.1 x86-64 binary (0x53EF30..0x53EFB0).
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "BossAbilityDefinition_MoveToArenaEdge", menuName = "HardlightProject/DefinitionData/AbilityDef/MoveToArenaEdge", order = 1)]
	public class BossAbilityDefinition_MoveToArenaEdge : BossAbilityDefinition_MoveTowardsTarget
	{
		public float Radius = 30f;

		public override Type ScriptType => typeof(BossAbility_MoveToArenaEdge);
	}
}
