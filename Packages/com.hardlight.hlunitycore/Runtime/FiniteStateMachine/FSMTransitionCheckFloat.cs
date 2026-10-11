// Original HLUnityCore.Runtime.dll, supplied Sonic Dream Team 1.10.1.
// Hardlight.FSMTransitionCheckFloat; TypeDef 0x020000cb.
// 0x0600046c .ctor: arm64 0x1acb858, x86_64 0x1ac7e70.
// 0x0600046d .ctor: arm64 0x1acb938, x86_64 0x1ac7f50.
// 0x0600046e CreateDefaultIdentifier: arm64 0x1acba2c, x86_64 0x1ac8040.
// 0x0600046f ConstructInstance: arm64 0x1acbca8, x86_64 0x1ac82c0.
// 0x06000470 SerialiseRuntimeToJSON: arm64 0x1acbf4c, x86_64 0x1ac8550.
// 0x06000471 DoUpdate: arm64 0x1acc10c, x86_64 0x1ac8700.
// Reconstructed from shipping metadata and both native implementations.
using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOptionAttribute(Option.NullChecks, false)]
    [Il2CppSetOptionAttribute(Option.ArrayBoundsChecks, false)]
    [GraphNodeMenuFormatAttribute("Core/{0}")]
    public class FSMTransitionCheckFloat : FSMTransition
    {
        public enum Comparison { GreaterThan = 0, GreaterThanEqual = 1, LessThan = 2, LessThanEqual = 3, Equal = 4 }
        [Serializable]
        [Il2CppSetOptionAttribute(Option.NullChecks, false)]
        [Il2CppSetOptionAttribute(Option.ArrayBoundsChecks, false)]
        [GraphNodeDefaultNameAttribute("CheckFloat")]
        private class JSONCtorArgs
        {
            public string Name;
            public string Node;
            public string FSM;
            public float TriggerPoint;
            public float Tolerance;
            public string Compare;
        }
        public readonly GraphStorageKey StorageKey;
        public readonly float TriggerPoint;
        public readonly float Tolerance;
        public readonly Comparison Compare;

        public FSMTransitionCheckFloat(FiniteStateMachine fsm, FSMIdentifier transitionId, GraphStorageKey storageKey, float triggerPoint, Comparison compare, float tolerance = 0f)
            : base(fsm, transitionId)
        {
            StorageKey = storageKey; Compare = compare; TriggerPoint = triggerPoint; Tolerance = tolerance;
        }
        public FSMTransitionCheckFloat(FiniteStateMachine fsm, GraphStorageKey storageKey, float triggerPoint, Comparison compare, float tolerance = 0f)
            : this(fsm, CreateDefaultIdentifier(storageKey, triggerPoint, compare, tolerance), storageKey, triggerPoint, compare, tolerance) { }
        private static FSMIdentifier CreateDefaultIdentifier(GraphStorageKey storageKey, float triggerPoint, Comparison compare, float tolerance)
            => compare == Comparison.Equal
                ? string.Format("CheckFloat_{0}_{1}_{2}_{3}", storageKey, compare, triggerPoint, tolerance)
                : string.Format("CheckFloat_{0}_{1}_{2}", storageKey, compare, triggerPoint);
        public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
            int node = ConvertNameToId(args.Node); int graph = ConvertNameToId(args.FSM);
            var key = new GraphStorageKey(args.Name, node, graph);
            var compare = (Comparison)Enum.Parse(typeof(Comparison), args.Compare, true);
            return new FSMTransitionCheckFloat(fsm, transitionId, key, args.TriggerPoint, compare, args.Tolerance);
        }
        public override string SerialiseRuntimeToJSON() => JsonUtility.ToJson(new JSONCtorArgs
        {
            Name = LookupNameUsingId(StorageKey.NameId), Node = LookupNameUsingId(StorageKey.NodeId),
            FSM = LookupNameUsingId(StorageKey.GraphId), TriggerPoint = TriggerPoint,
            Tolerance = Tolerance,
            Compare = Compare.ToString()
        });
        protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
        {
            // Read without inserting a missing value. Equality uses a strict
            // tolerance: zero tolerance is false even for identical finite values.
            user.Storage.FillValueOnly(StorageKey, out float value);
            switch (Compare)
            {
                case Comparison.GreaterThan: return value > TriggerPoint;
                case Comparison.GreaterThanEqual: return value >= TriggerPoint;
                case Comparison.LessThan: return value < TriggerPoint;
                case Comparison.LessThanEqual: return value <= TriggerPoint;
                case Comparison.Equal: return Math.Abs(value - TriggerPoint) < Tolerance;
                default: return false;
            }
        }
    }
}
