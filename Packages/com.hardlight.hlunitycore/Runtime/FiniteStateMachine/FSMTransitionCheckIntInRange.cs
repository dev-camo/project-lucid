// Original HLUnityCore.Runtime.dll, supplied Sonic Dream Team 1.10.1.
// Hardlight.FSMTransitionCheckIntInRange; TypeDef 0x020000d3.
// 0x06000481 .ctor: x86_64 0x1ac97a0.
// 0x06000482 .ctor: x86_64 0x1ac9870.
// 0x06000483 CreateDefaultIdentifier: x86_64 0x1ac9950.
// 0x06000484 ConstructInstance: x86_64 0x1ac9a30.
// 0x06000485 SerialiseRuntimeToJSON: x86_64 0x1ac9bf0.
// 0x06000486 DoUpdate: x86_64 0x1ac9d50.
// Reconstructed from shipping metadata and the native implementation.
using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeMenuFormat("Core/{0}")]
    public class FSMTransitionCheckIntInRange : FSMTransition
    {
        [Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [GraphNodeDefaultName("CheckIntInRange")]
        private class JSONCtorArgs
        {
            public string Name;
            public string Node;
            public string FSM;
            public int Low;
            public int High;
        }
        public readonly GraphStorageKey StorageKey;
        public readonly int Low;
        public readonly int High;

        public FSMTransitionCheckIntInRange(FiniteStateMachine fsm, FSMIdentifier transitionId, GraphStorageKey storageKey, int low, int high)
            : base(fsm, transitionId) { StorageKey = storageKey; Low = low; High = high; }
        public FSMTransitionCheckIntInRange(FiniteStateMachine fsm, GraphStorageKey storageKey, int low, int high)
            : this(fsm, CreateDefaultIdentifier(storageKey, low, high), storageKey, low, high) { }
        private static FSMIdentifier CreateDefaultIdentifier(GraphStorageKey storageKey, int low, int high)
            => string.Format("CheckIntInRange_{0}_{1}_{2}", storageKey, low, high);
        public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
            int node = ConvertNameToId(args.Node); int graph = ConvertNameToId(args.FSM);
            var key = new GraphStorageKey(args.Name, node, graph);
            return new FSMTransitionCheckIntInRange(fsm, transitionId, key, args.Low, args.High);
        }
        public override string SerialiseRuntimeToJSON() => JsonUtility.ToJson(new JSONCtorArgs
        {
            Name = LookupNameUsingId(StorageKey.NameId), Node = LookupNameUsingId(StorageKey.NodeId),
            FSM = LookupNameUsingId(StorageKey.GraphId), Low = Low, High = High
        });
        protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
        {
            // Inspect storage without inserting a missing zero; both bounds are inclusive.
            user.Storage.FillValueOnly(StorageKey, out int value);
            return value >= Low && value <= High;
        }
    }
}
