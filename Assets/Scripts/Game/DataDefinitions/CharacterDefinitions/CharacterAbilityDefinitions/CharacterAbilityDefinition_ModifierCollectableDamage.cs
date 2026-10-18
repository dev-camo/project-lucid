using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000504; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "CharacterAbilityDefinition_ModifierCollectableDamage", menuName = "HardlightProject/DefinitionData/AbilityDef/ModifierCollectableDamage", order = 1)]
	public class CharacterAbilityDefinition_ModifierCollectableDamage : CharacterAbilityDefinition_Modifier
	{
		[Tooltip("Collectable type that is lost when taking damage.")]
		public CollectableType CollectableType = CollectableType.Ring;

		[Tooltip("Maximum amount that can be lost to non-instant death hazards.")]
		public int MaxDamage = 10;

		public override Type ScriptType
		{
			get
			{
				return typeof(CharacterAbility_ModifierCollectableDamage);
			}
		}

		// 06001b99 (1.10.1 RVA 0x5455E0)
		public CharacterAbilityDefinition_ModifierCollectableDamage()
		{
		}
	}
}
