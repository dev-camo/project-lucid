using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000360; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [GraphNodeMenuFormat("Character/{0}")]
    [GraphNodeDefaultName("BurstAttackChaosGround")]
    public class CharacterState_BurstAttackChaosGround : CharacterState_BurstAttackChaos<CharacterAbility_BurstAttackChaosGround, CharacterAbilityDefinition_BurstAttackChaosGround>
    {
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [FiniteStateMachineJsonCtorArgs]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Serializable]
        protected class BurstAttackChaosGroundJSONCtorArgs : BurstAttackChaosJSONCtorArgs
        {
            // 0600153e (1.10.1 RVA 0x775B70)
            public BurstAttackChaosGroundJSONCtorArgs() { }
        }

        // 0600153c (1.10.1 RVA 0x775A40)
        protected CharacterState_BurstAttackChaosGround(FiniteStateMachine fsm, FSMIdentifier stateId, BurstAttackChaosGroundJSONCtorArgs ctorArgs)
            : base(fsm, stateId, ctorArgs) { }

        // 0600153d (1.10.1 RVA 0x775AB0)
        public static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new CharacterState_BurstAttackChaosGround(fsm, transitionId, JsonUtility.FromJson<BurstAttackChaosGroundJSONCtorArgs>(jsonCtorArgs));
        }
    }
}
