using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020004fe; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[CreateAssetMenu(fileName = "CharacterAbilityDefinition_Running", menuName = "HardlightProject/DefinitionData/AbilityDef/Running", order = 1)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterAbilityDefinition_Running : CharacterAbilityDefinition_MovementGround
	{
		// 06001b87 (1.10.1 RVA 0x5451A0)
		public override Type ScriptType
		{
			get
			{
				return typeof(CharacterAbility_Running);
			}
		}

		// 06001b88 (1.10.1 RVA 0x545200)
		public CharacterAbilityDefinition_Running()
		{
		}
	}
}
