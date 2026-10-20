using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000391; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("RailMovementStanding")]
	[GraphNodeMenuFormat("Character/{0}")]
	public class CharacterState_RailMovementStanding : CharacterState_RailMovement<CharacterAbility_MovementRailStanding, CharacterAbilityDefinition_MovementRailStanding>
	{
		// 060015f8 (1.10.1 RVA 0x77E240)
		protected CharacterState_RailMovementStanding(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
		}

		// 060015f9 (1.10.1 RVA 0x77E2B0)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_RailMovementStanding(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
		}
	}
}
