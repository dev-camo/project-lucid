// Original HLUnityCore.Runtime.dll, supplied Sonic Dream Team 1.10.1.
// Hardlight.FSMTransitionCheckFloatInRange; TypeDef 0x020000ce.
// 0x06000473 .ctor: arm64 0x1acc2f8, x86_64 0x1ac88a0.
// 0x06000474 .ctor: arm64 0x1acc3d0, x86_64 0x1ac8970.
// 0x06000475 CreateDefaultIdentifier: arm64 0x1acc4b8, x86_64 0x1ac8a50.
// 0x06000476 ConstructInstance: arm64 0x1acc5b8, x86_64 0x1ac8b40.
// 0x06000477 SerialiseRuntimeToJSON: arm64 0x1acc788, x86_64 0x1ac8d00.
// 0x06000478 DoUpdate: arm64 0x1acc8f8, x86_64 0x1ac8e70.
// Reconstructed from shipping metadata and both native implementations.
using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOptionAttribute(Option.NullChecks, false)]
    [Il2CppSetOptionAttribute(Option.ArrayBoundsChecks, false)]
    [GraphNodeMenuFormatAttribute("Core/{0}")]
    public class FSMTransitionCheckFloatInRange : FSMTransition
    {
        [Serializable]
        [Il2CppSetOptionAttribute(Option.ArrayBoundsChecks, false)]
        [GraphNodeDefaultNameAttribute("CheckFloatInRange")]
        [Il2CppSetOptionAttribute(Option.NullChecks, false)]
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

        public FSMTransitionCheckFloatInRange(FiniteStateMachine fsm, FSMIdentifier transitionId, GraphStorageKey storageKey, float low, float high)
            : base(fsm, transitionId) { StorageKey = storageKey; Low = low; High = high; }
        public FSMTransitionCheckFloatInRange(FiniteStateMachine fsm, GraphStorageKey storageKey, float low, float high)
            : this(fsm, CreateDefaultIdentifier(storageKey, low, high), storageKey, low, high) { }
        private static FSMIdentifier CreateDefaultIdentifier(GraphStorageKey storageKey, float low, float high)
            => string.Format("CheckFloatInRange_{0}_{1}_{2}", storageKey, low, high);
        public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
            int node = ConvertNameToId(args.Node); int graph = ConvertNameToId(args.FSM);
            var key = new GraphStorageKey(args.Name, node, graph);
            return new FSMTransitionCheckFloatInRange(fsm, transitionId, key, args.Low, args.High);
        }
        public override string SerialiseRuntimeToJSON() => JsonUtility.ToJson(new JSONCtorArgs
        {
            Name = LookupNameUsingId(StorageKey.NameId), Node = LookupNameUsingId(StorageKey.NodeId),
            FSM = LookupNameUsingId(StorageKey.GraphId), Low = Low, High = High
        });
        protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
        {
            user.Storage.FillValueOnly(StorageKey, out float value);
            // Both endpoints are inclusive; the lower comparison short-circuits.
            return value >= Low && value <= High;
        }
    }
}
