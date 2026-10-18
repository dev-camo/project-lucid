using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000509; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "CharacterAbilityDefinition_ModifierSwitchAvailable", menuName = "HardlightProject/DefinitionData/AbilityDef/ModifierSwitch", order = 1)]
	public class CharacterAbilityDefinition_ModifierSwitchAvailable : CharacterAbilityDefinition_Modifier
	{
		public override Type ScriptType
		{
			get
			{
				return typeof(CharacterAbility_ModifierSwitchAvailable);
			}
		}

		// 06001ba6 (1.10.1 RVA 0x545D90)
		public CharacterAbilityDefinition_ModifierSwitchAvailable()
		{
		}
	}
}
