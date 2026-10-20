using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020004f4; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[CreateAssetMenu(fileName = "CharacterAbilityDefinition_MovementRailStanding", menuName = "HardlightProject/DefinitionData/AbilityDef/MovementRailStanding", order = 1)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterAbilityDefinition_MovementRailStanding : CharacterAbilityDefinition_MovementRail
	{
		public override Type ScriptType
		{
			get
			{
				return typeof(CharacterAbility_MovementRailStanding);
			}
		}

		// 06001b6b (1.10.1 RVA 0x544970)
		public CharacterAbilityDefinition_MovementRailStanding()
		{
		}
	}
}
