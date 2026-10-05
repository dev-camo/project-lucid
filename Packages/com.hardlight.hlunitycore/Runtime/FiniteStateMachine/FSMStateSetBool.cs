using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [GraphNodeMenuFormat("Core/{0}")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class FSMStateSetBool : FSMState
    {
        [Serializable]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOption(Option.NullChecks, false)]
        [JSONCtorArgsAttribute]
        public class JSONCtorArgs { public string Name; public string Node; public string FSM; public bool Value; }
        private readonly GraphStorageKey m_storageKey;
        private readonly bool m_value;
        // HLUnityCore.Runtime.dll:0x06000448; arm64 0x1ac94f0. Register
        // before unguarded args access, then resolve node/graph/name in order.
        public FSMStateSetBool(FiniteStateMachine fsm, FSMIdentifier stateId, JSONCtorArgs ctorArgs) : base(fsm, stateId)
        {
            int node = ConvertNameToId(ctorArgs.Node); int graph = ConvertNameToId(ctorArgs.FSM);
            m_storageKey = new GraphStorageKey(ctorArgs.Name, node, graph); m_value = ctorArgs.Value;
        }
        // 0x06000449; arm64 0x1ac95e0. JSON failure precedes registration;
        // a deserialized null instead enters the original constructor first.
        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
            => new FSMStateSetBool(fsm, stateId, JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs));
        // 0x0600044a; arm64 0x1ac9690. Entry writes the retained Boolean,
        // independent of action and current active state.
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action) => user.Storage.SetValue(m_storageKey, m_value);
    }
}
