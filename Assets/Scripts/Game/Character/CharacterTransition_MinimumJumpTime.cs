using Unity.IL2CPP.CompilerServices;
using Hardlight;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020003f8; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("MinimumJumpTime")]
	[GraphNodeMenuFormat("Character/{0}")]
	public class CharacterTransition_MinimumJumpTime : Transition_Ability
	{
		// 06001776 (1.10.1 RVA 0x7930F0)
		protected CharacterTransition_MinimumJumpTime(FiniteStateMachine fsm, FSMIdentifier transitionId, AbilityJSONCtorArgs ctorArgs) : base(fsm, transitionId, ctorArgs)
		{
		}

		// 06001777 (1.10.1 RVA 0x793120)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterTransition_MinimumJumpTime(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
		}

		// 06001778 (1.10.1 RVA 0x7931C0); true once the jump has lasted its minimum time. A missing jump
		// ability never passes, and an unordered comparison (NaN) fails.
		protected override bool DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
		{
			if (base.DoUpdate(user, updateContext))
			{
				return true;
			}
			Character character = user.GetAs<Character>();
			CharacterAbility_MovementJump jumpAbility = character.GetAbility<CharacterAbility_MovementJump>(m_abilityType);
			if (jumpAbility == null)
			{
				return false;
			}
			return character.Storage.GetValue(ActorFSMKeys.JumpElapsedTime, 0f, true) >= jumpAbility.Definition.MinimumJumpTimeSeconds;
		}
	}
}
