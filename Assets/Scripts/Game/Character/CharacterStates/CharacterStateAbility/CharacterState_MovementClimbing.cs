using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000386; bodies translated from the 1.10.1 x86-64 binary.
    [GraphNodeMenuFormat("Character/{0}")]
    [GraphNodeDefaultName("MovementClimbing")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterState_MovementClimbing : CharacterState_Climbing<CharacterAbility_MovementClimbing, CharacterAbilityDefinition_MovementClimbing>
    {
        // 060015cf (1.10.1 RVA 0x77D9F0)
        protected CharacterState_MovementClimbing(FiniteStateMachine fsm, FSMIdentifier stateId, AbilityJSONCtorArgs ctorArgs)
            : base(fsm, stateId, ctorArgs) { }

        // 060015d0 (1.10.1 RVA 0x77DA60)
        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new CharacterState_MovementClimbing(fsm, transitionId, JsonUtility.FromJson<AbilityJSONCtorArgs>(jsonCtorArgs));
        }
    }
}
