using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020004d6; bodies translated from the 1.10.1 x86-64 binary.
	[CreateAssetMenu(fileName = "CharacterAbilityDefinition_HomingAttackAirLightspeedDash", menuName = "HardlightProject/DefinitionData/AbilityDef/HomingAttackAirLightspeedDash", order = 1)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterAbilityDefinition_HomingAttackAirLightspeedDash : CharacterAbilityDefinition_HomingAttackAir
	{
		// 06001b17 (1.10.1 RVA 0x543370)
		public override Type ScriptType
		{
			get
			{
				return typeof(CharacterAbility_HomingAttackAirLightspeedDash);
			}
		}

		// 06001b18 (1.10.1 RVA 0x5433D0)
		public CharacterAbilityDefinition_HomingAttackAirLightspeedDash()
		{
		}
	}
}
