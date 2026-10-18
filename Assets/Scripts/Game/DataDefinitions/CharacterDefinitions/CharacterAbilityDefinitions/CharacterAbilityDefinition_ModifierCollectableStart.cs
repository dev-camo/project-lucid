using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000506; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "CharacterAbilityDefinition_ModifierCollectableStart", menuName = "HardlightProject/DefinitionData/AbilityDef/ModifierCollectableStart", order = 1)]
	public class CharacterAbilityDefinition_ModifierCollectableStart : CharacterAbilityDefinition_Modifier
	{
		[Tooltip("Collectable type that is awarded at the start of a mission.")]
		public CollectableType CollectableType = CollectableType.Ring;

		[Tooltip("Amount awarded at the start of a mission.")]
		public int StartingCount = 100;

		public override Type ScriptType
		{
			get
			{
				return typeof(CharacterAbility_ModifierCollectableStart);
			}
		}

		// 06001b9d (1.10.1 RVA 0x5457C0)
		public CharacterAbilityDefinition_ModifierCollectableStart()
		{
		}
	}
}
