// Original HLUnityCore.Runtime.dll, supplied Sonic Dream Team 1.10.1.
// Hardlight.FSMStateSetString; TypeDef 0x020000c1.
// 0x06000454 .ctor: x86_64 0x1ac6800.
// 0x06000455 ConstructInstance: x86_64 0x1ac68f0.
// 0x06000456 DoOnEnter: x86_64 0x1ac6980.
// Reconstructed from shipping metadata and the native implementation.
using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [GraphNodeMenuFormat("Core/{0}")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    public class FSMStateSetString : FSMState
    {
        [Serializable]
        [JSONCtorArgsAttribute]
        [Il2CppSetOption(Option.NullChecks, false)]
        [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
        public class JSONCtorArgs
        {
            public string Name;
            public string Node;
            public string FSM;
            public string Value;
        }
        private readonly GraphStorageKey m_storageKey;
        private readonly string m_value;

        // Register before the unguarded args access; resolve node, graph, then name.
        public FSMStateSetString(FiniteStateMachine fsm, FSMIdentifier stateId, JSONCtorArgs ctorArgs) : base(fsm, stateId)
        {
            int node = ConvertNameToId(ctorArgs.Node); int graph = ConvertNameToId(ctorArgs.FSM);
            m_storageKey = new GraphStorageKey(ctorArgs.Name, node, graph); m_value = ctorArgs.Value;
        }
        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
            => new FSMStateSetString(fsm, stateId, JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs));
        // There is no SerialiseRuntimeToJSON override; the base returns empty.
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action) => user.Storage.SetValue(m_storageKey, m_value);
    }
}
