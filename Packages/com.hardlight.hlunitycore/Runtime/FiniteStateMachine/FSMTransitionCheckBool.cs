using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeMenuFormat("Core/{0}")]
    public class FSMTransitionCheckBool : FSMTransition
    {
        public enum Comparison { TrueCausesTransition = 0, FalseCausesTransition = 1 }
        [Serializable]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [GraphNodeDefaultName("CheckBool")]
        private class JSONCtorArgs
        {
            public string Name;
            public string Node;
            public string FSM;
            [GraphEnumPopup(typeof(Comparison))] public string Compare;
            public bool RemoveValueOnLeave;
            public bool OnlyIfExists;
            public bool RemoveValueOnFire;
        }
        public readonly GraphStorageKey StorageKey;
        public readonly Comparison Compare;
        public readonly bool RemoveValueOnLeave;
        public readonly bool OnlyIfExists;
        public readonly bool RemoveValueOnFire;

        // HLUnityCore.Runtime.dll:0x06000460; arm64 0x1acab0c.
        // Base registration precedes all retained key/options fields.
        public FSMTransitionCheckBool(FiniteStateMachine fsm, FSMIdentifier transitionId,
            GraphStorageKey storageKey, Comparison compare = Comparison.TrueCausesTransition,
            bool removeValueOnLeave = false, bool onlyIfExists = false, bool removeValueOnFire = false)
            : base(fsm, transitionId)
        {
            StorageKey = storageKey; Compare = compare; RemoveValueOnLeave = removeValueOnLeave;
            OnlyIfExists = onlyIfExists; RemoveValueOnFire = removeValueOnFire;
        }
        // 0x06000461; arm64 0x1acabf8. Resolve node/graph/name before the
        // case-insensitive throwing Enum.Parse; no default or catch is added.
        public static IFSMTransition ConstructInstance(FiniteStateMachine fsm, FSMIdentifier transitionId, string jsonCtorArgs)
        {
            JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
            int node = ConvertNameToId(args.Node); int graph = ConvertNameToId(args.FSM);
            var key = new GraphStorageKey(args.Name, node, graph);
            var compare = (Comparison)Enum.Parse(typeof(Comparison), args.Compare, true);
            return new FSMTransitionCheckBool(fsm, transitionId, key, compare,
                args.RemoveValueOnLeave, args.OnlyIfExists, args.RemoveValueOnFire);
        }
        // 0x06000462; arm64 0x1acae40. Names resolve independently;
        // zero node/graph IDs become empty strings through GraphNameLookup.
        public override string SerialiseRuntimeToJSON() => JsonUtility.ToJson(new JSONCtorArgs
        {
            Name = LookupNameUsingId(StorageKey.NameId), Node = LookupNameUsingId(StorageKey.NodeId),
            FSM = LookupNameUsingId(StorageKey.GraphId), Compare = Compare.ToString(),
            RemoveValueOnLeave = RemoveValueOnLeave, OnlyIfExists = OnlyIfExists, RemoveValueOnFire = RemoveValueOnFire
        });
        // 0x06000463; arm64 0x1acb008. Typed presence converts the stored
        // object and can throw. Re-read user.Storage for the later value fill.
        protected override bool DoUpdate(IGraphUser user, FSMUpdateContext context)
        {
            if (OnlyIfExists && !user.Storage.HasValue<bool>(StorageKey)) return false;
            user.Storage.FillValueOnly(StorageKey, out bool value);
            if (Compare == Comparison.TrueCausesTransition) return value;
            if (Compare == Comparison.FalseCausesTransition) return !value;
            return false;
        }
        // 0x06000464/465; arm64 0x1acb1b8/0x1acb314. Flags suppress even
        // user/storage access; removed type parameter is originally Boolean.
        protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
        { if (RemoveValueOnLeave) user.Storage.RemoveValue<bool>(StorageKey); }
        protected override void DoOnFire(IGraphUser user, FSMUpdateContext context, FSMStateChangeAction action)
        { if (RemoveValueOnFire) user.Storage.RemoveValue<bool>(StorageKey); }
    }
}
