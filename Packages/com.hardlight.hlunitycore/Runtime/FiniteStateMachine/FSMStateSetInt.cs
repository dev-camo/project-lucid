// Original HLUnityCore.Runtime.dll, supplied Sonic Dream Team 1.10.1.
// Hardlight.FSMStateSetInt; TypeDef 0x020000be.
// 0x06000450 .ctor: arm64 0x1ac9c90, x86_64 0x1ac63c0.
// 0x06000451 ConstructInstance: arm64 0x1ac9d80, x86_64 0x1ac64a0.
// 0x06000452 DoOnEnter: arm64 0x1ac9e30, x86_64 0x1ac6530.
// Reconstructed from shipping metadata and both native implementations.
using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [GraphNodeMenuFormatAttribute("Core/{0}")]
    [Il2CppSetOptionAttribute(Option.NullChecks, false)]
    [Il2CppSetOptionAttribute(Option.ArrayBoundsChecks, false)]
    public class FSMStateSetInt : FSMState
    {
        public enum Operation { Set = 0, Add = 1, Multiply = 2 }
        [Serializable]
        [Il2CppSetOptionAttribute(Option.ArrayBoundsChecks, false)]
        [Il2CppSetOptionAttribute(Option.NullChecks, false)]
        [JSONCtorArgsAttribute()]
        public class JSONCtorArgs
        {
            public string Name;
            public string Node;
            public string FSM;
            public int Value;
            public Operation Operation;
        }
        private readonly GraphStorageKey m_storageKey;
        private readonly int m_value;
        private readonly Operation m_operation;

        public FSMStateSetInt(FiniteStateMachine fsm, FSMIdentifier stateId, JSONCtorArgs ctorArgs) : base(fsm, stateId)
        {
            int node = ConvertNameToId(ctorArgs.Node); int graph = ConvertNameToId(ctorArgs.FSM);
            m_storageKey = new GraphStorageKey(ctorArgs.Name, node, graph);
            m_value = ctorArgs.Value; m_operation = ctorArgs.Operation;
        }
        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
            => new FSMStateSetInt(fsm, stateId, JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs));
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            // Insert a missing zero before selecting the operation, even when
            // the operation is invalid; arithmetic retains signed wraparound.
            int current = user.Storage.GetValue<int>(m_storageKey, 0, true);
            int value;
            switch (m_operation)
            {
                case Operation.Set: value = m_value; break;
                case Operation.Add: value = unchecked(current + m_value); break;
                case Operation.Multiply: value = unchecked(current * m_value); break;
                default: throw new ArgumentOutOfRangeException();
            }
            // Resolve user.Storage again after computing the result.
            user.Storage.SetValue(m_storageKey, value);
        }
    }
}
