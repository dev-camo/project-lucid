using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000505; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[CreateAssetMenu(fileName = "CharacterAbilityDefinition_ModifierCollectableRevive", menuName = "HardlightProject/DefinitionData/AbilityDef/ModifierCollectableRevive", order = 1)]
	public class CharacterAbilityDefinition_ModifierCollectableRevive : CharacterAbilityDefinition_Modifier
	{
		[Tooltip("Collectable type that is held on to.")]
		public CollectableType CollectableType = CollectableType.Ring;

		[Range(0f, 1f)]
		[Tooltip("Fraction of the currently held collectable type that will be lost.")]
		public float Multiplier = 0.5f;

		public override Type ScriptType
		{
			get
			{
				return typeof(CharacterAbility_ModifierCollectableRevive);
			}
		}

		// 06001b9b (1.10.1 RVA 0x5456D0)
		public CharacterAbilityDefinition_ModifierCollectableRevive()
		{
		}
	}
}
