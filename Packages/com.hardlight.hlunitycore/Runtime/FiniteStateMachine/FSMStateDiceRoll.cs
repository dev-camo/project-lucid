// Original HLUnityCore.Runtime.dll, supplied Sonic Dream Team 1.10.1.
// Hardlight.FSMStateDiceRoll; TypeDef 0x020000b1.
// 0x0600042b .ctor: arm64 0x1ac7bd8, x86_64 0x1ac4370.
// 0x0600042c .ctor: arm64 0x1ac7c24, x86_64 0x1ac43c0.
// 0x0600042d CreateDefaultIdentifier: arm64 0x1ac7c90, x86_64 0x1ac4430.
// 0x0600042e ConstructInstance: arm64 0x1ac7d90, x86_64 0x1ac4520.
// 0x0600042f SerialiseRuntimeToJSON: arm64 0x1ac7f04, x86_64 0x1ac4680.
// 0x06000430 DoOnEnter: arm64 0x1ac8074, x86_64 0x1ac47f0.
// Reconstructed from shipping metadata and both native implementations.
using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOptionAttribute(Option.ArrayBoundsChecks, false)]
    [GraphNodeMenuFormatAttribute("Core/{0}")]
    [Il2CppSetOptionAttribute(Option.NullChecks, false)]
    public class FSMStateDiceRoll : FSMState
    {
        [Serializable]
        [Il2CppSetOptionAttribute(Option.NullChecks, false)]
        [Il2CppSetOptionAttribute(Option.ArrayBoundsChecks, false)]
        [GraphNodeDefaultNameAttribute("DiceRoll")]
        [GraphTooltipAttribute("Simulates a random dice roll using the provided `Low` & `High` parameters.")]
        private class JSONCtorArgs
        {
            public string Name;
            public string Node;
            public string FSM;
            public float Low;
            public float High;
        }
        public readonly GraphStorageKey StorageKey;
        public readonly float Low;
        public readonly float High;

        public FSMStateDiceRoll(FiniteStateMachine fsm, FSMIdentifier stateId, GraphStorageKey storageKey, float low, float high)
            : base(fsm, stateId) { StorageKey = storageKey; Low = low; High = high; }
        public FSMStateDiceRoll(FiniteStateMachine fsm, GraphStorageKey storageKey, float low, float high)
            : this(fsm, CreateDefaultIdentifier(storageKey, low, high), storageKey, low, high) { }
        private static FSMIdentifier CreateDefaultIdentifier(GraphStorageKey storageKey, float low, float high)
            => string.Format("DiceRoll_{0}_{1}_{2}", storageKey, low, high);
        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
        {
            JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
            int node = ConvertNameToId(args.Node); int graph = ConvertNameToId(args.FSM);
            var key = new GraphStorageKey(args.Name, node, graph);
            return new FSMStateDiceRoll(fsm, stateId, key, args.Low, args.High);
        }
        public override string SerialiseRuntimeToJSON() => JsonUtility.ToJson(new JSONCtorArgs
        {
            Name = LookupNameUsingId(StorageKey.NameId), Node = LookupNameUsingId(StorageKey.NodeId),
            FSM = LookupNameUsingId(StorageKey.GraphId), Low = Low, High = High
        });
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            // Consume the random draw before dereferencing the user or its storage.
            float value = UnityEngine.Random.Range(Low, High);
            user.Storage.SetValue(StorageKey, value);
        }
    }
}
