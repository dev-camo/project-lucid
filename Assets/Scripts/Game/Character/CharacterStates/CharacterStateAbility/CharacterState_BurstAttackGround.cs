using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 02000362; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeDefaultName("BurstAttackGround")]
    [GraphNodeMenuFormat("Character/{0}")]
    public class CharacterState_BurstAttackGround : CharacterState_BurstAttack<CharacterAbility_BurstAttackGround, CharacterAbilityDefinition_BurstAttackGround>
    {
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [FiniteStateMachineJsonCtorArgs]
        [Serializable]
        protected class BurstAttackGroundJSONCtorArgs : BurstAttackJSONCtorArgs
        {
            // 06001541 (1.10.1 RVA 0x775CE0)
            public BurstAttackGroundJSONCtorArgs() { }
        }

        // 0600153f (1.10.1 RVA 0x775BB0)
        protected CharacterState_BurstAttackGround(FiniteStateMachine fsm, FSMIdentifier stateId, BurstAttackGroundJSONCtorArgs ctorArgs)
            : base(fsm, stateId, ctorArgs) { }

        // 06001540 (1.10.1 RVA 0x775C20)
        public static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new CharacterState_BurstAttackGround(fsm, transitionId, JsonUtility.FromJson<BurstAttackGroundJSONCtorArgs>(jsonCtorArgs));
        }
    }
}
