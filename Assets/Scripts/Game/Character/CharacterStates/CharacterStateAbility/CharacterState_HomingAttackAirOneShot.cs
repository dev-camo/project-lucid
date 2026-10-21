using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000375; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("HomingAttackAirOneShot")]
	[GraphNodeMenuFormat("Character/{0}")]
	public class CharacterState_HomingAttackAirOneShot : CharacterState_HomingAttackAir<CharacterAbility_HomingAttackAirOneShot, CharacterAbilityDefinition_HomingAttackAirOneShot>
	{
		// Original nested 02000376.
		[Il2CppSetOption(Option.NullChecks, false)]
		[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
		[FiniteStateMachineJsonCtorArgs]
		[Serializable]
		protected class HomingAttackOneShotJSONCtorArgs : HomingAttackJSONCtorArgs
		{
			// 0600159a (1.10.1 RVA 0x779910)
			public HomingAttackOneShotJSONCtorArgs()
			{
			}
		}

		// 06001598 (1.10.1 RVA 0x7797E0)
		protected CharacterState_HomingAttackAirOneShot(FiniteStateMachine fsm, FSMIdentifier stateId, HomingAttackOneShotJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
		}

		// 06001599 (1.10.1 RVA 0x779850)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_HomingAttackAirOneShot(fsm, transitionId, JsonUtility.FromJson<HomingAttackOneShotJSONCtorArgs>(jsonCtorArgs));
		}
	}
}
