// Original HLUnityCore.Runtime.dll, supplied Sonic Dream Team 1.10.1.
// Hardlight.FSMTransitionTimeoutRandom; TypeDef 0x020000df.
// 0x060004a3 .ctor: x86_64 0x1acbc40.
// 0x060004a4 ConstructInstance: x86_64 0x1acbdb0.
// 0x060004a5 SerialiseRuntimeToJSON: x86_64 0x1acbe70.
// 0x060004a6 GetElapsedSeconds: x86_64 0x1acbf90.
// 0x060004a7 SetElapsedSeconds: x86_64 0x1acc050.
// 0x060004a8 GetTimeoutSeconds: x86_64 0x1acc1a0.
// 0x060004a9 SetTimeoutSeconds: x86_64 0x1acc2e0.
// 0x060004aa DoOnEnter: x86_64 0x1acc430.
// 0x060004ab DoOnLeave: x86_64 0x1acc470.
// 0x060004ac DoUpdate: x86_64 0x1acc6c0.
// Reconstructed from shipping metadata and the native implementation.
using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [GraphNodeMenuFormat("Core/{0}")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class FSMTransitionTimeoutRandom : FSMTransition
    {
        [Serializable]
        [GraphNodeDefaultName("TimeoutRandom")]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        private class JSONCtorArgs
        {
            public float MinTimeoutSeconds;
            public float MaxTimeoutSeconds;
            public string ElapsedVariableName;
            public string TimeoutVariableName;
        }
        public readonly float MinTimeoutSeconds;
        public readonly float MaxTimeoutSeconds;
        public readonly GraphStorageKey ElapsedVariableName;
        public readonly GraphStorageKey TimeoutVariableName;
        private const string DefaultElapsedName = "elapsedSeconds";
        private const string DefaultTimeoutName = "timeoutSeconds";

        // Register first, keep the raw range (no ordering or clamping), and
        // substitute a default only for a null/empty variable name.
        public FSMTransitionTimeoutRandom(FiniteStateMachine fsm, FSMIdentifier transitionId, float minTimeoutSeconds, float maxTimeoutSeconds,
            string elapsedName = DefaultElapsedName, string timeoutName = DefaultTimeoutName)
            : base(fsm, transitionId)
        {
            MinTimeoutSeconds = minTimeoutSeconds;
            MaxTimeoutSeconds = maxTimeoutSeconds;
            ElapsedVariableName = new GraphStorageKey(string.IsNullOrEmpty(elapsedName) ? DefaultElapsedName : elapsedName, TransitionId, FSMId);
            TimeoutVariableName = new GraphStorageKey(string.IsNullOrEmpty(timeoutName) ? DefaultTimeoutName : timeoutName, TransitionId, FSMId);
        }
        // A null DTO is not repaired; its fields are read before allocation.
        public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
            return new FSMTransitionTimeoutRandom(fsm, transitionId, args.MinTimeoutSeconds, args.MaxTimeoutSeconds,
                args.ElapsedVariableName, args.TimeoutVariableName);
        }
        // Only the name IDs are serialised; node/graph scope comes from the constructor.
        public override string SerialiseRuntimeToJSON() => JsonUtility.ToJson(new JSONCtorArgs
        {
            MinTimeoutSeconds = MinTimeoutSeconds, MaxTimeoutSeconds = MaxTimeoutSeconds,
            ElapsedVariableName = LookupNameUsingId(ElapsedVariableName.NameId),
            TimeoutVariableName = LookupNameUsingId(TimeoutVariableName.NameId)
        });
        // Even a zero adjustment writes the value back; this getter mutates storage.
        public float GetElapsedSeconds(IGraphUser user, float adjustValue = 0f) => user.Storage.AdjustValue(ElapsedVariableName, adjustValue);
        public void SetElapsedSeconds(IGraphUser user, float value) => user.Storage.SetValue(ElapsedVariableName, value);
        // GetValue keeps its defaults: a missing timeout reads as zero and is stored.
        public float GetTimeoutSeconds(IGraphUser user) => user.Storage.GetValue<float>(TimeoutVariableName);
        public void SetTimeoutSeconds(IGraphUser user, float value) => user.Storage.SetValue(TimeoutVariableName, value);
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            SetTimeoutSeconds(user, UnityEngine.Random.Range(MinTimeoutSeconds, MaxTimeoutSeconds));
            SetElapsedSeconds(user, 0f);
        }
        protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
        {
            user.Storage.RemoveValue<float>(TimeoutVariableName);
            user.Storage.RemoveValue<float>(ElapsedVariableName);
        }
        // Advance before reading the timeout; >= keeps the NaN-false comparison.
        protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
            => GetElapsedSeconds(user, context.DeltaTime) >= GetTimeoutSeconds(user);
    }
}
