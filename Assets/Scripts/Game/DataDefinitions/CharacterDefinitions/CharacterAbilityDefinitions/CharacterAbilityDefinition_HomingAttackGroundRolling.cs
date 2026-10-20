using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020004db; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "CharacterAbilityDefinition_HomingAttackGroundRolling", menuName = "HardlightProject/DefinitionData/AbilityDef/HomingAttackGroundRolling", order = 1)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterAbilityDefinition_HomingAttackGroundRolling : CharacterAbilityDefinition_HomingAttackGround
	{
		// 06001b21 (1.10.1 RVA 0x5436B0)
		public override Type ScriptType
		{
			get
			{
				return typeof(CharacterAbility_HomingAttackGroundRolling);
			}
		}

		// 06001b22 (1.10.1 RVA 0x543710)
		public CharacterAbilityDefinition_HomingAttackGroundRolling()
		{
		}
	}
}
