using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 020003e9; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Character/{0}")]
	[GraphNodeDefaultName("ToJump")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterTransition_Jump : CharacterTransition_JumpBase<CharacterAbility_MovementJump, CharacterAbilityDefinition_Jump>
	{
		// Original nested 020003ea.
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[FiniteStateMachineJsonCtorArgs]
		[Serializable]
		protected class JumpTransitionJSONCtorArgs : JumpTransitionBaseJSONCtorArgs
		{
			// 06001755 (1.10.1 RVA 0x792120)
			public JumpTransitionJSONCtorArgs()
			{
			}
		}

		// 06001753 (1.10.1 RVA 0x791FF0)
		protected CharacterTransition_Jump(FiniteStateMachine fsm, FSMIdentifier transitionId, JumpTransitionJSONCtorArgs ctorArgs) : base(fsm, transitionId, ctorArgs)
		{
		}

		// 06001754 (1.10.1 RVA 0x792060)
		public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterTransition_Jump(fsm, transitionId, JsonUtility.FromJson<JumpTransitionJSONCtorArgs>(jsonCtorArgs));
		}
	}
}
