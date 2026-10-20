using Unity.IL2CPP.CompilerServices;
using System;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000502.
	[CreateAssetMenu(fileName = "CharacterAbilityDefinition_ModifierCollectableAttract", menuName = "HardlightProject/DefinitionData/AbilityDef/ModifierCollectableAttract", order = 1)]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterAbilityDefinition_ModifierCollectableAttract : CharacterAbilityDefinition_Modifier
	{
		[Tooltip("Collectable types to set attract tier on.  NB: Specifying none will default to applying to all types.")]
		public CollectableType[] CollectableTypes;

		[Tooltip("Collectable attract collision trigger tier index.  NB: 0 based.")]
		public int TierIndex;

		[Tooltip("Defines audio and pfx to trigger on collection.")]
		public ActorAnimationDefinition CollectAnimationDefinition;

		public override Type ScriptType
		{
			get
			{
				return typeof(CharacterAbility_ModifierCollectableAttract);
			}
		}

		// 06001b95 (1.10.1 RVA 0x545410)
		public CharacterAbilityDefinition_ModifierCollectableAttract()
		{
		}
	}
}
