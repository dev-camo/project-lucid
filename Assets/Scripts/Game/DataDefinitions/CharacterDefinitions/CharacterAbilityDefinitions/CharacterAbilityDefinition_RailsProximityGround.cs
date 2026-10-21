using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020004fa; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "CharacterAbilityDefinition_RailsProximityGround", menuName = "HardlightProject/DefinitionData/AbilityDef/RailsProximityGround", order = 1)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterAbilityDefinition_RailsProximityGround : CharacterAbilityDefinition_RailsHoming
	{
		// 06001b79 (1.10.1 RVA 0x544D40)
		public override Type ScriptType
		{
			get
			{
				return typeof(CharacterAbility_RailsProximityGround);
			}
		}

		// 06001b7a (1.10.1 RVA 0x544DA0)
		public CharacterAbilityDefinition_RailsProximityGround()
		{
		}
	}
}
