using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000377; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Character/{0}")]
	[GraphNodeDefaultName("HomingAttackAirTarget")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterState_HomingAttackAirTarget : CharacterState_HomingAttackAir<CharacterAbility_HomingAttackAirTarget, CharacterAbilityDefinition_HomingAttackAirTarget>
	{
		// Original nested 02000378.
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[FiniteStateMachineJsonCtorArgs]
		[Serializable]
		protected class HomingAttackTargetJSONCtorArgs : HomingAttackJSONCtorArgs
		{
			// 0600159d (1.10.1 RVA 0x779A80)
			public HomingAttackTargetJSONCtorArgs()
			{
			}
		}

		// 0600159b (1.10.1 RVA 0x779950)
		protected CharacterState_HomingAttackAirTarget(FiniteStateMachine fsm, FSMIdentifier stateId, HomingAttackTargetJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
		}

		// 0600159c (1.10.1 RVA 0x7799C0)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_HomingAttackAirTarget(fsm, transitionId, JsonUtility.FromJson<HomingAttackTargetJSONCtorArgs>(jsonCtorArgs));
		}
	}
}
