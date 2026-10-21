using System;
using Hardlight;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace HardlightProject
{
	// Original Game.Runtime 02000496; bodies translated from the 1.10.1 x86-64 binary (0x53F750..0x53F7D0).
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "BossAbilityDefinition_RegrowClaw", menuName = "HardlightProject/DefinitionData/AbilityDef/RegrowClaw", order = 1)]
	public class BossAbilityDefinition_RegrowClaw : AbilityDefinition
	{
		public int UsesToTriggerRegrowth = 2;

		public override Type ScriptType => typeof(BossAbility_RegrowClaw);
	}
}
