using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 02000387; bodies translated from the 1.10.1 x86-64 binary.
	[Il2CppSetOption(Option.NullChecks, false)]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	[GraphNodeDefaultName("PrepareHub")]
	[GraphNodeMenuFormat("Character/{0}")]
	public class CharacterState_PrepareHub : CharacterState_Ground<CharacterAbilityDefinition_InHub, CharacterAbility_InHub>
	{
		// 060015d1 (1.10.1 RVA 0x77DB20)
		protected CharacterState_PrepareHub(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
		}

		// 060015d2 (1.10.1 RVA 0x77DB90)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_PrepareHub(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
		}
	}
}
