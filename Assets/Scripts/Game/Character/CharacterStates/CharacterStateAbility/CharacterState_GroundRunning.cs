using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200036d; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("GroundRunning")]
	[GraphNodeMenuFormat("Character/{0}")]
	public class CharacterState_GroundRunning : CharacterState_Ground<CharacterAbilityDefinition_Running, CharacterAbility_Running>
	{
		// 0600157a (1.10.1 RVA 0x776B40)
		protected CharacterState_GroundRunning(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
		}

		// 0600157b (1.10.1 RVA 0x776BB0)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_GroundRunning(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
		}
	}
}
