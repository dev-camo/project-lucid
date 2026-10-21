using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000353; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("AirChaosAim")]
	[GraphNodeMenuFormat("Character/{0}")]
	public class CharacterState_AirChaosAim : CharacterState_AirChaos<CharacterAbility_AirChaosAim, CharacterAbilityDefinition_AirChaosAim>
	{
		// 0600151d (1.10.1 RVA 0x774DE0)
		protected CharacterState_AirChaosAim(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
		}

		// 0600151e (1.10.1 RVA 0x774E50)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_AirChaosAim(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
		}
	}
}
