// Original HLUnityCore.Runtime.dll, supplied Sonic Dream Team 1.10.1.
// Hardlight.FSMTransitionCheckInt; TypeDef 0x020000d0.
// 0x0600047a .ctor: arm64 0x1acca10, x86_64 0x1ac8f70.
// 0x0600047b .ctor: arm64 0x1accae8, x86_64 0x1ac9040.
// 0x0600047c CreateDefaultIdentifier: arm64 0x1accbd8, x86_64 0x1ac9120.
// 0x0600047d ConstructInstance: arm64 0x1acccf4, x86_64 0x1ac9210.
// 0x0600047e SerialiseRuntimeToJSON: arm64 0x1accf8c, x86_64 0x1ac94a0.
// 0x0600047f DoUpdate: arm64 0x1acd14c, x86_64 0x1ac9650.
// Reconstructed from shipping metadata and both native implementations.
using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOptionAttribute(Option.NullChecks, false)]
    [Il2CppSetOptionAttribute(Option.ArrayBoundsChecks, false)]
    [GraphNodeMenuFormatAttribute("Core/{0}")]
    public class FSMTransitionCheckInt : FSMTransition
    {
        public enum Comparison { GreaterThan = 0, GreaterThanEqual = 1, LessThan = 2, LessThanEqual = 3, Equal = 4 }
        [Serializable]
        [Il2CppSetOptionAttribute(Option.NullChecks, false)]
        [Il2CppSetOptionAttribute(Option.ArrayBoundsChecks, false)]
        [GraphNodeDefaultNameAttribute("CheckInt")]
        private class JSONCtorArgs
        {
            public string Name;
            public string Node;
            public string FSM;
            public int TriggerPoint;
            public string Compare;
        }
        public readonly GraphStorageKey StorageKey;
        public readonly int TriggerPoint;
        public readonly Comparison Compare;

        public FSMTransitionCheckInt(FiniteStateMachine fsm, FSMIdentifier transitionId, GraphStorageKey storageKey, int triggerPoint, Comparison compare)
            : base(fsm, transitionId)
        {
            StorageKey = storageKey; Compare = compare; TriggerPoint = triggerPoint;
        }
        public FSMTransitionCheckInt(FiniteStateMachine fsm, GraphStorageKey storageKey, int triggerPoint, Comparison compare)
            : this(fsm, CreateDefaultIdentifier(storageKey, triggerPoint, compare), storageKey, triggerPoint, compare) { }
        private static FSMIdentifier CreateDefaultIdentifier(GraphStorageKey storageKey, int triggerPoint, Comparison compare)
            => string.Format("CheckInt_{0}_{1}_{2}", storageKey, compare, triggerPoint);
        public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
            int node = ConvertNameToId(args.Node); int graph = ConvertNameToId(args.FSM);
            var key = new GraphStorageKey(args.Name, node, graph);
            var compare = (Comparison)Enum.Parse(typeof(Comparison), args.Compare, true);
            return new FSMTransitionCheckInt(fsm, transitionId, key, args.TriggerPoint, compare);
        }
        public override string SerialiseRuntimeToJSON() => JsonUtility.ToJson(new JSONCtorArgs
        {
            Name = LookupNameUsingId(StorageKey.NameId), Node = LookupNameUsingId(StorageKey.NodeId),
            FSM = LookupNameUsingId(StorageKey.GraphId), TriggerPoint = TriggerPoint,
            Compare = Compare.ToString()
        });
        protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
        {
            // Inspect existing storage without adding a missing default.
            user.Storage.FillValueOnly(StorageKey, out int value);
            switch (Compare)
            {
                case Comparison.GreaterThan: return value > TriggerPoint;
                case Comparison.GreaterThanEqual: return value >= TriggerPoint;
                case Comparison.LessThan: return value < TriggerPoint;
                case Comparison.LessThanEqual: return value <= TriggerPoint;
                case Comparison.Equal: return value == TriggerPoint;
                default: return false;
            }
        }
    }
}
