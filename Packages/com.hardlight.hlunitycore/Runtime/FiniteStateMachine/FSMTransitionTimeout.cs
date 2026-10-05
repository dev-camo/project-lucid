using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    [GraphNodeMenuFormat("Core/{0}")]
    public class FSMTransitionTimeout : FSMTransition
    {
        [Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [GraphNodeDefaultName("Timeout")]
        private class JSONCtorArgs { public float TimeoutSeconds; public string VariableName; }
        public readonly float TimeoutSeconds;
        public readonly GraphStorageKey VariableName;
        private const string DefaultVariableName = "elapsedSeconds";
        // HLUnityCore.Runtime.dll:0x0600049a; arm64 0x1acf278. Register,
        // retain raw timeout, and substitute only null/empty variable names.
        // Original literal16612 at encoded usage0x32b3388 is elapsedSeconds.
        public FSMTransitionTimeout(FiniteStateMachine fsm, FSMIdentifier transitionId, float timeoutSeconds, string variableName = "elapsedSeconds")
            : base(fsm, transitionId)
        {
            TimeoutSeconds = timeoutSeconds;
            VariableName = new GraphStorageKey(string.IsNullOrEmpty(variableName) ? DefaultVariableName : variableName, TransitionId, FSMId);
        }
        // 0x0600049b; arm64 0x1acf3e0. Null DTO is not repaired.
        public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
            return new FSMTransitionTimeout(fsm, transitionId, args.TimeoutSeconds, args.VariableName);
        }
        // 0x0600049c; arm64 0x1acf4a0. Serialize only the name ID;
        // node/graph identity remains scoped by the constructor.
        public override string SerialiseRuntimeToJSON() => JsonUtility.ToJson(new JSONCtorArgs
        { TimeoutSeconds = TimeoutSeconds, VariableName = LookupNameUsingId(VariableName.NameId) });
        // 0x0600049d; arm64 0x1acf570. Even zero adjustment writes the
        // value/default back to storage; this getter is intentionally mutating.
        public float GetElapsedSeconds(IGraphUser user, float adjustValue = 0f) => user.Storage.AdjustValue(VariableName, adjustValue);
        // 0x0600049e; arm64 0x1acf638. Raw float, without clamping.
        public void SetElapsedSeconds(IGraphUser user, float value) => user.Storage.SetValue(VariableName, value);
        // 0x0600049f; arm64 0x1acf784: reset to zero on entry.
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action) => SetElapsedSeconds(user, 0f);
        // 0x060004a0; arm64 0x1acf78c: remove Single value on leave.
        protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action) => user.Storage.RemoveValue<float>(VariableName);
        // 0x060004a1; arm64 0x1acf8c8. Advance by unchanged context
        // delta for every update type, then compare >= raw timeout, including NaN.
        protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context) => GetElapsedSeconds(user, context.DeltaTime) >= TimeoutSeconds;
    }
}
