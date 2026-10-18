using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020003d4; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Character/{0}")]
	[GraphNodeDefaultName("ExitBurstAttack")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterTransition_ExitBurstAttack : Transition_Ability
	{
		// 0600171b (1.10.1 RVA 0x790030)
		protected CharacterTransition_ExitBurstAttack(FiniteStateMachine fsm, FSMIdentifier transitionId, AbilityJSONCtorArgs ctorArgs) : base(fsm, transitionId, ctorArgs)
		{
		}

		// 0600171c (1.10.1 RVA 0x790060)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterTransition_ExitBurstAttack(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
		}

		// 0600171d (1.10.1 RVA 0x790100); true once the burst attack has run for its duration.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
		{
			if (base.DoUpdate(user, context))
			{
				return true;
			}
			Character character = user.GetAs<Character>();
			if (character.TryGetAbilityFromType(m_abilityType, out ActorAbility ability) && ability.DefinitionBase is CharacterAbilityDefinition_BurstAttack definition)
			{
				return character.Storage.GetValue(ActorFSMKeys.BurstAttackDurationTimer, 0f, true) >= definition.DurationSeconds;
			}
			return false;
		}
	}
}
