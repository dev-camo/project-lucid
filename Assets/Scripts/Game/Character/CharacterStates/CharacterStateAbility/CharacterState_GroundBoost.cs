using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000368; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeDefaultName("GroundBoost")]
	[GraphNodeMenuFormat("Character/{0}")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterState_GroundBoost : CharacterState_GroundBoostBase<CharacterAbilityDefinition_MovementGroundBoost, CharacterAbility_MovementGroundBoost>
	{
		// 0600156b (1.10.1 RVA 0x776380)
		protected CharacterState_GroundBoost(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
		}

		// 0600156c (1.10.1 RVA 0x7763F0)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_GroundBoost(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
		}
	}
}
