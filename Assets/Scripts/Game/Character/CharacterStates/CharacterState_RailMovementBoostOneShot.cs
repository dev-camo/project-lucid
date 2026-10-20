using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000390; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Character/{0}")]
	[GraphNodeDefaultName("RailMovementBoostOneShot")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterState_RailMovementBoostOneShot : CharacterState_RailMovementBoostBase<CharacterAbility_MovementRailBoostOneShot, CharacterAbilityDefinition_MovementRailBoostOneShot>
	{
		// 060015f6 (1.10.1 RVA 0x77E110)
		protected CharacterState_RailMovementBoostOneShot(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
		}

		// 060015f7 (1.10.1 RVA 0x77E180)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_RailMovementBoostOneShot(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
		}
	}
}
