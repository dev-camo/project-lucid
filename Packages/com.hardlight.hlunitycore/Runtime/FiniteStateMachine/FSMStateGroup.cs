using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;
namespace Hardlight
{
    [Il2CppSetOption(Option.NullChecks, false)]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [GraphNodeMenuFormat("Core/{0}")]
    public class FSMStateGroup : FSMState
    {
        [Serializable]
        [GraphNodeDefaultName("StateGroup")]
        [GraphNodeColour(0f, 0.75f, 1f)]
        private class JSONCtorArgs
        {
            [GraphNodePopup("Hardlight", "FSMStateGraphNode", null, false, false)]
            [GraphNodeFocus("Hardlight", "FSMStateGraphNode", null, false)]
            public List<string> States;
        }
        // HLUnityCore.Runtime.dll:Hardlight.FSMStateGroup:0x06000439/43a;
        // arm64 0x1ac82e8/2f0 return/store the list directly.
        public List<IFSMState> States { get; protected set; }
        // Original token 0x0600043b; arm64 0x1ac82f8. Base registration
        // precedes child list creation; optional non-null states are appended.
        public FSMStateGroup(FiniteStateMachine fsm, FSMIdentifier stateId, IFSMState stateA = null, IFSMState stateB = null) : base(fsm, stateId)
        {
            States = new List<IFSMState>(); AddState(stateA); AddState(stateB);
        }
        // Original token 0x0600043c; arm64 0x1ac85c0. Resolve all names
        // before constructing/registering the group. A missing key returns null;
        // duplicates survive, null values in existing rows are later skipped.
        public new static IFSMState ConstructInstance(FiniteStateMachine fsm, FSMIdentifier stateId, string jsonCtorArgs)
        {
            JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
            var states = new List<IFSMState>();
            foreach (string name in args.States)
            {
                if (!fsm.StateExists(name, out IFSMState state)) return null;
                states.Add(state);
            }
            var group = new FSMStateGroup(fsm, stateId);
            foreach (IFSMState state in states) group.AddState(state);
            return group;
        }
        // Original token 0x0600043d; arm64 0x1ac8a4c. Ordinary live list
        // enumeration preserves duplicate order and propagates child/id errors.
        public override string SerialiseRuntimeToJSON()
        {
            var args = new JSONCtorArgs { States = new List<string>() };
            foreach (IFSMState state in States) args.States.Add(LookupNameUsingId(state.StateId));
            return JsonUtility.ToJson(args);
        }
        // Original token 0x0600043e; arm64 0x1ac84f8. Only null is skipped;
        // repeated references are appended without checking membership.
        public void AddState(IFSMState state) { if (state != null) States.Add(state); }
        // Original tokens 0x0600043f/440/441; arm64 0x1ac8d54/8f08/90bc.
        // Child callbacks retain insertion order. The native List enumerators
        // detect mutation and dispose on exceptions; there is no child catch.
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action) { foreach (IFSMState state in States) state.OnEnter(user, action); }
        protected override void DoUpdate(IGraphUser user, FSMUpdateContext context) { foreach (IFSMState state in States) state.Update(user, context); }
        protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action) { foreach (IFSMState state in States) state.OnLeave(user, action); }
    }
}
