using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200038e; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("RailMovementBoost")]
	[GraphNodeMenuFormat("Character/{0}")]
	public class CharacterState_RailMovementBoost : CharacterState_RailMovementBoostBase<CharacterAbility_MovementRailBoost, CharacterAbilityDefinition_MovementRailBoost>
	{
		// 060015f0 (1.10.1 RVA 0x77DFE0)
		protected CharacterState_RailMovementBoost(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
		}

		// 060015f1 (1.10.1 RVA 0x77E050)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_RailMovementBoost(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
		}
	}
}
