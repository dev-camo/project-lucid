using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
	// Original Game.Runtime 0200038a; bodies translated from the 1.10.1 x86-64 binary.
	[GraphNodeDefaultName("RailHomingProximityAir")]
	[Il2CppSetOption(Option.NullChecks, false)]
	[GraphNodeMenuFormat("Character/{0}")]
	[Il2CppSetOption(Option.ArrayBoundsChecks, false)]
	public class CharacterState_RailHomingProximityAir : CharacterState_RailHoming<CharacterAbility_RailsProximityAir, CharacterAbilityDefinition_RailsProximityAir>
	{
		// 060015dc (1.10.1 RVA 0x77DC50)
		protected CharacterState_RailHomingProximityAir(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs) : base(fsm, stateId, ctorArgs)
		{
		}

		// 060015dd (1.10.1 RVA 0x77DCC0)
		public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
		{
			return new CharacterState_RailHomingProximityAir(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
		}
	}
}
