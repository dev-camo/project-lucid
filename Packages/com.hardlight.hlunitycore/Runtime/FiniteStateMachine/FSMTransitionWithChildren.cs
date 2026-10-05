using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.IL2CPP.CompilerServices;

namespace Hardlight
{
    [GraphNodeMenuFormat("Core/{0}")]
    [Il2CppSetOption(Option.ArrayBoundsChecks, false)]
    [Il2CppSetOption(Option.NullChecks, false)]
    public class FSMTransitionWithChildren : FSMTransition
    {
        [Serializable]
        [GraphNodeDefaultName("TransitionWithChildren")]
        private class JSONCtorArgs
        {
            [GraphNodeFocus("Hardlight", "FSMTransitionGraphNode", null, false)]
            [GraphNodePopup("Hardlight", "FSMTransitionGraphNode", null, false, false)]
            public List<string> ChildTransitionNames;
        }

        // HLUnityCore.Runtime.dll:0x060004ae/0x060004af;
        // arm64 0x1ad0544/0x1ad054c: retain the supplied list, including null.
        protected List<IFSMTransition> ChildTransitions { get; set; }

        // 0x060004b0; arm64 0x1ace6c0: base registration precedes list setup.
        public FSMTransitionWithChildren(FiniteStateMachine fsm, FSMIdentifier transitionId,
            IFSMTransition transitionA = null, IFSMTransition transitionB = null)
            : base(fsm, transitionId)
        {
            ChildTransitions = new List<IFSMTransition>();
            AddChildTransition(transitionA);
            AddChildTransition(transitionB);
        }

        // 0x060004b1; arm64 0x1ad0554: resolve all children before registering
        // this transition. A missing key returns null; a present null is skipped
        // by AddChildTransition after construction. Preserve authored duplicates.
        public static IFSMTransition ConstructInstance(FiniteStateMachine fsm,
            FSMIdentifier transitionId, string jsonCtorArgs)
        {
            JSONCtorArgs args = JsonUtility.FromJson<JSONCtorArgs>(jsonCtorArgs);
            var children = new List<IFSMTransition>();
            foreach (string name in args.ChildTransitionNames)
            {
                if (!fsm.TransitionExists(name, out IFSMTransition child)) return null;
                children.Add(child);
            }
            var transition = new FSMTransitionWithChildren(fsm, transitionId);
            foreach (IFSMTransition child in children) transition.AddChildTransition(child);
            return transition;
        }

        // 0x060004b2; arm64 0x1ad09e0: enumerate the live list in order.
        // Names come from each child's TransitionId, not its ToString override.
        public override string SerialiseRuntimeToJSON()
        {
            var args = new JSONCtorArgs { ChildTransitionNames = new List<string>() };
            foreach (IFSMTransition child in ChildTransitions)
                args.ChildTransitionNames.Add(LookupNameUsingId(child.TransitionId));
            return JsonUtility.ToJson(args);
        }

        // 0x060004b3; arm64 0x1aced50: null is ignored; duplicates are appended.
        public void AddChildTransition(IFSMTransition transition)
        {
            if (transition != null) ChildTransitions.Add(transition);
        }

        // 0x060004b4/0x060004b5; arm64 0x1ad0ce8/0x1ad0e9c.
        // Enumerator invalidation and child exceptions propagate after disposal.
        protected override void DoOnEnter(IGraphUser user, FSMStateChangeAction action)
        {
            foreach (IFSMTransition child in ChildTransitions) child.OnEnter(user, action);
        }

        protected override void DoOnLeave(IGraphUser user, FSMStateChangeAction action)
        {
            foreach (IFSMTransition child in ChildTransitions) child.OnLeave(user, action);
        }
    }
}
