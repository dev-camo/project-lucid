using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000503.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	[CreateAssetMenu(fileName = "CharacterAbilityDefinition_ModifierCollectableBoost", menuName = "HardlightProject/DefinitionData/AbilityDef/ModifierCollectableBoost", order = 1)]
	public class CharacterAbilityDefinition_ModifierCollectableBoost : CharacterAbilityDefinition_Modifier
	{
		[Tooltip("Collectable type that will give additional boost stamina.")]
		public CollectableType CollectableType = CollectableType.Ring;

		[Tooltip("Stamina gained from each collected type.")]
		public float StaminaMultiplier = 0.01f;

		[Tooltip("Defines audio and pfx to trigger on collection.")]
		public ActorAnimationDefinition CollectAnimationDefinition;

		public override Type ScriptType
		{
			get
			{
				return typeof(CharacterAbility_ModifierCollectableBoost);
			}
		}

		// 06001b97 (1.10.1 RVA 0x5454F0)
		public CharacterAbilityDefinition_ModifierCollectableBoost()
		{
		}
	}
}
