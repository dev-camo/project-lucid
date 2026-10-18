using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020004c9; bodies translated from the 1.10.1 x86-64 binary.
	[CreateAssetMenu(fileName = "CharacterAbilityDefinition_ClimbingDirectionOverride", menuName = "HardlightProject/DefinitionData/AbilityDef/ClimbingDirectionOverride", order = 1)]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterAbilityDefinition_ClimbingDirectionOverride : CharacterAbilityDefinition_EnterClimbing
	{
		// 06001b03 (1.10.1 RVA 0x542E80)
		public override Type ScriptType
		{
			get
			{
				return typeof(CharacterAbility_ClimbingDirectionOverride);
			}
		}

		// 06001b04 (1.10.1 RVA 0x542EE0)
		public CharacterAbilityDefinition_ClimbingDirectionOverride()
		{
		}
	}
}
