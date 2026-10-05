using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeMenuFormat("Core/{0}")]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class FSMTransitionHasFinished : FSMTransition
    {
        [Serializable]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        [GraphNodeDefaultName("HasFinished")]
        private class JSONCtorArgs
        {
            [GraphNodeFocus("Hardlight", "FSMStateGraphNode", null, false)]
            [GraphNodePopup("Hardlight", "FSMStateGraphNode", null, false, false)]
            public string State;
        }
        // HLUnityCore.Runtime.dll:0x0600048f; arm64 0x1ace21c.
        private IFSMState State { get; }
        // Original token 0x06000490; arm64 0x1ace224. Retain the state
        // after base transition registration, including a directly supplied null.
        public FSMTransitionHasFinished(FiniteStateMachine fsm, FSMIdentifier transitionId, IFSMState state = null) : base(fsm, transitionId) { State = state; }
        // Original token 0x06000491; arm64 0x1ace2e0. Blank State is valid
        // and retains null; missing nonblank keys return null before registration.
        public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
            IFSMState state = null;
            if (!string.IsNullOrWhiteSpace(args.State) && !fsm.StateExists(args.State, out state)) return null;
            return new FSMTransitionHasFinished(fsm, transitionId, state);
        }
        // Original token 0x06000492; arm64 0x1ace48c. Raw null retained
        // state has no serializer fallback; use the original StateId name lookup.
        public override string SerialiseRuntimeToJSON() => JsonUtility.ToJson(new JSONCtorArgs { State = LookupNameUsingId(State.StateId) });
        // Original token 0x06000493; arm64 0x1ace5c8. Null means false;
        // otherwise query the retained state with unchanged user and no end check.
        protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context) => State != null && State.HasFinished(user);
    }
}
