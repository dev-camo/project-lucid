using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020004d3; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "CharacterAbilityDefinition_HalfPipeTransfer", menuName = "HardlightProject/DefinitionData/AbilityDef/HalfPipeTransfer", order = 1)]
	public class CharacterAbilityDefinition_HalfPipeTransfer : CharacterAbilityDefinition_HalfPipe
	{
		// 06001b12 (1.10.1 RVA 0x543160)
		public override Type ScriptType
		{
			get
			{
				return typeof(CharacterAbility_HalfPipeTransfer);
			}
		}

		// 06001b13 (1.10.1 RVA 0x5431C0)
		public CharacterAbilityDefinition_HalfPipeTransfer()
		{
		}
	}
}
