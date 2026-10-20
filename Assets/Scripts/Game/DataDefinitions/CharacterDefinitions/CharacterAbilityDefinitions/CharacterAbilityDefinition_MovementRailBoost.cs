using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020004f1; bodies translated from the 1.10.1 x86-64 binary.
	[CreateAssetMenu(fileName = "CharacterAbilityDefinition_MovementRailBoost", menuName = "HardlightProject/DefinitionData/AbilityDef/MovementRailBoost", order = 1)]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterAbilityDefinition_MovementRailBoost : CharacterAbilityDefinition_MovementRailBoostBase
	{
		public override Type ScriptType
		{
			get
			{
				return typeof(CharacterAbility_MovementRailBoost);
			}
		}

		// 06001b65 (1.10.1 RVA 0x5447A0)
		public CharacterAbilityDefinition_MovementRailBoost()
		{
		}
	}
}
