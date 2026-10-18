using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020004ca; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "CharacterAbilityDefinition_ClimbingGround", menuName = "HardlightProject/DefinitionData/AbilityDef/ClimbingGround", order = 1)]
	public class CharacterAbilityDefinition_ClimbingGround : CharacterAbilityDefinition_EnterClimbing
	{
		[Header("Climb from ground properties")]
		[Tooltip("When character running velocity falls below this threshold, climbing can be triggered.")]
		[Range(0f, 200f)]
		public float VelocityThresholdGround = 10f;

		// 06001b05 (1.10.1 RVA 0x542F30)
		public override Type ScriptType
		{
			get
			{
				return typeof(CharacterAbility_ClimbingGround);
			}
		}

		// 06001b06 (1.10.1 RVA 0x542F90)
		public CharacterAbilityDefinition_ClimbingGround()
		{
		}
	}
}
