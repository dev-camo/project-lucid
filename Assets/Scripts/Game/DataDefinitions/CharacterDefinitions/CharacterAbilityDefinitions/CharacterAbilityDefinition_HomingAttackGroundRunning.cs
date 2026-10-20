using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020004dc; bodies translated from the 1.10.1 x86-64 binary.
	[CreateAssetMenu(fileName = "CharacterAbilityDefinition_HomingAttackGroundRunning", menuName = "HardlightProject/DefinitionData/AbilityDef/HomingAttackGroundRunning", order = 1)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterAbilityDefinition_HomingAttackGroundRunning : CharacterAbilityDefinition_HomingAttackGround
	{
		// 06001b23 (1.10.1 RVA 0x543780)
		public override Type ScriptType
		{
			get
			{
				return typeof(CharacterAbility_HomingAttackGroundRunning);
			}
		}

		// 06001b24 (1.10.1 RVA 0x5437E0)
		public CharacterAbilityDefinition_HomingAttackGroundRunning()
		{
		}
	}
}
