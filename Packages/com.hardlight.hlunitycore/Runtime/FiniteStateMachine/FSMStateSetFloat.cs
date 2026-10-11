// Original HLUnityCore.Runtime.dll, supplied Sonic Dream Team 1.10.1.
// Hardlight.FSMStateSetFloat; TypeDef 0x020000bb.
// 0x0600044c .ctor: arm64 0x1ac97f0, x86_64 0x1ac5f60.
// 0x0600044d ConstructInstance: arm64 0x1ac98e8, x86_64 0x1ac6050.
// 0x0600044e DoOnEnter: arm64 0x1ac9998, x86_64 0x1ac60e0.
// Reconstructed from shipping metadata and both native implementations.
using System;
using Unity.IL2CPP.CompilerServices;
using UnityEngine;

namespace Hardlight
{
    [Il2CppSetOptionAttribute(Option.ArrayBoundsChecks, false)]
    [GraphNodeMenuFormatAttribute("Core/{0}")]
    [Il2CppSetOptionAttribute(Option.NullChecks, false)]
    public class FSMStateSetFloat : FSMState
    {
        public enum Operation { Set = 0, Add = 1, Multiply = 2 }
        [Serializable]
        [Il2CppSetOptionAttribute(Option.NullChecks, false)]
        [Il2CppSetOptionAttribute(Option.ArrayBoundsChecks, false)]
        [JSONCtorArgsAttribute()]
        public class JSONCtorArgs
        {
            public string Name;
            public string Node;
            public string FSM;
            public float Value;
            public Operation Operation;
        }
        private readonly GraphStorageKey m_storageKey;
        private readonly float m_value;
        private readonly Operation m_operation;

        public FSMStateSetFloat(FiniteStateMachine fsm, FSMIdentifier stateId, JSONCtorArgs ctorArgs) : base(fsm, stateId)
        {
            int node = ConvertNameToId(ctorArgs.Node); int graph = ConvertNameToId(ctorArgs.FSM);
            m_storageKey = new GraphStorageKey(ctorArgs.Name, node, graph);
            m_value = ctorArgs.Value; m_operation = ctorArgs.Operation;
        }
        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
            => new FSMStateSetFloat(fsm, stateId, JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs));
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            // The original inserts a missing zero before selecting the operation,
            // including Set and the invalid-operation exception path.
            float current = user.Storage.GetValue<float>(m_storageKey, 0, true);
            float value;
            switch (m_operation)
            {
                case Operation.Set: value = m_value; break;
                case Operation.Add: value = current + m_value; break;
                case Operation.Multiply: value = current * m_value; break;
                default: throw new ArgumentOutOfRangeException();
            }
            // Resolve user.Storage again for the write, as the original does.
            user.Storage.SetValue(m_storageKey, value);
        }
    }
}
