using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020004d7; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "CharacterAbilityDefinition_HomingAttackAirOneShot", menuName = "HardlightProject/DefinitionData/AbilityDef/HomingAttackAirOneShot", order = 1)]
	public class CharacterAbilityDefinition_HomingAttackAirOneShot : CharacterAbilityDefinition_HomingAttackAir
	{
		[Tooltip("Period of time to allow a queued activation before being enabled.")]
		public float TriggerTimeBefore;

		[Tooltip("Period of time to allow a queued activation after being enabled.")]
		public float TriggerTimeAfter;

		// 06001b19 (1.10.1 RVA 0x543440)
		public override Type ScriptType
		{
			get
			{
				return typeof(CharacterAbility_HomingAttackAirOneShot);
			}
		}

		// 06001b1a (1.10.1 RVA 0x5434A0)
		public CharacterAbilityDefinition_HomingAttackAirOneShot()
		{
		}
	}
}
