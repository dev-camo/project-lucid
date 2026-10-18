using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 0200035e; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeDefaultName("BurstAttackChaosAir")]
    [GraphNodeMenuFormat("Character/{0}")]
    public class CharacterState_BurstAttackChaosAir : CharacterState_BurstAttackChaos<CharacterAbility_BurstAttackChaosAir, CharacterAbilityDefinition_BurstAttackChaosAir>
    {
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [FiniteStateMachineJsonCtorArgs]
        [Serializable]
        protected class BurstAttackChaosAirJSONCtorArgs : BurstAttackChaosJSONCtorArgs
        {
            // 0600153b (1.10.1 RVA 0x775A00)
            public BurstAttackChaosAirJSONCtorArgs() { }
        }

        // 06001538 (1.10.1 RVA 0x7758C0)
        protected CharacterState_BurstAttackChaosAir(FiniteStateMachine fsm, FSMIdentifier stateId, BurstAttackChaosAirJSONCtorArgs ctorArgs)
            : base(fsm, stateId, ctorArgs) { }

        // 06001539 (1.10.1 RVA 0x775930)
        public static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new CharacterState_BurstAttackChaosAir(fsm, transitionId, JsonUtility.FromJson<BurstAttackChaosAirJSONCtorArgs>(jsonCtorArgs));
        }

        // 0600153a (1.10.1 RVA 0x7759F0): the air variant refuses the early exit on entry.
        protected override bool PreventDeactivation()
        {
            return true;
        }
    }
}
