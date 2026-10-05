using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [GraphNodeMenuFormat("Core/{0}")]
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class FSMTransitionCheckExists : FSMTransition
    {
        [Serializable]
        [GraphNodeDefaultName("CheckExists")]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        private class JSONCtorArgs { public string Name; public string Node; public string FSM; public bool Invert; }
        public readonly GraphStorageKey StorageKey;
        public readonly bool Invert;
        // HLUnityCore.Runtime.dll:0x06000467; arm64 0x1acb470.
        public FSMTransitionCheckExists(FiniteStateMachine fsm, FSMIdentifier transitionId, GraphStorageKey storageKey, bool invert)
            : base(fsm, transitionId) { StorageKey = storageKey; Invert = invert; }
        // 0x06000468; arm64 0x1acb53c. String-based key constructor
        // preserves original name/node/graph conversion order.
        public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
            return new FSMTransitionCheckExists(fsm, transitionId, new GraphStorageKey(args.Name, args.Node, args.FSM), args.Invert);
        }
        // 0x06000469; arm64 0x1acb61c. No dictionary/value coercion here.
        public override string SerialiseRuntimeToJSON() => JsonUtility.ToJson(new JSONCtorArgs
        {
            Name = LookupNameUsingId(StorageKey.NameId), Node = LookupNameUsingId(StorageKey.NodeId),
            FSM = LookupNameUsingId(StorageKey.GraphId), Invert = Invert
        });
        // 0x0600046a; arm64 0x1acb78c. Query presence before reading Invert;
        // a present null or wrong-type value still counts as present.
        protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context) => user.Storage.HasValue(StorageKey) ^ Invert;
    }
}
