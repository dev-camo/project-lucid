using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200050b; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "CharacterAbilityDefinition_DetectorArchetype", menuName = "HardlightProject/DefinitionData/AbilityDef/DetectorArchetype", order = 1)]
	public class CharacterAbilityDefinition_DetectorArchetype : CharacterAbilityDefinition_Detector
	{
		public CharacterArchetype Archetype;

		// 06001bab (1.10.1 RVA 0x545E80)
		public override Type ScriptType
		{
			get
			{
				return typeof(CharacterAbility_DetectorArchetype);
			}
		}

		// 06001bac (1.10.1 RVA 0x545EE0)
		public CharacterAbilityDefinition_DetectorArchetype()
		{
		}
	}
}
