using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020004ec.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "CharacterAbilityDefinition_MovementGroundBoost", menuName = "HardlightProject/DefinitionData/AbilityDef/MovementGroundBoost", order = 1)]
	public class CharacterAbilityDefinition_MovementGroundBoost : CharacterAbilityDefinition_MovementGroundBoostBase
	{
		public override Type ScriptType
		{
			get
			{
				return typeof(CharacterAbility_MovementGroundBoost);
			}
		}

		// 06001b55 (1.10.1 RVA 0x5442F0)
		public CharacterAbilityDefinition_MovementGroundBoost()
		{
		}
	}
}
