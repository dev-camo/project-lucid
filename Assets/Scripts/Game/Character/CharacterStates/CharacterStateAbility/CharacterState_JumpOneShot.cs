using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000380; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("JumpOneShot")]
	[GraphNodeMenuFormat("Character/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterState_JumpOneShot : CharacterState_JumpBase<CharacterAbility_MovementJumpOneShot, CharacterAbilityDefinition_JumpOneShot>
	{
		// Original nested 02000381.
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[FiniteStateMachineJsonCtorArgs]
		[Serializable]
		protected class JumpOneShotJSONCtorArgs : JumpBaseJSONCtorArgs
		{
			// 060015b9 (1.10.1 RVA 0x77AF20)
			public JumpOneShotJSONCtorArgs()
			{
			}
		}

		// 060015b7 (1.10.1 RVA 0x77ADF0)
		protected CharacterState_JumpOneShot(FiniteStateMachine fsm, FSMIdentifier stateId, JumpOneShotJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
		}

		// 060015b8 (1.10.1 RVA 0x77AE60)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_JumpOneShot(fsm, transitionId, JsonUtility.FromJson<JumpOneShotJSONCtorArgs>(jsonCtorArgs));
		}
	}
}
