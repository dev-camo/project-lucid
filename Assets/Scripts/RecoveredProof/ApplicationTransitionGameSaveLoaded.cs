using System;
using Hardlight;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace HardlightProject
{
    // Game.Runtime.dll 0x02000174. Reads the authored startup completion flag;
    // SaveManager and platform save services remain separate original owners.
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeMenuFormat("Application/{0}")]
    public class ApplicationTransitionGameSaveLoaded : FSMTransition
    {
        // Original nested owner 0x02000175 and field 0x0400052a.
        [Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        public class JSONCtorArgs
        {
            public bool Inverse;

            // Original 0x06000857; ARM64 0x63dff4, x86_64 0x663cd0.
            public JSONCtorArgs() { }
        }

        // Original 0x04000529; native offset 0x1c on both supplied architectures.
        private readonly bool m_inverse;

        // Original 0x06000854; ARM64 0x63dd80, x86_64 0x663a80.
        // Base registration occurs before reading the JSON field. A missing DTO
        // retains the original failure and its already-registered transition.
        private ApplicationTransitionGameSaveLoaded(FiniteStateMachine fsm,
            FSMIdentifier transitionId, JSONCtorArgs jsonCtorArgs) : base(fsm, transitionId)
        {
            m_inverse = jsonCtorArgs.Inverse;
        }

        // Original 0x06000855; ARM64 0x63ddb0, x86_64 0x663ab0.
        public static IFSMTransition ConstructInstance(FiniteStateMachine fsm,
            FSMIdentifier transitionId, string jsonCtorArgs)
        {
            return new ApplicationTransitionGameSaveLoaded(fsm, transitionId,
                JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs));
        }

        // Original 0x06000856; ARM64 0x63de68, x86_64 0x663b50.
        // An absent flag reads false without storing a default. The original
        // interface access is direct; user/storage faults are not suppressed.
        protected override bool DoUpdate(IGraphUser user, FSMUpdateContext updateContext)
        {
            return user.Storage.GetValue<bool>(AppFSMKeys.GameSaveLoaded, false, false) ^ m_inverse;
        }
    }
}
