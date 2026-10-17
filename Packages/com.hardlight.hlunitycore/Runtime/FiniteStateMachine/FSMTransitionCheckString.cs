// Original HLUnityCore.Runtime.dll, supplied Sonic Dream Team 1.10.1.
// Hardlight.FSMTransitionCheckString; TypeDef 0x020000d5.
// 0x06000488 .ctor: x86_64 0x1ac9e40.
// 0x06000489 .ctor: x86_64 0x1ac9f10.
// 0x0600048a CreateDefaultIdentifier: x86_64 0x1ac9f70.
// 0x0600048b ConstructInstance: x86_64 0x1aca040.
// 0x0600048c SerialiseRuntimeToJSON: x86_64 0x1aca260.
// 0x0600048d DoUpdate: x86_64 0x1aca420.
// Reconstructed from shipping metadata and the native implementation.
using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeMenuFormat("Core/{0}")]
    public class FSMTransitionCheckString : FSMTransition
    {
        public enum Comparison { EqualCaseSensitive = 0, EqualCaseInsensitive = 1 }
        [Serializable]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [GraphNodeDefaultName("CheckString")]
        [Il2CppSetOption(Option.NullChecks, false)]
        private class JSONCtorArgs
        {
            public string Name;
            public string Node;
            public string FSM;
            public string TriggerOn;
            public string Compare;
        }
        public readonly GraphStorageKey StorageKey;
        public readonly string TriggerOn;
        public readonly Comparison Compare;

        public FSMTransitionCheckString(FiniteStateMachine fsm, FSMIdentifier transitionId, GraphStorageKey storageKey, string triggerOn,
            Comparison compare = Comparison.EqualCaseSensitive)
            : base(fsm, transitionId)
        {
            StorageKey = storageKey;
            TriggerOn = triggerOn;
            Compare = compare;
        }
        public FSMTransitionCheckString(FiniteStateMachine fsm, GraphStorageKey storageKey, string triggerOn, Comparison compare)
            : this(fsm, CreateDefaultIdentifier(storageKey, triggerOn, compare), storageKey, triggerOn, compare) { }
        // The comparison precedes the trigger text in the generated name.
        private static FSMIdentifier CreateDefaultIdentifier(GraphStorageKey storageKey, string triggerOn, Comparison compare)
            => string.Format("CheckString_{0}_{1}_{2}", storageKey, compare, triggerOn);
        public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
            int node = ConvertNameToId(args.Node);
            int graph = ConvertNameToId(args.FSM);
            var key = new GraphStorageKey(args.Name, node, graph);
            // Parse (ignoring case) before allocation, so a bad literal never registers.
            var compare = (Comparison)Enum.Parse(typeof(Comparison), args.Compare, true);
            return new FSMTransitionCheckString(fsm, transitionId, key, args.TriggerOn, compare);
        }
        public override string SerialiseRuntimeToJSON() => JsonUtility.ToJson(new JSONCtorArgs
        {
            Name = LookupNameUsingId(StorageKey.NameId), Node = LookupNameUsingId(StorageKey.NodeId),
            FSM = LookupNameUsingId(StorageKey.GraphId), TriggerOn = TriggerOn, Compare = Compare.ToString()
        });
        protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
        {
            // GetValue keeps its defaults, so a missing string is stored as null.
            string value = user.Storage.GetValue<string>(StorageKey);
            switch (Compare)
            {
                case Comparison.EqualCaseSensitive: return string.Equals(TriggerOn, value);
                case Comparison.EqualCaseInsensitive: return string.Equals(TriggerOn, value, StringComparison.OrdinalIgnoreCase);
                default: return false;
            }
        }
    }
}
