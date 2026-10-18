using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000358; bodies translated from the 1.10.1 x86-64 binary.
    [GraphNodeMenuFormat("Character/{0}")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeDefaultName("BurstAttackAir")]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class CharacterState_BurstAttackAir : CharacterState_BurstAttack<CharacterAbility_BurstAttackAir, CharacterAbilityDefinition_BurstAttackAir>
    {
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [FiniteStateMachineJsonCtorArgs]
        [Serializable]
        protected class BurstAttackAirJSONCtorArgs : BurstAttackJSONCtorArgs
        {
            // 0600152f (1.10.1 RVA 0x775880)
            public BurstAttackAirJSONCtorArgs() { }
        }

        // 0600152d (1.10.1 RVA 0x775750)
        protected CharacterState_BurstAttackAir(FiniteStateMachine fsm, FSMIdentifier stateId, BurstAttackAirJSONCtorArgs ctorArgs)
            : base(fsm, stateId, ctorArgs) { }

        // 0600152e (1.10.1 RVA 0x7757C0)
        public static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new CharacterState_BurstAttackAir(fsm, transitionId, JsonUtility.FromJson<BurstAttackAirJSONCtorArgs>(jsonCtorArgs));
        }
    }
}
