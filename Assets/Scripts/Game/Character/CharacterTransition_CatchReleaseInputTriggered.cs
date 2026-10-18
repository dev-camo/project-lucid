using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Original Game.Runtime 020003cd; bodies translated from the 1.10.1 x86-64 binary.
    [Il2CppSetOption(Option.NullChecks, false)]
    [GraphNodeDefaultName("CatchReleaseInputTriggered")]
    [GraphNodeMenuFormat("Character/{0}")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class CharacterTransition_CatchReleaseInputTriggered : FSMTransition
    {
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [FiniteStateMachineJsonCtorArgs]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Serializable]
        protected class CatchPointInputReleaseJSONCtorArgs
        {
            public CatchInputReleaseType InputReleaseType;

            public bool InverseCheck;

            // 0600170e (1.10.1 RVA 0x78F9E0)
            public CatchPointInputReleaseJSONCtorArgs() { }
        }

        private readonly CatchInputReleaseType m_inputReleaseType;

        private readonly bool m_inverseCheck;

        // 0600170b (1.10.1 RVA 0x78F850)
        protected CharacterTransition_CatchReleaseInputTriggered(FiniteStateMachine fsm, FSMIdentifier transitionId, CatchPointInputReleaseJSONCtorArgs ctorArgs)
            : base(fsm, transitionId)
        {
            m_inputReleaseType = ctorArgs.InputReleaseType;
            m_inverseCheck = ctorArgs.InverseCheck;
        }

        // 0600170c (1.10.1 RVA 0x78F880)
        public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new CharacterTransition_CatchReleaseInputTriggered(fsm, transitionId, JsonUtility.FromJson<CatchPointInputReleaseJSONCtorArgs>(jsonCtorArgs));
        }

        // 0600170d (1.10.1 RVA 0x78F920). A character without the ability never matches, even when inverted.
        protected override bool DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
        {
            if (base.DoUpdate(user, updateContext))
                return true;

            return user.GetAs<Character>().TryGetAbility(ActorAbilityType.Character_CatchRelease, out CharacterAbility_CatchRelease ability)
                && ability.HasInputReleaseType(m_inputReleaseType) != m_inverseCheck;
        }
    }
}
