using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 0200048f; bodies translated from the 1.10.1 x86-64 binary (0x53F120..0x53F1A0).
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "BossAbilityDefinition_MoveToTriangleFormation", menuName = "HardlightProject/DefinitionData/AbilityDef/MoveToTriangleFormation", order = 1)]
	public class BossAbilityDefinition_MoveToTriangleFormation : BossAbilityDefinition_MoveTowardsTarget
	{
		public float StartingRadius;

		public override Type ScriptType => typeof(BossAbility_MoveToTriangleFormation);
	}
}
