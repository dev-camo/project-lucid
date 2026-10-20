using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020003ee; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[GraphNodeDefaultName("ToJumpOneShot")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeMenuFormat("Character/{0}")]
	public class CharacterTransition_JumpOneShot : CharacterTransition_JumpBase<CharacterAbility_MovementJumpOneShot, CharacterAbilityDefinition_JumpOneShot>
	{
		// Original nested 020003ef.
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[Il2CppSetOption(Option.NullChecks, false)]
		[FiniteStateMachineJsonCtorArgs]
		[Serializable]
		protected class JumpOneShotTransitionJSONCtorArgs : JumpTransitionBaseJSONCtorArgs
		{
			// 06001761 (1.10.1 RVA 0x792540)
			public JumpOneShotTransitionJSONCtorArgs()
			{
			}
		}

		// 0600175f (1.10.1 RVA 0x792410)
		protected CharacterTransition_JumpOneShot(FiniteStateMachine fsm, FSMIdentifier transitionId, JumpOneShotTransitionJSONCtorArgs ctorArgs) : base(fsm, transitionId, ctorArgs)
		{
		}

		// 06001760 (1.10.1 RVA 0x792480)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterTransition_JumpOneShot(fsm, transitionId, JsonUtility.FromJson<JumpOneShotTransitionJSONCtorArgs>(jsonCtorArgs));
		}
	}
}
