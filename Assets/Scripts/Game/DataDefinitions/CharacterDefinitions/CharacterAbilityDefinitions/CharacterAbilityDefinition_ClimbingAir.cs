using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020004c8; bodies translated from the 1.10.1 x86-64 binary.
	[CreateAssetMenu(fileName = "CharacterAbilityDefinition_ClimbingAir", menuName = "HardlightProject/DefinitionData/AbilityDef/ClimbingAir", order = 1)]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterAbilityDefinition_ClimbingAir : CharacterAbilityDefinition_EnterClimbing
	{
		[Range(0f, 90f)]
		[Header("Climb from air properties")]
		[Tooltip("Minimum angle of attack on target surface to allow climbing to be entered without input.\nA larger value here means you have to approach more head-on, smaller allows a more parallel approach.")]
		public float MinAngleOfAttack = 45f;

		// 06001b01 (1.10.1 RVA 0x542D70)
		public override Type ScriptType
		{
			get
			{
				return typeof(CharacterAbility_ClimbingAir);
			}
		}

		// 06001b02 (1.10.1 RVA 0x542DD0)
		public CharacterAbilityDefinition_ClimbingAir()
		{
		}
	}
}
