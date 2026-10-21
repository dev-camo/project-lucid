using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200038b; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("RailHomingProximityGround")]
	[GraphNodeMenuFormat("Character/{0}")]
	public class CharacterState_RailHomingProximityGround : CharacterState_RailHoming<CharacterAbility_RailsProximityGround, CharacterAbilityDefinition_RailsProximityGround>
	{
		// 060015de (1.10.1 RVA 0x77DD80)
		protected CharacterState_RailHomingProximityGround(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
		}

		// 060015df (1.10.1 RVA 0x77DDF0)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_RailHomingProximityGround(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
		}
	}
}
