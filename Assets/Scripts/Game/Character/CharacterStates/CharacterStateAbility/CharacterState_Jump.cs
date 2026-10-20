using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200037c; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("Jump")]
	[GraphNodeMenuFormat("Character/{0}")]
	public class CharacterState_Jump : CharacterState_JumpBase<CharacterAbility_MovementJump, CharacterAbilityDefinition_Jump>
	{
		// Original nested 0200037d.
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[FiniteStateMachineJsonCtorArgs]
		[Serializable]
		protected class JumpJSONCtorArgs : JumpBaseJSONCtorArgs
		{
			// 060015af (1.10.1 RVA 0x77ADB0)
			public JumpJSONCtorArgs()
			{
			}
		}

		// 060015ad (1.10.1 RVA 0x77AC80)
		protected CharacterState_Jump(FiniteStateMachine fsm, FSMIdentifier stateId, JumpJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
		}

		// 060015ae (1.10.1 RVA 0x77ACF0)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_Jump(fsm, transitionId, JsonUtility.FromJson<JumpJSONCtorArgs>(jsonCtorArgs));
		}
	}
}
