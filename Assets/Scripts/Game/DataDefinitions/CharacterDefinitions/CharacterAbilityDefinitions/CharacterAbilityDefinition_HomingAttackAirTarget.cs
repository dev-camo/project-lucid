using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020004d8; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "CharacterAbilityDefinition_HomingAttackAirTarget", menuName = "HardlightProject/DefinitionData/AbilityDef/HomingAttackAirTarget", order = 1)]
	public class CharacterAbilityDefinition_HomingAttackAirTarget : CharacterAbilityDefinition_HomingAttackAir
	{
		// 06001b1b (1.10.1 RVA 0x543510)
		public override Type ScriptType
		{
			get
			{
				return typeof(CharacterAbility_HomingAttackAirTarget);
			}
		}

		// 06001b1c (1.10.1 RVA 0x543570)
		public CharacterAbilityDefinition_HomingAttackAirTarget()
		{
		}
	}
}
