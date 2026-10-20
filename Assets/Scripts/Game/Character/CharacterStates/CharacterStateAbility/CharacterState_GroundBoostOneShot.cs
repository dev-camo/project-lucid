using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200036a; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeMenuFormat("Character/{0}")]
	[GraphNodeDefaultName("GroundBoostOneShot")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[Il2CppSetOption(Option.NullChecks, false)]
	public class CharacterState_GroundBoostOneShot : CharacterState_GroundBoostBase<CharacterAbilityDefinition_MovementGroundBoostOneShot, CharacterAbility_MovementGroundBoostOneShot>
	{
		// 06001571 (1.10.1 RVA 0x7764B0)
		protected CharacterState_GroundBoostOneShot(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
		}

		// 06001572 (1.10.1 RVA 0x776520)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_GroundBoostOneShot(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
		}
	}
}
